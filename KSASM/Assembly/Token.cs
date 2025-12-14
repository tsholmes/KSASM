
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
    COpen, // $(
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