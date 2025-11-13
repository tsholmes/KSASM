
using System;

namespace KSACPU
{
  public partial struct Value
  {
    public void Add(Value other, ValueMode mode)
    {
      switch (mode)
      {
        case ValueMode.Unsigned:
          this.Unsigned += other.Unsigned;
          break;
        case ValueMode.Signed:
          this.Signed += other.Signed;
          break;
        case ValueMode.Float:
          this.Float += other.Float;
          break;
        case ValueMode.Complex:
        default:
          throw new NotImplementedException($"{mode}");
      }
    }
  }

  public partial class ValArray
  {
  }
}