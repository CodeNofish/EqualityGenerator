using System;
using System.CodeDom.Compiler;

namespace EqualityGenerator {

  [GeneratedCode(Const.Tool, Const.Version)]
  [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
  public class CustomEqualityAttribute : Attribute {
    public Type EqualityType { get; }
    public string FieldOrPropertyName { get; }

    public CustomEqualityAttribute(Type equalityType, string fieldOrPropertyName = "Default") {
      EqualityType = equalityType;
      FieldOrPropertyName = fieldOrPropertyName;
    }
  }

}
