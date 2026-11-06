using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Globalization;

namespace EqualityGenerator;

/// <summary>
/// 各类相等性生成器共用的常量与代码生成逻辑。
/// </summary>
class EqualityGeneratorBase {
  // 生成代码统一带上的 [GeneratedCode] 标记
  protected const string GeneratedCodeAttributeDeclaration =
    "[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"EqualityGenerator\", \"1.0.0.0\")]";

  // 生成的源码启用可空上下文
  internal const string EnableNullableContext = "#nullable enable";

  // CS0612：无说明的 Obsolete；CS0618：带说明的 Obsolete
  internal const string SuppressObsoleteWarningsPragma = "#pragma warning disable CS0612,CS0618";

  // 抑制类型冲突警告
  internal const string SuppressTypeConflictsWarningsPragma = "#pragma warning disable CS0436";

  protected static readonly string[] EqualsOperatorCodeComment = @"
/// <summary>
/// Indicates whether the object on the left is equal to the object on the right.
/// </summary>
/// <param name=""left"">The left object</param>
/// <param name=""right"">The right object</param>
/// <returns>true if the objects are equal; otherwise, false.</returns>".ToLines();

  protected static readonly string[] NotEqualsOperatorCodeComment = @"
/// <summary>
/// Indicates whether the object on the left is not equal to the object on the right.
/// </summary>
/// <param name=""left"">The left object</param>
/// <param name=""right"">The right object</param>
/// <returns>true if the objects are not equal; otherwise, false.</returns>".ToLines();

  protected const string InheritDocComment = "/// <inheritdoc/>";

  protected static readonly string[] EqualityComparerCodeComment = @"
/// <summary>
/// An equality comparer for the enclosing type that uses the generated equality semantics.
/// </summary>".ToLines();

  protected static readonly string[] EqualityComparerDefaultCodeComment = @"
/// <summary>
/// Gets the default instance of the comparer.
/// </summary>".ToLines();

  /// <summary>
  /// 构造集合比较器表达式，支持自定义元素比较器。
  /// </summary>
  static string GetCollectionComparerExpression(
    string comparerClassName,
    string elementTypeName,
    EqualityMemberModel memberModel) {
    // 指定了元素比较器时，按有无静态实例选择用法
    if (memberModel.ElementComparerType != null) {
      string elementComparerExpr;
      if (memberModel.ElementComparerHasStaticInstance) {
        elementComparerExpr = $"{memberModel.ElementComparerType}.{memberModel.ElementComparerMemberName}";
      }
      else {
        elementComparerExpr = $"new {memberModel.ElementComparerType}()";
      }

      return $"new global::EqualityGenerator.{comparerClassName}<{elementTypeName}>({elementComparerExpr})";
    }

    // 未指定元素比较器，使用默认实例
    return $"global::EqualityGenerator.{comparerClassName}<{elementTypeName}>.Default";
  }

  /// <summary>
  /// 按目标类型为精度字面量补上数值后缀（f / m）。
  /// </summary>
  static string FormatPrecisionLiteral(double precision, string typeName) {
    var formatted = precision.ToString("R", CultureInfo.InvariantCulture);

    return typeName switch {
      "global::System.Single" => formatted + "f",
      "global::System.Decimal" => formatted + "m",
      _ => formatted
    };
  }

