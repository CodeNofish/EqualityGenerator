using System;
using System.CodeDom.Compiler;

namespace EqualityGenerator {

  [GeneratedCode(Const.Tool, Const.Version)]
  [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
  public class PrecisionEqualityAttribute : Attribute {
    public double Precision { get; }

    public PrecisionEqualityAttribute(double precision) {
      Precision = precision;
    }
  }

}
