using System;
using System.Collections.Generic;
using System.Linq;
using EqualityGenerator;
using NUnit.Framework;

namespace EqualityGenerator.Tests {

  #region 测试用模型（Struct）

  [Equatable]
  partial struct BasicStruct {
    public int X { get; set; }
    public string Name { get; set; }
  }

  [Equatable]
  partial struct FieldStruct {
    public int UnattributedField; // 字段默认不参与相等性
    [DefaultEquality] public int AttributedField; // 显式标注后参与
  }

  [Equatable]
  partial struct OrderedStruct {
    [OrderedEquality] public List<int> Items { get; set; }
  }

  [Equatable]
  partial struct UnorderedStruct {
    [UnorderedEquality] public List<int> Items { get; set; }
  }

  [Equatable]
  partial struct DictionaryStruct {
    [UnorderedEquality] public Dictionary<string, int> Map { get; set; }
  }

  [Equatable]
  partial struct StringStruct {
    [StringEquality(StringComparison.OrdinalIgnoreCase)] public string Name { get; set; }
  }

  [Equatable]
  partial struct PrecisionStruct {
    [PrecisionEquality(0.001)] public float Value { get; set; }
    [PrecisionEquality(0.01)] public double? Maybe { get; set; }
  }

  [Equatable]
  partial struct CustomComparerStruct {
    [CustomEquality(typeof(LengthComparer))] public string Name { get; set; }
  }

  [Equatable]
  partial struct InnerStruct {
    public int Value { get; set; }
  }

  [Equatable]
  partial struct OuterStruct {
    public InnerStruct Inner { get; set; }
  }

  [Equatable]
  partial struct PointStruct {
    public int X { get; set; }
    public int Y { get; set; }
  }

  #endregion

  public class StructBasicEqualityTests {

    [Test]
    public void Equals_returns_true_when_all_members_equal() {
      var a = new BasicStruct { X = 1, Name = "A" };
      var b = new BasicStruct { X = 1, Name = "A" };

      Assert.IsTrue(a.Equals(b));
      Assert.IsTrue(a.Equals((object)b));
      Assert.IsTrue(BasicStruct.EqualityComparer.Default.Equals(a, b));
    }

    [Test]
    public void Equals_returns_false_when_any_member_differs() {
      var a = new BasicStruct { X = 1, Name = "A" };

      Assert.IsFalse(a.Equals(new BasicStruct { X = 2, Name = "A" }));
      Assert.IsFalse(a.Equals(new BasicStruct { X = 1, Name = "B" }));
    }

    [Test]
    public void Equals_object_returns_false_for_null_or_other_type() {
      var a = new BasicStruct { X = 1, Name = "A" };
      object boxed = a;

      Assert.IsTrue(a.Equals(boxed));          // 装箱后的同值实例
      Assert.IsFalse(a.Equals(null));
      Assert.IsFalse(a.Equals("not a BasicStruct"));
      Assert.IsFalse(a.Equals(new BasicStruct { X = 2, Name = "A" }));
    }

    [Test]
    public void GetHashCode_is_equal_for_equal_instances() {
      var a = new BasicStruct { X = 1, Name = "A" };
      var b = new BasicStruct { X = 1, Name = "A" };

      Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Test]
    public void Works_as_dictionary_and_hashset_key() {
      var origin = new PointStruct { X = 0, Y = 0 };
      var dict = new Dictionary<PointStruct, string> { [origin] = "origin" };
      var set = new HashSet<PointStruct> { new() { X = 1, Y = 2 } };

      Assert.AreEqual("origin", dict[new PointStruct { X = 0, Y = 0 }]);
      Assert.IsFalse(dict.ContainsKey(new PointStruct { X = 1, Y = 0 }));
      Assert.IsTrue(set.Contains(new PointStruct { X = 1, Y = 2 }));
    }

    [Test]
    public void Implements_IEquatable() {
      var a = new BasicStruct { X = 1, Name = "A" };

      Assert.IsTrue(a is IEquatable<BasicStruct>);
    }
  }

  public class StructOperatorTests {

    [Test]
    public void Equality_operators_compare_by_value() {
      var a = new BasicStruct { X = 1, Name = "A" };
      var b = new BasicStruct { X = 1, Name = "A" };
      var c = new BasicStruct { X = 2, Name = "A" };

      Assert.IsTrue(a == b);
      Assert.IsTrue(a != c);
      Assert.IsFalse(a == c);
      Assert.IsFalse(a != b);
    }

    [Test]
    public void Default_structs_are_equal() {
      var a = default(BasicStruct);
      var b = new BasicStruct { X = 0, Name = null };

      Assert.IsTrue(a == b);
      Assert.IsTrue(a.Equals(b));
    }
  }

  public class StructMemberAttributeTests {

    [Test]
    public void Fields_are_opt_in() {
      var a = new FieldStruct { UnattributedField = 1, AttributedField = 10 };
      var b = new FieldStruct { UnattributedField = 2, AttributedField = 10 };
      var c = new FieldStruct { UnattributedField = 1, AttributedField = 20 };

      Assert.IsTrue(a.Equals(b));  // 未标注字段不参与
      Assert.IsFalse(a.Equals(c)); // 标注字段参与
    }
  }

