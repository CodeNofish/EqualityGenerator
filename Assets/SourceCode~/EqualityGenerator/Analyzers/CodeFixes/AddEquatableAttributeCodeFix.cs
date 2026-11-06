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
using Microsoft.CodeAnalysis.Editing;

namespace EqualityGenerator;

/// <summary>
/// GE004 的代码修复：给所在类型加上 [Equatable]。
/// 若类型缺少 partial 修饰符则一并补上。
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddEquatableAttributeCodeFix))]
[Shared]
public sealed class AddEquatableAttributeCodeFix : CodeFixProvider {
  // 只处理 GE004
  public override ImmutableArray<string> FixableDiagnosticIds { get; } =
    ImmutableArray.Create(DiagnosticDescriptors.OrphanedEqualityAttribute.Id);

  public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

  /// <summary>
  /// 找到成员所在的类型并注册修复操作。
  /// </summary>
  public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
    var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
    if (root == null)
      return;

    var diagnostic = context.Diagnostics.First();
    var diagnosticSpan = diagnostic.Location.SourceSpan;

    // 找到带有"孤立"相等性特性的成员
    var node = root.FindNode(diagnosticSpan);
    var memberDeclaration = node.FirstAncestorOrSelf<MemberDeclarationSyntax>();

    // 再向上找到其所在类型
    var typeDeclaration = memberDeclaration?.FirstAncestorOrSelf<TypeDeclarationSyntax>();
    if (typeDeclaration == null)
      return;

    var codeAction = CodeAction.Create(
      title: "Add [Equatable] to containing type",
      createChangedDocument: ct => AddEquatableAttributeAsync(context.Document, typeDeclaration, ct),
      equivalenceKey: "AddEquatableAttribute");

    context.RegisterCodeFix(codeAction, diagnostic);
  }

  /// <summary>
  /// 给类型加 [Equatable]，必要时补上 partial。
  /// </summary>
  private static async Task<Document> AddEquatableAttributeAsync(
    Document document,
    TypeDeclarationSyntax typeDeclaration,
    CancellationToken cancellationToken) {
    var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
    var generator = editor.Generator;

    // 用 SyntaxGenerator 构造 [Equatable] 特性
    var equatableAttribute = generator.Attribute(generator.IdentifierName("Equatable"));

    // 把特性加到类型声明上
    var newTypeDeclaration = (TypeDeclarationSyntax)generator.AddAttributes(typeDeclaration, equatableAttribute);

    // 缺少 partial 时补上
    if (!typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)) {
      newTypeDeclaration = (TypeDeclarationSyntax)generator.WithModifiers(
        newTypeDeclaration,
        generator.GetModifiers(newTypeDeclaration).WithPartial(true));
    }

    editor.ReplaceNode(typeDeclaration, newTypeDeclaration);
    return editor.GetChangedDocument();
  }
}