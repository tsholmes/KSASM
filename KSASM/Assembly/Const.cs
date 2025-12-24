
using System;

namespace KSASM.Assembly
{
  public static class Const
  {
    public static bool TryParse<T>(T parser) where T : IConstParser, allows ref struct =>
      new CParser<T>(parser).TryParse();

    public static bool TryParse<T>(ref T parser) where T : struct, IConstParser =>
      new CParser<RefParser<T>>(new(ref parser)).TryParse();

    private readonly ref struct CParser<T>(T parser) where T : IConstParser, allows ref struct
    {
      private readonly T parser = parser;

      public bool TryParse() => ParseAddSub();
      
      private bool ParseAddSub()
      {
        if (!ParseMulDiv())
          return false;
        while (true)
        {
          if (parser.TakeType(TokenType.Plus, out var otoken))
          {
            if (!ParseMulDiv())
              return false;
            parser.PushConstNode(ConstOp.Add, otoken);
          }
          else if (parser.TakeType(TokenType.Minus, out otoken))
          {
            if (!ParseMulDiv())
              return false;
            parser.PushConstNode(ConstOp.Sub, otoken);
          }
          else
            return true;
        }
      }
      private bool ParseMulDiv()
      {
        if (!ParseGroup())
          return false;
        while (true)
        {
          if (parser.TakeType(TokenType.Mult, out var mtoken))
          {
            if (!ParseGroup())
              return false;
            parser.PushConstNode(ConstOp.Mul, mtoken);
          }
          else if (parser.TakeType(TokenType.Div, out var dtoken))
          {
            if (!ParseGroup())
              return false;
            parser.PushConstNode(ConstOp.Div, dtoken);
          }
          else
            return true;
        }
      }
      private bool ParseGroup()
      {
        if (parser.TakeType(TokenType.POpen, out _))
        {
          if (!ParseAddSub())
            return false;
          if (!parser.TakeType(TokenType.PClose, out _))
            return false;
        }
        else if (parser.TakeType(TokenType.Word, out var wtoken))
          parser.PushConstNode(ConstOp.Leaf, wtoken);
        else if (parser.TakeType(TokenType.Minus, out var otoken))
        {
          if (!ParseGroup())
            return false;
          parser.PushConstNode(ConstOp.Neg, otoken);
        }
        else if (parser.TakeType(TokenType.Plus, out _))
          return ParseGroup();
        else if (parser.TakeType(TokenType.Not, out var bntoken))
        {
          if (!ParseGroup())
            return false;
          parser.PushConstNode(ConstOp.Not, bntoken);
        }
        else if (parser.TakeType(TokenType.Number, out var ntoken))
          parser.PushConstNode(ConstOp.Leaf, ntoken);
        else
          return false;

        return true;
      }
    }

    // returns invalid token on error
    public static Token? TryEvaluate<T>(T eval, ValueMode mode, out Value val)
      where T : IConstEvaluator, allows ref struct
    {
      var vals = new EvalStack(stackalloc Value[64]);
      Value left = default, right = default;
      var ops = mode.Ops();
      val = default;

      while (eval.NextOp(out var op, out var token))
      {
        switch (op)
        {
          case ConstOp.Leaf:
            if (token.Type == TokenType.Number)
            {
              var data = eval.TokenData(token);
              if (!Token.TryParseValue(data, out left, out var lmode))
                return token;
              left.Convert(lmode, mode);
            }
            else if (token.Type == TokenType.Word)
            {
              var data = eval.TokenData(token);
              if (!eval.TryGetName(data, mode, out left))
                return token;
            }
            else
              return token;
            vals.Push(left);
            break;
          case ConstOp.Neg:
            right = vals.Pop();
            ops.Negate(ref right);
            vals.Push(right);
            break;
          case ConstOp.Not:
            right = vals.Pop();
            ops.BitNot(ref right);
            vals.Push(right);
            break;
          case ConstOp.Add:
            right = vals.Pop();
            left = vals.Pop();
            ops.Add(ref left, right);
            vals.Push(left);
            break;
          case ConstOp.Sub:
            right = vals.Pop();
            left = vals.Pop();
            ops.Sub(ref left, right);
            vals.Push(left);
            break;
          case ConstOp.Mul:
            right = vals.Pop();
            left = vals.Pop();
            ops.Mul(ref left, right);
            vals.Push(left);
            break;
          case ConstOp.Div:
            right = vals.Pop();
            left = vals.Pop();
            ops.Div(ref left, right);
            vals.Push(left);
            break;
          default:
            return token;
        }
      }
      val = vals.Pop();
      return null;
    }

    private ref struct EvalStack(Span<Value> vals)
    {
      private readonly Span<Value> vals = vals;
      private int count = 0;

      public void Push(Value val) => vals[count++] = val;
      public Value Pop() => vals[--count];
    }

    private readonly ref struct RefParser<T>(ref T parser) : IConstParser
      where T : struct, IConstParser
    {
      private readonly ref T parser = ref parser;
      public void PushConstNode(ConstOp op, Token token) => parser.PushConstNode(op, token);
      public bool TakeType(TokenType type, out Token token) => parser.TakeType(type, out token);
    }
  }

  public interface IConstParser
  {
    public bool TakeType(TokenType type, out Token token);
    public void PushConstNode(ConstOp op, Token token);
  }

  public interface IConstEvaluator
  {
    public bool NextOp(out ConstOp op, out Token token);
    public ReadOnlySpan<char> TokenData(Token token);
    public bool TryGetName(ReadOnlySpan<char> name, ValueMode mode, out Value val);
  }
}