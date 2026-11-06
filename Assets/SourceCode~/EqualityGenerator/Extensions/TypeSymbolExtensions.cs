using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace EqualityGenerator;

/// <summary>
/// 分析器使用的 <see cref="ITypeSymbol"/> 类型判断扩展方法。
/// </summary>
static class TypeSymbolExtensions {
  // 视作"简单类型"的常用 BCL 类型
  private static readonly HashSet<string> PrimitiveTypeNames = new() {
    "System.Boolean",
    "System.Byte",
    "System.SByte",
    "System.Char",
    "System.Decimal",
    "System.Double",
    "System.Single",
    "System.Int32",
    "System.UInt32",
    "System.Int64",
    "System.UInt64",
    "System.Int16",
    "System.UInt16",
    "System.IntPtr",
    "System.UIntPtr",
    "System.Object",
    "System.String",
    "System.DateTime",
    "System.DateTimeOffset",
    "System.TimeSpan",
    "System.Guid",
    "System.Uri"
  };

  /// <summary>
  /// 判断是否为集合（实现 IEnumerable&lt;T&gt;），string 除外。
  /// </summary>
  public static bool IsCollection(this ITypeSymbol typeSymbol) {
    // string 不算集合
    if (typeSymbol.SpecialType == SpecialType.System_String)
      return false;

    // 数组直接算集合
    if (typeSymbol is IArrayTypeSymbol)
      return true;

    // 其余看是否实现 IEnumerable<T>
    return typeSymbol.GetIEnumerableTypeArguments() != null;
  }

  /// <summary>
  /// 判断是否为需要 [Equatable] 的复杂用户类型
  ///（排除简单类型、枚举、系统类型和集合）。
  /// </summary>
  public static bool IsComplexType(this ITypeSymbol typeSymbol) {
    // 简单类型（含枚举）不算复杂类型
    if (IsPrimitiveType(typeSymbol))
      return false;

    // 可空的简单类型也不算
    if (typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } namedType) {
      if (IsPrimitiveType(namedType.TypeArguments[0]))
        return false;
    }

    // 集合不算
    if (typeSymbol.IsCollection())
      return false;

    // 常见的 System 类型不算
    if (IsWellKnownSystemType(typeSymbol))
      return false;

    // 接口不算
    if (typeSymbol.TypeKind == TypeKind.Interface)
      return false;

    // 已有相等性（特性或 IEquatable<T>）的不算
    if (typeSymbol.HasProperEquality())
      return false;

    // 成员全部可按值比较的 record / struct 也不算
    if (typeSymbol is INamedTypeSymbol nts &&
        (nts.IsRecord || typeSymbol.TypeKind == TypeKind.Struct)) {
      if (HasDeepValueEquality(nts, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
        return false;
    }

    // 剩下的自定义 class / struct 视为复杂类型
    return typeSymbol.TypeKind is TypeKind.Class or TypeKind.Struct;
  }

  /// <summary>
  /// 取集合的元素类型（IEnumerable&lt;T&gt; 中的 T）；
  /// 字典返回 KeyValuePair&lt;TKey, TValue&gt;。
  /// </summary>
  public static ITypeSymbol? GetCollectionElementType(this ITypeSymbol typeSymbol) {
    if (typeSymbol is IArrayTypeSymbol arrayType)
      return arrayType.ElementType;

    var arguments = typeSymbol.GetIEnumerableTypeArguments();
    return arguments?.Arguments?[0];
  }

  /// <summary>
  /// 判断是否为字典（实现 IDictionary&lt;TKey, TValue&gt;）。
  /// </summary>
  public static bool IsDictionary(this ITypeSymbol typeSymbol) {
    return typeSymbol.GetIDictionaryTypeArguments() != null;
  }

  /// <summary>
  /// 判断是否为 Set（实现 ISet&lt;T&gt;）。
  /// </summary>
  public static bool IsSet(this ITypeSymbol typeSymbol) {
    return typeSymbol.GetInterface("global::System.Collections.Generic.ISet<T>") != null;
  }

  /// <summary>
  /// 递归检查 record / struct 是否具备"深按值相等"：
  /// 其所有属性/字段都是简单类型、枚举、字符串，或自身也满足深按值相等的类型。
  /// </summary>
  private static bool HasDeepValueEquality(INamedTypeSymbol typeSymbol, HashSet<ITypeSymbol> visited) {
    // 防止循环引用导致无限递归
    if (!visited.Add(typeSymbol))
      return true; // 已在检查中，视为通过以打破循环

    // 逐个检查实例属性与字段
    foreach (var member in typeSymbol.GetMembers()) {
      ITypeSymbol? memberType = member switch {
        IPropertySymbol { IsStatic: false, IsIndexer: false } prop => prop.Type,
        IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } field => field.Type,
        _ => null
      };

      if (memberType == null)
        continue;

      if (!IsDeepValueEquatable(memberType, visited))
        return false;
    }

    return true;
  }

