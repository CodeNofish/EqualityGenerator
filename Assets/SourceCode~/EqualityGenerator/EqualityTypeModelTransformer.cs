using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EqualityGenerator;

/// <summary>
/// Roslyn 4.1（Unity 内置版本）没有 GeneratorAttributeSyntaxContext（4.3 才加入），
/// 这里提供语义等价的最小上下文：目标声明、目标符号及该声明上匹配到的特性数据。
/// 将来升级到 4.3+ 时换回官方类型即可，转换器逻辑无需改动。
/// </summary>
internal sealed class EquatableAttributeSyntaxContext {
  public SyntaxNode TargetNode { get; }
  public ISymbol TargetSymbol { get; }
  public ImmutableArray<AttributeData> Attributes { get; }

  public EquatableAttributeSyntaxContext(
    SyntaxNode targetNode,
    ISymbol targetSymbol,
    ImmutableArray<AttributeData> attributes) {
    TargetNode = targetNode;
    TargetSymbol = targetSymbol;
    Attributes = attributes;
  }
}

/// <summary>
/// 把带 [Equatable] 的类型符号转换为生成代码所需的 EqualityTypeModel。
/// </summary>
sealed class EqualityTypeModelTransformer {
  readonly EquatableAttributeSyntaxContext _context;

  public EqualityTypeModelTransformer(EquatableAttributeSyntaxContext context) {
    _context = context;
  }

  /// <summary>
  /// 转换入口：收集类型信息、继承链信息与成员模型。
  /// </summary>
  public EqualityTypeModel? Transform(CancellationToken token = default) {
    if (token.IsCancellationRequested) {
      return null;
    }

    var attributesMetadata = AttributesMetadata.Instance;

    var equatableAttributeData = _context.Attributes.SingleOrDefault();
    if (equatableAttributeData is null || !attributesMetadata.Equatable.Equals(equatableAttributeData.AttributeClass)) {
      // 防御性判断：增量编译期间特性可能不匹配
      return null;
    }

    if (token.IsCancellationRequested) {
      return null;
    }

    var explicitMode = equatableAttributeData.GetNamedArgumentValue(Const.EquatableAttribute_Explicit) is true;
    var ignoreInheritedMembers = equatableAttributeData.GetNamedArgumentValue(Const.EquatableAttribute_IgnoreInheritedMembers) is true;
    var generateClassEqualityOperators = equatableAttributeData.GeneratesClassEqualityOperators();

    if (_context.TargetSymbol is not ITypeSymbol symbol) {
      // 防御性判断：增量编译期间目标可能不是类型符号
      return null;
    }

    if (token.IsCancellationRequested) {
      return null;
    }

    var baseTypeFullname = symbol.BaseType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    var typeName = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    var fullname = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    if (token.IsCancellationRequested) {
      return null;
    }

    var containingSymbols = GetContainingSymbols(symbol, includeSelf: symbol.IsRecord);
    if (token.IsCancellationRequested) {
      return null;
    }

    // IgnoreInheritedMembers=false（默认）时，只有当属性最初声明所在的基类带
    // [Equatable] 才跳过重写属性；若基类没有 [Equatable]，base.Equals() 不会比较它，
    // 必须在此处纳入（含继承来的特性）。IgnoreInheritedMembers=true 时纳入全部成员。
    Predicate<ISymbol>? filter = ignoreInheritedMembers
      ? null
      : s => s is not IPropertySymbol prop || !ShouldSkipOverridingProperty(prop, attributesMetadata);

    // 沿继承链向上遍历一次，判定基类如何拥有相等性，并收集"普通"类祖先
    //（自身没有相等性、位于相等性祖先之下）。这些祖先的成员需由本类型显式比较，
    // 因为 base.Equals() 能触及的部分并不知道它们。record 基类不算普通祖先：
    // 它的相等性（编译器合成或手写）通过 base.Equals() 到达。
    var baseChain = ClassifyBaseEquality(symbol.BaseType, attributesMetadata);
    var collectInherited = !ignoreInheritedMembers && !baseChain.PlainAncestors.IsEmpty;

    var bems = EqualityMemberModelTransformer.BuildEqualityModels(symbol, attributesMetadata, explicitMode, filter);
    var inheritedModels = collectInherited
      ? CollectInheritedProperties(symbol, baseChain.PlainAncestors, attributesMetadata)
      : new EquatableImmutableArray<EqualityMemberModel>();

    var model = new EqualityTypeModel {
      TypeName = typeName,
      ContainingSymbols = containingSymbols,
      AttributesMetadata = attributesMetadata,
      ExplicitMode = explicitMode,
      IgnoreInheritedMembers = ignoreInheritedMembers,
      BuildEqualityModels = bems,
      IsSealed = symbol.IsSealed,
      BaseTypeFullname = baseTypeFullname,
      Fullname = fullname,
      SyntaxKind = _context.TargetNode.Kind(),
      BaseEquality = baseChain.Ownership,
      BaseEqualsArgument = baseChain.BaseEqualsArgument,
      InheritedEqualityModels = inheritedModels,
      GenerateClassEqualityOperators = generateClassEqualityOperators,
    };

    if (model.SyntaxKind is not (
        SyntaxKind.StructDeclaration
        or SyntaxKind.RecordStructDeclaration
        or SyntaxKind.RecordDeclaration
        or SyntaxKind.ClassDeclaration
        )
       ) {
      // 待办：此处可报告诊断信息
      // throw new Exception("Expected a struct, record struct, record, or class declaration syntax.");
      return null;
    }

    return model;
  }