  static void BuildEquality(EqualityMemberModel memberModel, IndentedTextWriter writer, string left, string right) {
    if (memberModel.Ignored) {
      return;
    }

    switch (memberModel.EqualityType) {
      case EqualityType.IgnoreEquality:
        break;

      case EqualityType.UnorderedEquality when !memberModel.IsDictionary: {
        var comparer = GetCollectionComparerExpression("UnorderedEqualityComparer", memberModel.TypeName, memberModel);
        writer.WriteLine(
          $"&& {comparer}.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)");
        break;
      }

      case EqualityType.UnorderedEquality when memberModel.IsDictionary:
        writer.WriteLine(
          $"&& global::EqualityGenerator.DictionaryEqualityComparer<{memberModel.TypeName}>.Default.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)");
        break;

      case EqualityType.OrderedEquality: {
        var comparer = GetCollectionComparerExpression("OrderedEqualityComparer", memberModel.TypeName, memberModel);
        writer.WriteLine(
          $"&& {comparer}.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)");
        break;
      }

      case EqualityType.ReferenceEquality:
        writer.WriteLine(
          $"&& global::EqualityGenerator.ReferenceEqualityComparer<{memberModel.TypeName}>.Default.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)");
        break;

      case EqualityType.SetEquality: {
        var comparer = GetCollectionComparerExpression("SetEqualityComparer", memberModel.TypeName, memberModel);
        writer.WriteLine(
          $"&& {comparer}.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)");
        break;
      }

      case EqualityType.StringEquality:
        writer.WriteLine(
          $"&& global::System.StringComparer.{memberModel.StringComparer}.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)");
        break;

      case EqualityType.PrecisionEquality when memberModel.IsNullable: {
        var literal = FormatPrecisionLiteral(memberModel.Precision!.Value, memberModel.TypeName);
        writer.WriteLine(
          $"&& ({left}.{memberModel.MemberName} == {right}.{memberModel.MemberName} || ({left}.{memberModel.MemberName}.HasValue && {right}.{memberModel.MemberName}.HasValue && global::System.Math.Abs({left}.{memberModel.MemberName}.Value - {right}.{memberModel.MemberName}.Value) < {literal}))");
        break;
      }

      case EqualityType.PrecisionEquality: {
        var literal = FormatPrecisionLiteral(memberModel.Precision!.Value, memberModel.TypeName);
        writer.WriteLine(
          $"&& global::System.Math.Abs({left}.{memberModel.MemberName} - {right}.{memberModel.MemberName}) < {literal}");
        break;
      }

      case EqualityType.CustomEquality when memberModel.ComparerHasStaticInstance:
        writer.WriteLine(
          $"&& {memberModel.ComparerType}.{memberModel.ComparerMemberName}.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)");

        break;

      case EqualityType.CustomEquality when !memberModel.ComparerHasStaticInstance:
        writer.WriteLine(
          $"&& new {memberModel.ComparerType}().Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)");

        break;

      case EqualityType.DefaultEquality:
        writer.WriteLine(
          $"&& global::EqualityGenerator.DefaultEqualityComparer<{memberModel.TypeName}>.Default.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)");
        break;
    }
  }

  /// <summary>
  /// 为所有成员生成相等性比较表达式。
  /// </summary>
  internal static void BuildMembersEquality(
    ImmutableArray<EqualityMemberModel> models,
    IndentedTextWriter writer,
    string left = "x",
    string right = "y"
  ) {
    foreach (var model in models) {
      BuildEquality(model, writer, left, right);
    }
  }

