
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
      var buffer = new ParseBuffer();
      var sindex = Lexer.LexSource(buffer, source, TokenIndex.Invalid);
      var tokens = new AppendBuffer<Token>();
      var mp = new MacroParser(buffer, sindex);
      while (mp.Next(out var token))
        tokens.Add(token);

      var parser = new Parser(buffer, tokens);
      parser.Parse();

      var ctx = new Context(buffer, tokens, parser.ConstNodes, parser.ConstRanges);

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

  public abstract class TokenProcessor(ParseBuffer buffer)
  {
    protected readonly ParseBuffer buffer = buffer;

    protected abstract bool Peek(out Token token);

    protected Exception Invalid()
    {
      Peek(out var token);
      return Invalid(token);
    }

    protected Exception Invalid(Token token) =>
      throw new InvalidOperationException($"invalid token {token.Type} '{buffer[token]}'\n{StackPos(token)}");

    public static string StackPos(ParseBuffer buffer, Token token, int frameLimit = 20)
    {
      var sb = new StringBuilder();
      var lastRoot = token.Index;
      var current = token.Index;
      var more = true;

      var debug = new DebugSymbols(buffer, null);

      for (var i = 0; i < 20; i++)
      {
        bool newRoot;
        if (newRoot = current == TokenIndex.Invalid)
          lastRoot = current = buffer.Source(buffer[lastRoot].Source).Producer;
        if (current == TokenIndex.Invalid)
        {
          more = false;
          break;
        }

        if (i > 0)
          sb.AppendLine();

        if (newRoot)
          sb.AppendLine("== expanded from ==");

        var tok = buffer[current];

        var sname = buffer.SourceName(tok.Source);
        var sline = debug.SourceLine(current, out var lnum, out var loff);

        sb.Append(sname).Append(':').Append(lnum + 1).Append(':').Append(loff);
        if (sline.Length > 0)
          sb.AppendLine().Append(sline);

        current = tok.Previous;
      }

      if (more && frameLimit > 1)
        sb.AppendLine().Append("...");

      return sb.ToString();
    }

    protected string StackPos(Token token, int frameLimit = 20) => StackPos(buffer, token, frameLimit);
  }

  public class Context(
    ParseBuffer buffer,
    AppendBuffer<Token> tokens,
    AppendBuffer<ExprNode> constNodes,
    AppendBuffer<FixedRange> constRanges
  ) : TokenProcessor(buffer)
  {
    public readonly DebugSymbols Symbols = new(buffer, tokens);
    public readonly ParseBuffer Buffer = buffer;

    public readonly AppendBuffer<ExprNode> ConstNodes = constNodes;
    public readonly AppendBuffer<FixedRange> ConstRanges = constRanges;

    public readonly Dictionary<string, int> Labels = [];

    public readonly AppendBuffer<Value> RawValues = new();
    public readonly AppendBuffer<(int, DataType, FixedRange)> Values = new();

    // public readonly List<(DataType, ConstExprList, int)> ConstExprs = [];
    // public readonly Dictionary<(DataType, ConstExprList), int> ConstExprAddrs = [];

    public int Addr = 0;

    public void EmitLabel(string label)
    {
      Symbols.AddLabel(label, Addr);
      Labels[label] = Addr;
    }

    public void EmitInst(Token token, ulong encoded)
    {
      Symbols.AddInst(Addr, token.Index);
      // TODO
      using var emitter = Emitter(DataType.P24);
      emitter.Emit(new() { Unsigned = encoded });
    }

    public ValueEmitter Emitter(DataType type) => new(this, type);

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

    public Value EvalExpr(int index, ValueMode mode)
    {
      // TODO: reuse stack
      var vals = new Stack<Value>();
      Value left = default, right = default;
      var ops = mode.Ops();
      var range = ConstRanges[index];
      for (var i = range.Start; i < range.End; i++)
      {
        var node = ConstNodes[i];
        var token = Buffer[node.Token];
        switch (node.Op)
        {
          case ConstOp.Leaf:
            if (token.Type == TokenType.Number)
            {
              if (!Token.TryParseValue(Buffer[token], out left, out var lmode))
                throw Invalid(token);
              left.Convert(lmode, mode);
            }
            else if (token.Type == TokenType.Word)
            {
              if (!Labels.TryGetValue(new(Buffer[token]), out var lpos))
                throw Invalid(token);
              left.Unsigned = (ulong)lpos;
              left.Convert(ValueMode.Unsigned, mode);
            }
            else
              throw Invalid(token);
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
            throw new InvalidOperationException($"Unknown const op {node.Op}");
        }
      }
      return vals.Pop();
    }

    protected override bool Peek(out Token token) => throw new NotImplementedException();

    public readonly struct ValueEmitter(Context ctx, DataType type) : IDisposable
    {
      private readonly Context ctx = ctx;
      private readonly int addr = ctx.Addr;
      private readonly DataType type = type;
      private readonly int valSize = type.SizeBytes();
      private readonly int start = ctx.RawValues.Length;

      public readonly void Emit(Value val)
      {
        ctx.RawValues.Add(val);
        ctx.Addr += valSize;
      }

      public readonly void EmitRange(Span<Value> vals)
      {
        ctx.RawValues.AddRange(vals);
        ctx.Addr += valSize * vals.Length;
      }

      readonly void IDisposable.Dispose() =>
        ctx.Values.Add((addr, type, new(start, ctx.RawValues.Length - start)));
    }
  }
}