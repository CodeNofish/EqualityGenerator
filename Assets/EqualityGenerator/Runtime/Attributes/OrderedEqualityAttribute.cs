using System;
using System.CodeDom.Compiler;

namespace EqualityGenerator {

  [GeneratedCode(Const.Tool, Const.Version)]
  [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
  public class OrderedEqualityAttribute : Attribute {
    public Type? ComparerType { get; }
    public string ComparerMemberName { get; }
    public StringComparison? StringComparison { get; }

    public OrderedEqualityAttribute() {
      ComparerMemberName = "Default";
    }

    public OrderedEqualityAttribute(StringComparison stringComparison) {
      StringComparison = stringComparison;
      ComparerMemberName = "Default";
    }

    public OrderedEqualityAttribute(Type comparerType, string comparerMemberName = "Default") {
      ComparerType = comparerType;
      ComparerMemberName = comparerMemberName;
    }
  }

}