  public class StructCollectionEqualityTests {

    [Test]
    public void OrderedEquality_respects_order() {
      var a = new OrderedStruct { Items = new List<int> { 1, 2 } };
      var b = new OrderedStruct { Items = new List<int> { 1, 2 } };
      var c = new OrderedStruct { Items = new List<int> { 2, 1 } };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
    }

    [Test]
    public void UnorderedEquality_ignores_order() {
      var a = new UnorderedStruct { Items = new List<int> { 1, 2 } };
      var b = new UnorderedStruct { Items = new List<int> { 2, 1 } };
      var c = new UnorderedStruct { Items = new List<int> { 1, 2, 2 } };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
    }

    [Test]
    public void Dictionary_equality_ignores_insertion_order() {
      var a = new DictionaryStruct { Map = new Dictionary<string, int> { ["x"] = 1, ["y"] = 2 } };
      var b = new DictionaryStruct { Map = new Dictionary<string, int> { ["y"] = 2, ["x"] = 1 } };
      var c = new DictionaryStruct { Map = new Dictionary<string, int> { ["x"] = 1 } };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
    }
  }

  public class StructScalarAttributeTests {

    [Test]
    public void StringEquality_ordinal_ignore_case() {
      var a = new StringStruct { Name = "ABC" };
      var b = new StringStruct { Name = "abc" };
      var c = new StringStruct { Name = "ABD" };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
      Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Test]
    public void PrecisionEquality_compares_within_tolerance() {
      var a = new PrecisionStruct { Value = 1.0f, Maybe = 1.0 };
      var b = new PrecisionStruct { Value = 1.0005f, Maybe = 1.005 };
      var c = new PrecisionStruct { Value = 1.5f, Maybe = 1.5 };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
    }

    [Test]
    public void PrecisionEquality_handles_nullable_members() {
      var bothNullA = new PrecisionStruct { Maybe = null };
      var bothNullB = new PrecisionStruct { Maybe = null };
      var oneNullA = new PrecisionStruct { Maybe = null };
      var oneNullB = new PrecisionStruct { Maybe = 1.0 };

      Assert.IsTrue(bothNullA.Equals(bothNullB));
      Assert.IsFalse(oneNullA.Equals(oneNullB));
    }

    [Test]
    public void CustomEquality_uses_custom_comparer() {
      var a = new CustomComparerStruct { Name = "abc" };
      var b = new CustomComparerStruct { Name = "xyz" };
      var c = new CustomComparerStruct { Name = "ab" };

      Assert.IsTrue(a.Equals(b));  // 长度相同即相等
      Assert.IsFalse(a.Equals(c));
    }
  }

  public class StructNestedMemberTests {

    [Test]
    public void Nested_equatable_structs_are_compared_by_value() {
      var a = new OuterStruct { Inner = new InnerStruct { Value = 1 } };
      var b = new OuterStruct { Inner = new InnerStruct { Value = 1 } };
      var c = new OuterStruct { Inner = new InnerStruct { Value = 2 } };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
    }
  }

  public class StructInequalitiesTests {

    [Test]
    public void Reports_simple_member_differences() {
      var a = new BasicStruct { X = 1, Name = "A" };
      var b = new BasicStruct { X = 2, Name = "A" };

      var diffs = BasicStruct.EqualityComparer.Default.Inequalities(a, b).ToList();

      Assert.AreEqual(1, diffs.Count);
      Assert.AreEqual("X", diffs[0].Path.ToString());
      Assert.AreEqual(1, diffs[0].Left);
      Assert.AreEqual(2, diffs[0].Right);
    }

    [Test]
    public void Reports_no_differences_for_equal_instances() {
      var a = new BasicStruct { X = 1, Name = "A" };
      var b = new BasicStruct { X = 1, Name = "A" };

      Assert.IsEmpty(BasicStruct.EqualityComparer.Default.Inequalities(a, b).ToList());
    }

    [Test]
    public void Reports_nested_struct_path() {
      var a = new OuterStruct { Inner = new InnerStruct { Value = 1 } };
      var b = new OuterStruct { Inner = new InnerStruct { Value = 2 } };

      var diffs = OuterStruct.EqualityComparer.Default.Inequalities(a, b).ToList();

      Assert.AreEqual(1, diffs.Count);
      Assert.AreEqual("Inner.Value", diffs[0].Path.ToString());
    }

    [Test]
    public void Reports_ordered_collection_index_path() {
      var a = new OrderedStruct { Items = new List<int> { 1, 2 } };
      var b = new OrderedStruct { Items = new List<int> { 9, 2 } };

      var diffs = OrderedStruct.EqualityComparer.Default.Inequalities(a, b).ToList();

      Assert.AreEqual(1, diffs.Count);
      Assert.AreEqual("Items[0]", diffs[0].Path.ToString());
    }
  }
}