  /// <summary>
  /// 按成员的比较方式生成对应的哈希计算代码。
  /// </summary>
  static void BuildHashCode(EqualityMemberModel memberModel, IndentedTextWriter writer, string obj) {
    if (memberModel.Ignored) {
      return;
    }

    switch (memberModel.EqualityType) {
      case EqualityType.IgnoreEquality:
        break;

      case EqualityType.UnorderedEquality when memberModel.IsDictionary:
        BuildHashCodeAdd($"global::EqualityGenerator.DictionaryEqualityComparer<{memberModel.TypeName}>.Default");
        break;

      case EqualityType.UnorderedEquality when !memberModel.IsDictionary:
        BuildHashCodeAdd(GetCollectionComparerExpression("UnorderedEqualityComparer", memberModel.TypeName, memberModel));
        break;

      case EqualityType.OrderedEquality:
        BuildHashCodeAdd(GetCollectionComparerExpression("OrderedEqualityComparer", memberModel.TypeName, memberModel));
        break;

      case EqualityType.ReferenceEquality:
        BuildHashCodeAdd($"global::EqualityGenerator.ReferenceEqualityComparer<{memberModel.TypeName}>.Default");
        break;

      case EqualityType.SetEquality:
        BuildHashCodeAdd(GetCollectionComparerExpression("SetEqualityComparer", memberModel.TypeName, memberModel));
        break;

      case EqualityType.PrecisionEquality:
        break; // 不参与哈希：容差比较无法得到稳定的哈希分桶

      case EqualityType.StringEquality:
        BuildHashCodeAdd($"global::System.StringComparer.{memberModel.StringComparer}");
        break;

      case EqualityType.CustomEquality when memberModel.ComparerHasStaticInstance:
        BuildHashCodeAdd($"{memberModel.ComparerType}.{memberModel.ComparerMemberName}");
        break;

      case EqualityType.CustomEquality when !memberModel.ComparerHasStaticInstance:
        BuildHashCodeAdd($"new {memberModel.ComparerType}()");
        break;

      case EqualityType.DefaultEquality:
        BuildHashCodeAdd($"global::EqualityGenerator.DefaultEqualityComparer<{memberModel.TypeName}>.Default");
        break;
    }

    void BuildHashCodeAdd(string comparer) {
      writer.WriteLine("hashCode.Add(");
      writer.Indent++;
      writer.WriteLine($"{obj}.{memberModel.MemberName}!,");
      writer.WriteLine(comparer);
      writer.Indent--;
      writer.WriteLine(");");
    }
  }

  public static void BuildMembersHashCode(ImmutableArray<EqualityMemberModel> models, IndentedTextWriter writer, string obj = "obj") {
    foreach (var model in models) {
      BuildHashCode(model, writer, obj);
    }
  }

  protected static readonly string[] InequalitiesMethodComment = @"
/// <summary>
/// Returns the inequalities between two instances.
/// </summary>
/// <param name=""x"">The first instance to compare.</param>
/// <param name=""y"">The second instance to compare.</param>
/// <param name=""path"">The base path for difference reporting.</param>
/// <returns>An enumerable of differences, where each difference contains the path, left value, and right value.</returns>".ToLines();

  static string GetComparerEquals(EqualityMemberModel memberModel, string left, string right) {
    return memberModel.EqualityType switch {
      EqualityType.IgnoreEquality => null!,

      EqualityType.UnorderedEquality when memberModel.IsDictionary =>
        $"global::EqualityGenerator.DictionaryEqualityComparer<{memberModel.TypeName}>.Default.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)",

      EqualityType.UnorderedEquality =>
        $"{GetCollectionComparerExpression("UnorderedEqualityComparer", memberModel.TypeName, memberModel)}.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)",

      EqualityType.OrderedEquality =>
        $"{GetCollectionComparerExpression("OrderedEqualityComparer", memberModel.TypeName, memberModel)}.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)",

      EqualityType.ReferenceEquality =>
        $"global::EqualityGenerator.ReferenceEqualityComparer<{memberModel.TypeName}>.Default.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)",

      EqualityType.SetEquality =>
        $"{GetCollectionComparerExpression("SetEqualityComparer", memberModel.TypeName, memberModel)}.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)",

      EqualityType.PrecisionEquality when memberModel.IsNullable =>
        $"({left}.{memberModel.MemberName} == {right}.{memberModel.MemberName} || ({left}.{memberModel.MemberName}.HasValue && {right}.{memberModel.MemberName}.HasValue && global::System.Math.Abs({left}.{memberModel.MemberName}.Value - {right}.{memberModel.MemberName}.Value) < {FormatPrecisionLiteral(memberModel.Precision!.Value, memberModel.TypeName)}))",

      EqualityType.PrecisionEquality =>
        $"(global::System.Math.Abs({left}.{memberModel.MemberName} - {right}.{memberModel.MemberName}) < {FormatPrecisionLiteral(memberModel.Precision!.Value, memberModel.TypeName)})",

      EqualityType.StringEquality =>
        $"global::System.StringComparer.{memberModel.StringComparer}.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)",

      EqualityType.CustomEquality when memberModel.ComparerHasStaticInstance =>
        $"{memberModel.ComparerType}.{memberModel.ComparerMemberName}.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)",

      EqualityType.CustomEquality =>
        $"new {memberModel.ComparerType}().Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)",

      EqualityType.DefaultEquality =>
        $"global::EqualityGenerator.DefaultEqualityComparer<{memberModel.TypeName}>.Default.Equals({left}.{memberModel.MemberName}!, {right}.{memberModel.MemberName}!)",

      _ => null!
    };
  }

