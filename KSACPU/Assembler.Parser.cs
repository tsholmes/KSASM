
using System;
using System.Collections.Generic;
using System.Globalization;

namespace KSACPU
{
  public partial class Assembler
  {
    public class Parser
    {
      private readonly SourceString source;
      private readonly LexerReader lexer;
      public readonly List<Statement> Statements = [];

      public Parser(SourceString source)
      {
        this.source = source;
        ITokenStream stream = new Lexer(source);
        stream = new MacroParser(source, stream);
        lexer = new(stream);
      }

      public void Parse()
      {
        while (!lexer.EOF())
          ParseLine();
      }

      private void ParseLine()
      {
        while (!lexer.EOF() && !PeekType(TokenType.EOL, out _))
        {
          if (TakeType(TokenType.Label, out var ltoken))
            AddLabel(ltoken);
          else if (TakeType(TokenType.Position, out var ptoken))
            AddPosition(ptoken);
          else
            break;
        }
        if (TakeType(TokenType.EOL, out _))
          return;
        if (PeekType(TokenType.Type, out _))
          ParseDataLine();
        else
          ParseInstruction();

        if (!TakeType(TokenType.EOL, out _))
          Invalid();
      }

      private bool PeekType(TokenType type, out Token token) =>
        lexer.Peek(out token) && token.Type == type;

      private bool TakeType(TokenType type, out Token token) =>
        PeekType(type, out token) && lexer.Take(out token);

      private void AddLabel(Token token) =>
        Statements.Add(new LabelStatement { Label = source[token][..^1] });

      private void AddPosition(Token token)
      {
        if (!int.TryParse(source[token][1..], out var addr))
          Invalid(token);
        Statements.Add(new PositionStatement { Addr = addr });
      }

      private void ParseDataLine()
      {
        if (!TakeType(TokenType.Type, out var token))
          Invalid();

        if (!Enum.TryParse(source[token][1..], true, out DataType curType))
          Invalid(token);

        while (!lexer.EOF() && !PeekType(TokenType.EOL, out _))
        {
          if (TakeType(TokenType.Type, out token))
          {
            if (!Enum.TryParse(source[token][1..], true, out curType))
              Invalid(token);
          }
          else if (TakeType(TokenType.Label, out token))
            AddLabel(token);
          else if (TakeType(TokenType.Position, out token))
            AddPosition(token);
          else if (TakeType(TokenType.Word, out token))
            Statements.Add(new ValueStatement() { Type = curType, StrValue = source[token] });
          else if (TakeType(TokenType.Number, out token))
          {
            var stmt = new ValueStatement() { Type = curType };
            var valid = false;
            switch (curType.VMode())
            {
              case ValueMode.Unsigned:
                valid = TryParseUnsigned(source[token], out stmt.Value.Unsigned);
                break;
              case ValueMode.Signed:
                valid = TryParseSigned(source[token], out stmt.Value.Signed);
                break;
              case ValueMode.Float:
                valid = TryParseFloat(source[token], out stmt.Value.Float);
                break;
            }
            if (!valid)
              Invalid(token);
            if (TakeType(TokenType.Width, out token))
            {
              if (!int.TryParse(source[token][1..], out stmt.Width))
                Invalid(token);
            }
            Statements.Add(stmt);
          }
          else
            Invalid();
        }
      }

      private void ParseInstruction()
      {
        var inst = new InstructionStatement();

        if (!TakeType(TokenType.Word, out var opword))
          Invalid();
        if (!Enum.TryParse(source[opword], true, out inst.Op))
          Invalid(opword);

        if (TakeType(TokenType.Width, out var wtoken) && !int.TryParse(source[wtoken][1..], out inst.Width))
          Invalid(wtoken);

        if (TakeType(TokenType.Type, out var ttoken))
        {
          if (!Enum.TryParse(source[ttoken][1..], true, out DataType parsedType))
            Invalid(ttoken);
          inst.Type = parsedType;
        }

        inst.OperandA = ParseOperand();

        if (!TakeType(TokenType.Comma, out _))
          Invalid();

        inst.OperandB = ParseOperand();

        Statements.Add(inst);
      }

