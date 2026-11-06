using System;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace EqualityGenerator;

/// <summary>
/// 把生成的代码包裹进原有的命名空间与外层类型中。
/// </summary>
internal static class ContainingTypesBuilder {
  /// <summary>
  /// 按由外到内的顺序写出容器声明，再写入正文并补齐右括号。
  /// </summary>
  public static string Build(ImmutableArray<ContainingSymbol> containingSymbols, Action<IndentedTextWriter> content) {
    using var buffer = new StringWriter(new StringBuilder(capacity: 4096));
    using var writer = new IndentedTextWriter(buffer);

    // 模型按由内到外保存，这里反转后从最外层开始写
    foreach (var parentSymbol in containingSymbols.Reverse()) {
      if (parentSymbol is NamespaceContainingSymbol namespaceSymbol) {
        // 命名空间声明前写入可空上下文与警告抑制指令
        writer.WriteLine();
        writer.WriteLine(EqualityGeneratorBase.EnableNullableContext);
        writer.WriteLine(EqualityGeneratorBase.SuppressObsoleteWarningsPragma);
        writer.WriteLine(EqualityGeneratorBase.SuppressTypeConflictsWarningsPragma);
        writer.WriteLine();

        if (!string.IsNullOrEmpty(namespaceSymbol.Name)) {
          writer.WriteLine($"namespace {namespaceSymbol.Name}");
          writer.AppendOpenBracket();
        }
      }
      else if (parentSymbol is TypeContainingSymbol typeContainingSymbol) {
        var keyword = typeContainingSymbol.Kind switch {
          SyntaxKind.ClassDeclaration => "class",
          SyntaxKind.RecordDeclaration => "record",
          SyntaxKind.RecordStructDeclaration => "record struct",
          SyntaxKind.StructDeclaration => "struct",
          var x => throw new ArgumentOutOfRangeException($"Syntax kind {x} not supported")
        };

        writer.WriteLine($"partial {keyword} {parentSymbol.Name}");
        writer.AppendOpenBracket();
      }
    }

    content(writer);

    writer.UnwindOpenedBrackets();

    return buffer.ToString();
  }
}