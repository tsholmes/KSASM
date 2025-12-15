
using System;
using System.Collections.Generic;
using System.Text;

namespace KSASM.Assembly
{
  public class Assembler
  {
    public const int DEFAULT_STRINGS_START = Processor.MAIN_MEM_SIZE - (2 << 20);
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
      ctx.EmitInlineStrings();
      ctx.Addr = 0;
      foreach (var stmt in parser.Statements)
        stmt.SecondPass(ctx);

      var sb = new StringBuilder();

      var varray = new ValArray();
      foreach (var (addr, type, vals) in ctx.Values)
      {
        var tsize = type.SizeBytes();
        varray.Mode = type.VMode();
        for (var i = 0; i < vals.Length; i += 8)
        {
          var count = 8;
          if (i + count > vals.Length)
            count = vals.Length - count;

          varray.Width = count;
          ctx.RawValues[new FixedRange(vals.Start + i, count)].CopyTo(varray.Values);

          target.Write(
            new ValuePointer { Address = addr + tsize * i, Type = type, Width = (byte)count },
            varray);
        }
        if (Debug)
        {
          sb.Clear();
          sb.Append("ASM ");
          foreach (var (label, laddr) in ctx.Labels)
            if (laddr == addr)
              sb.Append(label).Append(": ");
          sb.Append($"{addr}: {type}");
          for (var i = vals.Start; i < vals.Length; i++)
            sb.Append($" {ctx.RawValues[i].As(type)}");
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

    public readonly AppendBuffer<Value> StringChars = new();
    public readonly AppendBuffer<FixedRange> InlineStrings = new();

    public readonly Dictionary<string, int> Labels = [];

    public readonly AppendBuffer<Value> RawValues = new();
    public readonly AppendBuffer<(int, DataType, FixedRange)> Values = new();

    public int Addr = 0;

    // On first pass, stores the current length of all inline strings
    // On second pass, stores the start memory address of all inline strings
    public int InlineStringStart = 0;

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

    public bool TryAddString(Token tok, out FixedRange range)
    {
      var start = StringChars.Length;
      Span<Value> chunk = stackalloc Value[256];

      var parser = new Token.StringParser(Buffer[tok]);
      while (!parser.Done)
      {
        if (!parser.NextChunk(chunk, out var count))
        {
          StringChars.Length = start;
          range = default;
          return false;
        }
      }

      range = new(start, StringChars.Length - start);
      return true;
    }

    // add InlineStringStart to Start after first pass to get actual memory range
    public FixedRange AddInlineString(FixedRange str)
    {
      InlineStrings.Add(str);
      var res = new FixedRange(InlineStringStart, str.Length);
      InlineStringStart = res.End;
      return res;
    }

    private static bool TryAppendString(AppendBuffer<Value> buf, ReadOnlySpan<char> data, out FixedRange range)
    {
      var start = buf.Length;
      Span<Value> chunk = stackalloc Value[256];

      var parser = new Token.StringParser(data);
      while (!parser.Done)
      {
        if (!parser.NextChunk(chunk, out var count))
        {
          buf.Length = start;
          range = default;
          return false;
        }
      }

      range = new(start, buf.Length - start);
      return true;
    }

    public void EmitInlineStrings()
    {
      if (!Labels.TryGetValue("STRINGS", out InlineStringStart))
        InlineStringStart = Labels["STRINGS"] = Assembler.DEFAULT_STRINGS_START;

      Addr = InlineStringStart;
      using var emitter = Emitter(DataType.U8);

      foreach (var str in InlineStrings)
        emitter.EmitString(str);
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

      public readonly void EmitRange(ReadOnlySpan<Value> vals)
      {
        ctx.RawValues.AddRange(vals);
        ctx.Addr += valSize * vals.Length;
      }

      public readonly void EmitString(FixedRange range)
      {
        foreach (var chunk in ctx.StringChars.Chunks(range))
          EmitRange(chunk);
      }

      readonly void IDisposable.Dispose() =>
        ctx.Values.Add((addr, type, new(start, ctx.RawValues.Length - start)));
    }
  }
}