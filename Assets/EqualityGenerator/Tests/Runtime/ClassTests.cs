using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace EqualityGenerator.Tests {

  #region 测试用模型（Class）

  [Equatable]
  partial class BasicClass {
    public int Id { get; set; }
    public string Name { get; set; } = "";
  }

  [Equatable(GenerateClassEqualityOperators = false)]
  partial class NoOperatorsClass {
    public int Value { get; set; }
  }

  [Equatable]
  partial class IgnoredMemberClass {
    public int Id { get; set; }
    [IgnoreEquality]
    public string Tag { get; set; } = "";
  }

  [Equatable]
  partial class FieldOptInClass {
    public int UnattributedField; // 字段默认不参与相等性
    [DefaultEquality]
    public int AttributedField; // 显式标注后参与
    public string Name { get; set; } = "";
  }

  [Equatable(Explicit = true)]
  partial class ExplicitClass {
    public int Ignored { get; set; } // Explicit 模式下未标注成员被忽略
    [DefaultEquality]
    public int Included { get; set; }
  }

  [Equatable]
  partial class AnimalBase {
    public string Name { get; set; } = "";
  }

  [Equatable]
  partial class Dog : AnimalBase {
    public string Breed { get; set; } = "";
  }

  [Equatable(IgnoreInheritedMembers = true)]
  partial class DogIgnoreBase : AnimalBase {
    public string Breed { get; set; } = "";
  }

  [Equatable]
  partial class OrderedClass {
    [OrderedEquality]
    public List<int> Items { get; set; } = new();
  }

  [Equatable]
  partial class UnorderedClass {
    [UnorderedEquality]
    public List<int> Items { get; set; } = new();
  }

  [Equatable]
  partial class CaseInsensitiveListClass {
    [UnorderedEquality(typeof(StringComparer), nameof(StringComparer.OrdinalIgnoreCase))]
    public List<string> Items { get; set; } = new();
  }

  [Equatable]
  partial class DictionaryClass {
    [UnorderedEquality]
    public Dictionary<string, int> Map { get; set; } = new();
  }

  [Equatable]
  partial class SetClass {
    [SetEquality]
    public List<int> Items { get; set; } = new();
  }

  [Equatable]
  partial class StringClass {
    [StringEquality(StringComparison.OrdinalIgnoreCase)]
    public string Name { get; set; } = "";
  }

  [Equatable]
  partial class PrecisionClass {
    [PrecisionEquality(0.001)]
    public float Value { get; set; }
    [PrecisionEquality(0.01)]
    public double? Maybe { get; set; }
  }

  class Payload {
    public int Value { get; set; }
  }

  [Equatable]
  partial class ReferenceClass {
    [ReferenceEquality]
    public Payload Payload { get; set; } = new();
  }

  // 无静态实例的比较器：生成代码会 new 一个实例
  sealed class LengthComparer : IEqualityComparer<string> {
    public bool Equals(string? x, string? y) => (x?.Length ?? 0) == (y?.Length ?? 0);
    public int GetHashCode(string obj) => obj.Length;
  }

  [Equatable]
  partial class CustomComparerClass {
    [CustomEquality(typeof(LengthComparer))]
    public string Name { get; set; } = "";
  }

  [Equatable]
  partial class CustomStaticComparerClass {
    [CustomEquality(typeof(StringComparer), nameof(StringComparer.OrdinalIgnoreCase))]
    public string Name { get; set; } = "";
  }

  [Equatable]
  partial class InnerClass {
    public int Value { get; set; }
  }

  [Equatable]
  partial class OuterClass {
    public InnerClass Inner { get; set; } = new();
  }

  #endregion


  public class ClassBasicEqualityTests {
    [Test]
    public void Equals_returns_true_when_all_members_equal() {
      var a = new BasicClass { Id = 1, Name = "A" };
      var b = new BasicClass { Id = 1, Name = "A" };

      Assert.IsTrue(a.Equals(b));
      Assert.IsTrue(a.Equals((object)b));
      Assert.IsTrue(BasicClass.EqualityComparer.Default.Equals(a, b));
    }

    [Test]
    public void Equals_returns_false_when_any_member_differs() {
      var a = new BasicClass { Id = 1, Name = "A" };

      Assert.IsFalse(a.Equals(new BasicClass { Id = 2, Name = "A" }));
      Assert.IsFalse(a.Equals(new BasicClass { Id = 1, Name = "B" }));
    }

    [Test]
    public void Equals_returns_false_for_null_or_other_type() {
      var a = new BasicClass { Id = 1, Name = "A" };

      Assert.IsFalse(a.Equals(null));
      Assert.IsFalse(a.Equals("not a BasicClass"));
      Assert.IsFalse(BasicClass.EqualityComparer.Default.Equals(a, null));
    }

    [Test]
    public void GetHashCode_is_equal_for_equal_instances() {
      var a = new BasicClass { Id = 1, Name = "A" };
      var b = new BasicClass { Id = 1, Name = "A" };

      Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Test]
    public void GetHashCode_differs_for_different_instances() {
      var a = new BasicClass { Id = 1, Name = "A" };
      var b = new BasicClass { Id = 2, Name = "B" };

      Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Test]
    public void Works_as_dictionary_and_hashset_key() {
      var set = new HashSet<BasicClass> { new() { Id = 1, Name = "A" } };
      var dict = new Dictionary<BasicClass, string> { [new BasicClass { Id = 2, Name = "B" }] = "b" };

      Assert.IsTrue(set.Contains(new BasicClass { Id = 1, Name = "A" }));
      Assert.IsFalse(set.Contains(new BasicClass { Id = 1, Name = "C" }));
      Assert.AreEqual("b", dict[new BasicClass { Id = 2, Name = "B" }]);
    }

    [Test]
    public void Implements_IEquatable() {
      var a = new BasicClass { Id = 1, Name = "A" };

      Assert.IsTrue(a is IEquatable<BasicClass>);
    }
  }

  public class ClassOperatorTests {
    [Test]
    public void Equality_operators_compare_by_value() {
      var a = new BasicClass { Id = 1, Name = "A" };
      var b = new BasicClass { Id = 1, Name = "A" };
      var c = new BasicClass { Id = 1, Name = "B" };

      Assert.IsTrue(a == b);
      Assert.IsTrue(a != c);
      Assert.IsFalse(a == c);
      Assert.IsFalse(a != b);
    }

    [Test]
    public void Equality_operators_handle_null() {
      BasicClass? n1 = null;
      BasicClass? n2 = null;
      var a = new BasicClass { Id = 1, Name = "A" };

      Assert.IsTrue(n1 == n2);
      Assert.IsFalse(n1 == a);
      Assert.IsFalse(a == n1);
      Assert.IsTrue(n1 != a);
    }

    [Test]
    public void GenerateClassEqualityOperators_false_falls_back_to_reference_equality() {
      var a = new NoOperatorsClass { Value = 1 };
      var b = new NoOperatorsClass { Value = 1 };

      Assert.IsTrue(a == a); // 同一引用
      Assert.IsFalse(a == b); // 值相同但引用不同
      Assert.IsTrue(a != b);
      Assert.IsTrue(a.Equals(b)); // Equals 仍按值比较
    }
  }

  public class ClassInheritanceTests {
    [Test]
    public void Derived_compares_base_and_own_members() {
      var a = new Dog { Name = "Rex", Breed = "Husky" };
      var b = new Dog { Name = "Rex", Breed = "Husky" };

      Assert.IsTrue(a.Equals(b));

      Assert.IsFalse(a.Equals(new Dog { Name = "Rex", Breed = "Poodle" }));
      Assert.IsFalse(a.Equals(new Dog { Name = "Fido", Breed = "Husky" }));
    }

    [Test]
    public void Base_comparer_dispatches_to_derived_equality() {
      AnimalBase a = new Dog { Name = "Rex", Breed = "Husky" };
      AnimalBase b = new Dog { Name = "Rex", Breed = "Husky" };
      AnimalBase c = new Dog { Name = "Rex", Breed = "Poodle" };
      AnimalBase d = new AnimalBase { Name = "Rex" };

      var comparer = AnimalBase.EqualityComparer.Default;
      Assert.IsTrue(comparer.Equals(a, b)); // 通过基类引用比较派生实例
      Assert.IsFalse(comparer.Equals(a, c)); // 派生成员差异被感知
      Assert.IsFalse(comparer.Equals(a, d)); // 运行时类型不同
    }

    [Test]
    public void IgnoreInheritedMembers_only_compares_own_members() {
      var a = new DogIgnoreBase { Name = "Rex", Breed = "Husky" };
      var b = new DogIgnoreBase { Name = "Fido", Breed = "Husky" };
      var c = new DogIgnoreBase { Name = "Rex", Breed = "Poodle" };

      Assert.IsTrue(a.Equals(b)); // 基类成员 Name 的差异被忽略
      Assert.IsFalse(a.Equals(c));
    }
  }

  public class ClassMemberAttributeTests {
    [Test]
    public void IgnoreEquality_excludes_member_from_equality() {
      var a = new IgnoredMemberClass { Id = 1, Tag = "v1" };
      var b = new IgnoredMemberClass { Id = 1, Tag = "v2" };
      var c = new IgnoredMemberClass { Id = 2, Tag = "v2" };

      Assert.IsTrue(a.Equals(b)); // Tag 被忽略
      Assert.IsFalse(a.Equals(c));

      Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Test]
    public void Fields_are_opt_in() {
      var a = new FieldOptInClass { UnattributedField = 1, AttributedField = 10, Name = "A" };
      var b = new FieldOptInClass { UnattributedField = 2, AttributedField = 10, Name = "A" };
      var c = new FieldOptInClass { UnattributedField = 1, AttributedField = 20, Name = "A" };

      Assert.IsTrue(a.Equals(b)); // 未标注字段不参与
      Assert.IsFalse(a.Equals(c)); // 标注字段参与
    }

    [Test]
    public void Explicit_mode_only_includes_attributed_members() {
      var a = new ExplicitClass { Ignored = 1, Included = 10 };
      var b = new ExplicitClass { Ignored = 2, Included = 10 };
      var c = new ExplicitClass { Ignored = 1, Included = 20 };

      Assert.IsTrue(a.Equals(b)); // 未标注属性被忽略
      Assert.IsFalse(a.Equals(c));
    }
  }

  public class ClassCollectionEqualityTests {
    [Test]
    public void OrderedEquality_respects_order() {
      var a = new OrderedClass { Items = { 1, 2 } };
      var b = new OrderedClass { Items = { 1, 2 } };
      var c = new OrderedClass { Items = { 2, 1 } };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
    }

    [Test]
    public void UnorderedEquality_ignores_order_but_not_multiplicity() {
      var a = new UnorderedClass { Items = { 1, 2 } };
      var b = new UnorderedClass { Items = { 2, 1 } };
      var c = new UnorderedClass { Items = { 1, 2, 2 } };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
    }

    [Test]
    public void UnorderedEquality_uses_element_comparer() {
      var a = new CaseInsensitiveListClass { Items = { "ab", "CD" } };
      var b = new CaseInsensitiveListClass { Items = { "cd", "AB" } };
      var c = new CaseInsensitiveListClass { Items = { "ab", "cd" } };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
    }

    [Test]
    public void Dictionary_equality_ignores_insertion_order() {
      var a = new DictionaryClass { Map = { ["x"] = 1, ["y"] = 2 } };
      var b = new DictionaryClass { Map = { ["y"] = 2, ["x"] = 1 } };
      var c = new DictionaryClass { Map = { ["x"] = 1, ["y"] = 3 } };
      var d = new DictionaryClass { Map = { ["x"] = 1 } };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
      Assert.IsFalse(a.Equals(d));
    }

    [Test]
    public void SetEquality_ignores_duplicates() {
      var a = new SetClass { Items = { 1, 2, 2 } };
      var b = new SetClass { Items = { 2, 1 } };
      var c = new SetClass { Items = { 1, 2, 3 } };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
    }

    [Test]
    public void Null_collections_with_nulls_are_equal() {
      var a = new UnorderedClass { Items = null };
      var b = new UnorderedClass { Items = null };
      var c = new UnorderedClass { Items = { } };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c)); // null 与空集合不相等
    }
  }

  public class ClassScalarAttributeTests {
    [Test]
    public void StringEquality_ordinal_ignore_case() {
      var a = new StringClass { Name = "ABC" };
      var b = new StringClass { Name = "abc" };
      var c = new StringClass { Name = "ABD" };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
      Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Test]
    public void PrecisionEquality_compares_within_tolerance() {
      var a = new PrecisionClass { Value = 1.0f, Maybe = 1.0 };
      var b = new PrecisionClass { Value = 1.0005f, Maybe = 1.005 };
      var c = new PrecisionClass { Value = 1.5f, Maybe = 1.5 };

      Assert.IsTrue(a.Equals(b)); // 差值在容差内
      Assert.IsFalse(a.Equals(c));
    }

    [Test]
    public void PrecisionEquality_handles_nullable_members() {
      var bothNull = (a: new PrecisionClass { Maybe = null }, b: new PrecisionClass { Maybe = null });
      var oneNull = (a: new PrecisionClass { Maybe = null }, b: new PrecisionClass { Maybe = 1.0 });

      Assert.IsTrue(bothNull.a.Equals(bothNull.b));
      Assert.IsFalse(oneNull.a.Equals(oneNull.b));
    }

    [Test]
    public void ReferenceEquality_compares_by_reference() {
      var shared = new Payload { Value = 1 };
      var a = new ReferenceClass { Payload = shared };
      var b = new ReferenceClass { Payload = shared };
      var c = new ReferenceClass { Payload = new Payload { Value = 1 } };

      Assert.IsTrue(a.Equals(b)); // 同一引用
      Assert.IsFalse(a.Equals(c)); // 值相同但引用不同
    }

    [Test]
    public void CustomEquality_with_instantiable_comparer() {
      var a = new CustomComparerClass { Name = "abc" };
      var b = new CustomComparerClass { Name = "xyz" };
      var c = new CustomComparerClass { Name = "ab" };

      Assert.IsTrue(a.Equals(b)); // 长度相同即相等
      Assert.IsFalse(a.Equals(c));
    }

    [Test]
    public void CustomEquality_with_static_comparer_instance() {
      var a = new CustomStaticComparerClass { Name = "ABC" };
      var b = new CustomStaticComparerClass { Name = "abc" };
      var c = new CustomStaticComparerClass { Name = "ABD" };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
    }
  }

  public class ClassNestedMemberTests {
    [Test]
    public void Nested_equatable_members_are_compared_by_value() {
      var a = new OuterClass { Inner = new InnerClass { Value = 1 } };
      var b = new OuterClass { Inner = new InnerClass { Value = 1 } };
      var c = new OuterClass { Inner = new InnerClass { Value = 2 } };

      Assert.IsTrue(a.Equals(b));
      Assert.IsFalse(a.Equals(c));
    }
  }

  public class ClassInequalitiesTests {
    [Test]
    public void Reports_simple_member_differences() {
      var a = new BasicClass { Id = 1, Name = "A" };
      var b = new BasicClass { Id = 1, Name = "B" };

      var diffs = BasicClass.EqualityComparer.Default.Inequalities(a, b).ToList();

      Assert.AreEqual(1, diffs.Count);
      Assert.AreEqual("Name", diffs[0].Path.ToString());
      Assert.AreEqual("A", diffs[0].Left);
      Assert.AreEqual("B", diffs[0].Right);
    }

    [Test]
    public void Reports_no_differences_for_equal_instances() {
      var a = new BasicClass { Id = 1, Name = "A" };
      var b = new BasicClass { Id = 1, Name = "A" };

      Assert.IsEmpty(BasicClass.EqualityComparer.Default.Inequalities(a, b).ToList());
    }

    [Test]
    public void Reports_null_argument_as_single_difference() {
      var a = new BasicClass { Id = 1, Name = "A" };

      var diffs = BasicClass.EqualityComparer.Default.Inequalities(null, a).ToList();

      Assert.AreEqual(1, diffs.Count);
      Assert.AreEqual("", diffs[0].Path.ToString());
      Assert.AreEqual(null, diffs[0].Left);
      Assert.AreEqual(a, diffs[0].Right);
    }

    [Test]
    public void Reports_nested_member_path() {
      var a = new OuterClass { Inner = new InnerClass { Value = 1 } };
      var b = new OuterClass { Inner = new InnerClass { Value = 2 } };

      var diffs = OuterClass.EqualityComparer.Default.Inequalities(a, b).ToList();

      Assert.AreEqual(1, diffs.Count);
      Assert.AreEqual("Inner.Value", diffs[0].Path.ToString());
    }

    [Test]
    public void Reports_ordered_collection_index_path() {
      var a = new OrderedClass { Items = { 1, 2 } };
      var b = new OrderedClass { Items = { 1, 3 } };

      var diffs = OrderedClass.EqualityComparer.Default.Inequalities(a, b).ToList();

      Assert.AreEqual(1, diffs.Count);
      Assert.AreEqual("Items[1]", diffs[0].Path.ToString());
      Assert.AreEqual(2, diffs[0].Left);
      Assert.AreEqual(3, diffs[0].Right);
    }

    [Test]
    public void Reports_unordered_collection_added_and_removed() {
      var a = new UnorderedClass { Items = { 1, 2 } };
      var b = new UnorderedClass { Items = { 1 } };

      var diffs = UnorderedClass.EqualityComparer.Default.Inequalities(a, b).ToList();

      Assert.AreEqual(1, diffs.Count);
      Assert.AreEqual("Items[-]", diffs[0].Path.ToString());
      Assert.AreEqual(2, diffs[0].Left);
    }

    [Test]
    public void Reports_dictionary_key_path() {
      var a = new DictionaryClass { Map = { ["x"] = 1 } };
      var b = new DictionaryClass { Map = { ["x"] = 2 } };

      var diffs = DictionaryClass.EqualityComparer.Default.Inequalities(a, b).ToList();

      Assert.AreEqual(1, diffs.Count);
      Assert.AreEqual("Map[\"x\"]", diffs[0].Path.ToString());
      Assert.AreEqual(1, diffs[0].Left);
      Assert.AreEqual(2, diffs[0].Right);
    }
  }

}
