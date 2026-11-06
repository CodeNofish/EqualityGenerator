using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EqualityGenerator;

/// <summary>
/// GE006 的代码修复：给类型声明加上 partial 修饰符。
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeTypePartialCodeFix))]
[Shared]
public sealed class MakeTypePartialCodeFix : CodeFixProvider {
  // 只处理 GE006
  public override ImmutableArray<string> FixableDiagnosticIds { get; } =
    ImmutableArray.Create(DiagnosticDescriptors.NonPartialType.Id);

  public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

  /// <summary>
  /// 找到诊断对应的类型声明并注册修复操作。
  /// </summary>
  public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
    var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
    if (root == null)
      return;

    var diagnostic = context.Diagnostics.First();
    var diagnosticSpan = diagnostic.Location.SourceSpan;

    // 找到诊断指向的类型声明
    var node = root.FindNode(diagnosticSpan);
    var typeDeclaration = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();

    if (typeDeclaration == null)
      return;

    var codeAction = CodeAction.Create(
      title: "Make type partial",
      createChangedDocument: ct => MakeTypePartialAsync(context.Document, typeDeclaration, ct),
      equivalenceKey: "MakeTypePartial");

    context.RegisterCodeFix(codeAction, diagnostic);
  }

  /// <summary>
  /// 在类型关键字之前插入 partial 修饰符。
  /// </summary>
  private static async Task<Document> MakeTypePartialAsync(
    Document document,
    TypeDeclarationSyntax typeDeclaration,
    CancellationToken cancellationToken) {
    var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
    if (root == null)
      return document;

    // 构造带尾随空格的 partial 关键字
    var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword)
      .WithTrailingTrivia(SyntaxFactory.Space);

    // 插入位置：所有现有修饰符之后、类型关键字之前
    var typeKeywordIndex = typeDeclaration.Modifiers.Count;
    var newModifiers = typeDeclaration.Modifiers.Insert(typeKeywordIndex, partialToken);

    var newTypeDeclaration = typeDeclaration.WithModifiers(newModifiers);
    var newRoot = root.ReplaceNode(typeDeclaration, newTypeDeclaration);

    return document.WithSyntaxRoot(newRoot);
  }
}