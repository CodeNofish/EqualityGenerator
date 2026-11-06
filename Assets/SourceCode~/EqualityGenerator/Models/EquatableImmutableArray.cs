using System;
using System.Collections.Immutable;
using System.Linq;

namespace EqualityGenerator;

/// <summary>
/// 包装 ImmutableArray&lt;T&gt;，使其可按内容比较相等（用于增量生成的缓存键）。
/// </summary>
struct EquatableImmutableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableImmutableArray<T>> {
  public ImmutableArray<T> Items { get; set; } = items;

  public EquatableImmutableArray() : this(ImmutableArray<T>.Empty) { }

  // 按顺序逐个元素比较
  public bool Equals(EquatableImmutableArray<T> other) {
    return Items.SequenceEqual(other.Items);
  }

  public override bool Equals(object? obj) {
    return obj is EquatableImmutableArray<T> other && Equals(other);
  }

  // 按元素哈希值组合出整体哈希
  public override int GetHashCode() {
    // 不支持 HashCode
    // var hash = new HashCode();
    // foreach (var item in Items) {
    //   hash.Add(item);
    // }
    // if (items == null) return 0;
    // return hash.ToHashCode();

    unchecked {
      int hash = 17;
      foreach (var item in Items) {
        hash = hash * 31 + (item?.GetHashCode() ?? 0);
      }

      return hash;
    }
  }

  public static bool operator ==(EquatableImmutableArray<T> left, EquatableImmutableArray<T> right) {
    return left.Equals(right);
  }

  public static bool operator !=(EquatableImmutableArray<T> left, EquatableImmutableArray<T> right) {
    return !left.Equals(right);
  }

  // 与 ImmutableArray<T> 之间的隐式转换
  public static implicit operator EquatableImmutableArray<T>(ImmutableArray<T> items) => new(items);
  public static implicit operator ImmutableArray<T>(EquatableImmutableArray<T> items) => items.Items;
}