
using System;
using System.Collections.Generic;
using System.Text;

namespace KSASM
{
  public partial class Assembler
  {
    public static bool Debug = false;

    public static void Assemble(SourceString source, MemoryAccessor target)
    {
      var ctx = new Context();
      var parser = new Parser(source, ctx);
      parser.Parse();

      var state = new State(ctx);

      foreach (var stmt in parser.Statements)
        stmt.FirstPass(state);
      state.EmitConstants();
      state.Addr = 0;
      foreach (var stmt in parser.Statements)
        stmt.SecondPass(state);

      var sb = new StringBuilder();

      foreach (var (addr, type, vals) in state.Values)
      {
        for (var i = 0; i < vals.Count; i++)
          target.Write(addr + i * type.SizeBytes(), type, vals[i]);
        if (Debug)
        {
          sb.Clear();
          sb.Append("ASM ");
          foreach (var (label, laddr) in state.Labels)
            if (laddr == addr)
              sb.Append(label).Append(": ");
          sb.Append($"{addr}: {type}");
          foreach (var val in vals)
            sb.Append($" {val.As(type)}");
          Console.WriteLine(sb.ToString());
        }
      }
    }

    public class Context
    {
      public List<Token> Frames = [];

      public int AddFrame(Token token)
      {
        var idx = Frames.Count;
        Frames.Add(token);
        return idx;
      }

      public string StackPos(Token token)
      {
        var sb = new StringBuilder();

        while (true)
        {
          if (sb.Length > 0)
            sb.Append('@');
          if (token.Source != null)
          {
            var (line, lpos) = token.Source.LinePos(token.Pos);
            sb.AppendFormat("{0}:{1}:{2}('{3}')", token.Source.Name, line, lpos, token.Str());
          }
          else
            sb.AppendFormat("?:{0}('{1}')", token.Pos, token.Str());

          if (token.ParentFrame == -1)
            break;
          token = Frames[token.ParentFrame];
        }

        return sb.ToString();
      }
    }

    public abstract class TokenProcessor(Context ctx)
    {
      protected readonly Context ctx = ctx;

      protected abstract bool Peek(out Token token);

      protected Exception Invalid()
      {
        Peek(out var token);
        return Invalid(token);
      }

      protected Exception Invalid(Token token) => throw new InvalidOperationException(
        $"invalid token {token.Type} {ctx.StackPos(token)}");
    }

    public class State(Context ctx)
    {
      public readonly Dictionary<string, int> Labels = [];
      public readonly List<(int, DataType, List<Value>)> Values = [];

      public readonly List<(DataType, ConstExprList, int)> ConstExprs = [];
      public readonly Dictionary<(DataType, ConstExprList), int> ConstExprAddrs = [];

      public int Addr = 0;

      public void Emit(DataType type, params List<Value> values)
      {
        Values.Add((Addr, type, [.. values]));
        Addr += type.SizeBytes() * values.Count;
      }

      public void Emit(DataType type, Span<Value> values)
      {
        Values.Add((Addr, type, [.. values]));
        Addr += type.SizeBytes() * values.Length;
      }

      public void RegisterConst(DataType type, ConstExprList consts, int width)
      {
        // if (Debug)
        //   Console.WriteLine($"ASM RCONST {type}*{width} {val.As(type)}");
        ConstExprs.Add((type, consts, width));
      }

      public void EmitConstants()
      {
        var vals = new Dictionary<(DataType, ConstExprList), ValueX8>();
        var maxWidths = new Dictionary<(DataType, ValueX8), int>();
        foreach (var (type, consts, wid) in ConstExprs)
        {
          ValueX8 vs = new();
          for (var i = 0; i < consts.Count; i++)
          {
            vs[i] = EvalExpr(consts[i], type.VMode());
          }
          for (var i = consts.Count + 1; i < 8; i++)
          {
            vs[i] = vs[i % consts.Count];
          }
          vals[(type, consts)] = vs;

          maxWidths[(type, vs)] = Math.Max(maxWidths.GetValueOrDefault((type, vs)), wid);
        }

        if (maxWidths.Count == 0)
          return;

        if (!Labels.TryGetValue("CONST", out var caddr))
          throw new InvalidOperationException("Cannot emit inlined constants without CONST label");

        Addr = caddr;
        var constAddrs = new Dictionary<(DataType, ValueX8), int>();
        foreach (var ((type, vs), width) in maxWidths)
        {
          constAddrs[(type, vs)] = Addr;
          for (var i = 0; i < width; i++)
            Emit(type, vs[i]);
        }

        foreach (var (type, expr, _) in ConstExprs)
        {
          var val = vals[(type, expr)];
          ConstExprAddrs[(type, expr)] = constAddrs[(type, val)];
        }
      }

      public Value EvalExpr(ConstExpr expr, ValueMode mode)
      {
        Value left = default, right = default;
        switch (expr.Op)
        {
          case ConstOp.Leaf:
            if (expr.Val.StringVal != null)
            {
              if (!Labels.TryGetValue(expr.Val.StringVal, out var lpos))
                throw Invalid(expr.Token);
              left.Unsigned = (ulong)lpos;
              left.Convert(ValueMode.Unsigned, mode);
            }
            else
            {
              left = expr.Val.Value;
              left.Convert(expr.Val.Mode, mode);
            }
            return left;
          case ConstOp.Neg:
            right = EvalExpr(expr.Right, mode);
            mode.Ops().Negate(ref right);
            return right;
          case ConstOp.Add:
            left = EvalExpr(expr.Left, mode);
            right = EvalExpr(expr.Right, mode);
            mode.Ops().Add(ref left, right);
            return left;
          case ConstOp.Sub:
            left = EvalExpr(expr.Left, mode);
            right = EvalExpr(expr.Right, mode);
            mode.Ops().Sub(ref left, right);
            return left;
          case ConstOp.Mul:
            left = EvalExpr(expr.Left, mode);
            right = EvalExpr(expr.Right, mode);
            mode.Ops().Mul(ref left, right);
            return left;
          case ConstOp.Div:
            left = EvalExpr(expr.Left, mode);
            right = EvalExpr(expr.Right, mode);
            mode.Ops().Div(ref left, right);
            return left;
          default:
            throw new InvalidOperationException($"{expr.Op}");
        }
      }

      private Exception Invalid(Token token) => throw new InvalidOperationException(
        $"invalid token {token.Type} '{token.Str()}' at {ctx.StackPos(token)}");
    }
  }
}