  /// <summary>
  /// 生成单个成员的差异报告代码，集合类型会分派到专门的生成方法。
  /// </summary>
  static void BuildMemberInequality(EqualityMemberModel memberModel, IndentedTextWriter writer, string left, string right, string pathExpr) {
    if (memberModel.Ignored) {
      return;
    }

    var comparerEquals = GetComparerEquals(memberModel, left, right);
    if (comparerEquals == null) return;

    switch (memberModel.EqualityType) {
      case EqualityType.OrderedEquality:
        BuildOrderedCollectionInequalities(memberModel, writer, left, right, pathExpr);
        break;

      case EqualityType.UnorderedEquality when !memberModel.IsDictionary:
      case EqualityType.SetEquality:
        BuildUnorderedCollectionInequalities(memberModel, writer, left, right, pathExpr);
        break;

      case EqualityType.UnorderedEquality when memberModel.IsDictionary:
        BuildDictionaryInequalities(memberModel, writer, left, right, pathExpr);
        break;

      default:
        // 普通成员：比较不等时直接报告该成员
        writer.WriteLine($"if (!{comparerEquals})");
        writer.WriteLine(1,
          $"yield return new global::EqualityGenerator.Inequality({pathExpr}.Append(global::EqualityGenerator.MemberPathSegment.{(memberModel.IsField ? "Field" : "Property")}(\"{memberModel.MemberName}\")), {left}.{memberModel.MemberName}, {right}.{memberModel.MemberName});");
        break;
    }
  }

