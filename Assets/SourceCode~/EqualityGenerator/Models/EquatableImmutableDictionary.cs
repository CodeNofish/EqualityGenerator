using System;
using System.Collections.Immutable;

namespace EqualityGenerator;

/// <summary>
/// 包装 ImmutableDictionary，使其按键值内容比较相等（用于增量生成的缓存键）。
/// </summary>
internal class EquatableImmutableDictionary<TKey, TValue>(ImmutableDictionary<TKey, TValue> Items) : IEquatable<EquatableImmutableDictionary<TKey, TValue>> where TKey : notnull {
  internal ImmutableDictionary<TKey, TValue> Items { get; set; } = Items;

  public EquatableImmutableDictionary() : this(ImmutableDictionary<TKey, TValue>.Empty) { }

  // 按键取值
  public bool TryGetValue(TKey key, out TValue value) => Items.TryGetValue(key, out value!);

  // 索引器访问
  public TValue this[TKey key] => Items[key];

  // 键集合与对应值都相同才相等
  public bool Equals(EquatableImmutableDictionary<TKey, TValue>? other) {
    if (ReferenceEquals(null, other)) return false;
    if (ReferenceEquals(this, other)) return true;

    foreach (var item in Items) {
      if (!other.Items.TryGetValue(item.Key, out var otherValue)) {
        return false;
      }

      if (!Equals(item.Value, otherValue)) {
        return false;
      }
    }

    return Items.Count == other.Items.Count;
  }

  public override bool Equals(object? obj) {
    if (ReferenceEquals(null, obj)) return false;
    if (ReferenceEquals(this, obj)) return true;
    if (obj.GetType() != this.GetType()) return false;
    return Equals((EquatableImmutableDictionary<TKey, TValue>)obj);
  }

  public override int GetHashCode() {
    return Items.GetHashCode();
  }

  public static bool operator ==(
    EquatableImmutableDictionary<TKey, TValue>? left,
    EquatableImmutableDictionary<TKey, TValue>? right
  ) {
    return Equals(left, right);
  }

  public static bool operator !=(
    EquatableImmutableDictionary<TKey, TValue>? left,
    EquatableImmutableDictionary<TKey, TValue>? right
  ) {
    return !Equals(left, right);
  }

  // 与 ImmutableDictionary 之间的隐式转换
  public static implicit operator EquatableImmutableDictionary<TKey, TValue>(ImmutableDictionary<TKey, TValue> items) =>
    new(items);

  public static implicit operator ImmutableDictionary<TKey, TValue>(EquatableImmutableDictionary<TKey, TValue> items) =>
    items.Items;
}