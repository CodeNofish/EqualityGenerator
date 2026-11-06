using System;
using Microsoft.CodeAnalysis;

namespace EqualityGenerator;

/// <summary>
/// 读取特性数据的辅助方法。
/// </summary>
static class AttributeDataExtensions {
  /// <summary>
  /// 按名称取特性的命名参数值，未指定时返回 null。
  /// </summary>
  public static object? GetNamedArgumentValue(this AttributeData attributeData, string name) {
    if (string.IsNullOrEmpty(name)) {
      throw new ArgumentNullException(nameof(name));
    }

    if (attributeData == null) {
      throw new ArgumentNullException(nameof(attributeData));
    }

    foreach (var pair in attributeData.NamedArguments) {
      if (pair.Key == name) {
        return pair.Value.Value;
      }
    }

    return null;
  }

  /// <summary>
  /// [Equatable] 是否要求为 class 生成 == 和 !=。
  /// 该规则在此处统一定义，供生成器与分析器共用，避免两处判断不一致。
  /// </summary>
  public static bool GeneratesClassEqualityOperators(this AttributeData equatableAttributeData) =>
    equatableAttributeData.GetNamedArgumentValue(Const.EquatableAttribute_GenerateClassEqualityOperators) is not false;
}