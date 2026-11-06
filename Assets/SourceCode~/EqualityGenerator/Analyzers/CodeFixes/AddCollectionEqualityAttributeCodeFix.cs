using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace EqualityGenerator;

/// <summary>
/// GE001 的代码修复：为集合属性添加相等性特性。
/// 根据集合类型推荐 [OrderedEquality]、[UnorderedEquality] 或 [SetEquality]。
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddCollectionEqualityAttributeCodeFix))]
[Shared]
public sealed class AddCollectionEqualityAttributeCodeFix : CodeFixProvider {
  // 只处理 GE001
  public override ImmutableArray<string> FixableDiagnosticIds { get; } =
    ImmutableArray.Create(DiagnosticDescriptors.CollectionMissingAttribute.Id);

  public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

  /// <summary>
  /// 注册可用的修复操作，按推荐程度排序。
  /// </summary>
  public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
    var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
    if (root == null)
      return;

    var diagnostic = context.Diagnostics[0];
    var diagnosticSpan = diagnostic.Location.SourceSpan;

    // 找到诊断所在的属性声明
    var node = root.FindNode(diagnosticSpan);
    var propertyDeclaration = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();

    if (propertyDeclaration == null)
      return;

    // 通过语义模型取得属性类型，用于挑选推荐特性
    var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
    if (semanticModel == null)
      return;

    var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration, context.CancellationToken);
    var propertyType = propertySymbol?.Type;

    // 按集合类型确定默认推荐项
    var defaultAttribute = GetDefaultAttribute(propertyType);

    // 按推荐优先级依次注册修复操作
    if (defaultAttribute == "SetEquality") {
      // 集合为 Set：优先 SetEquality
      RegisterCodeFix(context, propertyDeclaration, diagnostic, "SetEquality", isDefault: true);
      RegisterCodeFix(context, propertyDeclaration, diagnostic, "UnorderedEquality", isDefault: false);
      RegisterCodeFix(context, propertyDeclaration, diagnostic, "OrderedEquality", isDefault: false);
    }
    else if (defaultAttribute == "UnorderedEquality") {
      // 集合为字典：优先 UnorderedEquality
      RegisterCodeFix(context, propertyDeclaration, diagnostic, "UnorderedEquality", isDefault: true);
      RegisterCodeFix(context, propertyDeclaration, diagnostic, "OrderedEquality", isDefault: false);
      RegisterCodeFix(context, propertyDeclaration, diagnostic, "SetEquality", isDefault: false);
    }
    else {
      // 其他集合：优先最常用的 OrderedEquality
      RegisterCodeFix(context, propertyDeclaration, diagnostic, "OrderedEquality", isDefault: true);
      RegisterCodeFix(context, propertyDeclaration, diagnostic, "UnorderedEquality", isDefault: false);
      RegisterCodeFix(context, propertyDeclaration, diagnostic, "SetEquality", isDefault: false);
    }
  }

  /// <summary>
  /// 注册一条"添加指定特性"的修复操作。
  /// </summary>
  private static void RegisterCodeFix(
    CodeFixContext context,
    PropertyDeclarationSyntax propertyDeclaration,
    Diagnostic diagnostic,
    string attributeName,
    bool isDefault) {
    // 推荐项在标题中标注 recommended
    var title = isDefault
      ? $"Add [{attributeName}] (recommended)"
      : $"Add [{attributeName}]";

    var codeAction = CodeAction.Create(
      title: title,
      createChangedDocument: ct => AddAttributeAsync(context.Document, propertyDeclaration, attributeName, ct),
      equivalenceKey: $"AddCollectionAttribute_{attributeName}");

    context.RegisterCodeFix(codeAction, diagnostic);
  }

  /// <summary>
  /// 根据集合类型选出推荐的特性。
  /// </summary>
  private static string GetDefaultAttribute(ITypeSymbol? propertyType) {
    if (propertyType == null)
      return "OrderedEquality";

    // ISet<T> 等集合用 SetEquality
    if (propertyType.IsSet())
      return "SetEquality";

    // IDictionary<TKey, TValue> 用 UnorderedEquality
    if (propertyType.IsDictionary())
      return "UnorderedEquality";

    // 其他集合（List、数组等）默认用 OrderedEquality
    return "OrderedEquality";
  }

  /// <summary>
  /// 把特性加到属性上，并保留原有缩进与换行格式。
  /// </summary>
  private static async Task<Document> AddAttributeAsync(
    Document document,
    PropertyDeclarationSyntax propertyDeclaration,
    string attributeName,
    CancellationToken cancellationToken) {
    var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
    if (root == null)
      return document;

    // 构造要添加的特性
    var attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attributeName));
    var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));

    // 把属性原有的前置注释/缩进移到特性上
    var leadingTrivia = propertyDeclaration.GetLeadingTrivia();
    attributeList = attributeList.WithLeadingTrivia(leadingTrivia);

    // 插入到特性列表最前面
    var newAttributeLists = propertyDeclaration.AttributeLists.Insert(0, attributeList);

    // 更新属性声明
    var newPropertyDeclaration = propertyDeclaration
      .WithAttributeLists(newAttributeLists)
      .WithLeadingTrivia(SyntaxFactory.TriviaList()); // 避免前置内容重复

    // 原本没有特性时，需要额外处理换行与缩进
    if (propertyDeclaration.AttributeLists.Count == 0) {
      // 按文档配置的换行符，在特性后补一个换行
      var options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
      var newLine = options.GetOption(FormattingOptions.NewLine);

      var lastAttr = newPropertyDeclaration.AttributeLists[newPropertyDeclaration.AttributeLists.Count - 1];
      var updatedAttrList = lastAttr.WithTrailingTrivia(SyntaxFactory.EndOfLine(newLine));
      newPropertyDeclaration = newPropertyDeclaration.WithAttributeLists(
        newPropertyDeclaration.AttributeLists.Replace(lastAttr, updatedAttrList));

      // 把原来的前置内容还给修饰符或类型
      if (newPropertyDeclaration.Modifiers.Any()) {
        var firstModifier = newPropertyDeclaration.Modifiers[0];
        var updatedModifier = firstModifier.WithLeadingTrivia(leadingTrivia);
        newPropertyDeclaration = newPropertyDeclaration.WithModifiers(
          newPropertyDeclaration.Modifiers.Replace(firstModifier, updatedModifier));
      }
      else {
        newPropertyDeclaration = newPropertyDeclaration.WithType(
          newPropertyDeclaration.Type.WithLeadingTrivia(leadingTrivia));
      }
    }

    var newRoot = root.ReplaceNode(propertyDeclaration, newPropertyDeclaration);
    return document.WithSyntaxRoot(newRoot);
  }
}