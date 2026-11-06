using System.Diagnostics.CodeAnalysis;

[assembly:
  SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>", Scope = "member",
    Target = "~M:Generator.Equals.Models.AttributeMetadataExtensions.HasAttribute(Microsoft.CodeAnalysis.ISymbol,Generator.Equals.Models.AttributeMetadata)~System.Boolean")]

// RS2008：本项目不需要分析器发布跟踪
[assembly: SuppressMessage("MicrosoftCodeAnalysisReleaseTracking", "RS2008:Enable analyzer release tracking", Justification = "Release tracking not required for this generator")]

// RS1038：CodeFix 提供器需要引用 Workspaces
[assembly:
  SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1038:Compiler extensions should be implemented in assemblies with compiler-provided references",
    Justification = "Workspaces reference is needed for CodeFix providers")]

// CA1062：Roslyn 保证分析器回调的 context 参数非空
[assembly:
  SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Roslyn guarantees non-null context parameter", Scope = "member",
    Target = "~M:Generator.Equals.Analyzers.EquatableAnalyzer.Initialize(Microsoft.CodeAnalysis.Diagnostics.AnalysisContext)")]