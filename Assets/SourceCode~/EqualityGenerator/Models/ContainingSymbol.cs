using Microsoft.CodeAnalysis.CSharp;

namespace EqualityGenerator;

/// <summary>
/// ContainingSymbol
/// 外层包含符号（命名空间或类型）的模型。
/// </summary>
internal record ContainingSymbol {
  public required string Name { get; init; }
}

/// <summary>
/// ContainingSymbol
/// 外层是命名空间
/// </summary>
internal sealed record NamespaceContainingSymbol : ContainingSymbol { }

/// <summary>
/// ContainingSymbol
/// 外层是类型，Kind 表示 class / struct / record 等种类
/// </summary>
internal sealed record TypeContainingSymbol : ContainingSymbol {
  public SyntaxKind? Kind { get; init; }
}