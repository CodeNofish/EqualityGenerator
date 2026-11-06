using System.CodeDom.Compiler;

namespace EqualityGenerator;

/// <summary>
/// 为 record struct 生成相等性代码。
/// </summary>
internal sealed class RecordStructEqualityGenerator : EqualityGeneratorBase {
  /// <summary>
  /// 生成强类型 Equals 方法。
  /// </summary>
  static void BuildEquals(
    EqualityTypeModel model,
    IndentedTextWriter writer) {
    var symbolName = model.Fullname;

    writer.WriteLine(InheritDocComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine($"public bool Equals({symbolName} other)");
    writer.AppendOpenBracket();

    writer.WriteLine("return true");

    writer.Indent++;
    BuildMembersEquality(model.BuildEqualityModels, writer, "this", "other");
    writer.WriteLine(";");
    writer.Indent--;

    writer.AppendCloseBracket();
  }

  /// <summary>
  /// 生成 GetHashCode。
  /// </summary>
  static void BuildGetHashCode(
    EqualityTypeModel model,
    IndentedTextWriter writer) {
    writer.WriteLine(InheritDocComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine(@"public override int GetHashCode()");
    writer.AppendOpenBracket();

    writer.WriteLine(@"var hashCode = new global::System.HashCode();");
    writer.WriteLine();

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
    IndentedTextWriter writer) {
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

    // Equals(T, T)：委托给类型自身的 Equals
    writer.WriteLine(InheritDocComment);
    writer.WriteLine($"public bool Equals({symbolName} x, {symbolName} y)");
    writer.AppendOpenBracket();

    writer.WriteLine("return x.Equals(y);");

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
  /// 生成完整的 partial record struct 代码。
  /// </summary>
  public static string Generate(EqualityTypeModel model) {
    var code = ContainingTypesBuilder.Build(
      model.ContainingSymbols,
      content: writer => {
        BuildEquals(model, writer);
        writer.WriteLine();
        BuildGetHashCode(model, writer);
        BuildNestedEqualityComparer(model, writer);
      });

    return code;
  }
}