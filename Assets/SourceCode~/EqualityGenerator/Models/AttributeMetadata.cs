using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace EqualityGenerator;

/// <summary>
/// 单个相等性特性的名称信息：全名、命名空间、长名（带 Attribute 后缀）和短名。
/// </summary>
sealed record AttributeMetadata(string FullName, string Namespace, string LongName, string ShortName) {
  /// <summary>
  /// 由特性全名解析出元数据。
  /// </summary>
  public static AttributeMetadata FromFullName(string fullName) {
    var parts = fullName.Split('.');

    var name = parts.Last();

    // 按是否有 Attribute 后缀，分别得到短名与长名
    var shortName = name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, "Attribute".Length) : name;
    var longName = name.EndsWith("Attribute", StringComparison.Ordinal) ? name : name + "Attribute";

    var metadata = new AttributeMetadata
    (
      FullName: fullName,
      Namespace: parts.Length == 1 ? string.Empty : string.Join(".", parts.Take(parts.Length - 1)),
      LongName: longName,
      ShortName: shortName
    );

    return metadata;
  }

  /// <summary>
  /// 判断 Roslyn 类型符号是否就是该特性。
  /// </summary>
  public bool Equals(INamedTypeSymbol? symbol) {
    if (symbol is null) {
      return false;
    }

    // 名称允许带或不带 Attribute 后缀
    var nameEquals = string.Equals(symbol.Name, LongName, StringComparison.Ordinal)
                     || string.Equals(symbol.Name, ShortName, StringComparison.Ordinal);

    // 命名空间也必须一致
    var containingNamespace = symbol.ContainingNamespace.ToDisplayString();
    var namespaceEquals = string.Equals(containingNamespace, Namespace, StringComparison.Ordinal);

    return nameEquals && namespaceEquals;
  }
}