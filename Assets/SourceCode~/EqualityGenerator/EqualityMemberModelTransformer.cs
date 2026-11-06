using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace EqualityGenerator;

/// <summary>
/// 元素比较器的信息：类型名、静态成员名、是否有静态实例。
/// </summary>
record ElementComparerInfo(
  string? ComparerType,
  string? MemberName,
  bool HasStaticInstance
);

/// <summary>
/// 把成员及其相等性特性转换为 EqualityMemberModel。
/// </summary>
static class EqualityMemberModelTransformer {
  /// <summary>
  /// 从特性的构造参数中解析元素比较器。
  /// </summary>
  static ElementComparerInfo? ExtractElementComparerInfo(
    AttributeData attribute,
    AttributesMetadata attributesMetadata
  ) {
    var args = attribute.ConstructorArguments;

    if (args.Length == 0) {
      return null;
    }

    // 第一个构造重载：StringComparison 参数
    if (args[0].Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.StringComparison") {
      var stringComparisonValue = Convert.ToInt64(args[0].Value, CultureInfo.InvariantCulture);
      if (attributesMetadata.StringComparisonLookup.TryGetValue(stringComparisonValue, out var enumMemberName)) {
        return new ElementComparerInfo(
          "global::System.StringComparer",
          enumMemberName,
          true
        );
      }

      return null;
    }

    // 第二个构造重载：传入比较器类型（及可选的静态成员名）
    if (args[0].Value is INamedTypeSymbol comparerType) {
      var comparerTypeName = comparerType.ToFQF();
      var comparerMemberName = args.Length > 1 && args[1].Value is string memberName
        ? memberName
        : "Default";

      var hasStaticInstance = comparerType.GetMembers().Any(x => x.Name == comparerMemberName && x.IsStatic)
                              || comparerType.GetPropertiesAndFields().Any(x => x.Name == comparerMemberName && x.IsStatic);

      return new ElementComparerInfo(
        comparerTypeName,
        comparerMemberName,
        hasStaticInstance
      );
    }

    return null;
  }

  /// <summary>
  /// 为类型的所有成员建立模型，可按谓词过滤。
  /// </summary>
  public static ImmutableArray<EqualityMemberModel> BuildEqualityModels(
    ITypeSymbol symbol,
    AttributesMetadata attributesMetadata,
    bool explicitMode,
    Predicate<ISymbol>? filter = null
  ) {
    var isRecord = symbol.IsRecord;

    var members = symbol.GetPropertiesAndFields();
    var models = members
      .Where(member => filter == null || filter(member))

      // record 自带的 EqualityContract 不参与比较
      .Where(member => !isRecord || member.Name != "EqualityContract")
      .Select(member => member switch {
        IPropertySymbol propertySymbol
          => BuildEqualityModel(propertySymbol, propertySymbol.Type, attributesMetadata, explicitMode, isField: false),
        IFieldSymbol fieldSymbol => BuildEqualityModel(fieldSymbol, fieldSymbol.Type, attributesMetadata, explicitMode, isField: true),
        _ => throw new NotSupportedException($"Member of type {member.GetType()} not supported")
      })
      .ToImmutableArray();

    return models;
  }

