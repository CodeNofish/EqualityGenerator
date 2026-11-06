namespace System.Runtime.CompilerServices {
  /// <summary>
  /// Indicates that compiler support for a particular feature is required 
  /// for the location where this attribute is applied.
  /// </summary>
  [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
  internal sealed class CompilerFeatureRequiredAttribute : Attribute {
    public string FeatureName { get; }
    public bool IsOptional { get; set; }

    public CompilerFeatureRequiredAttribute(string featureName) {
      FeatureName = featureName;
    }

    public const string RefStructs = nameof(RefStructs);
    public const string RequiredMembers = nameof(RequiredMembers);
  }
}