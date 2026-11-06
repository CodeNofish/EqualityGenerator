using System.CodeDom.Compiler;

namespace EqualityGenerator;

/// <summary>
/// 为 record（引用类型）生成相等性代码。
/// </summary>
internal class RecordEqualityGenerator : EqualityGeneratorBase {
  /// <summary>
  /// 生成 EqualityContract 一致性判定。不委托基类时（根 record 或
  /// IgnoreInheritedMembers）由生成的 Equals 自行检查，否则由基类 record 的
  /// Equals 按真实运行时类型检查。Inequalities 同步该判定，以便契约不一致时
  /// 给出差异而不是静默返回空。
  /// </summary>
  static TypeIdentityGuard? TypeIdentity(EqualityTypeModel model) =>
    DelegatesToBase(model)
      ? null
      : new TypeIdentityGuard("EqualityContract == other.EqualityContract", "x.EqualityContract != y.EqualityContract");

  /// <summary>
  /// 生成强类型 Equals 方法。
  /// </summary>
  static void BuildEquals(
    EqualityTypeModel model,
    IndentedTextWriter writer
  ) {
    var symbolName = model.Fullname;

    writer.WriteLine(InheritDocComment);
    writer.WriteLine(GeneratedCodeAttributeDeclaration);
    writer.WriteLine(model.IsSealed
      ? $"public bool Equals({symbolName}? other)"
      : $"public virtual bool Equals({symbolName}? other)");
    writer.AppendOpenBracket();

    writer.WriteLine("return");

    writer.Indent++;

    // record 基类总拥有相等性（编译器合成或手写），因此链到 base.Equals()；
    // 只有根 record（或 IgnoreInheritedMembers）才自行检查契约。
    if (TypeIdentity(model) is { } guard) {
      writer.WriteLine($"!ReferenceEquals(other, null) && {guard.EqualsCondition}");
    }
    else {
      writer.WriteLine($"base.Equals({model.BaseEqualsArgument})");
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

    // 与 BuildEquals 对应：委托基类时接入 base.GetHashCode()，否则以契约类型为种子，
    // 保证 Equals/GetHashCode 契约成立。
    writer.WriteLine(DelegatesToBase(model)
      ? "hashCode.Add(base.GetHashCode());"
      : "hashCode.Add(this.EqualityContract);");

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
  /// 生成完整的 partial record 代码。
  /// </summary>
  public static string Generate(EqualityTypeModel model) {
    var code = ContainingTypesBuilder.Build(model.ContainingSymbols, content: writer => {
      BuildEquals(model, writer);

      writer.WriteLine();

      BuildGetHashCode(model, writer);

      // 没有自身比较器的 record 基类对嵌套比较器的 Inequalities 不可见，
      // 桥接方法让它改问 base.Equals；record 则直接绑定到强类型 Equals。
      if (UsesBaseEqualityBridge(model))
        BuildBaseEqualityBridge(model, writer, "other");

      BuildNestedEqualityComparer(model, writer);
    });

    return code;
  }
}