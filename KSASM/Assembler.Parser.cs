
using System;
using System.Collections.Generic;
using System.Globalization;

namespace KSASM
{
  public partial class Assembler
  {
    public class Parser : TokenProcessor
    {
      private readonly LexerReader lexer;
      public readonly List<Statement> Statements = [];

      public Parser(SourceString source, Context ctx) : base(ctx)
      {
        ITokenStream stream = new Lexer(source);
        stream = new MacroParser(stream, ctx);
        lexer = new(stream, -2);
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
        Statements.Add(new LabelStatement { Label = token.Str()[..^1] });

      private void AddPosition(Token token)
      {
        if (!int.TryParse(token.Str()[1..], out var addr))
          Invalid(token);
        Statements.Add(new PositionStatement { Addr = addr });
      }

      private void ParseDataLine()
      {
        if (!TakeType(TokenType.Type, out var token))
          Invalid();

        if (!Enum.TryParse(token.Str()[1..], true, out DataType curType))
          Invalid(token);

        while (!lexer.EOF() && !PeekType(TokenType.EOL, out _))
        {
          if (TakeType(TokenType.Type, out token))
          {
            if (!Enum.TryParse(token.Str()[1..], true, out curType))
              Invalid(token);
          }
          else if (TakeType(TokenType.Label, out token))
            AddLabel(token);
          else if (TakeType(TokenType.Position, out token))
            AddPosition(token);
          else if (TakeType(TokenType.Word, out token))
            Statements.Add(new ValueStatement() { Type = curType, StrValue = token.Str() });
          else if (TakeType(TokenType.Number, out token))
          {
            var stmt = new ValueStatement() { Type = curType };
            var valid = false;
            switch (curType.VMode())
            {
              case ValueMode.Unsigned:
                valid = TryParseUnsigned(token.Str(), out stmt.Value.Unsigned);
                break;
              case ValueMode.Signed:
                valid = TryParseSigned(token.Str(), out stmt.Value.Signed);
                break;
              case ValueMode.Float:
                valid = TryParseFloat(token.Str(), out stmt.Value.Float);
                break;
            }
            if (!valid)
              Invalid(token);
            if (TakeType(TokenType.Width, out token))
            {
              if (!int.TryParse(token.Str()[1..], out stmt.Width))
                Invalid(token);
            }
            Statements.Add(stmt);
          }
          else if (TakeType(TokenType.String, out token))
          {
            if (curType != DataType.U8)
              Invalid(token);

            var stmt = new ValueListStatement { Values = [] };
            var str = token.Str()[1..^1];
            for (var i = 0; i < str.Length; i++)
            {
              var c = str[i];
              if (c == '\\')
              {
                if (i == str.Length - 1)
                  Invalid(token);
                var cn = str[i + 1];
                c = cn switch
                {
                  'n' => '\n',
                  'r' => '\r',
                  '\\' => '\\',
                  't' => '\t',
                  _ => throw Invalid(token),
                };
                i++;
              }
              stmt.Values.Add(new() { Unsigned = c });
            }
            Statements.Add(stmt);
          }
          else if (PeekType(TokenType.COpen, out _))
          {
            var stmt = new ExprValueStatement
            {
              Type = curType,
              Exprs = ParseConsts(),
            };
            if (TakeType(TokenType.Width, out token))
            {
              if (!int.TryParse(token.Str()[1..], out stmt.Width))
                throw Invalid(token);
            }
            Statements.Add(stmt);
          }
          else
            Invalid();
        }
      }

      private void ParseInstruction()
      {
        var inst = new InstructionStatement { Context = this.ctx };

        if (!TakeType(TokenType.Word, out inst.OpToken))
          Invalid();
        if (!Enum.TryParse(inst.OpToken.Str(), true, out inst.Op))
          Invalid(inst.OpToken);

        if (TakeType(TokenType.Type, out var ttoken))
        {
          if (!Enum.TryParse(ttoken.Str()[1..], true, out DataType parsedType))
            Invalid(ttoken);
          inst.Type = parsedType;
        }

        if (TakeType(TokenType.Width, out var wtoken) && !int.TryParse(wtoken.Str()[1..], out inst.Width))
          Invalid(wtoken);

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
          return op;
        }

        var indirect = TakeType(TokenType.IOpen, out _);

        if (PeekType(TokenType.COpen, out _) || PeekType(TokenType.CIOpen, out _))
        {
          op.Consts = ParseConsts();
          op.Consts.Indirect = indirect;
          op.Mode = ParsedOpMode.Const;

          if (indirect && !TakeType(TokenType.IClose, out _))
            throw Invalid();
        }
        else
        {
          var first = ParseAddr(indirect, false);
          if (lexer.Peek(out var token) &&
              (token.Type is TokenType.IOpen or TokenType.Offset or TokenType.Word or TokenType.Number))
          {
            op.Base = first;
            op.Addr = ParseAddr(indirect, true);
            op.Mode = ParsedOpMode.BaseOffset;
          }
          else
          {
            op.Addr = first;
            op.Mode = first.Offset != null ? ParsedOpMode.Offset : ParsedOpMode.Addr;
          }
        }

        if (TakeType(TokenType.Type, out var ttoken))
        {
          if (!Enum.TryParse(ttoken.Str()[1..], true, out DataType type))
            Invalid(ttoken);
          op.Type = type;
        }

        return op;
      }

