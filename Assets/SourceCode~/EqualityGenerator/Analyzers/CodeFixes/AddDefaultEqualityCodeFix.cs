using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace EqualityGenerator;

/// <summary>
/// 添加 [DefaultEquality] 以消除 GE002、GE003 的修复器。
/// 适用于自带 Equals/GetHashCode 实现的类型（如 protobuf 生成的类型）。
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddDefaultEqualityCodeFix))]
[Shared]
public sealed class AddDefaultEqualityCodeFix : CodeFixProvider {
  // 只处理 GE002、GE003
  public override ImmutableArray<string> FixableDiagnosticIds { get; } =
    ImmutableArray.Create(
      DiagnosticDescriptors.ComplexTypeMissingEquatable.Id,        // GE002
      DiagnosticDescriptors.CollectionElementMissingEquatable.Id); // GE003

  public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

  /// <summary>
  /// 为每个诊断注册一条修复操作。
  /// </summary>
  public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
    var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
    if (root == null)
      return;

    foreach (var diagnostic in context.Diagnostics) {
      var node = root.FindNode(diagnostic.Location.SourceSpan);

      // 找到要加特性的属性
      var property = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
      if (property == null)
        continue;

      var codeAction = CodeAction.Create(
        title: "Add [DefaultEquality] (type has its own equality)",
        createChangedDocument: ct => AddDefaultEqualityAsync(
          context.Document, property, ct),
        equivalenceKey: "AddDefaultEquality");

      context.RegisterCodeFix(codeAction, diagnostic);
    }
  }

  /// <summary>
  /// 给属性加上 [DefaultEquality]。
  /// </summary>
  private static async Task<Document> AddDefaultEqualityAsync(
    Document document,
    PropertyDeclarationSyntax property,
    CancellationToken cancellationToken) {
    var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
    var generator = editor.Generator;

    // 构造 [DefaultEquality] 特性
    var attribute = generator.Attribute(
      generator.IdentifierName("DefaultEquality"));

    // 把特性加到属性上
    var newProperty = generator.AddAttributes(property, attribute);
    editor.ReplaceNode(property, newProperty);

    return editor.GetChangedDocument();
  }
}