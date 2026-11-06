namespace EqualityGenerator;

/// <summary>
/// 成员参与相等性比较的方式，与各相等性特性一一对应。
/// </summary>
internal enum EqualityType {
  DefaultEquality,   // 默认比较
  OrderedEquality,   // 集合按顺序比较
  UnorderedEquality, // 集合忽略顺序比较
  ReferenceEquality, // 只比较引用
  SetEquality,       // 按集合（去重）比较
  StringEquality,    // 按指定的字符串比较方式
  CustomEquality,    // 使用自定义比较器
  PrecisionEquality, // 数值按精度比较
  IgnoreEquality     // 忽略该成员
}