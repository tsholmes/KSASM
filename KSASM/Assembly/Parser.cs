
using System;
using System.Collections.Generic;

namespace KSASM.Assembly
{
  public class Parser : TokenProcessor
  {
    private readonly LexerReader lexer;
    public readonly List<Statement> Statements = [];

    public Parser(SourceString source, Context ctx) : base(ctx)
    {
      var tsource = Lexer.LexTokens(source);
      lexer = new(new MacroParser(tsource.AsStream(), ctx), -2);
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
      Statements.Add(new LabelStatement { Token = token, Label = new(token[..^1]) });

    private void AddPosition(Token token)
    {
      if (!int.TryParse(token[1..], out var addr))
        Invalid(token);
      Statements.Add(new PositionStatement { Token = token, Addr = addr });
    }

    private void ParseDataLine()
    {
      if (!TakeType(TokenType.Type, out var token))
        Invalid();

      if (!Enum.TryParse(token[1..], true, out DataType curType))
        Invalid(token);

      while (!lexer.EOF() && !PeekType(TokenType.EOL, out _))
      {
        if (TakeType(TokenType.Type, out token))
        {
          if (!Enum.TryParse(token[1..], true, out curType))
            Invalid(token);
        }
        else if (TakeType(TokenType.Label, out token))
          AddLabel(token);
        else if (TakeType(TokenType.Position, out token))
          AddPosition(token);
        else if (TakeType(TokenType.Word, out token))
          Statements.Add(new ValueStatement { Token = token, Type = curType, StrValue = token.Str() });
        else if (TakeType(TokenType.Number, out token))
        {
          var stmt = new ValueStatement { Token = token, Type = curType };
          var valid = false;
          switch (curType.VMode())
          {
            case ValueMode.Unsigned:
              valid = Values.TryParseUnsigned(token, out stmt.Value.Unsigned);
              break;
            case ValueMode.Signed:
              valid = Values.TryParseSigned(token, out stmt.Value.Signed);
              break;
            case ValueMode.Float:
              valid = Values.TryParseFloat(token, out stmt.Value.Float);
              break;
          }
          if (!valid)
            Invalid(token);
          if (TakeType(TokenType.Width, out token))
          {
            if (!int.TryParse(token[1..], out stmt.Width))
              Invalid(token);
          }
          Statements.Add(stmt);
        }
        else if (TakeType(TokenType.String, out token))
        {
          if (curType != DataType.U8)
            Invalid(token);

          var stmt = new ValueListStatement { Token = token, Values = [] };
          var str = token[1..^1];
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
            if (!int.TryParse(token[1..], out stmt.Width))
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
      if (!Enum.TryParse(inst.OpToken, true, out inst.Op))
        Invalid(inst.OpToken);

      if (TakeType(TokenType.Type, out var ttoken))
      {
        if (!Enum.TryParse(ttoken[1..], true, out DataType parsedType))
          Invalid(ttoken);
        inst.Type = parsedType;
      }

      if (TakeType(TokenType.Width, out var wtoken) && !int.TryParse(wtoken[1..], out inst.Width))
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
        if (!Enum.TryParse(ttoken[1..], true, out DataType type))
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
        if (!Values.TryParseValue(ntoken, out var val, out var mode))
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
      var expr = new ConstExpr();

      void parseAddSub()
      {
        parseMulDiv();
        while (true)
        {
          if (TakeType(TokenType.Offset, out var otoken))
          {
            parseMulDiv();
            expr.PushOp(otoken, otoken[0] == '-' ? ConstOp.Sub : ConstOp.Add);
          }
          else
            break;
        }
      }
      void parseMulDiv()
      {
        parseGroup();
        while (true)
        {
          if (TakeType(TokenType.Mult, out var mtoken))
          {
            parseGroup();
            expr.PushOp(mtoken, ConstOp.Mul);
          }
          else if (TakeType(TokenType.Div, out var dtoken))
          {
            parseGroup();
            expr.PushOp(dtoken, ConstOp.Div);
          }
          else if (TakeType(TokenType.Width, out var wtoken))
          {
            if (!Values.TryParseValue(wtoken[1..], out var val, out var mode))
              throw Invalid(wtoken);
            expr.PushVal(wtoken, new(Value: val, Mode: mode));
            expr.PushOp(wtoken, ConstOp.Mul);
          }
          else
            break;
        }
      }
      void parseGroup()
      {
        if (TakeType(TokenType.POpen, out _))
        {
          parseAddSub();
          if (!TakeType(TokenType.PClose, out _))
            throw Invalid();
        }
        else if (TakeType(TokenType.Word, out var wtoken))
          expr.PushVal(wtoken, new(StringVal: wtoken.Str()));
        else if (TakeType(TokenType.Offset, out var otoken))
        {
          parseGroup();
          if (otoken[0] == '-')
            expr.PushOp(otoken, ConstOp.Neg);
        }
        else if (TakeType(TokenType.Number, out var ntoken))
        {
          if (!Values.TryParseValue(ntoken, out var val, out var mode))
            throw Invalid(ntoken);
          expr.PushVal(ntoken, new(Value: val, Mode: mode));
        }
        else
          throw Invalid();
      }
      parseAddSub();
      return expr;
    }

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

  public record struct ConstVal(string StringVal = null, ValueMode Mode = default, Value Value = default);

  public class ConstExprList : List<ConstExpr>
  {
    public bool Addr;
    public bool Indirect;
  }

  public enum ConstOp { Leaf, Neg, Add, Sub, Mul, Div }

  public record struct ExprNode(ConstOp Op, Token Token, ConstVal Val = default);

  public class ConstExpr : List<ExprNode>
  {
    public void PushVal(Token token, ConstVal val) => DoAdd(new(ConstOp.Leaf, token, val));
    public void PushOp(Token token, ConstOp op) => DoAdd(new(op, token));

    public ExprNode Root => Count > 0 ? this[^1] : default;

    private void DoAdd(ExprNode node)
    {
      Add(node);
    }
  }
}