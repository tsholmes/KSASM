
using System;
using System.Collections.Generic;
using System.Text;

namespace KSASM.Assembly
{
  public class Assembler
  {
    public static bool Debug = false;

    public static DebugSymbols Assemble(SourceString source, MemoryAccessor target)
    {
      var ctx = new Context();
      var parser = new Parser(source, ctx);
      parser.Parse();

      foreach (var stmt in parser.Statements)
        stmt.FirstPass(ctx);
      ctx.EmitConstants();
      ctx.Addr = 0;
      foreach (var stmt in parser.Statements)
        stmt.SecondPass(ctx);

      var sb = new StringBuilder();

      foreach (var (addr, type, vals) in ctx.Values)
      {
        for (var i = 0; i < vals.Count; i++)
          target.Write(addr + i * type.SizeBytes(), type, vals[i]);
        if (Debug)
        {
          sb.Clear();
          sb.Append("ASM ");
          foreach (var (label, laddr) in ctx.Labels)
            if (laddr == addr)
              sb.Append(label).Append(": ");
          sb.Append($"{addr}: {type}");
          foreach (var val in vals)
            sb.Append($" {val.As(type)}");
          Console.WriteLine(sb.ToString());
        }
      }

      return ctx.Symbols;
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

  public class Context
  {
    public readonly List<Token> Frames = [];
    public readonly DebugSymbols Symbols = new();

    public readonly Dictionary<string, int> Labels = [];
    public readonly List<(int, DataType, List<Value>)> Values = [];

    public readonly List<(DataType, ConstExprList, int)> ConstExprs = [];
    public readonly Dictionary<(DataType, ConstExprList), int> ConstExprAddrs = [];

    public int Addr = 0;

    public int AddFrame(Token token)
    {
      var idx = Frames.Count;
      Frames.Add(token);
      return idx;
    }

    public string StackPos(Token token, int frameLimit = 20)
    {
      var sb = new StringBuilder();

      for (var i = 0; i < frameLimit; i++)
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

    public void EmitInst(Token token, ulong encoded)
    {
      Symbols.AddInst(Addr, StackPos(token, 1));
      Emit(DataType.U64, new Value() { Unsigned = encoded });
    }

    public void Emit(DataType type, List<Value> values)
    {
      Values.Add((Addr, type, [.. values]));
      Addr += type.SizeBytes() * values.Count;
    }

    public void Emit(DataType type, params ReadOnlySpan<Value> values)
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
      var sb = new StringBuilder();
      foreach (var (type, consts, wid) in ConstExprs)
      {
        if (consts.Addr)
        {
          var addr = EvalExpr(consts[0], type.VMode());
          addr.Convert(type.VMode(), ValueMode.Unsigned);
          if (Assembler.Debug)
            Console.WriteLine($"CONST ADDR {addr}");
          ConstExprAddrs[(type, consts)] = (int)addr.Unsigned;
          continue;
        }
        ValueX8 vs = new();
        for (var i = 0; i < consts.Count; i++)
        {
          vs[i] = EvalExpr(consts[i], type.VMode());
        }
        for (var i = consts.Count; i < 8; i++)
        {
          vs[i] = vs[i % consts.Count];
        }
        vals[(type, consts)] = vs;

        if (Assembler.Debug)
        {
          sb.Clear();
          sb.Append($"CONST {type}*{wid}");
          for (var i = 0; i < 8; i++)
            sb.Append($" {vs[i].As(type)}");
          Console.WriteLine(sb.ToString());
        }

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
        Emit(type, vs[..width]);
      }

      foreach (var (type, expr, _) in ConstExprs)
      {
        if (expr.Addr) continue;
        var val = vals[(type, expr)];
        ConstExprAddrs[(type, expr)] = constAddrs[(type, val)];
      }
    }

    public Value EvalExpr(ConstExpr expr, ValueMode mode)
    {
      var vals = new Stack<Value>();
      Value left = default, right = default;
      var ops = mode.Ops();
      foreach (var node in expr)
      {
        switch (node.Op)
        {
          case ConstOp.Leaf:
            if (node.Val.StringVal != null)
            {
              if (!Labels.TryGetValue(node.Val.StringVal, out var lpos))
                throw Invalid(node.Token);
              left.Unsigned = (ulong)lpos;
              left.Convert(ValueMode.Unsigned, mode);
            }
            else
            {
              left = node.Val.Value;
              left.Convert(node.Val.Mode, mode);
            }
            vals.Push(left);
            break;
          case ConstOp.Neg:
            right = vals.Pop();
            ops.Negate(ref right);
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
            throw new InvalidOperationException($"Unknown const op {node.Op}");
        }
      }
      return vals.Pop();
    }

    private Exception Invalid(Token token) => throw new InvalidOperationException(
      $"invalid token {token.Type} '{token.Str()}' at {StackPos(token)}");
  }
}