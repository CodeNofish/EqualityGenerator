namespace EqualityGenerator;

/// <summary>
/// 单个成员参与相等性比较所需的全部信息。
/// </summary>
internal sealed record EqualityMemberModel {
  // 成员名、是否为字段、类型名、比较方式
  public required string MemberName { get; init; }
  public bool IsField { get; init; }
  public required string TypeName { get; init; }
  public required EqualityType EqualityType { get; init; }

  // 自定义比较器、字符串比较方式、精度等选项
  public string? ComparerType { get; init; }
  public string? ComparerMemberName { get; init; }
  public string? StringComparer { get; init; }
  public bool IsDictionary { get; init; }
  public bool IsValueTypeCollection { get; init; }
  public double? Precision { get; init; }
  public bool IsNullable { get; init; }

  // 标记 [IgnoreEquality] 的成员不参与比较
  public bool Ignored => EqualityType == EqualityType.IgnoreEquality;
  public bool ComparerHasStaticInstance { get; init; }

  // 集合相等性特性所需的元素比较器信息
  public string? ElementComparerType { get; init; }
  public string? ElementComparerMemberName { get; init; }
  public bool ElementComparerHasStaticInstance { get; init; }

  /// <summary>
  /// 元素类型带 [Equatable] 时的全名，用于继续深入比较；元素不可比较时为 null。
  /// </summary>
  public string? EquatableElementTypeName { get; init; }
}