  /// <summary>
  /// 由运行时提供 <c>Equals</c>/<c>GetHashCode</c> 的基类型，
  /// 不能当作手写的按值相等契约（遇到它们即结束继承链遍历）。
  /// </summary>
  static bool IsWellKnownEqualityBase(ITypeSymbol type) =>
    type.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum;

  /// <summary>
  /// 类型是否通过生成的比较器拥有相等性：带 <c>[Equatable]</c> 或有生成的
  /// <c>EqualityComparer</c>（跨程序集的信号），调用其比较器即可到达。
  /// </summary>
  static bool OwnsEqualityViaComparer(INamedTypeSymbol type, AttributesMetadata attributesMetadata) =>
    type.HasAttribute(attributesMetadata.Equatable) || type.HasGeneratedEqualityComparer();

  /// <summary>
  /// 类型是否公开了可正常查找的 <c>bool Equals(TSelf)</c>，即隐式的
  /// <c>IEquatable&lt;TSelf&gt;</c> 实现。<c>base.Equals(other as TSelf)</c> 会精确绑定到该重载，
  /// 因此优先于经由 <c>Equals(object)</c> 的路由（显式接口实现不是常规查找候选，不予匹配）。
  /// </summary>
  static bool DeclaresPublicTypedEquals(INamedTypeSymbol type) =>
    type.GetMembers("Equals").OfType<IMethodSymbol>().Any(m =>
      m is { MethodKind: MethodKind.Ordinary, DeclaredAccessibility: Accessibility.Public, IsStatic: false, Parameters.Length: 1 }
      && m.ReturnType.SpecialType == SpecialType.System_Boolean
      && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, type));

  /// <summary>
  /// 类型是否拥有无需生成比较器、可由 <c>base.Equals</c> 触及的相等性。
  /// 所有 record 都满足：编译器会合成公开的强类型 <c>Equals(TSelf?)</c>（或由作者提供）
  /// 以及 <c>GetHashCode</c>。class 则需手写完整契约：重写 <c>GetHashCode</c>，
  /// 并配有公开的强类型 <c>Equals(TSelf)</c>（优先，由 <paramref name="hasTypedEquals"/> 报告）
  /// 或 <c>Equals(object)</c>（兜底）。要求同时有 GetHashCode 可保证委托的一致性；
  /// 调用方会先排除拥有比较器的祖先。
  /// </summary>
  static bool OwnsEqualityViaBaseCall(INamedTypeSymbol type, out bool hasTypedEquals) {
    if (type.IsRecord) {
      hasTypedEquals = true;
      return true;
    }

    hasTypedEquals = false;
    if (!type.GetMembers("GetHashCode").OfType<IMethodSymbol>().Any(SymbolExtensions.IsGetHashCodeOverride))
      return false;

    hasTypedEquals = DeclaresPublicTypedEquals(type);
    return hasTypedEquals || type.GetMembers("Equals").OfType<IMethodSymbol>().Any(SymbolExtensions.IsEqualsObjectOverride);
  }

  /// <summary>
  /// 构造 <c>base.Equals(...)</c> 的参数，使其绑定到 <paramref name="target"/> 的相等性：
  /// record 通过强制转换绑定到强类型 <c>Equals</c>（record 基类总是直接基类）；
  /// 有公开强类型 <c>Equals(TSelf)</c> 的 class 用 <c>as</c>；其余用 <c>object</c>，
  /// 让重载解析落到 <c>Equals(object)</c>，而不是带 [Equatable] 的祖先生成的 <c>Equals(TAncestor?)</c>。
  /// </summary>
  static string BaseEqualsArgumentFor(INamedTypeSymbol target, bool hasTypedEquals) =>
    target.IsRecord ? $"({target.ToFQF()}?)other"
    : hasTypedEquals ? $"other as {target.ToFQF()}"
    : "(object?) other";

  /// <summary><see cref="ClassifyBaseEquality"/> 的结果：模型所需的全部基类链信息。</summary>
  readonly record struct BaseChain(
    BaseEqualityOwnership Ownership,
    string? BaseEqualsArgument,
    ImmutableArray<INamedTypeSymbol> PlainAncestors);

  /// <summary>
  /// 沿继承链向上遍历一次判定基类如何拥有相等性（见 <see cref="BaseEqualityOwnership"/>），
  /// 遇到 object/ValueType/Enum 等不会有用户契约的基类型即停止。同时给出相等性祖先之下
  /// 的"普通"类祖先（由近到远）以及 <c>base.Equals</c> 的参数（见 <see cref="BaseEqualsArgumentFor"/>）：
  /// 比较器路径绑定直接基类；base 调用路径绑定拥有契约的祖先（它可能位于普通类中间层之后，
  /// 若绑定直接基类就会漏掉）。
  /// </summary>
  static BaseChain ClassifyBaseEquality(INamedTypeSymbol? baseType, AttributesMetadata attributesMetadata) {
    var plainAncestors = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
    string? baseCallArgument = null;

    for (var current = baseType; current != null && !IsWellKnownEqualityBase(current); current = current.BaseType) {
      if (OwnsEqualityViaComparer(current, attributesMetadata)) {
        return baseCallArgument is null
          ? new BaseChain(BaseEqualityOwnership.Comparer, BaseEqualsArgumentFor(baseType!, hasTypedEquals: true), plainAncestors.ToImmutable())
          : new BaseChain(BaseEqualityOwnership.ComparerBehindManual, baseCallArgument, plainAncestors.ToImmutable());
      }

      if (baseCallArgument is not null)
        continue;

      if (OwnsEqualityViaBaseCall(current, out var hasTypedEquals))
        baseCallArgument = BaseEqualsArgumentFor(current, hasTypedEquals);
      else
        plainAncestors.Add(current);
    }

    return baseCallArgument is null
      ? new BaseChain(BaseEqualityOwnership.None, null, plainAncestors.ToImmutable())
      : new BaseChain(BaseEqualityOwnership.Manual, baseCallArgument, plainAncestors.ToImmutable());
  }

  /// <summary>
  /// 按由祖父类到父类的顺序收集 <paramref name="plainAncestors"/>（由近到远保存）的属性，
  /// 排除已被当前类型重写的属性（那些由 BuildEqualityModels 处理）。
  /// </summary>
  static EquatableImmutableArray<EqualityMemberModel> CollectInheritedProperties(
    ITypeSymbol currentType,
    ImmutableArray<INamedTypeSymbol> plainAncestors,
    AttributesMetadata attributesMetadata) {
    var overriddenPropertyNames = new HashSet<string>(
      currentType.GetMembers()
        .OfType<IPropertySymbol>()
        .Where(p => p.IsOverride)
        .Select(p => p.Name));

    // 继承来的属性始终不用显式模式，需要全部纳入
    Predicate<ISymbol> filter = s => s is not IPropertySymbol prop || !overriddenPropertyNames.Contains(prop.Name);

    var builder = ImmutableArray.CreateBuilder<EqualityMemberModel>();
    for (var i = plainAncestors.Length - 1; i >= 0; i--) {
      builder.AddRange(EqualityMemberModelTransformer.BuildEqualityModels(
        plainAncestors[i], attributesMetadata, explicitMode: false, filter));
    }

    return builder.ToImmutable();
  }

  /// <summary>
  /// 判断重写属性是否应跳过：其重写链上的某个父类型带 [Equatable]
  /// 或含有生成的 EqualityComparer 时跳过。
  /// </summary>
  static bool ShouldSkipOverridingProperty(IPropertySymbol property, AttributesMetadata attributesMetadata) {
    if (!property.IsOverride)
      return false;

    // 沿重写链向上逐个检查父类型
    var overridden = property.OverriddenProperty;
    while (overridden != null) {
      // 链上任一带 [Equatable]，跳过该属性
      if (overridden.ContainingType.HasAttribute(attributesMetadata.Equatable)) {
        return true;
      }

      // 任一带生成的 EqualityComparer（跨程序集），同样跳过
      if (overridden.ContainingType is INamedTypeSymbol namedType && namedType.HasGeneratedEqualityComparer()) {
        return true;
      }

      overridden = overridden.OverriddenProperty;
    }

    // 没有这样的父类型，需要纳入该属性
    return false;
  }

  public static ImmutableArray<ContainingSymbol> GetContainingSymbols(ISymbol symbol, bool includeSelf = false) {
    var parentSymbols = symbol
      .GetParentSymbols(includeSelf)
      .TakeUntilAfterNamespace();

    return parentSymbols.Select(x => {
        if (x.IsNamespace) {
          var format = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

          var namespaceName = x.ToDisplayString(format);

          return (ContainingSymbol)new NamespaceContainingSymbol {
            Name = namespaceName
          };
        }

        var typeDeclarationSyntax = x.DeclaringSyntaxReferences
          .Select(x => x.GetSyntax())
          .OfType<TypeDeclarationSyntax>()
          .First();

        var typeName = x.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        return (ContainingSymbol)new TypeContainingSymbol {
          Name = typeName,
          Kind = typeDeclarationSyntax.Kind()
        };
      })
      .ToImmutableArray();
  }
}