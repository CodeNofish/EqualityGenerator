using Microsoft.CodeAnalysis;

namespace EqualityGenerator;

/// <summary>
/// 存放 EqualityGenerator 分析器的全部诊断描述符。
/// </summary>
public static class DiagnosticDescriptors {
  // 诊断所属类别
  private const string Category = "EqualityGenerator";

  /// <summary>
  /// GE001：集合属性未指定相等性特性。
  /// </summary>
  public static readonly DiagnosticDescriptor CollectionMissingAttribute = new(
    id: "GE001",
    title: "Collection property missing equality attribute",
    messageFormat: "Collection property '{0}' should have an equality attribute like [OrderedEquality], [UnorderedEquality], or [SetEquality]",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description:
    "Collection properties in an [Equatable] type should specify how elements are compared using [OrderedEquality], [UnorderedEquality], or [SetEquality] attributes. Not reported when the collection type is itself [Equatable], or when the member is marked [DefaultEquality] to opt into the type's own equality.");

  /// <summary>
  /// GE002：属性使用的复杂类型未标记 [Equatable]。
  /// </summary>
  public static readonly DiagnosticDescriptor ComplexTypeMissingEquatable = new(
    id: "GE002",
    title: "Complex object property type lacks [Equatable]",
    messageFormat: "Property '{0}' has type '{1}' which is a complex type without [Equatable] attribute",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Info,
    isEnabledByDefault: true,
    description: "User-defined classes and structs used in [Equatable] types should also have the [Equatable] attribute for proper deep equality comparison.");

  /// <summary>
  /// GE003：集合元素类型未标记 [Equatable]。
  /// </summary>
  public static readonly DiagnosticDescriptor CollectionElementMissingEquatable = new(
    id: "GE003",
    title: "Collection element type lacks [Equatable]",
    messageFormat: "Collection property '{0}' has element type '{1}' which is a complex type without [Equatable] attribute",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Info,
    isEnabledByDefault: true,
    description: "Complex types used as collection elements in [Equatable] types should also have the [Equatable] attribute for proper deep equality comparison.");

  /// <summary>
  /// GE004：使用了相等性特性，但所在类型未标记 [Equatable]。
  /// </summary>
  public static readonly DiagnosticDescriptor OrphanedEqualityAttribute = new(
    id: "GE004",
    title: "Equality attribute used but containing type lacks [Equatable]",
    messageFormat: "Property '{0}' has [{1}] attribute but containing type '{2}' is not marked with [Equatable]",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description: "Equality attributes like [OrderedEquality], [IgnoreEquality], etc. have no effect unless the containing type is marked with [Equatable].");

  /// <summary>
  /// GE005：带 [Equatable] 的类型手写 Equals/GetHashCode。
  /// </summary>
  public static readonly DiagnosticDescriptor ManualEqualsImplementation = new(
    id: "GE005",
    title: "Type with [Equatable] has manual Equals/GetHashCode",
    messageFormat: "Type '{0}' has [Equatable] attribute but also has a manual {1} implementation",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description: "Types with [Equatable] attribute have Equals and GetHashCode generated automatically. Manual implementations may conflict or be overwritten.");

  /// <summary>
  /// GE006：带 [Equatable] 的类型未声明为 partial。
  /// </summary>
  public static readonly DiagnosticDescriptor NonPartialType = new(
    id: "GE006",
    title: "[Equatable] on non-partial type",
    messageFormat: "Type '{0}' has [Equatable] attribute but is not declared as partial",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Error,
    isEnabledByDefault: true,
    description: "Types with [Equatable] attribute must be declared as partial so that the generator can add the Equals and GetHashCode methods.");

  /// <summary>
  /// GE007：同一属性上存在冲突的相等性特性。
  /// </summary>
  public static readonly DiagnosticDescriptor ConflictingAttributes = new(
    id: "GE007",
    title: "Conflicting attributes on same property",
    messageFormat: "Property '{0}' has conflicting equality attributes [{1}] and [{2}]",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Error,
    isEnabledByDefault: true,
    description: "A property can only have one equality attribute. Remove duplicate or conflicting attributes.");

