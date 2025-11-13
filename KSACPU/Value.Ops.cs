
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

    public void Sub(Value other, ValueMode mode)
    {
      switch (mode)
      {
        case ValueMode.Unsigned:
          this.Unsigned -= other.Unsigned;
          break;
        case ValueMode.Signed:
          this.Signed -= other.Signed;
          break;
        case ValueMode.Float:
          this.Float -= other.Float;
          break;
        case ValueMode.Complex:
        default:
          throw new NotImplementedException($"{mode}");
      }
    }

    public int Sign(ValueMode mode) => mode switch
    {
      ValueMode.Unsigned => Unsigned == 0 ? 0 : 1,
      ValueMode.Signed => Signed < 0 ? -1 : Signed > 0 ? 1 : 0,
      ValueMode.Float => Float < 0 ? -1 : Float > 0 ? 1 : 0,
      _ => throw new InvalidOperationException($"{mode}"),
    };
  }

  public partial class ValArray
  {
  }
}