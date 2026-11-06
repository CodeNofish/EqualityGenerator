using System;
using System.CodeDom.Compiler;

namespace EqualityGenerator {

  [GeneratedCode(Const.Tool, Const.Version)]
  [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
  public class SetEqualityAttribute : Attribute {
    public Type? ComparerType { get; }
    public string ComparerMemberName { get; }
    public StringComparison? StringComparison { get; }

    public SetEqualityAttribute() {
      ComparerMemberName = "Default";
    }

    public SetEqualityAttribute(StringComparison stringComparison) {
      StringComparison = stringComparison;
      ComparerMemberName = "Default";
    }

    public SetEqualityAttribute(Type comparerType, string comparerMemberName = "Default") {
      ComparerType = comparerType;
      ComparerMemberName = comparerMemberName;
    }
  }

}
