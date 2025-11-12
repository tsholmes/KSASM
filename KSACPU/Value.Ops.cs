
using System;

namespace KSACPU
{
  public partial class Value
  {
    public void Add(int index, Value other, int otherIndex)
    {
      if (Mode != other.Mode) throw new InvalidOperationException($"{Mode} != {other.Mode}");

      switch (Mode)
      {
        case ValueMode.Unsigned:
          this.Unsigned[index] += other.Unsigned[otherIndex];
          break;
        case ValueMode.Signed:
          this.Signed[index] += other.Signed[otherIndex];
          break;
        case ValueMode.Floating:
          this.Floating[index] += other.Floating[otherIndex];
          break;
        case ValueMode.Complex:
        default:
          throw new NotImplementedException($"{Mode}");
      }
    }
  }
}