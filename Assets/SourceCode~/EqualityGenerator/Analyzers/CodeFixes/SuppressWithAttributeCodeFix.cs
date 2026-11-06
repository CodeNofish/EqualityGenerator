using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace EqualityGenerator;

/// <summary>
/// 通过 [SuppressMessage] 抑制诊断的修复器。
/// 用于分析器可能过于保守、需要用户自行判断的场景。
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SuppressWithAttributeCodeFix))]
[Shared]
public sealed class SuppressWithAttributeCodeFix : CodeFixProvider {
  // 允许被抑制的诊断
  public override ImmutableArray<string> FixableDiagnosticIds { get; } =
    ImmutableArray.Create(
      DiagnosticDescriptors.CollectionMissingAttribute.Id,        // GE001
      DiagnosticDescriptors.ComplexTypeMissingEquatable.Id,       // GE002
      DiagnosticDescriptors.CollectionElementMissingEquatable.Id, // GE003
      DiagnosticDescriptors.OrphanedEqualityAttribute.Id,         // GE004
      DiagnosticDescriptors.ManualEqualsImplementation.Id);       // GE005

  // 抑制诊断应由用户逐个确认，不提供批量修复
  public override FixAllProvider? GetFixAllProvider() => null;

  /// <summary>
  /// 为每个诊断注册一条抑制修复操作。
  /// </summary>
  public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
    var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
    if (root == null)
      return;

    foreach (var diagnostic in context.Diagnostics) {
      var node = root.FindNode(diagnostic.Location.SourceSpan);

      // 找到要加特性的成员声明
      var member = node.FirstAncestorOrSelf<MemberDeclarationSyntax>();
      if (member == null)
        continue;

      var codeAction = CodeAction.Create(
        title: $"Suppress {diagnostic.Id} with [SuppressMessage]",
        createChangedDocument: ct => AddSuppressMessageAsync(
          context.Document, member, diagnostic, ct),
        equivalenceKey: $"Suppress_{diagnostic.Id}");

      context.RegisterCodeFix(codeAction, diagnostic);
    }
  }

  /// <summary>
  /// 给成员加上对应的 [SuppressMessage] 特性。
  /// </summary>
  private static async Task<Document> AddSuppressMessageAsync(
    Document document,
    MemberDeclarationSyntax member,
    Diagnostic diagnostic,
    CancellationToken cancellationToken) {
    var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
    var generator = editor.Generator;

    // 构造 [SuppressMessage] 特性
    // 形如 [SuppressMessage("Generator.Equals", "GE002:诊断标题")]
    var suppressMessageAttribute = SyntaxFactory.Attribute(
      SyntaxFactory.ParseName("System.Diagnostics.CodeAnalysis.SuppressMessage"),
      SyntaxFactory.AttributeArgumentList(
        SyntaxFactory.SeparatedList(new[] {
          SyntaxFactory.AttributeArgument(
            SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
              SyntaxFactory.Literal("Generator.Equals"))),
          SyntaxFactory.AttributeArgument(
            SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
              SyntaxFactory.Literal($"{diagnostic.Id}:{diagnostic.Descriptor.Title}")))
        })));

    // 把特性插入到成员的最前面
    var attributeList = SyntaxFactory.AttributeList(
      SyntaxFactory.SingletonSeparatedList(suppressMessageAttribute));

    var newMember = member.WithAttributeLists(
      member.AttributeLists.Insert(0, attributeList));

    editor.ReplaceNode(member, newMember);

    return editor.GetChangedDocument();
  }
}