  /// <summary>
  /// 生成有序集合按下标逐项比较的差异报告代码。
  /// </summary>
  static void BuildOrderedCollectionInequalities(EqualityMemberModel memberModel, IndentedTextWriter writer, string left, string right, string pathExpr) {
    var comparerEquals = GetComparerEquals(memberModel, left, right);

    writer.WriteLine($"if (!{comparerEquals})");
    writer.AppendOpenBracket();

    writer.WriteLine($"var __propPath = {pathExpr}.Append(global::EqualityGenerator.MemberPathSegment.{(memberModel.IsField ? "Field" : "Property")}(\"{memberModel.MemberName}\"));");
    if (memberModel.IsValueTypeCollection) {
      writer.WriteLine($"var __xList = new global::System.Collections.Generic.List<{memberModel.TypeName}>({left}.{memberModel.MemberName});");
      writer.WriteLine($"var __yList = new global::System.Collections.Generic.List<{memberModel.TypeName}>({right}.{memberModel.MemberName});");
    }
    else {
      writer.WriteLine(
        $"var __xList = {left}.{memberModel.MemberName} is null ? new global::System.Collections.Generic.List<{memberModel.TypeName}>() : new global::System.Collections.Generic.List<{memberModel.TypeName}>({left}.{memberModel.MemberName});");
      writer.WriteLine(
        $"var __yList = {right}.{memberModel.MemberName} is null ? new global::System.Collections.Generic.List<{memberModel.TypeName}>() : new global::System.Collections.Generic.List<{memberModel.TypeName}>({right}.{memberModel.MemberName});");
    }

    writer.WriteLine("var __maxLen = global::System.Math.Max(__xList.Count, __yList.Count);");
    writer.WriteLine();
    writer.WriteLine("for (var __i = 0; __i < __maxLen; __i++)");
    writer.AppendOpenBracket();

    if (memberModel.EquatableElementTypeName != null) {
      // 元素类型带 [Equatable]：深入到逐属性的差异
      writer.WriteLine("if (__i < __xList.Count && __i < __yList.Count)");
      writer.AppendOpenBracket();
      writer.WriteLine(
        $"foreach (var __ineq in {memberModel.EquatableElementTypeName}.EqualityComparer.Default.Inequalities(__xList[__i], __yList[__i], __propPath.Append(global::EqualityGenerator.MemberPathSegment.Index(__i))))");
      writer.WriteLine(1, "yield return __ineq;");
      writer.AppendCloseBracket();
      writer.WriteLine("else");
      writer.AppendOpenBracket();
      writer.WriteLine("var __xVal = __i < __xList.Count ? (object?)__xList[__i] : null;");
      writer.WriteLine("var __yVal = __i < __yList.Count ? (object?)__yList[__i] : null;");
      writer.WriteLine("yield return new global::EqualityGenerator.Inequality(__propPath.Append(global::EqualityGenerator.MemberPathSegment.Index(__i)), __xVal, __yVal);");
      writer.AppendCloseBracket();
    }
    else {
      // 元素类型不支持 [Equatable]：只做浅层比较
      writer.WriteLine("var __xVal = __i < __xList.Count ? (object?)__xList[__i] : null;");
      writer.WriteLine("var __yVal = __i < __yList.Count ? (object?)__yList[__i] : null;");
      writer.WriteLine("if (!global::System.Object.Equals(__xVal, __yVal))");
      writer.WriteLine(1, "yield return new global::EqualityGenerator.Inequality(__propPath.Append(global::EqualityGenerator.MemberPathSegment.Index(__i)), __xVal, __yVal);");
    }

    writer.AppendCloseBracket();

    writer.AppendCloseBracket();
  }

  /// <summary>
  /// 生成无序集合/Set 的差异报告代码。
  /// </summary>
  static void BuildUnorderedCollectionInequalities(EqualityMemberModel memberModel, IndentedTextWriter writer, string left, string right, string pathExpr) {
    var comparerEquals = GetComparerEquals(memberModel, left, right);
    var comparerClassName = memberModel.EqualityType == EqualityType.SetEquality ? "SetEqualityComparer" : "UnorderedEqualityComparer";
    var comparer = GetCollectionComparerExpression(comparerClassName, memberModel.TypeName, memberModel);

    writer.WriteLine($"if (!{comparerEquals})");
    writer.AppendOpenBracket();

    writer.WriteLine($"var __propPath = {pathExpr}.Append(global::EqualityGenerator.MemberPathSegment.{(memberModel.IsField ? "Field" : "Property")}(\"{memberModel.MemberName}\"));");
    // 差异算法（重复次数、元素比较器、null 处理）由运行时比较器统一实现，
    // 集中在一处便于测试，不必把算法复制到每个生成的类型里。
    writer.WriteLine($"foreach (var __ineq in {comparer}.Inequalities({left}.{memberModel.MemberName}, {right}.{memberModel.MemberName}, __propPath))");
    writer.WriteLine(1, "yield return __ineq;");

    writer.AppendCloseBracket();
  }