      private ParsedOperand ParseOperand()
      {
        var op = new ParsedOperand();

        if (TakeType(TokenType.Placeholder, out _))
        {
          op.Mode = ParsedOpMode.Placeholder;
        }
        else if (PeekType(TokenType.COpen, out _))
        {
          op.Const = ParseConst();
          op.Mode = ParsedOpMode.Const;
        }
        else
        {
          var first = ParseAddr(false);
          if (lexer.Peek(out var token) &&
              (token.Type is TokenType.IOpen or TokenType.Offset or TokenType.Word or TokenType.Number))
          {
            op.Base = first;
            op.Addr = ParseAddr(true);
            op.Mode = ParsedOpMode.BaseOffset;
          }
          else
          {
            op.Addr = first;
            op.Mode = first.Offset != null ? ParsedOpMode.Offset : ParsedOpMode.Addr;
          }
        }

        if (op.Mode != ParsedOpMode.Placeholder && TakeType(TokenType.Type, out var ttoken))
        {
          if (!Enum.TryParse(source[ttoken][1..], true, out DataType type))
            Invalid(ttoken);
          op.Type = type;
        }

        return op;
      }

      private AddrRef ParseAddr(bool requireOffset)
      {
        var addr = new AddrRef();

        addr.Indirect = TakeType(TokenType.IOpen, out _);

        if (TakeType(TokenType.Offset, out var otoken))
          addr.Offset = source[otoken];
        else if (requireOffset)
          Invalid();

        if (TakeType(TokenType.Word, out var wtoken))
          addr.StrAddr = source[wtoken];
        else if (TakeType(TokenType.Number, out var ntoken))
        {
          if (!TryParseValue(source[ntoken], out var val, out var mode))
            Invalid(ntoken);
          if (mode != ValueMode.Unsigned)
            Invalid(ntoken);
          addr.IntAddr = (int)val.Unsigned;
        }
        else
          Invalid();

        if (addr.Indirect && !TakeType(TokenType.IClose, out _))
          Invalid();

        return addr;
      }

      private ConstVal ParseConst()
      {
        if (!TakeType(TokenType.COpen, out _))
          Invalid();

        var cval = new ConstVal();

        if (TakeType(TokenType.Word, out var wtoken))
          cval.StringVal = source[wtoken];
        else if (TakeType(TokenType.Number, out var ntoken))
        {
          if (!TryParseValue(source[ntoken], out cval.Value, out cval.Mode))
            Invalid(ntoken);
        }
        else
          Invalid();

        if (!TakeType(TokenType.PClose, out _))
          Invalid();

        return cval;
      }

      private bool TryParseValue(string str, out Value value, out ValueMode mode)
      {
        str = str.ToLowerInvariant();
        if (TryParseUnsigned(str, out var uval))
        {
          value = new() { Unsigned = uval };
          mode = ValueMode.Unsigned;
          return true;
        }
        else if (TryParseSigned(str, out var sval))
        {
          value = new() { Signed = sval };
          mode = ValueMode.Signed;
          return true;
        }
        else if (TryParseFloat(str, out var fval))
        {
          value = new() { Float = fval };
          mode = ValueMode.Signed;
          return true;
        }
        value = default;
        mode = default;
        return false;
      }

      private static bool TryParseUnsigned(string str, out ulong val) =>
        ulong.TryParse(str, NumberStyles.Integer, null, out val) ||
        (str.StartsWith("0b") && ulong.TryParse(str[2..], NumberStyles.BinaryNumber, null, out val)) ||
        (str.StartsWith("0x") && ulong.TryParse(str[2..], NumberStyles.HexNumber, null, out val));

      private static bool TryParseSigned(string str, out long val) =>
        long.TryParse(str, NumberStyles.Integer, null, out val) ||
        (str.StartsWith("0b") && long.TryParse(str[2..], NumberStyles.BinaryNumber, null, out val)) ||
        (str.StartsWith("0x") && long.TryParse(str[2..], NumberStyles.HexNumber, null, out val));

      private static bool TryParseFloat(string str, out double val) =>
        double.TryParse(str, NumberStyles.Float, null, out val) ||
        (str.StartsWith("0b") && double.TryParse(str[2..], NumberStyles.BinaryNumber, null, out val)) ||
        (str.StartsWith("0x") && double.TryParse(str[2..], NumberStyles.HexNumber, null, out val));

      private void Invalid()
      {
        lexer.Peek(out var token);
        Invalid(token);
      }

      private void Invalid(Token token) => throw new InvalidOperationException(
        $"invalid token {token.Type} '{source[token]}' at {source.Pos(token.Pos)}");
    }

    public enum ParsedOpMode
    {
      Placeholder,
      Addr,
      Offset,
      BaseOffset,
      Const,
    }

    public class ParsedOperand
    {
      public ParsedOpMode Mode;
      public AddrRef Base;
      public AddrRef Addr;
      public ConstVal Const;
      public DataType? Type;
    }

    public class AddrRef
    {
      public string Offset;

      public string StrAddr;
      public int IntAddr;

      public bool Indirect;
    }

    public class ConstVal
    {
      public string StringVal;
      public ValueMode Mode;
      public Value Value;
    }
  }
}