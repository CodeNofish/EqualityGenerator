using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace EqualityGenerator;

/// <summary>
/// Roslyn 符号的辅助扩展方法。
/// </summary>
static class SymbolExtensions {
  /// <summary>
  /// 依次返回符号，遇到第一个命名空间后停止。
  /// </summary>
  public static IEnumerable<INamespaceOrTypeSymbol> TakeUntilAfterNamespace(this IEnumerable<INamespaceOrTypeSymbol> symbols) {
    _ = symbols ?? throw new System.ArgumentNullException(nameof(symbols));

    foreach (var symbol in symbols) {
      yield return symbol;

      if (symbol is INamespaceSymbol)
        yield break;
    }
  }

  /// <summary>
  /// 由内向外返回符号的所有外层符号，可包含自身。
  /// </summary>
  public static IEnumerable<INamespaceOrTypeSymbol> GetParentSymbols(this ISymbol symbol, bool includeSelf) {
    if (symbol is null) {
      throw new System.ArgumentNullException(nameof(symbol));
    }

    if (includeSelf && symbol is INamespaceOrTypeSymbol self)
      yield return self;

    while (true) {
      symbol = symbol.ContainingSymbol;

      if (symbol is not INamespaceOrTypeSymbol namespaceOrTypeSymbol)
        yield break;

      yield return namespaceOrTypeSymbol;
    }
  }

  /// <summary>
  /// 判断符号是否带指定特性（含继承）。
  /// </summary>
  [SuppressMessage("ReSharper", "ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator", Justification = "Performance")]
  public static bool HasAttribute(this ISymbol symbol, AttributeMetadata metadata) {
    return symbol.GetAttributeIncludingInherited(metadata) != null;
  }

  /// <summary>
  /// 取符号上的指定特性（含继承）。
  /// </summary>
  public static AttributeData? GetAttribute(this ISymbol symbol, AttributeMetadata metadata) {
    return symbol.GetAttributeIncludingInherited(metadata);
  }

  /// <summary>
  /// 是否为手写的 <c>public override bool Equals(object)</c>。
  /// </summary>
  public static bool IsEqualsObjectOverride(this IMethodSymbol method) =>
    method is { Name: "Equals", IsOverride: true, MethodKind: MethodKind.Ordinary, DeclaredAccessibility: Accessibility.Public, Parameters.Length: 1 }
    && method.Parameters[0].Type.SpecialType == SpecialType.System_Object
    && method.ReturnType.SpecialType == SpecialType.System_Boolean;

  /// <summary>
  /// 是否为手写的 <c>public override int GetHashCode()</c>。
  /// </summary>
  public static bool IsGetHashCodeOverride(this IMethodSymbol method) =>
    method is { Name: "GetHashCode", IsOverride: true, DeclaredAccessibility: Accessibility.Public, Parameters.Length: 0 };

  /// <summary>
  /// 沿属性重写链向上查找指定特性。
  /// </summary>
  static AttributeData? GetAttributeIncludingInherited(this ISymbol symbol, AttributeMetadata metadata) {
    var current = symbol;

    while (current != null) {
      foreach (var attribute in current.GetAttributes()) {
        if (metadata.Equals(attribute.AttributeClass)) {
          return attribute;
        }
      }

      // 若是重写属性，继续查找被重写的父属性
      current = (current as IPropertySymbol)?.OverriddenProperty;
    }

    return null;
  }
}