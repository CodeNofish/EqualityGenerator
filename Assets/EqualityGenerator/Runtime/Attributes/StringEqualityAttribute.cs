using System;
using System.CodeDom.Compiler;

namespace EqualityGenerator {

  [GeneratedCode(Const.Tool, Const.Version)]
  [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
  public class StringEqualityAttribute : Attribute {
    public StringComparison ComparisonType { get; }

    public StringEqualityAttribute(StringComparison comparisonType) {
      ComparisonType = comparisonType;
    }
  }

}
