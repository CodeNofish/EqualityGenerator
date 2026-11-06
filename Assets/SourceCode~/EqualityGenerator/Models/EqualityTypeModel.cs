using Microsoft.CodeAnalysis.CSharp;

namespace EqualityGenerator;

/// <summary>
/// 基类继承链对相等性的"归属"情况。由转换器沿链向上遍历一次得出：
/// 任意祖先存在生成的比较器就以其为准；其下是否有手写契约决定
/// <see cref="Comparer"/> 还是 <see cref="ComparerBehindManual"/>；
/// 整条链没有比较器但存在手写完整契约时，基类归为 <see cref="Manual"/>。
/// </summary>
internal enum BaseEqualityOwnership {
  /// <summary>
  /// 没有祖先拥有相等性，继承来的成员逐个显式比较。
  /// </summary>
  None,

  /// <summary>
  /// 最近的相等性祖先提供生成的 EqualityComparer（[Equatable] 或跨程序集），
  /// 且中间没有手写契约；直接委托给该比较器。
  /// </summary>
  Comparer,

  /// <summary>
  /// 存在带比较器的祖先，但它与本类型之间夹着一套手写完整契约。
  /// base.Equals() 会遵循该契约，而继承来的比较器看不到这些成员，
  /// 因此深比较需走基类相等性桥接，而非在成员级别委托。
  /// </summary>
  ComparerBehindManual,

  /// <summary>
  /// 祖先手写了完整的 Equals/GetHashCode 契约，且整条链上没有比较器；
  /// 通过 base.Equals()/base.GetHashCode() 及桥接来委托。
  /// </summary>
  Manual,
}

/// <summary>
/// 生成一个类型的相等性代码所需的全部模型数据。
/// </summary>
internal sealed record EqualityTypeModel {
  // 类型种类（class / struct / record 等）与类型名
  public required SyntaxKind SyntaxKind { get; init; }

  public required string TypeName { get; init; }

  /// <summary>
  /// 基类型的全名（如 global::Namespace.BaseClass），
  /// 用于生成 BaseClass.EqualityComparer.Default 之类的调用。
  /// </summary>
  public string? BaseTypeFullname { get; init; }

  public required bool IsSealed { get; init; }
  public required EquatableImmutableArray<ContainingSymbol> ContainingSymbols { get; init; }
  public required AttributesMetadata AttributesMetadata { get; init; }
  public required bool ExplicitMode { get; init; }
  public required bool IgnoreInheritedMembers { get; init; }
  public required EquatableImmutableArray<EqualityMemberModel> BuildEqualityModels { get; init; }

  /// <summary>
  /// 最近的相等性祖先之下、自身没有相等性的普通祖先中的成员，
  /// 由本类型显式比较；IgnoreInheritedMembers=true 时为空。
  /// </summary>
  public EquatableImmutableArray<EqualityMemberModel> InheritedEqualityModels { get; init; }

  public required string Fullname { get; init; }

  /// <summary>
  /// 继承链的相等性归属，见 <see cref="BaseEqualityOwnership"/>。
  /// </summary>
  public BaseEqualityOwnership BaseEquality { get; init; }

  /// <summary>
  /// 是否从祖先继承了生成的 EqualityComparer；若是，本类型声明的嵌套比较器
  /// 会隐藏它，需要加 <c>new</c> 修饰符。
  /// </summary>
  public bool BaseHasEquatable => BaseEquality is BaseEqualityOwnership.Comparer or BaseEqualityOwnership.ComparerBehindManual;

  /// <summary>
  /// <c>base.Equals(...)</c> 委托调用的参数表达式，通过强制转换让重载解析绑定到目标重载：
  /// record 直接基类的强类型 Equals、直接基类生成的强类型 Equals、手写祖先的公开强类型 Equals，
  /// 或手写 <c>Equals(object)</c> 时的 <c>object</c>。
  /// <see cref="BaseEquality"/> 为 <see cref="BaseEqualityOwnership.None"/> 时为 null。
  /// </summary>
  public string? BaseEqualsArgument { get; init; }

  /// <summary>
  /// 对 class 而言，是否生成 == 和 != 运算符。
  /// <br/>对 struct（总是生成）与 record（由编译器生成）无影响。
  /// </summary>
  public required bool GenerateClassEqualityOperators { get; init; }
}