  /// <summary>
  /// 判断类型本身是否按值比较（无需 [Equatable] 也能正确比较）。
  /// </summary>
  private static bool IsDeepValueEquatable(ITypeSymbol typeSymbol, HashSet<ITypeSymbol> visited) {
    // 简单类型（含枚举）天然按值比较
    if (IsPrimitiveType(typeSymbol))
      return true;

    // 可空类型看其基础类型
    if (typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType) {
      var underlyingType = nullableType.TypeArguments[0];
      return IsDeepValueEquatable(underlyingType, visited);
    }

    // 集合默认按引用比较，不算按值比较
    if (typeSymbol.IsCollection())
      return false;

    // 常见的 System 类型视为按值比较
    if (IsWellKnownSystemType(typeSymbol))
      return true;

    // 已有相等性（特性或 IEquatable<T>）的视为按值比较
    if (typeSymbol.HasProperEquality())
      return true;

    // record 与 struct 递归检查成员
    if (typeSymbol is INamedTypeSymbol nts &&
        (nts.IsRecord || typeSymbol.TypeKind == TypeKind.Struct)) {
      return HasDeepValueEquality(nts, visited);
    }

    // 未实现 IEquatable<T> 的 class 按引用比较
    return false;
  }

  /// <summary>
  /// 判断是否为无需生成相等性代码的简单类型。
  /// </summary>
  private static bool IsPrimitiveType(ITypeSymbol typeSymbol) {
    // 枚举自带相等性
    if (typeSymbol.TypeKind == TypeKind.Enum)
      return true;

    if (typeSymbol.SpecialType != SpecialType.None)
      return true;

    var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
      .Replace("global::", "");

    return PrimitiveTypeNames.Contains(fullName);
  }

