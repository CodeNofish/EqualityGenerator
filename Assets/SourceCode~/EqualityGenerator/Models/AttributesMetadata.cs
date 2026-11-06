using System.Collections.Generic;
using System.Collections.Immutable;

namespace EqualityGenerator;

/// <summary>
/// 所有相等性特性的元数据集合，以及字符串比较方式的查找表。
/// </summary>
internal sealed record AttributesMetadata(
  AttributeMetadata Equatable,
  AttributeMetadata DefaultEquality,
  AttributeMetadata OrderedEquality,
  AttributeMetadata IgnoreEquality,
  AttributeMetadata UnorderedEquality,
  AttributeMetadata ReferenceEquality,
  AttributeMetadata SetEquality,
  AttributeMetadata StringEquality,
  AttributeMetadata CustomEquality,
  AttributeMetadata PrecisionEquality,
  EquatableImmutableDictionary<long, string> StringComparisonLookup) {
  // 全局共用的唯一实例
  public static AttributesMetadata Instance { get; } = CreateDefault();

  /// <summary>
  /// 创建默认的特性元数据。
  /// </summary>
  internal static AttributesMetadata CreateDefault() {
    var attributesMetadata = new AttributesMetadata(
      Equatable: AttributeMetadata.FromFullName("EqualityGenerator.EquatableAttribute"),
      DefaultEquality: AttributeMetadata.FromFullName("EqualityGenerator.DefaultEqualityAttribute"),
      OrderedEquality: AttributeMetadata.FromFullName("EqualityGenerator.OrderedEqualityAttribute"),
      IgnoreEquality: AttributeMetadata.FromFullName("EqualityGenerator.IgnoreEqualityAttribute"),
      UnorderedEquality: AttributeMetadata.FromFullName("EqualityGenerator.UnorderedEqualityAttribute"),
      ReferenceEquality: AttributeMetadata.FromFullName("EqualityGenerator.ReferenceEqualityAttribute"),
      SetEquality: AttributeMetadata.FromFullName("EqualityGenerator.SetEqualityAttribute"),
      StringEquality: AttributeMetadata.FromFullName("EqualityGenerator.StringEqualityAttribute"),
      CustomEquality: AttributeMetadata.FromFullName("EqualityGenerator.CustomEqualityAttribute"),
      PrecisionEquality: AttributeMetadata.FromFullName("EqualityGenerator.PrecisionEqualityAttribute"),
      StringComparisonLookup: new Dictionary<long, string> {
        [0] = "CurrentCulture",
        [1] = "CurrentCultureIgnoreCase",
        [2] = "InvariantCulture",
        [3] = "InvariantCultureIgnoreCase",
        [4] = "Ordinal",
        [5] = "OrdinalIgnoreCase"
      }.ToImmutableDictionary()
    );

    return attributesMetadata;
  }
}