
using System;
using System.Collections.Generic;

namespace KSASM.Assembly
{
  public class Parser : TokenProcessor, IConstParser
  {
    public readonly List<Statement> Statements = [];
    public readonly AppendBuffer<ExprNode> ConstNodes = new();
    public readonly AppendBuffer<FixedRange> ConstRanges = new(); // ranges in ConstNodes of a single expression

    private readonly AppendBuffer<Token> tokens;
    private int index = 0;

    public Parser(ParseBuffer buffer, AppendBuffer<Token> tokens) : base(buffer)
    {
      this.tokens = tokens;
    }

    private bool EOF => index >= tokens.Length;

    protected override bool Peek(out Token token)
    {
      if (EOF)
      {
        token = default;
        return false;
      }
      token = tokens[index];
      return true;
    }

    private bool TakeType(TokenType type)
    {
      if (EOF || tokens[index].Type != type)
        return false;
      index++;
      return true;
    }
    public bool TakeType(TokenType type, out Token token)
    {
      if (!Peek(out token) || token.Type != type)
        return false;
      index++;
      return true;
    }

    private bool PeekType(TokenType type) => !EOF && tokens[index].Type == type;
    private bool PeekType(TokenType type, out Token token) => Peek(out token) && token.Type == type;

    public void Parse()
    {
      while (!EOF)
        ParseLine();
    }

    private void ParseLine()
    {
      ParseLinePrefix();
      if (TakeType(TokenType.EOL))
        return;
      if (PeekType(TokenType.Type))
        ParseDataLine();
      else
        ParseInstruction();

      if (!TakeType(TokenType.EOL))
        Invalid();
    }

    private void ParseLinePrefix()
    {
      while (true)
      {
        if (TakeType(TokenType.Label, out var ltoken))
          AddLabel(ltoken);
        else if (TakeType(TokenType.Position, out var ptoken))
          AddPosition(ptoken);
        else
          break;
      }
    }

    private void AddLabel(Token token) =>
      Statements.Add(new LabelStatement { Token = token, Label = new(buffer[token][..^1]) });

    private void AddPosition(Token token)
    {
      if (!int.TryParse(buffer[token][1..], out var addr))
        Invalid(token);
      Statements.Add(new PositionStatement { Token = token, Addr = addr });
    }

    private void ParseDataLine()
    {
      while (TakeType(TokenType.Type, out var typeToken))
        ParseDataGroup(typeToken);
    }

    private void ParseDataGroup(Token typeToken)
    {
      if (!Token.TryParseType(buffer[typeToken], out var type))
        throw Invalid(typeToken);

      while (true)
      {
        ParseLinePrefix();

        var data = new ParsedData() { Type = type };
        if (TakeType(TokenType.Number, out data.Value)) { }
        else if (TakeType(TokenType.Word, out data.Value)) { }
        else if (TakeType(TokenType.String, out data.Value))
        {
          if (type is not (DataType.U8 or DataType.S48))
            throw Invalid(data.Value);
        }
        else if (TakeType(TokenType.POpen, out data.Value))
        {
          data.ExprVal = ParseConsts();
          if (!TakeType(TokenType.PClose))
            throw Invalid();
        }
        else
          break;

        if (TakeType(TokenType.Mult))
        {
          if (TakeType(TokenType.Number, out var wtoken))
            data.Width = wtoken;
          else
            throw Invalid();
        }

        Statements.Add(new DataStatement(data));
      }
    }

    private void ParseInstruction()
    {
      var inst = new ParsedInstruction();

      if (!TakeType(TokenType.Word, out inst.OpCode))
        throw Invalid();

      if (TakeType(TokenType.Type, out var ttoken))
        inst.DefaultType = ttoken;

      if (TakeType(TokenType.Mult))
      {
        if (!TakeType(TokenType.Number, out var wtoken))
          throw Invalid();
        inst.Width = wtoken;
      }

      var allowImm = true;
      while (inst.OperandCount < 3 && !PeekType(TokenType.EOL))
      {
        if (inst.ResultIndex == -1 && TakeType(TokenType.Result))
        {
          inst.ResultIndex = inst.OperandCount;
          allowImm = false;
          continue;
        }

        ParseOperand(ref inst.Op(inst.OperandCount++), ref allowImm);

        if (inst.OperandCount == 1 && inst.Width == null && TakeType(TokenType.Mult))
        {
          if (!TakeType(TokenType.Number, out var wtoken))
            throw Invalid();
          inst.Width = wtoken;
        }
      }

      Statements.Add(new InstructionStatement(inst));
    }

    private void ParseOperand(ref ParsedOperand op, ref bool allowImm)
    {
      if (TakeType(TokenType.Placeholder, out var token))
      {
        allowImm = false;
        op.Val = token;
      }
      else if (!allowImm)
        throw Invalid();
      else if (TakeType(TokenType.Number, out token))
        op.Val = token;
      else if (TakeType(TokenType.Word, out token))
        op.Val = token;
      else if (TakeType(TokenType.String, out token))
        op.Val = token;
      else if (TakeType(TokenType.POpen, out token))
      {
        op.Val = token;
        op.ExprVal = ParseConsts();
        if (!TakeType(TokenType.PClose))
          throw Invalid();
      }
      else
        throw Invalid();

      if (TakeType(TokenType.Type, out token))
        op.Type = token;
    }

    private FixedRange ParseConsts()
    {
      var start = ConstRanges.Length;

      ParseConstInner();

      while (TakeType(TokenType.Comma, out _))
        ParseConstInner();

      return new(start, ConstRanges.Length - start);
    }

    private void ParseConstInner()
    {
      var start = ConstNodes.Length;
      if (!Const.TryParse(this))
        throw Invalid();
      ConstRanges.Add(new(start, ConstNodes.Length - start));
    }

    public void PushConstNode(ConstOp op, Token token) => ConstNodes.Add(new(op, token.Index));
  }

  public class ParsedData
  {
    public DataType Type;
    public Token Value;
    public FixedRange? ExprVal;
    public Token? Width;
  }

  public class ParsedInstruction()
  {
    public Token OpCode;
    public Token? DefaultType;
    public Token? Width;

    public int ResultIndex = -1;
    public int OperandCount;

    public ParsedOperand A;
    public ParsedOperand B;
    public ParsedOperand C;

    public ref ParsedOperand Op(int idx)
    {
      switch (idx)
      {
        case 0: return ref A;
        case 1: return ref B;
        case 2: return ref C;
        default: throw new IndexOutOfRangeException($"{idx}");
      }
    }
  }

  public struct ParsedOperand
  {
    public Token? Val;
    public FixedRange? ExprVal;
    public Token? Type;
  }

  public enum ConstOp { Leaf, Neg, Not, Add, Sub, Mul, Div }
  public record struct ExprNode(ConstOp Op, TokenIndex Token);
}