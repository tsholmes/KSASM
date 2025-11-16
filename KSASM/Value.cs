
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace KSASM
{
  public enum ValueMode
  {
    Unsigned,
    Signed,
    Float,
    Complex,
  }

  public struct ValuePointer
  {
    public int Address;
    public DataType Type;
    public byte Width;

    public override string ToString() => $"{Type}*{Width}@{Address}";
  }

  [StructLayout(LayoutKind.Explicit)]
  public partial struct Value
  {
    [FieldOffset(0)]
    public ulong Unsigned;
    [FieldOffset(0)]
    public long Signed;
    [FieldOffset(0)]
    public double Float;

    // TODO: complex

    public void Convert(ValueMode from, ValueMode to)
    {
      if (from == to)
        return;
      switch ((from, to))
      {
        case (ValueMode.Unsigned, ValueMode.Signed):
          Signed = (long)Unsigned;
          break;
        case (ValueMode.Unsigned, ValueMode.Float):
          Float = Unsigned;
          break;
        case (ValueMode.Signed, ValueMode.Unsigned):
          Unsigned = (ulong)Signed;
          break;
        case (ValueMode.Signed, ValueMode.Float):
          Float = Signed;
          break;
        case (ValueMode.Float, ValueMode.Unsigned):
          Unsigned = (ulong)Float;
          break;
        case (ValueMode.Float, ValueMode.Signed):
          Signed = (long)Float;
          break;
        default:
          throw new NotImplementedException($"conversion {from}->{to} not implemented");
      }
    }

    public object As(DataType type) => Get(type.VMode());

    public object Get(ValueMode mode) => mode switch
    {
      ValueMode.Unsigned => Unsigned,
      ValueMode.Signed => Signed,
      ValueMode.Float => Float,
      _ => "Invalid",
    };
  }

  public partial class ValArray
  {
    public ValueMode Mode;
    public int Width;

    public Value[] Values = new Value[8];

    public void Init(ValueMode mode, int width)
    {
      this.Mode = mode;
      this.Width = width;
      Array.Fill(Values, default);
    }

    public void Convert(ValueMode target)
    {
      if (Mode == target)
        return;

      for (var i = 0; i < Width; i++)
        Values[i].Convert(Mode, target);

      Mode = target;
    }

    public ulong UnsignedAt(int index)
    {
      if (index < 0 || index >= Width)
        return 0;
      var v = Values[index];
      v.Convert(Mode, ValueMode.Unsigned);
      return v.Unsigned;
    }

    public long SignedAt(int index)
    {
      if (index < 0 || index >= Width)
        return 0;
      var v = Values[index];
      v.Convert(Mode, ValueMode.Signed);
      return v.Signed;
    }

    public double FloatAt(int index)
    {
      if (index < 0 || index >= Width)
        return 0;
      var v = Values[index];
      v.Convert(Mode, ValueMode.Float);
      return v.Float;
    }

    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.AppendFormat("{0}:", Mode);

      for (var i = 0; i < Width; i++)
      {
        ref var val = ref Values[i];
        if (i != 0)
          sb.Append(',');
        sb.Append(Mode switch
        {
          ValueMode.Unsigned => val.Unsigned,
          ValueMode.Signed => val.Signed,
          ValueMode.Float => val.Float,
          _ => "Invalid",
        });
      }

      return sb.ToString();
    }
  }
}