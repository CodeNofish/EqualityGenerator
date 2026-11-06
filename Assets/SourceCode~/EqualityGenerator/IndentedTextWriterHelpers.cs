using System.CodeDom.Compiler;

namespace EqualityGenerator;

/// <summary>
/// 生成代码时使用的 IndentedTextWriter 辅助方法。
/// </summary>
public static class IndentedTextWriterHelpers {
  /// <summary>
  /// 以临时缩进写一行，写完后恢复缩进。
  /// </summary>
  public static void WriteLine(this IndentedTextWriter writer, int offset, string value) {
    writer.Indent += offset;
    writer.WriteLine(value);
    writer.Indent -= offset;
  }

  /// <summary>
  /// 逐行写出多行文本。
  /// </summary>
  public static void WriteLines(this IndentedTextWriter writer, string[] lines) {
    foreach (var line in lines) {
      writer.WriteLine(line);
    }
  }

  public static void AppendOpenBracket(this IndentedTextWriter writer) {
    writer.WriteLine("{");
    writer.Indent++;
  }

  public static void AppendCloseBracket(this IndentedTextWriter writer) {
    writer.Indent--;
    writer.WriteLine("}");
  }

  public static void UnwindOpenedBrackets(this IndentedTextWriter writer) {
    while (writer.Indent != 0) {
      AppendCloseBracket(writer);
    }
  }
}