  /// <summary>
  /// 判断类型是否为自身实现 IEquatable&lt;T&gt;。
  /// 跨程序集看不到 [Equatable] 特性时作为兜底判断。
  /// </summary>
  public static bool ImplementsIEquatable(this ITypeSymbol typeSymbol) {
    if (typeSymbol is not INamedTypeSymbol namedType)
      return false;

    // 找出 T 恰为自身的 IEquatable<T>
    foreach (var iface in namedType.AllInterfaces) {
      // 是否为 IEquatable<T>
      if (iface is not { Name: "IEquatable", TypeArguments.Length: 1, ContainingNamespace: { } ns })
        continue;

      // 命名空间须为 System
      if (ns.ToDisplayString() != "System")
        continue;

      // 类型参数须是自身
      if (SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], namedType))
        return true;
    }

    return false;
  }

  /// <summary>
  /// 判断类型的相等性是否由 Generator.Equals 生成：同程序集看可见的
  /// [Equatable] 特性，跨程序集则看生成的嵌套 EqualityComparer 类
  ///（此时 [Conditional] 特性已被擦除）。
  /// 与 <see cref="HasProperEquality"/> 不同，仅实现 IEquatable&lt;T&gt; 的类型不算，
  /// 因为它们可能仍按引用语义比较（如 ImmutableArray&lt;T&gt;）。
  /// </summary>
  public static bool HasGeneratedEquality(this ITypeSymbol typeSymbol) {
    if (typeSymbol.HasEquatableAttribute())
      return true;

    return typeSymbol is INamedTypeSymbol namedType && namedType.HasGeneratedEqualityComparer();
  }

  /// <summary>
  /// 判断类型是否含有生成的嵌套 EqualityComparer 类。
  /// 跨程序集时特性可能被擦除，因此以此为准。
  /// </summary>
  public static bool HasGeneratedEqualityComparer(this INamedTypeSymbol typeSymbol) {
    var comparerType = typeSymbol.GetTypeMembers("EqualityComparer").FirstOrDefault();
    if (comparerType is null || !comparerType.IsSealed)
      return false;

    // 检查是否实现 IEqualityComparer<T>
    return comparerType.Interfaces.Any(i =>
      i.Name == "IEqualityComparer"
      && i.TypeArguments.Length == 1
      && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], typeSymbol));
  }

  /// <summary>
  /// 判断类型是否已有相等性（[Equatable] 或 IEquatable&lt;T&gt;）。
  /// 同程序集优先看特性；跨程序集（来自元数据）才回退到 IEquatable&lt;T&gt;。
  /// </summary>
  public static bool HasProperEquality(this ITypeSymbol typeSymbol) {
    // 先按特性判断（同程序集适用）
    if (typeSymbol.HasEquatableAttribute())
      return true;

    // 仅对其他程序集的类型回退到 IEquatable<T>；同程序集能看到特性，无需回退
    var isFromMetadata = typeSymbol.Locations.All(l => l.Kind == LocationKind.MetadataFile);
    if (isFromMetadata && typeSymbol.ImplementsIEquatable())
      return true;

    return false;
  }

  /// <summary>
  /// 判断类型是否带 [Equatable] 特性。
  /// 注意：由于特性上有 [Conditional]，对引用程序集中的类型可能失效，
  /// 跨程序集判断应优先使用 <see cref="ImplementsIEquatable"/>。
  /// </summary>
  public static bool HasEquatableAttribute(this ITypeSymbol typeSymbol) {
    if (typeSymbol is not INamedTypeSymbol namedType)
      return false;

    foreach (var attribute in namedType.GetAttributes()) {
      var attrClass = attribute.AttributeClass;
      if (attrClass == null)
        continue;

      // 先按名称判断，通常为 "EquatableAttribute"
      if (attrClass.Name is not ("EquatableAttribute" or "Equatable"))
        continue;

      // 再检查命名空间
      var ns = attrClass.ContainingNamespace;
      if (ns == null || ns.IsGlobalNamespace)
        continue;

      // 检查完整命名空间
      var nsName = ns.ToDisplayString();
      if (nsName == "Generator.Equals")
        return true;

      // ToDisplayString 可能有差异，再用全限定名兜底
      var fullName = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
      if (fullName is "global::Generator.Equals.EquatableAttribute" or "global::Generator.Equals.Equatable")
        return true;
    }

    return false;
  }

  /// <summary>
  /// 判断是否为常见的 BCL 类型（这些类型不算"复杂类型"）。
  /// </summary>
  private static bool IsWellKnownSystemType(ITypeSymbol typeSymbol) {
    var containingNamespace = typeSymbol.ContainingNamespace?.ToDisplayString();
    if (containingNamespace == null)
      return false;

    // 非 System 命名空间的用户类型不在此列
    if (!containingNamespace.StartsWith("System", StringComparison.Ordinal))
      return false;

    // System 下也可能有用户自定义类型，因此只认常见 BCL 类型
    var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
      .Replace("global::", "");

    // 以下常见 BCL 类型不算复杂类型
    return fullName.StartsWith("System.Collections.", StringComparison.Ordinal) ||
           fullName.StartsWith("System.Threading.", StringComparison.Ordinal) ||
           fullName.StartsWith("System.IO.", StringComparison.Ordinal) ||
           fullName.StartsWith("System.Net.", StringComparison.Ordinal) ||
           fullName.StartsWith("System.Text.", StringComparison.Ordinal) ||
           fullName.StartsWith("System.Tuple", StringComparison.Ordinal) ||
           fullName.StartsWith("System.ValueTuple", StringComparison.Ordinal) ||
           fullName == "System.Type" ||
           fullName == "System.Version" ||
           fullName == "System.Exception";
  }
}