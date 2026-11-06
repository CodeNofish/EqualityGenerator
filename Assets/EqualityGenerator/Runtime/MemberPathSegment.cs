using System;

namespace EqualityGenerator {

  public enum MemberPathSegmentKind {
    Property,
    Field,
    Index,
    Key,
    Added,
    Removed
  }

  public readonly struct MemberPathSegment : IEquatable<MemberPathSegment> {
    public MemberPathSegmentKind Kind { get; }
    public object? Value { get; }

    MemberPathSegment(MemberPathSegmentKind kind, object? value) {
      Kind = kind;
      Value = value;
    }

    public static MemberPathSegment Property(string name) => new(MemberPathSegmentKind.Property, name);
    public static MemberPathSegment Field(string name) => new(MemberPathSegmentKind.Field, name);
    public static MemberPathSegment Index(int index) => new(MemberPathSegmentKind.Index, index);
    public static MemberPathSegment Key(object key) => new(MemberPathSegmentKind.Key, key);
    public static MemberPathSegment Added() => new(MemberPathSegmentKind.Added, null);
    public static MemberPathSegment Removed() => new(MemberPathSegmentKind.Removed, null);

    public override string ToString() {
      switch (Kind) {
      case MemberPathSegmentKind.Property:
      case MemberPathSegmentKind.Field:
        return (string)Value!;
      case MemberPathSegmentKind.Index:
        return "[" + Value + "]";
      case MemberPathSegmentKind.Key:
        if (Value is string s)
          return "[\"" + s + "\"]";
        return "[" + Value + "]";
      case MemberPathSegmentKind.Added:
        return "[+]";
      case MemberPathSegmentKind.Removed:
        return "[-]";
      default:
        return "";
      }
    }

    public bool Equals(MemberPathSegment other) {
      return Kind == other.Kind
             && Equals(Value, other.Value);
    }

    public override bool Equals(object? obj) => obj is MemberPathSegment other && Equals(other);

    public override int GetHashCode() {
      var hashCode = new HashCode();
      hashCode.Add(Kind);
      hashCode.Add(Value);
      return hashCode.ToHashCode();
    }

    public static bool operator ==(MemberPathSegment left, MemberPathSegment right) => left.Equals(right);
    public static bool operator !=(MemberPathSegment left, MemberPathSegment right) => !left.Equals(right);
  }

}
