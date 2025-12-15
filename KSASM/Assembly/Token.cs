
using System;
using System.Globalization;

namespace KSASM.Assembly
{
  public enum TokenType
  {
    Invalid,
    EOL, // non-escaped newline, or end of file
    Placeholder, // _
    Word, // [\w][\w\d]*
    Label, // word:\b
    Position, // @integer
    Result, // ->
    Comma, // ,
    Type, // :typename
    Number, // decimal, hex with 0x prefix, binary with 0b prefix
    POpen, // (
    PClose, // )
    Macro, // .macroname
    String, // "string"
    BOpen, // {
    BClose, // }
    Plus, // +
    Minus, // -
    Mult, // *
    Div, // /
    Not, // ~
  }

  public partial struct Token
  {
    public static bool TryParseType(ReadOnlySpan<char> data, out DataType type)
    {
      if (data.Length < 2 || char.IsAsciiDigit(data[0]))
      {
        type = default;
        return false;
      }
      return Enum.TryParse(data[1..], true, out type);
    }

    public static bool TryParseOpCode(ReadOnlySpan<char> data, out OpCode op)
    {
      if (data.Length == 0 || char.IsAsciiDigit(data[0]))
      {
        op = default;
        return false;
      }
      return Enum.TryParse(data, true, out op);
    }

    public static bool TryParseValue(ReadOnlySpan<char> str, out Value val, out ValueMode mode)
    {
      val = default;
      // we never get a signed value here, so just try unsigned and float
      if (TryParseUnsigned(str, out val.Unsigned))
      {
        mode = ValueMode.Unsigned;
        return true;
      }
      else if (double.TryParse(str, NumberStyles.Float, null, out val.Float))
      {
        mode = ValueMode.Float;
        return true;
      }
      mode = default;
      return false;
    }

    public static bool TryParseUnsigned(ReadOnlySpan<char> str, out ulong val) =>
      ulong.TryParse(str, NumberStyles.Integer, null, out val) ||
      TryParseHex(str, out val) ||
      TryParseBinary(str, out val);

    public static bool TryParseHex(ReadOnlySpan<char> str, out ulong val)
    {
      if (str.Length < 3 || str[0] != '0' || (str[1] != 'x' && str[1] != 'X'))
      {
        val = 0;
        return false;
      }
      return ulong.TryParse(str[2..], NumberStyles.HexNumber, null, out val);
    }

    public static bool TryParseBinary(ReadOnlySpan<char> str, out ulong val)
    {
      if (str.Length < 3 || str[0] != '0' || (str[1] != 'b' && str[1] != 'B'))
      {
        val = 0;
        return false;
      }
      return ulong.TryParse(str[1..], NumberStyles.BinaryNumber, null, out val);
    }

    public ref struct StringParser(ReadOnlySpan<char> data)
    {
      private ReadOnlySpan<char> data = data[1..1];

      public bool Done => data.Length == 0;

      public bool Next(out char c)
      {
        if (data.Length == 0)
        {
          c = default;
          return false;
        }
        c = data[0];
        data = data[1..];
        if (c == '\\')
        {
          if (data.Length == 0)
            return false;
          c = data[0];
          data = data[1..];
          c = c switch
          {
            'n' => '\n',
            'r' => '\r',
            '\\' => '\\',
            't' => '\t',
            _ => default,
          };
        }
        return c != default;
      }

      public bool NextChunk(scoped Span<Value> vals, out int count)
      {
        count = 0;
        while (!Done && count < vals.Length)
        {
          if (!Next(out var c))
            return false;
          vals[count++].Unsigned = c;
        }
        return true;
      }
    }
  }

  public static class Values
  {
    public static bool TryParseUnsigned(ReadOnlySpan<char> str, out ulong val) =>
      ulong.TryParse(str, NumberStyles.Integer, null, out val) ||
      TryParseHex(str, out val) ||
      TryParseBinary(str, out val);

    public static bool TryParseSigned(ReadOnlySpan<char> str, out long val)
    {
      if (long.TryParse(str, NumberStyles.Integer, null, out val))
        return true;
      else if (TryParseHex(str, out var uval) || TryParseBinary(str, out uval))
      {
        val = (long)uval;
        return true;
      }
      return false;
    }

    public static bool TryParseFloat(ReadOnlySpan<char> str, out double val)
    {
      if (double.TryParse(str, NumberStyles.Float, null, out val))
        return true;
      else if (TryParseSigned(str, out var uval))
      {
        val = uval;
        return true;
      }
      return false;
    }

    public static bool TryParseValue(ReadOnlySpan<char> str, out Value val, out ValueMode mode)
    {
      val = default;
      // we never get a signed value here, so just try unsigned and float
      if (TryParseUnsigned(str, out val.Unsigned))
      {
        mode = ValueMode.Unsigned;
        return true;
      }
      else if (double.TryParse(str, NumberStyles.Float, null, out val.Float))
      {
        mode = ValueMode.Float;
        return true;
      }
      mode = default;
      return false;
    }

    public static bool TryParseHex(ReadOnlySpan<char> str, out ulong val)
    {
      if (str.Length < 3 || str[0] != '0' || (str[1] != 'x' && str[1] != 'X'))
      {
        val = 0;
        return false;
      }
      return ulong.TryParse(str[2..], NumberStyles.HexNumber, null, out val);
    }

    public static bool TryParseBinary(ReadOnlySpan<char> str, out ulong val)
    {
      if (str.Length < 3 || str[0] != '0' || (str[1] != 'b' && str[1] != 'B'))
      {
        val = 0;
        return false;
      }
      return ulong.TryParse(str[1..], NumberStyles.BinaryNumber, null, out val);
    }

    public static bool TryFormat(Value value, ValueMode mode, Span<char> buf, out int length) =>
      mode switch
      {
        ValueMode.Unsigned => value.Unsigned.TryFormat(buf, out length),
        ValueMode.Signed => value.Signed.TryFormat(buf, out length),
        ValueMode.Float => value.Float.TryFormat(buf, out length),
        _ => throw new NotImplementedException($"{mode}"),
      };
  }
}