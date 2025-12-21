
using System;
using KSASM.Assembly;

namespace KSASM
{
  public struct Instruction
  {
    public OpCode OpCode;
    public byte Width;
    public byte ImmCount;
    public DataType AType;
    public DataType BType;
    public DataType CType;

    public static class Encoding
    {
      public static readonly ShiftMask OpCode = new() { Shift = 0, Bits = 7 };
      public static readonly ShiftMask Width = new() { Shift = 7, Bits = 3 };
      public static readonly ShiftMask ImmCount = new() { Shift = 10, Bits = 2 };
      public static readonly ShiftMask AType = new() { Shift = 12, Bits = 4 };
      public static readonly ShiftMask BType = new() { Shift = 16, Bits = 4 };
      public static readonly ShiftMask CType = new() { Shift = 20, Bits = 4 };
    }

    public static Instruction Decode(ulong encoded) => new()
    {
      OpCode = (OpCode)Encoding.OpCode.Decode(encoded),
      Width = (byte)(Encoding.Width.Decode(encoded) + 1u),
      ImmCount = (byte)Encoding.ImmCount.Decode(encoded),
      AType = (DataType)Encoding.AType.Decode(encoded),
      BType = (DataType)Encoding.BType.Decode(encoded),
      CType = (DataType)Encoding.CType.Decode(encoded),
    };

    public readonly ulong Encode() =>
      Encoding.OpCode.Encode((ulong)OpCode) |
      Encoding.Width.Encode(Width - 1u) |
      Encoding.ImmCount.Encode(ImmCount) |
      Encoding.AType.Encode((ulong)AType) |
      Encoding.BType.Encode((ulong)BType) |
      Encoding.CType.Encode((ulong)CType);

    public readonly DataType Type(int idx) => idx switch
    {
      0 => AType,
      1 => BType,
      2 => CType,
      _ => throw new IndexOutOfRangeException($"{idx}"),
    };

    public void Format(ref LineBuilder line, DebugSymbols debug = null)
    {
      var info = OpCodeInfo.For(OpCode);

      line.Add(OpCode, "g");

      for (var i = 0; i < info.InOps; i++)
      {
        var opInfo = info[i];
        if (i < ImmCount)
          line.Add(" <>");
        else
          line.Add(" _");
        line.Add(opInfo.Type ?? Type(i));
        line.Add('*');
        line.Add(opInfo.Width ?? Width, "g");
      }

      for (var i = 0; i < info.OutOps; i++)
      {
        if (i == 0)
          line.Add(" ->");
        var j = i + info.InOps;
        var opInfo = info[j];
        line.Add(" _");
        line.Add(opInfo.Type ?? Type(j));
        line.Add('*');
        line.Add(opInfo.Width ?? Width, "g");
      }
    }
  }
}