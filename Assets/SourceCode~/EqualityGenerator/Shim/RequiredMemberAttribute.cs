namespace System.Runtime.CompilerServices {
  /// <summary>
  /// Specifies that a type has required members or that a member is required.
  /// Required for C# 11 required keyword.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
  internal sealed class RequiredMemberAttribute : Attribute { }
}