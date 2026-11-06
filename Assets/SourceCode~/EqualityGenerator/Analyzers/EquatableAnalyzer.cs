using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EqualityGenerator;

/// <summary>
/// 检查 EqualityGenerator 特性常见误用的 Roslyn 分析器。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EquatableAnalyzer : DiagnosticAnalyzer {
  // 所有特性的元数据
  private static readonly AttributesMetadata Metadata = AttributesMetadata.Instance;

  /// <summary>
  /// 可标注在属性/字段上的全部相等性特性。
  /// </summary>
  private static readonly ImmutableArray<AttributeMetadata> PropertyEqualityAttributes = ImmutableArray.Create(
    Metadata.DefaultEquality,
    Metadata.OrderedEquality,
    Metadata.UnorderedEquality,
    Metadata.SetEquality,
    Metadata.StringEquality,
    Metadata.CustomEquality,
    Metadata.ReferenceEquality,
    Metadata.PrecisionEquality,
    Metadata.IgnoreEquality);

  /// <summary>
  /// 专用于集合的相等性特性。
  /// </summary>
  private static readonly ImmutableArray<AttributeMetadata> CollectionEqualityAttributes = ImmutableArray.Create(
    Metadata.OrderedEquality,
    Metadata.UnorderedEquality,
    Metadata.SetEquality);

  // 本分析器支持的全部诊断
  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
    DiagnosticDescriptors.CollectionMissingAttribute,                    // GE001
    DiagnosticDescriptors.ComplexTypeMissingEquatable,                   // GE002
    DiagnosticDescriptors.CollectionElementMissingEquatable,             // GE003
    DiagnosticDescriptors.OrphanedEqualityAttribute,                     // GE004
    DiagnosticDescriptors.ManualEqualsImplementation,                    // GE005
    DiagnosticDescriptors.NonPartialType,                                // GE006
    DiagnosticDescriptors.ConflictingAttributes,                         // GE007
    DiagnosticDescriptors.StringEqualityOnNonString,                     // GE008
    DiagnosticDescriptors.CollectionAttributeOnNonCollection,            // GE009
    DiagnosticDescriptors.PrecisionEqualityOnUnsupportedType,            // GE010
    DiagnosticDescriptors.GenerateClassEqualityOperatorsIgnored,         // GE011
    DiagnosticDescriptors.GenerateClassEqualityOperatorsShadowedByBase); // GE012

  /// <summary>
  /// 初始化分析器：忽略生成的代码，并注册符号分析回调。
  /// </summary>
  public override void Initialize(AnalysisContext context) {
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();

    // 分析类型声明：检查带 [Equatable] 的类型
    context.RegisterSymbolAction(AnalyzeTypeSymbol, SymbolKind.NamedType);

    // 分析属性/字段：检查孤立的特性
    context.RegisterSymbolAction(AnalyzePropertyOrFieldSymbol, SymbolKind.Property, SymbolKind.Field);
  }

  /// <summary>
  /// 检查单个类型是否符合 [Equatable] 的使用要求。
  /// </summary>
  private static void AnalyzeTypeSymbol(SymbolAnalysisContext context) {
    var typeSymbol = (INamedTypeSymbol)context.Symbol;

    // 只分析带 [Equatable] 特性的类型
    if (typeSymbol.GetAttribute(Metadata.Equatable) is not { } equatableAttr)
      return;

    // GE006：检查类型是否为 partial
    CheckNonPartialType(context, typeSymbol);

    // GE005：检查是否手写 Equals/GetHashCode
    CheckManualEqualsImplementation(context, typeSymbol);

    var explicitMode = GetExplicitMode(equatableAttr);

    // GE011/GE012：检查 GenerateClassEqualityOperators 选项能否生效
    CheckGenerateClassEqualityOperators(context, typeSymbol, equatableAttr);

    // 逐个分析类型中的属性/字段
    foreach (var member in typeSymbol.GetPropertiesAndFields()) {
      var memberType = GetMemberType(member);
      if (memberType == null)
        continue;

      AnalyzeMemberInEquatableType(context, member, memberType, explicitMode);
    }
  }

  /// <summary>
  /// 检查属性/字段上的相等性特性是否合法。
  /// </summary>
  private static void AnalyzePropertyOrFieldSymbol(SymbolAnalysisContext context) {
    var memberSymbol = context.Symbol;
    var containingType = memberSymbol.ContainingType;

    if (containingType == null)
      return;

    // 取出成员上的所有相等性特性
    var memberAttributes = GetEqualityAttributesOnMember(memberSymbol);

    if (memberAttributes.Count == 0)
      return;

    // GE004：所在类型未标记 [Equatable] 时，这些特性无效
    if (!containingType.HasAttribute(Metadata.Equatable)) {
      foreach (var (attr, attrData) in memberAttributes) {
        var location = GetAttributeLocation(attrData, memberSymbol);

        context.ReportDiagnostic(Diagnostic.Create(
          DiagnosticDescriptors.OrphanedEqualityAttribute,
          location,
          memberSymbol.Name,
          attr.ShortName,
          containingType.Name));
      }

      return;
    }

    // GE007：检查特性冲突（只看直接声明的，不含继承来的）
    var directlyDeclaredAttributes = GetDirectlyDeclaredEqualityAttributes(memberSymbol);
    CheckConflictingAttributes(context, memberSymbol, directlyDeclaredAttributes);

    // GE008：检查 [StringEquality] 是否用在非字符串类型上
    CheckStringEqualityOnNonString(context, memberSymbol, memberAttributes);

    // GE009：检查集合特性是否用在非集合类型上
    CheckCollectionAttributeOnNonCollection(context, memberSymbol, memberAttributes);

    // GE010：检查 [PrecisionEquality] 是否用在不支持的类型上
    CheckPrecisionEqualityOnUnsupportedType(context, memberSymbol, memberAttributes);
  }

  /// <summary>
  /// 检查 [Equatable] 类型中单个成员的类型是否满足相等性要求。
  /// </summary>
  private static void AnalyzeMemberInEquatableType(
    SymbolAnalysisContext context,
    ISymbol member,
    ITypeSymbol memberType,
    bool explicitMode) {
    // 取出成员上的相等性特性
    var memberAttributes = GetEqualityAttributesOnMember(member);

    // [IgnoreEquality] 或 [ReferenceEquality] 直接跳过所有检查
    if (memberAttributes.Any(x => x.Metadata.Equals(Metadata.IgnoreEquality) || x.Metadata.Equals(Metadata.ReferenceEquality)))
      return;

    // 显式模式下只检查带 [DefaultEquality] 的成员
    if (explicitMode && !memberAttributes.Any(x => x.Metadata.Equals(Metadata.DefaultEquality)))
      return;

    // 字段需显式标注才参与比较（见 issue #86）；无任何特性的字段无需告警，
    // 带特性的字段仍继续下面的检查。
    if (member is IFieldSymbol && memberAttributes.Count == 0)
      return;

    // [DefaultEquality] 表示用户选择默认相等性（如自带实现的 protobuf 类），
    // 可抑制 GE001/GE002/GE003。
    var hasDefaultEquality = memberAttributes.Any(x => x.Metadata.Equals(Metadata.DefaultEquality));

    // 显式模式下 [DefaultEquality] 只表示"参与比较"，不代表集合的比较语义
    var optedIntoDefaultEquality = hasDefaultEquality && !explicitMode;

    // 是否已标注集合相等性特性
    var hasCollectionAttribute = memberAttributes.Any(x =>
      CollectionEqualityAttributes.Contains(x.Metadata));

    // GE001：集合未标注集合相等性特性。
    // 集合自身带 [Equatable] 时跳过：默认比较器会委托给它。这里刻意不用
    // HasProperEquality()，因为普通的 IEquatable<T> 可能仍按引用比较
    //（如 ImmutableArray<T>），那正是 GE001 要发现的问题。
    if (memberType.IsCollection() && !hasCollectionAttribute && !optedIntoDefaultEquality && !memberType.HasGeneratedEquality()) {
      var location = GetMemberTypeLocation(member) ?? member.Locations.FirstOrDefault() ?? Location.None;
      context.ReportDiagnostic(Diagnostic.Create(
        DiagnosticDescriptors.CollectionMissingAttribute,
        location,
        member.Name));
    }

    // 带 [DefaultEquality] 时跳过 GE002/GE003
    if (hasDefaultEquality)
      return;

    // GE002：复杂类型没有 [Equatable]
    if (memberType.IsComplexType() && !memberType.HasEquatableAttribute()) {
      var location = GetMemberTypeLocation(member) ?? member.Locations.FirstOrDefault() ?? Location.None;
      context.ReportDiagnostic(Diagnostic.Create(
        DiagnosticDescriptors.ComplexTypeMissingEquatable,
        location,
        member.Name,
        memberType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    // GE003：集合元素为复杂类型且没有 [Equatable]
    if (memberType.IsCollection()) {
      var elementType = memberType.GetCollectionElementType();
      if (elementType != null && elementType.IsComplexType() && !elementType.HasEquatableAttribute()) {
        var location = GetMemberTypeLocation(member) ?? member.Locations.FirstOrDefault() ?? Location.None;
        context.ReportDiagnostic(Diagnostic.Create(
          DiagnosticDescriptors.CollectionElementMissingEquatable,
          location,
          member.Name,
          elementType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
      }
    }
  }

  /// <summary>
  /// 检查类型是否声明为 partial（GE006）。
  /// </summary>
  private static void CheckNonPartialType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol) {
    // 遍历所有声明，只要有一个带 partial 即可
    var allDeclarations = typeSymbol.DeclaringSyntaxReferences;
    var hasPartialDeclaration = false;

    foreach (var syntaxRef in allDeclarations) {
      var syntax = syntaxRef.GetSyntax();
      if (syntax is TypeDeclarationSyntax typeDecl &&
          typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword)) {
        hasPartialDeclaration = true;
        break;
      }
    }

    if (!hasPartialDeclaration) {
      var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
      context.ReportDiagnostic(Diagnostic.Create(
        DiagnosticDescriptors.NonPartialType,
        location,
        typeSymbol.Name));
    }
  }

  /// <summary>
  /// GE011/GE012：只有当祖先类型没有提供 == / != 时，
  /// GenerateClassEqualityOperators 选项才真正起作用。
  /// </summary>
  private static void CheckGenerateClassEqualityOperators(
    SymbolAnalysisContext context,
    INamedTypeSymbol typeSymbol,
    AttributeData equatableAttr) {
    // 未指定（或取不到值）即用默认值，选项始终有效
    if (equatableAttr.GetNamedArgumentValue(Const.EquatableAttribute_GenerateClassEqualityOperators) is not bool generateOperators)
      return;

    // GE011：record 与 struct 总会有相等运算符，选项对它们无效
    if (typeSymbol.IsRecord || typeSymbol.TypeKind == TypeKind.Struct) {
      context.ReportDiagnostic(Diagnostic.Create(
        DiagnosticDescriptors.GenerateClassEqualityOperatorsIgnored,
        GetAttributeLocation(equatableAttr, typeSymbol),
        typeSymbol.Name,
        typeSymbol.IsRecord
          ? DiagnosticDescriptors.RecordOperatorsAlwaysEmittedReason
          : DiagnosticDescriptors.StructOperatorsAlwaysGeneratedReason));
      return;
    }

    if (generateOperators)
      return;

    // GE012：基类的运算符同样参与派生类的重载解析，此处省略也无法回到引用比较
    var shadowingBase = FindBaseTypeDefiningEqualityOperators(typeSymbol, context.Compilation);
    if (shadowingBase is not null) {
      context.ReportDiagnostic(Diagnostic.Create(
        DiagnosticDescriptors.GenerateClassEqualityOperatorsShadowedByBase,
        GetAttributeLocation(equatableAttr, typeSymbol),
        typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
        shadowingBase.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }
  }

  /// <summary>
  /// 取特性所在位置，取不到时回退到符号位置。
  /// </summary>
  private static Location GetAttributeLocation(AttributeData attributeData, ISymbol fallbackSymbol) =>
    attributeData.ApplicationSyntaxReference?.GetSyntax().GetLocation()
    ?? fallbackSymbol.Locations.FirstOrDefault()
    ?? Location.None;

  /// <summary>
  /// 沿基类链查找最近一个已有（或即将由 [Equatable] 生成）相等运算符的祖先。
  /// </summary>
  private static INamedTypeSymbol? FindBaseTypeDefiningEqualityOperators(INamedTypeSymbol typeSymbol, Compilation compilation) {
    for (var baseType = typeSymbol.BaseType;
         baseType is not null && baseType.SpecialType != SpecialType.System_Object;
         baseType = baseType.BaseType) {
      // 尚未生成：本编译中带 [Equatable] 的基类会生成运算符（除非它也关闭了选项）。
      // 先判断这种情况，代价小且最常见。
      if (baseType.GetAttribute(Metadata.Equatable) is { } baseEquatableAttr
          && baseEquatableAttr.GeneratesClassEqualityOperators())
        return baseType;

      // 已存在：手写的或其他编译生成的运算符。只有能绑定为
      // typeSymbol == typeSymbol 的重载才算数，例如 operator ==(Base, string)
      // 不是候选，不会使选项失效。
      foreach (var member in baseType.GetMembers(WellKnownMemberNames.EqualityOperatorName)) {
        if (member is not IMethodSymbol { Parameters.Length: 2 } op)
          continue;

        if (compilation.ClassifyConversion(typeSymbol, op.Parameters[0].Type).IsImplicit
            && compilation.ClassifyConversion(typeSymbol, op.Parameters[1].Type).IsImplicit)
          return baseType;
      }
    }

    return null;
  }

  /// <summary>
  /// 检查类型是否手写了 Equals/GetHashCode（GE005）。
  /// </summary>
  private static void CheckManualEqualsImplementation(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol) {
    // record 由编译器自动生成这些方法，属于预期行为
    if (typeSymbol.IsRecord)
      return;

    foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>()) {
      var member = method.IsEqualsObjectOverride() ? "Equals(object)"
        : method.IsGetHashCodeOverride() ? "GetHashCode()"
        : null;
      if (member is null)
        continue;

      context.ReportDiagnostic(Diagnostic.Create(
        DiagnosticDescriptors.ManualEqualsImplementation,
        method.Locations.FirstOrDefault() ?? Location.None,
        typeSymbol.Name,
        member));
    }
  }

  /// <summary>
  /// 检查成员上是否存在互斥的相等性特性（GE007）。
  /// </summary>
  private static void CheckConflictingAttributes(
    SymbolAnalysisContext context,
    ISymbol memberSymbol,
    List<(AttributeMetadata Metadata, AttributeData Data)> memberAttributes) {
    if (memberAttributes.Count <= 1)
      return;

    // [IgnoreEquality] 与其他所有相等性特性都冲突
    var ignoreAttr = memberAttributes.FirstOrDefault(x => x.Metadata.Equals(Metadata.IgnoreEquality));
    var otherAttrs = memberAttributes.Where(x => !x.Metadata.Equals(Metadata.IgnoreEquality)).ToList();

    if (ignoreAttr.Metadata != null && otherAttrs.Count > 0) {
      var location = GetAttributeLocation(otherAttrs[0].Data, memberSymbol);
      context.ReportDiagnostic(Diagnostic.Create(
        DiagnosticDescriptors.ConflictingAttributes,
        location,
        memberSymbol.Name,
        ignoreAttr.Metadata.ShortName,
        otherAttrs[0].Metadata.ShortName));
      return;
    }

    // 多个非 [IgnoreEquality] 特性互相冲突；[DefaultEquality] 不与集合特性冲突
    var nonDefaultAttrs = memberAttributes
      .Where(x => !x.Metadata.Equals(Metadata.DefaultEquality) &&
                  !x.Metadata.Equals(Metadata.IgnoreEquality))
      .ToList();

    if (nonDefaultAttrs.Count > 1) {
      var location = GetAttributeLocation(nonDefaultAttrs[1].Data, memberSymbol);
      context.ReportDiagnostic(Diagnostic.Create(
        DiagnosticDescriptors.ConflictingAttributes,
        location,
        memberSymbol.Name,
        nonDefaultAttrs[0].Metadata.ShortName,
        nonDefaultAttrs[1].Metadata.ShortName));
    }
  }

  /// <summary>
  /// 检查 [StringEquality] 是否用于字符串类型（GE008）。
  /// </summary>
  private static void CheckStringEqualityOnNonString(
    SymbolAnalysisContext context,
    ISymbol memberSymbol,
    List<(AttributeMetadata Metadata, AttributeData Data)> memberAttributes) {
    var stringEqualityAttr = memberAttributes.FirstOrDefault(x => x.Metadata.Equals(Metadata.StringEquality));
    if (stringEqualityAttr.Metadata == null)
      return;

    var memberType = GetMemberType(memberSymbol);
    if (memberType?.SpecialType == SpecialType.System_String)
      return;

    var location = GetAttributeLocation(stringEqualityAttr.Data, memberSymbol);
    context.ReportDiagnostic(Diagnostic.Create(
      DiagnosticDescriptors.StringEqualityOnNonString,
      location,
      memberSymbol.Name,
      memberType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "unknown"));
  }

  /// <summary>
  /// 检查集合相等性特性是否用于集合类型（GE009）。
  /// </summary>
  private static void CheckCollectionAttributeOnNonCollection(
    SymbolAnalysisContext context,
    ISymbol memberSymbol,
    List<(AttributeMetadata Metadata, AttributeData Data)> memberAttributes) {
    var memberType = GetMemberType(memberSymbol);
    if (memberType == null)
      return;

    // 本身就是集合，无需告警
    if (memberType.IsCollection())
      return;

    // 找出误用的集合特性并报告
    foreach (var (attrMetadata, attrData) in memberAttributes) {
      if (CollectionEqualityAttributes.Contains(attrMetadata)) {
        var location = GetAttributeLocation(attrData, memberSymbol);
        context.ReportDiagnostic(Diagnostic.Create(
          DiagnosticDescriptors.CollectionAttributeOnNonCollection,
          location,
          memberSymbol.Name,
          attrMetadata.ShortName,
          memberType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
      }
    }
  }

  // [PrecisionEquality] 支持的数值类型
  private static readonly ImmutableHashSet<SpecialType> SupportedPrecisionTypes = ImmutableHashSet.Create(
    SpecialType.System_Single,
    SpecialType.System_Double,
    SpecialType.System_Decimal,
    SpecialType.System_Int32,
    SpecialType.System_Int64,
    SpecialType.System_Int16,
    SpecialType.System_SByte);

  /// <summary>
  /// 检查 [PrecisionEquality] 是否用于受支持的数值类型（GE010）。
  /// </summary>
  private static void CheckPrecisionEqualityOnUnsupportedType(
    SymbolAnalysisContext context,
    ISymbol memberSymbol,
    List<(AttributeMetadata Metadata, AttributeData Data)> memberAttributes) {
    var precisionAttr = memberAttributes.FirstOrDefault(x => x.Metadata.Equals(Metadata.PrecisionEquality));
    if (precisionAttr.Metadata == null)
      return;

    var memberType = GetMemberType(memberSymbol);
    if (memberType == null)
      return;

    // 拆开 Nullable<T>，检查其基础类型
    var checkType = memberType;
    if (memberType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType)
      checkType = nullableType.TypeArguments[0];

    if (SupportedPrecisionTypes.Contains(checkType.SpecialType))
      return;

    var location = GetAttributeLocation(precisionAttr.Data, memberSymbol);
    context.ReportDiagnostic(Diagnostic.Create(
      DiagnosticDescriptors.PrecisionEqualityOnUnsupportedType,
      location,
      memberSymbol.Name,
      memberType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
  }

  /// <summary>
  /// 取出成员上的所有相等性特性（含继承来的）。
  /// </summary>
  private static List<(AttributeMetadata Metadata, AttributeData Data)> GetEqualityAttributesOnMember(ISymbol member) {
    var result = new List<(AttributeMetadata, AttributeData)>();

    foreach (var attrMetadata in PropertyEqualityAttributes) {
      var attrData = member.GetAttribute(attrMetadata);
      if (attrData != null) {
        result.Add((attrMetadata, attrData));
      }
    }

    return result;
  }

  /// <summary>
  /// 只取成员直接声明的相等性特性（不含重写属性继承来的）。
  /// 用于冲突检测，避免子类重写父类属性时误报。
  /// </summary>
  private static List<(AttributeMetadata Metadata, AttributeData Data)> GetDirectlyDeclaredEqualityAttributes(ISymbol member) {
    var result = new List<(AttributeMetadata, AttributeData)>();

    foreach (var attribute in member.GetAttributes()) {
      foreach (var attrMetadata in PropertyEqualityAttributes) {
        if (attrMetadata.Equals(attribute.AttributeClass)) {
          result.Add((attrMetadata, attribute));
          break;
        }
      }
    }

    return result;
  }

  /// <summary>
  /// 取属性或字段的类型。
  /// </summary>
  private static ITypeSymbol? GetMemberType(ISymbol member) {
    return member switch {
      IPropertySymbol prop => prop.Type,
      IFieldSymbol field => field.Type,
      _ => null
    };
  }

  /// <summary>
  /// 取成员声明中类型名所在的位置，便于诊断精确定位。
  /// </summary>
  private static Location? GetMemberTypeLocation(ISymbol member) {
    var syntaxRef = member.DeclaringSyntaxReferences.FirstOrDefault();
    if (syntaxRef == null)
      return null;

    var syntax = syntaxRef.GetSyntax();
    return syntax switch {
      PropertyDeclarationSyntax propSyntax => propSyntax.Type.GetLocation(),
      FieldDeclarationSyntax fieldSyntax => fieldSyntax.Declaration.Type.GetLocation(),
      VariableDeclaratorSyntax varSyntax when varSyntax.Parent is VariableDeclarationSyntax varDecl
        => varDecl.Type.GetLocation(),
      _ => null
    };
  }

  /// <summary>
  /// 判断 [Equatable] 是否启用了显式模式。
  /// </summary>
  private static bool GetExplicitMode(AttributeData equatableAttr) =>
    equatableAttr.GetNamedArgumentValue(Const.EquatableAttribute_Explicit) is true;
}