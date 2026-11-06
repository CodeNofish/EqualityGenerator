using System.CodeDom.Compiler;

namespace EqualityGenerator;

/// <summary>
/// 为 struct 生成相等性代码，实现都委托给嵌套比较器。
/// </summary>
internal sealed class StructEqualityGenerator : EqualityGeneratorBase {
  /// <summary>
  /// 生成委托给 EqualityComparer 的运算符、Equals 与 GetHashCode。
  /// </summary>
  static void BuildDelegatingMethods(EqualityTypeModel model, IndentedTextWriter writer) {
    var symbolName = model.Fullname;

    // == 运算符
    writer.WriteLines(EqualsOperatorCodeComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine($"public static bool operator ==({symbolName} left, {symbolName} right) =>");
    writer.WriteLine(1, "EqualityComparer.Default.Equals(left, right);");
    writer.WriteLine();

    // != 运算符
    writer.WriteLines(NotEqualsOperatorCodeComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine($"public static bool operator !=({symbolName} left, {symbolName} right) =>");
    writer.WriteLine(1, "!EqualityComparer.Default.Equals(left, right);");
    writer.WriteLine();

    // 生成 Equals(object?)
    writer.WriteLine(InheritDocComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine("public override bool Equals(object? obj) =>");
    writer.WriteLine(1, $"obj is {symbolName} o && EqualityComparer.Default.Equals(this, o);");
    writer.WriteLine();

    // 生成强类型 Equals(T)
    writer.WriteLine(InheritDocComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine($"public bool Equals({symbolName} other) =>");
    writer.WriteLine(1, "EqualityComparer.Default.Equals(this, other);");
    writer.WriteLine();

    // 生成 GetHashCode()
    writer.WriteLine(InheritDocComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine("public override int GetHashCode() =>");
    writer.WriteLine(1, "EqualityComparer.Default.GetHashCode(this);");
  }

  /// <summary>
  /// 生成嵌套的 EqualityComparer 类。
  /// </summary>
  static void BuildNestedEqualityComparer(EqualityTypeModel model, IndentedTextWriter writer) {
    var symbolName = model.Fullname;

    writer.WriteLine();
    writer.WriteLines(EqualityComparerCodeComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine($"public sealed class EqualityComparer : global::System.Collections.Generic.IEqualityComparer<{symbolName}>");
    writer.AppendOpenBracket();

    // 默认单例
    writer.WriteLines(EqualityComparerDefaultCodeComment);
    writer.WriteLine("public static EqualityComparer Default { get; } = new EqualityComparer();");
    writer.WriteLine();

    // 生成 Equals(T, T)
    BuildComparerEquals(model, writer, symbolName);

    writer.WriteLine();

    // 生成 GetHashCode(T)
    BuildComparerGetHashCode(model, writer, symbolName);

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
      $"public global::System.Collections.Generic.IEnumerable<global::EqualityGenerator.Inequality> Inequalities({symbolName} x, {symbolName} y, global::EqualityGenerator.MemberPath path = default)");
    writer.AppendOpenBracket();

    BuildMembersInequalities(model.BuildEqualityModels, writer, "x", "y");

    // 没有成员时补一个 yield break，确保方法仍是迭代器
    if (model.BuildEqualityModels.Items.Length == 0) {
      writer.WriteLine("yield break;");
    }

    writer.AppendCloseBracket();
  }

  /// <summary>
  /// 生成比较器的 Equals 方法。
  /// </summary>
  static void BuildComparerEquals(EqualityTypeModel model, IndentedTextWriter writer, string symbolName) {
    writer.WriteLine(InheritDocComment);
    writer.WriteLine($"public bool Equals({symbolName} x, {symbolName} y)");
    writer.AppendOpenBracket();

    writer.WriteLine("return true");

    writer.Indent++;
    BuildMembersEquality(model.BuildEqualityModels, writer, "x", "y");
    writer.WriteLine(";");
    writer.Indent--;

    writer.AppendCloseBracket();
  }

  static void BuildComparerGetHashCode(EqualityTypeModel model, IndentedTextWriter writer, string symbolName) {
    writer.WriteLine(InheritDocComment);
    writer.WriteLine($"public int GetHashCode({symbolName} obj)");
    writer.AppendOpenBracket();

    writer.WriteLine("var hashCode = new global::System.HashCode();");
    writer.WriteLine();

    BuildMembersHashCode(model.BuildEqualityModels, writer, "obj");

    writer.WriteLine();
    writer.WriteLine("return hashCode.ToHashCode();");

    writer.AppendCloseBracket();
  }

  public static string Generate(EqualityTypeModel model) {
    var code = ContainingTypesBuilder.Build(model.ContainingSymbols, content: writer => {
      writer.WriteLine($"partial struct {model.TypeName} : global::System.IEquatable<{model.Fullname}>");
      writer.AppendOpenBracket();

      BuildDelegatingMethods(model, writer);

      BuildNestedEqualityComparer(model, writer);

      writer.AppendCloseBracket();
    });

    return code;
  }
}