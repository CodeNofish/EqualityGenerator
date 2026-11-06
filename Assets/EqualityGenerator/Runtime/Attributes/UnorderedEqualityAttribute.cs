using System;
using System.CodeDom.Compiler;

namespace EqualityGenerator {

  [GeneratedCode(Const.Tool, Const.Version)]
  [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
  public class UnorderedEqualityAttribute : Attribute {
    public Type? ComparerType { get; }
    public string ComparerMemberName { get; }
    public StringComparison? StringComparison { get; }

    public UnorderedEqualityAttribute() {
      ComparerMemberName = "Default";
    }

    public UnorderedEqualityAttribute(StringComparison stringComparison) {
      StringComparison = stringComparison;
      ComparerMemberName = "Default";
    }

    public UnorderedEqualityAttribute(Type comparerType, string comparerMemberName = "Default") {
      ComparerType = comparerType;
      ComparerMemberName = comparerMemberName;
    }
  }

}