  /// <summary>
  /// 生成字典按键逐项比较的差异报告代码。
  /// </summary>
  static void BuildDictionaryInequalities(EqualityMemberModel memberModel, IndentedTextWriter writer, string left, string right, string pathExpr) {
    var comparerEquals = GetComparerEquals(memberModel, left, right);

    writer.WriteLine($"if (!{comparerEquals})");
    writer.AppendOpenBracket();

    writer.WriteLine($"var __propPath = {pathExpr}.Append(global::EqualityGenerator.MemberPathSegment.{(memberModel.IsField ? "Field" : "Property")}(\"{memberModel.MemberName}\"));");
    writer.WriteLine($"var __xDict = {left}.{memberModel.MemberName};");
    writer.WriteLine($"var __yDict = {right}.{memberModel.MemberName};");
    writer.WriteLine();

    if (memberModel.IsValueTypeCollection) {
      // 值类型集合无需判空，直接输出比较代码块
      writer.WriteLine("// Value-type collection — null checks skipped");
      writer.AppendOpenBracket();
    }
    else {
      writer.WriteLine("if (__xDict is null && __yDict is not null)");
      writer.AppendOpenBracket();
      writer.WriteLine("foreach (var __kvp in __yDict)");
      writer.WriteLine(1, "yield return new global::EqualityGenerator.Inequality(__propPath.Append(global::EqualityGenerator.MemberPathSegment.Key(__kvp.Key)), null, __kvp.Value);");
      writer.AppendCloseBracket();
      writer.WriteLine("else if (__xDict is not null && __yDict is null)");
      writer.AppendOpenBracket();
      writer.WriteLine("foreach (var __kvp in __xDict)");
      writer.WriteLine(1, "yield return new global::EqualityGenerator.Inequality(__propPath.Append(global::EqualityGenerator.MemberPathSegment.Key(__kvp.Key)), __kvp.Value, null);");
      writer.AppendCloseBracket();
      writer.WriteLine("else if (__xDict is not null && __yDict is not null)");
      writer.AppendOpenBracket();
    }

    // 检查 x 中被删除或值发生变化的键
    writer.WriteLine("foreach (var __kvp in __xDict)");
    writer.AppendOpenBracket();
    writer.WriteLine("if (!__yDict.TryGetValue(__kvp.Key, out var __yVal))");
    writer.WriteLine(1, "yield return new global::EqualityGenerator.Inequality(__propPath.Append(global::EqualityGenerator.MemberPathSegment.Key(__kvp.Key)), __kvp.Value, null);");

    if (memberModel.EquatableElementTypeName != null) {
      // 值类型带 [Equatable]：深入到逐属性的差异
      writer.WriteLine("else");
      writer.AppendOpenBracket();
      writer.WriteLine(
        $"foreach (var __ineq in {memberModel.EquatableElementTypeName}.EqualityComparer.Default.Inequalities(__kvp.Value, __yVal, __propPath.Append(global::EqualityGenerator.MemberPathSegment.Key(__kvp.Key))))");
      writer.WriteLine(1, "yield return __ineq;");
      writer.AppendCloseBracket();
    }
    else {
      writer.WriteLine("else if (!global::System.Object.Equals(__kvp.Value, __yVal))");
      writer.WriteLine(1, "yield return new global::EqualityGenerator.Inequality(__propPath.Append(global::EqualityGenerator.MemberPathSegment.Key(__kvp.Key)), __kvp.Value, __yVal);");
    }

    writer.AppendCloseBracket();

    // 检查 y 中新增的键
    writer.WriteLine("foreach (var __kvp in __yDict)");
    writer.AppendOpenBracket();
    writer.WriteLine("if (!__xDict.ContainsKey(__kvp.Key))");
    writer.WriteLine(1, "yield return new global::EqualityGenerator.Inequality(__propPath.Append(global::EqualityGenerator.MemberPathSegment.Key(__kvp.Key)), null, __kvp.Value);");
    writer.AppendCloseBracket();

    writer.AppendCloseBracket();

    writer.AppendCloseBracket();
  }

  internal static void BuildMembersInequalities(
    ImmutableArray<EqualityMemberModel> models,
    IndentedTextWriter writer,
    string left = "x",
    string right = "y",
    string pathExpr = "path"
  ) {
    foreach (var model in models) {
      BuildMemberInequality(model, writer, left, right, pathExpr);
    }
  }

  /// <summary>
  /// Equals/GetHashCode 是否委托给 <c>base.Equals()</c>/<c>base.GetHashCode()</c>。
  /// </summary>
  protected static bool DelegatesToBase(EqualityTypeModel model) =>
    !model.IgnoreInheritedMembers && model.BaseEquality != BaseEqualityOwnership.None;

