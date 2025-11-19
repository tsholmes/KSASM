
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
      var parser = new Parser(source);
      parser.Parse();

      var state = new State();

      foreach (var stmt in parser.Statements)
        stmt.FirstPass(state);
      state.EmitConstants();
      state.Addr = 0;
      foreach (var stmt in parser.Statements)
        stmt.SecondPass(state);

      var sb = new StringBuilder();

      foreach (var (addr, type, val) in state.Values)
      {
        target.Write(addr, type, val);
        if (Debug)
        {
          sb.Clear();
          sb.Append("ASM ");
          foreach (var (label, laddr) in state.Labels)
            if (laddr == addr)
              sb.Append(label).Append(": ");
          sb.Append($"{addr}: {type} {val.As(type)}");
          Console.WriteLine(sb.ToString());
        }
      }
    }

    public class State
    {
      public readonly Dictionary<string, int> Labels = [];
      public readonly List<(int, DataType, Value)> Values = [];

      public readonly List<(DataType, ConstExpr, int)> ConstExprs = [];
      public readonly Dictionary<(DataType, ConstExpr), int> ConstExprAddrs = [];

      public int Addr = 0;

      public void Emit(DataType type, Value value)
      {
        Values.Add((Addr, type, value));
        Addr += type.SizeBytes();
      }

      public void RegisterConst(DataType type, ConstExpr expr, int width)
      {
        // if (Debug)
        //   Console.WriteLine($"ASM RCONST {type}*{width} {val.As(type)}");
        ConstExprs.Add((type, expr, width));
      }

      public void EmitConstants()
      {
        var vals = new Dictionary<(DataType, ConstExpr), Value>();
        var maxWidths = new Dictionary<(DataType, Value), int>();
        foreach (var (type, expr, wid) in ConstExprs)
        {
          var val = EvalExpr(expr, type.VMode());
          vals[(type, expr)] = val;

          maxWidths[(type, val)] = Math.Max(maxWidths.GetValueOrDefault((type, val)), wid);
        }

        if (maxWidths.Count == 0)
          return;

        if (!Labels.TryGetValue("CONST", out var caddr))
          throw new InvalidOperationException("Cannot emit inlined constants without CONST label");

        Addr = caddr;
        var constAddrs = new Dictionary<(DataType, Value), int>();
        foreach (var ((type, val), width) in maxWidths)
        {
          constAddrs[(type, val)] = Addr;
          for (var i = 0; i < width; i++)
            Emit(type, val);
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
        $"invalid token {token.Type} '{token.Str()}' at {token.PosStr()}");
    }
  }
}