  /// <summary>
  /// GE008：[StringEquality] 用在非字符串类型上。
  /// </summary>
  public static readonly DiagnosticDescriptor StringEqualityOnNonString = new(
    id: "GE008",
    title: "[StringEquality] on non-string type",
    messageFormat: "Property '{0}' has [StringEquality] attribute but its type is '{1}', not string",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Error,
    isEnabledByDefault: true,
    description: "[StringEquality] attribute can only be used on properties of type string.");

  /// <summary>
  /// GE009：集合相等性特性用在非集合类型上。
  /// </summary>
  public static readonly DiagnosticDescriptor CollectionAttributeOnNonCollection = new(
    id: "GE009",
    title: "Collection equality attribute on non-collection type",
    messageFormat: "Property '{0}' has [{1}] attribute but its type '{2}' is not a collection",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Error,
    isEnabledByDefault: true,
    description: "Collection equality attributes like [OrderedEquality], [UnorderedEquality], and [SetEquality] can only be used on collection types (IEnumerable<T>).");

  /// <summary>
  /// GE010：[PrecisionEquality] 用在不支持的类型上。
  /// </summary>
  public static readonly DiagnosticDescriptor PrecisionEqualityOnUnsupportedType = new(
    id: "GE010",
    title: "[PrecisionEquality] on unsupported type",
    messageFormat:
    "Property '{0}' has [PrecisionEquality] attribute but its type is '{1}', which is not a supported numeric type (float, double, decimal, int, long, short, sbyte, or their nullable variants)",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Error,
    isEnabledByDefault: true,
    description: "[PrecisionEquality] attribute can only be used on properties of type float, double, decimal, int, long, short, sbyte, or their nullable variants.");

  /// <summary>
  /// GE011 按类型种类给出的触发原因，与分析器测试共用，避免措辞分散。
  /// </summary>
  public const string RecordOperatorsAlwaysEmittedReason = "the compiler always emits equality operators for records";

  // struct 的触发原因（含义同上）
  /// <inheritdoc cref="RecordOperatorsAlwaysEmittedReason"/>
  public const string StructOperatorsAlwaysGeneratedReason = "equality operators are always generated for structs";

  /// <summary>
  /// GE011：<see cref="EquatableAttribute.GenerateClassEqualityOperators"/> 对 record 和 struct 无效。
  /// </summary>
  public static readonly DiagnosticDescriptor GenerateClassEqualityOperatorsIgnored = new(
    id: "GE011",
    title: $"[Equatable({Const.EquatableAttribute_GenerateClassEqualityOperators})] has no effect on records or structs",
    messageFormat: $"{Const.EquatableAttribute_GenerateClassEqualityOperators} has no effect on '{{0}}' because {{1}}",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description:
    $"The {Const.EquatableAttribute_GenerateClassEqualityOperators} parameter only controls whether the generator emits == and != for classes: records get compiler-generated operators the generator cannot suppress, and a struct that had none would give CS0019 at every use site rather than falling back to reference equality.");

  /// <summary>
  /// GE012：基类已有相等运算符，导致 <see cref="EquatableAttribute.GenerateClassEqualityOperators"/> 的关闭设置无效。
  /// </summary>
  public static readonly DiagnosticDescriptor GenerateClassEqualityOperatorsShadowedByBase = new(
    id: "GE012",
    title: $"[Equatable({Const.EquatableAttribute_GenerateClassEqualityOperators} = false)] has no effect on a derived class",
    messageFormat: $"{Const.EquatableAttribute_GenerateClassEqualityOperators} = false has no effect on '{{0}}' because base type '{{1}}' defines equality operators that also apply to '{{0}}'",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description:
    $"C# operator overload resolution considers operators declared on base types, so omitting == and != on a derived class does not restore reference equality while an ancestor still declares them. If the ancestor is [Equatable], apply {Const.EquatableAttribute_GenerateClassEqualityOperators} = false there as well - to every class in the chain, not just this one. If it declares operators of its own, or comes from a referenced assembly, the opt-out cannot take effect at all.");
}