  /// <summary>
  /// 委托链的起点是否只能通过 <c>base.Equals</c> 到达（手写契约的 class 或任意 record 基类），
  /// 这类基类对（继承来的）比较器不可见，Inequalities 只能通过 <c>__BaseEquals</c> 桥接
  /// 粗粒度地报告基类部分。生成桥接与使用桥接共用这一个判定，避免两者不一致。
  /// </summary>
  protected static bool UsesBaseEqualityBridge(EqualityTypeModel model) =>
    !model.IgnoreInheritedMembers
    && model.BaseEquality is BaseEqualityOwnership.Manual or BaseEqualityOwnership.ComparerBehindManual;

  /// <summary>
  /// 生成器自行实施的类型一致性判定（没有比较器根按真实运行时类型判定时）：
  /// <see cref="EqualsCondition"/> 是 <c>Equals</c> 使用的 <c>this</c>/<c>other</c> 表达式，
  /// <see cref="MismatchCondition"/> 是 <c>Inequalities</c> 据以报告整体差异的 <c>x</c>/<c>y</c> 表达式。
  /// 两个方法共用同一实例，保证判定不会只出现在其中一处。
  /// </summary>
  protected readonly record struct TypeIdentityGuard(string EqualsCondition, string MismatchCondition);

  /// <summary>
  /// 生成与类型一致性判定配套的 Inequalities 代码：类型不同即整体不等，
  /// 直接报告对象本身（无法归因到某个成员）并停止枚举。
  /// </summary>
  protected static void BuildTypeMismatchInequality(IndentedTextWriter writer, TypeIdentityGuard? guard) {
    if (guard is not { } g)
      return;

    writer.WriteLine($"if ({g.MismatchCondition})");
    writer.AppendOpenBracket();
    writer.WriteLine("yield return new global::EqualityGenerator.Inequality(path, x, y);");
    writer.WriteLine("yield break;");
    writer.AppendCloseBracket();
    writer.WriteLine();
  }

  /// <summary>
  /// 生成私有的 <c>__BaseEquals</c> 桥接方法，让嵌套比较器能通过非虚的 base 调用
  /// 触及基类型的 <c>Equals</c>（在比较器类型内部无法直接写出这种调用）。
  /// <paramref name="argument"/> 是传给 <c>base.Equals</c> 并经转换以绑定目标重载的表达式；
  /// record 则直接绑定到其强类型 <c>Equals</c>。
  /// </summary>
  protected static void BuildBaseEqualityBridge(EqualityTypeModel model, IndentedTextWriter writer, string argument) {
    writer.WriteLine();
    writer.WriteLine("// Non-virtual bridge to the base type's Equals, for use by the nested comparer.");
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine($"private bool __BaseEquals({model.BaseTypeFullname}? other) => base.Equals({argument});");
  }

  /// <summary>
  /// 生成 Inequalities 中基类部分的代码。<paramref name="memberLevel"/> 表示逐条转发直接基类
  /// 比较器的差异（仅当该基类拥有自己的比较器时有效）；<paramref name="coarse"/> 表示
  /// <c>__BaseEquals</c> 桥接判定不一致时报告一条整体差异（用于无法在成员级别委托的基类）。
  /// 两者最多只会启用一个。
  /// </summary>
  protected static void BuildBaseInequalities(EqualityTypeModel model, IndentedTextWriter writer, bool memberLevel, bool coarse) {
    if (memberLevel) {
      writer.WriteLine($"foreach (var __ineq in {model.BaseTypeFullname}.EqualityComparer.Default.Inequalities(x, y, path))");
      writer.WriteLine(1, "yield return __ineq;");
      writer.WriteLine();
    }
    else if (coarse) {
      writer.WriteLine("if (!x.__BaseEquals(y))");
      writer.WriteLine(1, "yield return new global::EqualityGenerator.Inequality(path, x, y);");
      writer.WriteLine();
    }
  }
}