  /// <summary>
  /// 按成员上的相等性特性建立单个成员的模型。
  /// </summary>
  public static EqualityMemberModel BuildEqualityModel(
    ISymbol memberSymbol,
    ITypeSymbol typeSymbol,
    AttributesMetadata attributesMetadata,
    bool explicitMode,
    // 必须显式传入、不设默认值：字段是"选择加入"的，调用方若漏传，
    // 会把字段当作默认参与比较的属性，导致 Equals 与 GetHashCode 不一致。
    bool isField
  ) {
    var propertyName = memberSymbol.ToFQF();
    var typeName = typeSymbol.ToNullableFQF();

    // [IgnoreEquality]：直接忽略该成员
    if (memberSymbol.HasAttribute(attributesMetadata.IgnoreEquality)) {
      return new EqualityMemberModel {
        MemberName = propertyName, IsField = isField,
        TypeName = typeName,
        EqualityType = EqualityType.IgnoreEquality,
      };
    }

    // 依次识别其余相等性特性并转换为对应的模型
    if (memberSymbol.HasAttribute(attributesMetadata.UnorderedEquality)) {
      var args = typeSymbol.GetIDictionaryTypeArguments()
                 ?? typeSymbol.GetIEnumerableTypeArguments()!;

      var attribute = memberSymbol.GetAttribute(attributesMetadata.UnorderedEquality)!;
      var elementComparer = ExtractElementComparerInfo(attribute, attributesMetadata);

      var isDictionary = args is DictionaryArgumentsResult;
      string? equatableElementTypeName = null;
      if (isDictionary) {
        var valueTypeSymbol = args.Arguments!.Value[1];
        if (valueTypeSymbol.HasEquatableAttribute())
          equatableElementTypeName = valueTypeSymbol.ToFQF();
      }

      return new EqualityMemberModel {
        MemberName = propertyName, IsField = isField,
        TypeName = args.Name,
        EqualityType = EqualityType.UnorderedEquality,
        IsDictionary = isDictionary,
        IsValueTypeCollection = typeSymbol.IsValueType,
        ElementComparerType = elementComparer?.ComparerType,
        ElementComparerMemberName = elementComparer?.MemberName,
        ElementComparerHasStaticInstance = elementComparer?.HasStaticInstance ?? false,
        EquatableElementTypeName = equatableElementTypeName
      };
    }
    else if (memberSymbol.HasAttribute(attributesMetadata.OrderedEquality)) {
      var types = typeSymbol.GetIEnumerableTypeArguments()!;

      var attribute = memberSymbol.GetAttribute(attributesMetadata.OrderedEquality)!;
      var elementComparer = ExtractElementComparerInfo(attribute, attributesMetadata);

      var elementTypeSymbol = types.Arguments!.Value[0];
      string? equatableElementTypeName = elementTypeSymbol.HasEquatableAttribute()
        ? elementTypeSymbol.ToFQF()
        : null;

      return new EqualityMemberModel {
        MemberName = propertyName, IsField = isField,
        TypeName = types.Name,
        EqualityType = EqualityType.OrderedEquality,
        IsValueTypeCollection = typeSymbol.IsValueType,
        ElementComparerType = elementComparer?.ComparerType,
        ElementComparerMemberName = elementComparer?.MemberName,
        ElementComparerHasStaticInstance = elementComparer?.HasStaticInstance ?? false,
        EquatableElementTypeName = equatableElementTypeName
      };
    }
    else if (memberSymbol.HasAttribute(attributesMetadata.ReferenceEquality)) {
      return new EqualityMemberModel {
        MemberName = propertyName, IsField = isField,
        TypeName = typeName,
        EqualityType = EqualityType.ReferenceEquality
      };
    }
    else if (memberSymbol.HasAttribute(attributesMetadata.SetEquality)) {
      var types = typeSymbol.GetIEnumerableTypeArguments()!;

      var attribute = memberSymbol.GetAttribute(attributesMetadata.SetEquality)!;
      var elementComparer = ExtractElementComparerInfo(attribute, attributesMetadata);

      return new EqualityMemberModel {
        MemberName = propertyName, IsField = isField,
        TypeName = types.Name,
        EqualityType = EqualityType.SetEquality,
        IsValueTypeCollection = typeSymbol.IsValueType,
        ElementComparerType = elementComparer?.ComparerType,
        ElementComparerMemberName = elementComparer?.MemberName,
        ElementComparerHasStaticInstance = elementComparer?.HasStaticInstance ?? false
      };
    }
    else if (memberSymbol.HasAttribute(attributesMetadata.StringEquality)) {
      var attribute = memberSymbol.GetAttribute(attributesMetadata.StringEquality)!;
      var stringComparisonValue = Convert.ToInt64(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture);

      if (!attributesMetadata.StringComparisonLookup.TryGetValue(stringComparisonValue, out var enumMemberName)) {
        throw new InvalidOperationException("Unexpected StringComparison value.");
      }

      return new EqualityMemberModel {
        MemberName = propertyName, IsField = isField,
        TypeName = typeName,
        EqualityType = EqualityType.StringEquality,
        StringComparer = enumMemberName
      };
    }
    else if (memberSymbol.HasAttribute(attributesMetadata.PrecisionEquality)) {
      var attribute = memberSymbol.GetAttribute(attributesMetadata.PrecisionEquality)!;
      var precision = Convert.ToDouble(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture);

      string underlyingTypeName;
      bool isNullable;

      if (typeSymbol is INamedTypeSymbol {
            OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
          } nullableType) {
        isNullable = true;
        underlyingTypeName = nullableType.TypeArguments[0].ToNullableFQF();
      }
      else {
        isNullable = false;
        underlyingTypeName = typeName;
      }

      return new EqualityMemberModel {
        MemberName = propertyName, IsField = isField,
        TypeName = underlyingTypeName,
        EqualityType = EqualityType.PrecisionEquality,
        Precision = precision,
        IsNullable = isNullable
      };
    }
    else if (memberSymbol.HasAttribute(attributesMetadata.CustomEquality)) {
      var attribute = memberSymbol.GetAttribute(attributesMetadata.CustomEquality);
      var comparerType = ((INamedTypeSymbol)attribute?.ConstructorArguments[0].Value!);

      var comparerTypeName = comparerType.ToFQF();
      var comparerMemberName = (string)attribute?.ConstructorArguments[1].Value!;

      var hasDefault = comparerType.GetMembers().Any(x => x.Name == comparerMemberName && x.IsStatic)
                       || comparerType.GetPropertiesAndFields().Any(x => x.Name == comparerMemberName && x.IsStatic);

      return new EqualityMemberModel {
        MemberName = propertyName, IsField = isField,
        TypeName = typeName,
        EqualityType = EqualityType.CustomEquality,
        ComparerType = comparerTypeName,
        ComparerMemberName = comparerMemberName,
        ComparerHasStaticInstance = hasDefault
      };
    }

    // 字段是"选择加入"的：只有带 [DefaultEquality]（或上面已处理的其他相等性特性）
    // 才参与比较；属性默认参与，除非类型启用了 Explicit 模式。排除字段是最初的设计意图，
    // 3.2.0 重构时丢失，v5 中作为有意的破坏性变更恢复（见 issue #86）。
    var isIgnored = (explicitMode || isField)
                    && !memberSymbol.HasAttribute(attributesMetadata.DefaultEquality);

    return new EqualityMemberModel {
      MemberName = propertyName, IsField = isField,
      TypeName = typeName,
      EqualityType = isIgnored ? EqualityType.IgnoreEquality : EqualityType.DefaultEquality,
    };
  }
}