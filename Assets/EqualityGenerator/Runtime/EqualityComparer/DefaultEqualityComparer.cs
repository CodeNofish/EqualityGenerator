using System.CodeDom.Compiler;
using System.Collections.Generic;

namespace EqualityGenerator {

  [GeneratedCode(Const.Tool, Const.Version)]
  public class DefaultEqualityComparer<T> : IEqualityComparer<T> {
    public static DefaultEqualityComparer<T> Default { get; } = new();

    static readonly IEqualityComparer<T> Underlying;

    static DefaultEqualityComparer() {
      if (typeof(T).IsSealed)
        Underlying = EqualityComparer<T>.Default;
      else
        Underlying = new ObjectEqualityComparer();
    }

    public bool Equals(T? x, T? y) {
      return Underlying.Equals(x!, y!);
    }

    public int GetHashCode(T obj) {
      return Underlying.GetHashCode(obj!);
    }

    [GeneratedCode(Const.Tool, Const.Version)]
    class ObjectEqualityComparer : IEqualityComparer<T> {
      public bool Equals(T? x, T? y) {
        return object.Equals(x!, y!);
      }

      public int GetHashCode(T obj) => obj?.GetHashCode() ?? 0;
    }
  }

}
