using System;
using System.CodeDom.Compiler;

namespace EqualityGenerator {

  [GeneratedCode(Const.Tool, Const.Version)]
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
  public class EquatableAttribute : Attribute {
    /// <summary>
    /// IEquatable will only be generated for explicitly defined attributes.
    /// </summary>
    public bool Explicit { get; set; }

    /// <summary>
    /// When true, only members declared directly on this type are compared,
    /// treating this type as an equality root. No base.Equals() call is made
    /// and inherited members are not included.
    /// When false (default) and any ancestor has [Equatable], base.Equals() is called.
    /// When false and NO ancestor has [Equatable], all inherited members from the
    /// entire inheritance chain are explicitly compared.
    /// </summary>
    public bool IgnoreInheritedMembers { get; set; }

    /// <summary>
    /// Indicates whether the equality operators (<c>==</c> and <c>!=</c>) will be generated for the
    /// decorated class. Set to <see langword="false"/> to leave <c>==</c> as reference equality, or to
    /// declare the operators by hand.
    /// <br/>Only classes are affected: records always get compiler-generated operators, and structs
    /// always get generated ones (reported as GE011).
    /// <br/>Operators declared on a base class also apply to derived operands, so opting out on a
    /// derived class alone has no effect (reported as GE012). Opt out on every <c>[Equatable]</c>
    /// class in the inheritance chain.
    /// <br/>Default value is <see langword="true"/>.
    /// </summary>
    public bool GenerateClassEqualityOperators { get; set; } = true;
  }

}
