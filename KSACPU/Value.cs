
using System;
using System.Text;

namespace KSACPU
{
  public enum ValueMode
  {
    Unsigned,
    Signed,
    Floating,
    Complex,
  }

  public struct ValuePointer
  {
    public int Address;
    public DataType Type;
    public byte Width;

    public override string ToString() => $"{Type}*{Width}@{Address}";
  }

  public partial class Value
  {
    public ValueMode Mode;
    public int Width;
    public ulong[] Unsigned = new ulong[8];
    public long[] Signed = new long[8];
    public double[] Floating = new double[8];
    // TODO: complex

    public void Init(ValueMode mode, int width)
    {
      this.Mode = mode;
      this.Width = width;
      switch (mode)
      {
        case ValueMode.Unsigned:
          Array.Fill(Unsigned, 0u);
          break;
        case ValueMode.Signed:
          Array.Fill(Signed, 0);
          break;
        case ValueMode.Floating:
          Array.Fill(Floating, 0.0);
          break;
        case ValueMode.Complex:
        default:
          throw new InvalidOperationException($"{mode}");
      }
    }

    public void Convert(ValueMode target)
    {
      if (Mode == target)
        return;

      switch ((Mode, target))
      {
        case (ValueMode.Unsigned, ValueMode.Signed):
          for (var i = 0; i < Width; i++)
            Signed[i] = (long)Unsigned[i];
          break;
        case (ValueMode.Unsigned, ValueMode.Floating):
          for (var i = 0; i < Width; i++)
            Floating[i] = Unsigned[i];
          break;
        case (ValueMode.Signed, ValueMode.Unsigned):
          for (var i = 0; i < Width; i++)
            Unsigned[i] = (ulong)Signed[i];
          break;
        case (ValueMode.Signed, ValueMode.Floating):
          for (var i = 0; i < Width; i++)
            Floating[i] = Signed[i];
          break;
        case (ValueMode.Floating, ValueMode.Unsigned):
          for (var i = 0; i < Width; i++)
            Unsigned[i] = (ulong)Floating[i];
          break;
        case (ValueMode.Floating, ValueMode.Signed):
          for (var i = 0; i < Width; i++)
            Signed[i] = (long)Floating[i];
          break;
        default:
          throw new NotImplementedException($"conversion {Mode}->{target} not implemented");
      }
      Mode = target;
    }

    public void Load(Memory mem, ValuePointer ptr)
    {
      this.Width = ptr.Width;
      this.Mode = ptr.Type.VMode();

      var size = ptr.Type.SizeBytes();
      var addr = ptr.Address;

      for (var i = 0; i < Width; i++)
      {
        LoadSingle(mem, addr, ptr.Type, i);
        addr += size;
      }
    }

    private void LoadSingle(Memory mem, int addr, DataType type, int index)
    {
      switch (type)
      {
        case DataType.U8:
          this.Unsigned[index] = mem.ReadU8(addr);
          break;
        case DataType.I16:
          this.Signed[index] = mem.ReadI16(addr);
          break;
        case DataType.I32:
          this.Signed[index] = mem.ReadI32(addr);
          break;
        case DataType.I64:
          this.Signed[index] = mem.ReadI64(addr);
          break;
        case DataType.U64:
          this.Unsigned[index] = mem.ReadU64(addr);
          break;
        case DataType.F64:
          this.Floating[index] = mem.ReadF64(addr);
          break;
        case DataType.P24:
          this.Unsigned[index] = mem.ReadP24(addr);
          break;
        case DataType.C128:
        default:
          throw new InvalidOperationException($"Invalid DataType {type}");
      }
    }

    public void Store(Memory mem, ValuePointer ptr)
    {
      if (ptr.Width != Width)
        throw new InvalidOperationException($"Mismatched data width {ptr.Width} != {Width}");

      Convert(ptr.Type.VMode());

      var addr = ptr.Address;
      var size = ptr.Type.SizeBytes();

      for (var i = 0; i < Width; i++)
      {
        StoreSingle(mem, addr, ptr.Type, i);
        addr += size;
      }
    }

    private void StoreSingle(Memory mem, int addr, DataType type, int index)
    {
      switch (type)
      {
        case DataType.U8:
          mem.WriteU8(addr, this.Unsigned[index]);
          break;
        case DataType.I16:
          mem.WriteI16(addr, this.Signed[index]);
          break;
        case DataType.I32:
          mem.WriteI32(addr, this.Signed[index]);
          break;
        case DataType.I64:
          mem.WriteI64(addr, this.Signed[index]);
          break;
        case DataType.U64:
          mem.WriteU64(addr, this.Unsigned[index]);
          break;
        case DataType.F64:
          mem.WriteF64(addr, this.Floating[index]);
          break;
        case DataType.P24:
          mem.WriteP24(addr, this.Unsigned[index]);
          break;
        case DataType.C128:
        default:
          throw new InvalidOperationException($"Invalid DataType {type}");
      }
    }

    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.AppendFormat("{0}:", Mode);

      for (var i = 0; i < Width; i++)
      {
        if (i != 0)
          sb.Append(',');
        sb.Append(Mode switch
        {
          ValueMode.Unsigned => Unsigned[i],
          ValueMode.Signed => Signed[i],
          ValueMode.Floating => Floating[i],
          _ => "Invalid",
        });
      }

      return sb.ToString();
    }
  }
}