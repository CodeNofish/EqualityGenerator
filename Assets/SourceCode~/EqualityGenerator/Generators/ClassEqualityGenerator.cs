using System.CodeDom.Compiler;

namespace EqualityGenerator;

/// <summary>
/// 为 class 生成 Equals / GetHashCode / 运算符及嵌套比较器的代码。
/// </summary>
internal sealed class ClassEqualityGenerator : EqualityGeneratorBase {
  /// <summary>
  /// 生成"运行时类型必须完全相同"的判定。若基类已有生成的比较器根，
  /// 它会通过 base.Equals 完成该判定；手写基类没有此保证（如 obj is Animal
  /// 会接受任意子类），所以其余情况都要保留检查。Inequalities 也同步该判定，
  /// 以便类型不一致时给出差异而不是静默返回空。
  /// </summary>
  static TypeIdentityGuard? TypeIdentity(EqualityTypeModel model) =>
    model.IgnoreInheritedMembers || !model.BaseHasEquatable
      ? new TypeIdentityGuard("other.GetType() == this.GetType()", "x.GetType() != y.GetType()")
      : null;

  /// <summary>
  /// 生成委托到强类型 Equals 的运算符和接口实现。
  /// </summary>
  static void BuildDelegatingMethods(
    EqualityTypeModel model,
    IndentedTextWriter writer
  ) {
    var symbolName = model.Fullname;

    // == 与 != 运算符按选项决定是否生成
    if (model.GenerateClassEqualityOperators) {
      // == 运算符
      writer.WriteLines(EqualsOperatorCodeComment);
      writer.WriteLine(GeneratedCodeAttributeDeclaration);
      writer.WriteLine($"public static bool operator ==({symbolName}? left, {symbolName}? right) =>");
      writer.WriteLine(1, "EqualityComparer.Default.Equals(left, right);");
      writer.WriteLine();

      // != 运算符
      writer.WriteLines(NotEqualsOperatorCodeComment);
      writer.WriteLine(GeneratedCodeAttributeDeclaration);
      writer.WriteLine($"public static bool operator !=({symbolName}? left, {symbolName}? right) =>");
      writer.WriteLine(1, "!EqualityComparer.Default.Equals(left, right);");
      writer.WriteLine();
    }

    // 生成 Equals(object?)
    writer.WriteLine(InheritDocComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine("public override bool Equals(object? obj) =>");
    writer.WriteLine(1, $"Equals(obj as {symbolName});");
    writer.WriteLine();

    // IEquatable<T>.Equals(T?) 的显式接口实现
    writer.WriteLine(InheritDocComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine($"bool global::System.IEquatable<{symbolName}>.Equals({symbolName}? obj) => Equals((object?) obj);");
    writer.WriteLine();
  }

  /// <summary>
  /// 生成强类型 Equals 方法（含基类委托与成员比较）。
  /// </summary>
  static void BuildEquals(
    EqualityTypeModel model,
    IndentedTextWriter writer
  ) {
    var symbolName = model.Fullname;

    writer.WriteLine(InheritDocComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine($"{(model.IsSealed ? "private" : "protected")} bool Equals({symbolName}? other)");
    writer.AppendOpenBracket();

    writer.WriteLine("if (ReferenceEquals(null, other)) return false;");
    writer.WriteLine("if (ReferenceEquals(this, other)) return true;");
    writer.WriteLine();

    // 有类型判定就自己检查，否则交给基类比较器
    if (TypeIdentity(model) is { } guard) {
      writer.WriteLine($"return {guard.EqualsCondition}");
      writer.Indent++;
      if (DelegatesToBase(model))
        writer.WriteLine($"&& base.Equals({model.BaseEqualsArgument})");
    }
    else {
      writer.WriteLine($"return base.Equals({model.BaseEqualsArgument})");
      writer.Indent++;
    }

    BuildMembersEquality(model.InheritedEqualityModels, writer, "this", "other");
    BuildMembersEquality(model.BuildEqualityModels, writer, "this", "other");
    writer.WriteLine(";");
    writer.Indent--;

    writer.AppendCloseBracket();
  }

  /// <summary>
  /// 生成 GetHashCode，与 Equals 保持一致的组成项。
  /// </summary>
  static void BuildGetHashCode(
    EqualityTypeModel model,
    IndentedTextWriter writer
  ) {
    writer.WriteLine(InheritDocComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine(@"public override int GetHashCode()");
    writer.AppendOpenBracket();

    writer.WriteLine(@"var hashCode = new global::System.HashCode();");
    writer.WriteLine();

    // 与 BuildEquals 对应：基类拥有相等性时接入 base.GetHashCode()，
    // 否则以确切运行时类型为种子，保证 Equals/GetHashCode 契约成立。
    writer.WriteLine(DelegatesToBase(model)
      ? "hashCode.Add(base.GetHashCode());"
      : "hashCode.Add(this.GetType());");

    BuildMembersHashCode(model.InheritedEqualityModels, writer, "this");
    BuildMembersHashCode(model.BuildEqualityModels, writer, "this");

    writer.WriteLine();
    writer.WriteLine("return hashCode.ToHashCode();");

    writer.AppendCloseBracket();
  }

  /// <summary>
  /// 生成嵌套的 EqualityComparer 类。
  /// </summary>
  static void BuildNestedEqualityComparer(
    EqualityTypeModel model,
    IndentedTextWriter writer
  ) {
    var symbolName = model.Fullname;
    // 隐藏基类的 EqualityComparer 时用 new 抑制 CS0108 警告
    var newKeyword = model.BaseHasEquatable ? "new " : "";

    writer.WriteLine();
    writer.WriteLines(EqualityComparerCodeComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine($"public {newKeyword}sealed class EqualityComparer : global::System.Collections.Generic.IEqualityComparer<{symbolName}>");
    writer.AppendOpenBracket();

    // 默认单例
    writer.WriteLines(EqualityComparerDefaultCodeComment);
    writer.WriteLine("public static EqualityComparer Default { get; } = new EqualityComparer();");
    writer.WriteLine();

    // Equals(T?, T?)：委托给类型自身的 Equals
    writer.WriteLine(InheritDocComment);
    writer.WriteLine($"public bool Equals({symbolName}? x, {symbolName}? y)");
    writer.AppendOpenBracket();

    writer.WriteLine("if (ReferenceEquals(x, y)) return true;");
    writer.WriteLine("if (x is null || y is null) return false;");
    writer.WriteLine();
    // 非密封类须通过虚方法 Equals(object?) 分派，以便经基类引用比较时
    // 使用运行时（最派生）类型的相等性。若直接调用 x.Equals(y)，会非虚地
    // 绑定到基类的 protected Equals(T?)，忽略派生类型声明的成员（见 issue #77）。
    // 这样也与已经走虚分派的 GetHashCode 保持一致。
    writer.WriteLine($"return x.Equals({(model.IsSealed ? "y" : "(object?) y")});");

    writer.AppendCloseBracket();

    writer.WriteLine();

    // GetHashCode(T)：委托给类型自身的 GetHashCode
    writer.WriteLine(InheritDocComment);
    writer.WriteLine($"public int GetHashCode({symbolName} obj)");
    writer.AppendOpenBracket();

    writer.WriteLine("return obj.GetHashCode();");

    writer.AppendCloseBracket();

    writer.WriteLine();

    // Inequalities 方法
    BuildInequalitiesMethod(model, writer, symbolName);

    writer.AppendCloseBracket();
  }

  /// <summary>
  /// 生成逐成员列出差异的 Inequalities 方法。
  /// </summary>
  static void BuildInequalitiesMethod(EqualityTypeModel model, IndentedTextWriter writer, string symbolName) {
    writer.WriteLines(InequalitiesMethodComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine(
      $"public global::System.Collections.Generic.IEnumerable<global::EqualityGenerator.Inequality> Inequalities({symbolName}? x, {symbolName}? y, global::EqualityGenerator.MemberPath path = default)");
    writer.AppendOpenBracket();

    writer.WriteLine("if (ReferenceEquals(x, y)) yield break;");
    writer.WriteLine("if (x is null || y is null)");
    writer.AppendOpenBracket();
    writer.WriteLine("yield return new global::EqualityGenerator.Inequality(path, x, y);");
    writer.WriteLine("yield break;");
    writer.AppendCloseBracket();
    writer.WriteLine();

    BuildTypeMismatchInequality(writer, TypeIdentity(model));

    BuildBaseInequalities(model, writer,
      memberLevel: !model.IgnoreInheritedMembers && model.BaseEquality == BaseEqualityOwnership.Comparer,
      coarse: UsesBaseEqualityBridge(model));

    BuildMembersInequalities(model.InheritedEqualityModels, writer, "x", "y");
    BuildMembersInequalities(model.BuildEqualityModels, writer, "x", "y");

    writer.AppendCloseBracket();
  }

  /// <summary>
  /// 生成完整的 partial class 代码。
  /// </summary>
  public static string Generate(EqualityTypeModel model) {
    var code = ContainingTypesBuilder.Build(model.ContainingSymbols, content: writer => {
      writer.WriteLine($"partial class {model.TypeName} : global::System.IEquatable<{model.Fullname}>");
      writer.AppendOpenBracket();

      BuildDelegatingMethods(model, writer);

      BuildEquals(model, writer);

      writer.WriteLine();

      BuildGetHashCode(model, writer);

      if (UsesBaseEqualityBridge(model))
        BuildBaseEqualityBridge(model, writer, model.BaseEqualsArgument!);

      BuildNestedEqualityComparer(model, writer);

      writer.AppendCloseBracket();
    });

    return code;
  }
}