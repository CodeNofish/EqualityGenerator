using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EqualityGenerator;

/// <summary>
/// 增量生成器入口：找出带 [Equatable] 的类型并生成相等性代码。
/// </summary>
[Generator(LanguageNames.CSharp)]
public class EqualsGenerator : IIncrementalGenerator {
  /// <summary>
  /// 注册语法筛选器，把候选类型转换为模型再输出源码。
  /// Unity 内置的 Roslyn 4.1 没有 ForAttributeWithMetadataName，
  /// 因此改用 CreateSyntaxProvider：语法粗筛 + 语义模型确认特性。
  /// </summary>
  public void Initialize(IncrementalGeneratorInitializationContext context) {
    var provider = context.SyntaxProvider.CreateSyntaxProvider(
      static (node, _) => IsCandidateDeclaration(node),
      static (syntaxContext, token) => Transform(syntaxContext, token)
    );

    context.RegisterSourceOutput(provider, (spc, model) => Execute(spc, model));
  }

  /// <summary>
  /// 语法级粗筛：只保留带特性列表的 class / struct / record / record struct 声明，
  /// 是否为 [Equatable] 留给 <see cref="Transform"/> 用语义模型判定。
  /// </summary>
  static bool IsCandidateDeclaration(SyntaxNode node) =>
    node is TypeDeclarationSyntax { AttributeLists.Count: > 0 } declaration
    && declaration.Kind() is SyntaxKind.ClassDeclaration
      or SyntaxKind.StructDeclaration
      or SyntaxKind.RecordDeclaration
      or SyntaxKind.RecordStructDeclaration;

  /// <summary>
  /// 语义确认：取声明符号，收集写在当前声明上的 [Equatable]，
  /// 再构造 4.1 兼容上下文交给转换器。
  /// </summary>
  static EqualityTypeModel? Transform(GeneratorSyntaxContext syntaxContext, CancellationToken token) {
    if (token.IsCancellationRequested) {
      return null;
    }

    var declaration = (TypeDeclarationSyntax)syntaxContext.Node;
    var symbol = syntaxContext.SemanticModel.GetDeclaredSymbol(declaration, token);
    if (symbol is null) {
      return null;
    }

    var equatable = AttributesMetadata.Instance.Equatable;
    var attributes = ImmutableArray.CreateBuilder<AttributeData>();
    foreach (var attribute in symbol.GetAttributes()) {
      if (equatable.Equals(attribute.AttributeClass) && IsAppliedOn(attribute, declaration)) {
        attributes.Add(attribute);
      }
    }

    if (attributes.Count == 0) {
      return null;
    }

    var context = new EquatableAttributeSyntaxContext(declaration, symbol, attributes.ToImmutable());
    return new EqualityTypeModelTransformer(context).Transform(token);
  }

  /// <summary>
  /// 只接受写在当前声明上的特性：partial 类型的其它部分同样是候选节点，
  /// 若不过滤会重复生成同名源文件。
  /// </summary>
  static bool IsAppliedOn(AttributeData attribute, SyntaxNode declaration) {
    var reference = attribute.ApplicationSyntaxReference;
    return reference != null
      && reference.SyntaxTree == declaration.SyntaxTree
      && declaration.Span.Contains(reference.Span);
  }

  /// <summary>
  /// 按类型种类选择生成器，把生成的源码添加到编译中。
  /// </summary>
  static void Execute(SourceProductionContext productionContext, EqualityTypeModel? model) {
    if (productionContext.CancellationToken.IsCancellationRequested || model is null) {
      return;
    }

    var source = model.SyntaxKind switch {
      SyntaxKind.StructDeclaration => StructEqualityGenerator.Generate(model),
      SyntaxKind.RecordStructDeclaration => RecordStructEqualityGenerator.Generate(model),
      SyntaxKind.RecordDeclaration => RecordEqualityGenerator.Generate(model),
      SyntaxKind.ClassDeclaration => ClassEqualityGenerator.Generate(model),
      _ => null
    };

    if (source is null) {
      return;
    }

    var fileName = $"{EscapeFileName(model.Fullname)}.EqualityGenerator.g.cs"!;
    productionContext.AddSource(fileName, source);
  }

  static readonly char[] _illegalFilenameChars = ['<', '>', ',', ':'];

  static string EscapeFileName(string fileName) => _illegalFilenameChars
    .Aggregate(new StringBuilder(fileName), (s, c) => s.Replace(c, '_'))
    .ToString();
}