      private AddrRef ParseAddr(bool indirect, bool requireOffset)
      {
        var addr = new AddrRef();

        addr.Indirect = indirect || TakeType(TokenType.IOpen, out _);

        if (TakeType(TokenType.Offset, out var otoken))
          addr.Offset = otoken.Str();
        else if (requireOffset)
          Invalid();

        if (TakeType(TokenType.Word, out var wtoken))
          addr.StrAddr = wtoken.Str();
        else if (TakeType(TokenType.Number, out var ntoken))
        {
          if (!TryParseValue(ntoken.Str(), out var val, out var mode))
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

      private ConstExprList ParseConsts()
      {
        var consts = new ConstExprList();

        if (TakeType(TokenType.CIOpen, out _))
          consts.Addr = true;
        else if (!TakeType(TokenType.COpen, out _))
          throw Invalid();

        consts.Add(ParseConstInner());

        while (TakeType(TokenType.Comma, out _))
          consts.Add(ParseConstInner());

        if (consts.Addr && !TakeType(TokenType.IClose, out _))
          throw Invalid();
        else if (!consts.Addr && !TakeType(TokenType.PClose, out _))
          throw Invalid();

        return consts;
      }

      private ConstExpr ParseConstInner()
      {
        ConstExpr parseAddSub()
        {
          var left = parseMulDiv();
          while (true)
          {
            if (TakeType(TokenType.Offset, out var otoken))
            {
              var right = parseMulDiv();
              left = new()
              {
                Op = otoken.Str() == "-" ? ConstOp.Sub : ConstOp.Add,
                Left = left,
                Right = right,
                Token = otoken
              };
            }
            else
              break;
          }
          return left;
        }
        ConstExpr parseMulDiv()
        {
          var left = parseGroup();
          while (true)
          {
            if (TakeType(TokenType.Mult, out var mtoken))
            {
              var right = parseGroup();
              left = new() { Op = ConstOp.Mul, Left = left, Right = right, Token = mtoken };
            }
            else if (TakeType(TokenType.Div, out var dtoken))
            {
              var right = parseGroup();
              left = new() { Op = ConstOp.Mul, Left = left, Right = right, Token = mtoken };
            }
            else if (TakeType(TokenType.Width, out var wtoken))
            {
              if (!TryParseValue(wtoken.Str()[1..], out var val, out var mode))
                throw Invalid(wtoken);
              var right = new ConstExpr() { Op = ConstOp.Leaf, Val = new() { Value = val, Mode = mode } };
              left = new() { Op = ConstOp.Mul, Left = left, Right = right, Token = wtoken };
            }
            else
              break;
          }
          return left;
        }
        ConstExpr parseGroup()
        {
          if (TakeType(TokenType.POpen, out _))
          {
            var res = parseAddSub();
            if (!TakeType(TokenType.PClose, out _))
              throw Invalid();
            return res;
          }
          else if (TakeType(TokenType.Word, out var wtoken))
            return new() { Op = ConstOp.Leaf, Val = new() { StringVal = wtoken.Str() }, Token = wtoken };
          else if (TakeType(TokenType.Offset, out var otoken))
          {
            var inner = parseGroup();
            if (otoken.Str() == "-")
              return new() { Op = ConstOp.Neg, Right = inner, Token = otoken };
            else
              return inner;
          }
          else if (TakeType(TokenType.Number, out var ntoken))
          {
            if (!TryParseValue(ntoken.Str(), out var val, out var mode))
              throw Invalid(ntoken);
            return new() { Op = ConstOp.Leaf, Val = new() { Value = val, Mode = mode } };
          }
          else
            throw Invalid();
        }
        return parseAddSub();
      }

      public static bool TryParseValue(string str, out Value value, out ValueMode mode)
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
          mode = ValueMode.Float;
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

      protected override bool Peek(out Token token) => lexer.Peek(out token);
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
      public ConstExprList Consts;
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

    public class ConstExprList : List<ConstExpr>
    {
      public bool Addr;
      public bool Indirect;
    }

    public class ConstExpr
    {
      public ConstOp Op;
      public ConstExpr Left;
      public ConstExpr Right;
      public ConstVal Val;
      public Token Token;
    }

    public enum ConstOp { Leaf, Neg, Add, Sub, Mul, Div }
  }
}