
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KSACPU
{
  public class Assembler
  {
    public static bool Debug = false;

    private readonly Dictionary<string, int> labels = [];
    private readonly List<(int, DataType, Value)> values = [];
    private readonly List<ParsedInst> instructions = [];

    private int nextAddress = 0;

    public void LoadSource(string source)
    {
      var lines = source.Split("\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in lines)
        LoadLine(line);
    }

    private void LoadLine(string line)
    {
      var commentIdx = line.IndexOf('#');
      if (commentIdx != -1)
        line = line[..commentIdx];

      var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

      if (parts.Length == 0)
        return;

      var idx = 0;
      var label = (string)null;

      if (parts[idx].EndsWith(':'))
        label = parts[idx++][..^1];

      if (idx < parts.Length && parts[idx].StartsWith('@'))
      {
        var addr = parts[idx++][1..];
        if (!int.TryParse(addr, out nextAddress))
          throw new InvalidOperationException($"Invalid address {addr}");
      }

      if (label != null)
        labels[label] = nextAddress;

      if (idx == parts.Length)
        return;

      // disallow ints here to avoid Enum.TryParse interpreting them as an enum
      if (int.TryParse(parts[idx], out _))
        throw new InvalidOperationException($"Invalid instruction {parts[idx]}");

      if (Enum.TryParse<DataType>(parts[idx], true, out var dtype))
      {
        for (var i = idx + 1; i < parts.Length; i++)
          LoadValue(dtype, parts[i]);
      }
      else
        LoadInstruction(parts[idx..]);
    }

    private void LoadValue(DataType type, string strVal)
    {
      Value val = default;

      var valid = type.VMode() switch
      {
        ValueMode.Unsigned => TryParseUnsigned(strVal, out val.Unsigned),
        ValueMode.Signed => TryParseSigned(strVal, out val.Signed),
        ValueMode.Float => TryParseFloating(strVal, out val.Float),
        _ => throw new NotImplementedException($"{type}"),
      };
      if (!valid)
        throw new InvalidOperationException($"Invalid {type} value '{strVal}'");

      values.Add((nextAddress, type, val));

      nextAddress += type.SizeBytes();
    }

    private static bool TryParseUnsigned(string str, out ulong val) =>
      ulong.TryParse(str, NumberStyles.Integer, null, out val) ||
      (str.StartsWith("0b") && ulong.TryParse(str[2..], NumberStyles.BinaryNumber, null, out val)) ||
      (str.StartsWith("0x") && ulong.TryParse(str[2..], NumberStyles.HexNumber, null, out val));

    private static bool TryParseSigned(string str, out long val) =>
      long.TryParse(str, NumberStyles.Integer, null, out val) ||
      (str.StartsWith("0b") && long.TryParse(str[2..], NumberStyles.BinaryNumber, null, out val)) ||
      (str.StartsWith("0x") && long.TryParse(str[2..], NumberStyles.HexNumber, null, out val));

    private static bool TryParseFloating(string str, out double val) =>
      double.TryParse(str, NumberStyles.Integer, null, out val) ||
      (str.StartsWith("0b") && double.TryParse(str[2..], NumberStyles.BinaryNumber, null, out val)) ||
      (str.StartsWith("0x") && double.TryParse(str[2..], NumberStyles.HexNumber, null, out val));

    private void LoadInstruction(string[] args)
    {
      if (args.Length != 3)
        throw new InvalidOperationException($"invalid instruction '{string.Join(' ', args)}'");

      var strOp = args[0];
      var strA = args[1];
      var strB = args[2];

      if (strA == "_")
        strA = strB;
      else if (strB == "_")
        strB = strA;

      bool valid;
      OpCode op;
      var width = 1;

      var widx = strOp.IndexOf('*');
      if (widx == -1)
        valid = Enum.TryParse(strOp, true, out op);
      else
        valid = Enum.TryParse(strOp[..widx], true, out op) && int.TryParse(strOp[(widx + 1)..], out width);

      if (!valid)
        throw new InvalidOperationException($"invalid instruction {strOp}");

      if (width < 1 || width > 8)
        throw new InvalidOperationException($"invalid width {width}");

      if (!TryParseAddress(strA, out var addrA))
        throw new InvalidOperationException($"invalid operand {strA}");
      if (!TryParseAddress(strB, out var addrB, defType: addrA.Type))
        throw new InvalidOperationException($"invalid operand {strB}");

      if (addrA.Base == null && addrA.Relative)
        throw new InvalidOperationException($"operand A({strA}) cannot be relative without base");

      if (addrA.Base != null && !addrB.Relative)
        throw new InvalidOperationException(
          $"operand B({strB}) must be relative when operand A({strA}) has relative base");

      if (addrB.Base != null)
        throw new InvalidOperationException($"operand B({strB}) cannot have relative base");

      instructions.Add(new()
      {
        Address = nextAddress,
        Op = op,
        Width = (byte)width,
        OperandA = addrA,
        OperandB = addrB,
      });

      nextAddress += 8;
    }

    private bool TryParseAddress(string str, out ParsedAddr addr, DataType? defType = null)
    {
      addr = default;

      var parts = str.Split(':');
      if (parts.Length < 2 && defType == null)
        return false;
      if (parts.Length > 2)
        return false;

      if (parts.Length < 2)
        addr.Type = defType.Value;
      else if (!Enum.TryParse(parts[1], true, out addr.Type))
        return false;

      str = parts[0];

      var relIdx = str.IndexOfAny(['+', '-']);
      if (relIdx > 0)
      {
        addr.Base = str[..relIdx];
        if (relIdx > 2 && addr.Base[0] == '[' && addr.Base[^1] == ']')
        {
          addr.BaseIndirect = true;
          addr.Base = addr.Base[1..^1];
        }
      }

      if (relIdx != -1)
      {
        addr.Relative = true;
        addr.Addr = str[relIdx..];
        if (addr.Addr.Length > 3 && addr.Addr[1] == '[' && addr.Addr[^1] == ']')
        {
          addr.Indirect = true;
          // +[addr] => +addr
          addr.Addr = addr.Addr[..1] + addr.Addr[2..^1];
        }
      }
      else
      {
        addr.Addr = str;
        if (str.Length > 2 && str[0] == '[' && str[^1] == ']')
        {
          addr.Indirect = true;
          addr.Addr = str[1..^1];
        }
      }

      return true;
    }

    public void Assemble(Memory mem)
    {
      foreach (var (addr, type, val) in values)
      {
        if (Debug)
          Console.WriteLine($"{addr} {type} {type.VMode() switch
          {
            ValueMode.Unsigned => val.Unsigned,
            ValueMode.Signed => val.Signed,
            ValueMode.Float => val.Float,
            _ => "Invalid",
          }}");

        mem.Write(addr, type, val);
      }

      foreach (var pinst in instructions)
      {
        if (Debug)
          Console.WriteLine($"{pinst.Address} {pinst.Op} {pinst.OperandA} {pinst.OperandB}");

        var inst = new Instruction { OpCode = pinst.Op, DataWidth = pinst.Width };
        var (opA, opB) = (pinst.OperandA, pinst.OperandB);

        if (opA.Base != null)
        {
          inst.OperandMode = OperandMode.AddrBaseOffAB;
          inst.AddrBase = ResolveAddr(opA.Base);
          inst.BaseIndirect = opA.BaseIndirect;

          inst.OffsetA = ResolveAddr(opA.Addr);
          inst.OffsetB = ResolveAddr(opB.Addr);
        }
        else
        {
          inst.AddrBase = ResolveAddr(opA.Addr);
          inst.OffsetB = ResolveAddr(opB.Addr);
          if (!opB.Relative)
            inst.OffsetB -= inst.AddrBase;
        }
        inst.AType = opA.Type;
        inst.BType = opB.Type;
        if (opA.Indirect)
          inst.OperandMode |= OperandMode.IndirectA;
        if (opB.Indirect)
          inst.OperandMode |= OperandMode.IndirectB;

        mem.WriteU64(pinst.Address, inst.Encode());
      }
    }

    private int ResolveAddr(string strAddr)
    {
      var sign = 1;
      if (strAddr[0] == '+')
        strAddr = strAddr[1..];
      else if (strAddr[0] == '-')
      {
        strAddr = strAddr[1..];
        sign = -1;
      }

      if (TryParseSigned(strAddr, out var laddr))
        return (int)laddr * sign;

      if (labels.TryGetValue(strAddr, out var addr))
        return addr * sign;

      throw new InvalidOperationException($"invalid address {strAddr}");
    }

    private class ParsedInst
    {
      public int Address;
      public OpCode Op;
      public byte Width;

      public ParsedAddr OperandA;
      public ParsedAddr OperandB;
    }

    private struct ParsedAddr
    {
      public string Base;
      public bool BaseIndirect;

      public bool Relative;
      public string Addr;
      public bool Indirect;

      public DataType Type;

      public override string ToString()
      {
        var sb = new StringBuilder();
        if (Base != null)
          sb.Append(BaseIndirect ? $"[{Base}]" : Base);
        sb.Append(Indirect ? $"[{Addr}]" : Addr);
        sb.Append($":{Type}");
        return sb.ToString();
      }
    }
  }
}