
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
            count = vals.Length - i;

          varray.Width = count;
          ctx.RawValues[new FixedRange(vals.Start + i, count)].CopyTo(varray.Values);

          var vaddr = addr + tsize * i;
          target.Write(
            new ValuePointer { Address = vaddr, Type = type, Width = (byte)count },
            varray);

          ctx.Symbols.AddData(vaddr, type, count);
        }
        if (Debug)
        {
          sb.Clear();
          sb.Append("ASM ");
          foreach (var (label, laddr) in ctx.Labels)
            if (laddr == addr)
              sb.Append(label).Append(": ");
          sb.Append($"{addr:X6}: {type}");
          for (var i = vals.Start; i < vals.End; i++)
          {
            if (type == DataType.P24)
              sb.Append($" {ctx.RawValues[i].As(type):X6}");
            else
              sb.Append($" {ctx.RawValues[i].As(type)}");
          }
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

    public Exception Invalid(Token token) => throw Invalid(token, $"invalid token {token.Type} '{buffer[token]}'");

    public Exception Invalid(Token token, string msg) =>
      throw new InvalidOperationException($"{msg}\n{StackPos(token)}");

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
      if (Assembler.Debug)
      {
        var inst = Instruction.Decode(encoded);
        Console.WriteLine($"ASM INST {Addr:X6}: {inst.OpCode}*{inst.Width} {inst.AType} {inst.BType} {inst.CType}");
      }

      Symbols.AddInst(Addr, token.Index);
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
        StringChars.AddRange(chunk[..count]);
      }

      if (StringChars.Length == start)
      {
        range = default;
        return false;
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
      {
        InlineStringStart = Labels["STRINGS"] = Assembler.DEFAULT_STRINGS_START;
        Symbols.AddLabel("STRINGS", Assembler.DEFAULT_STRINGS_START);
      }

      Addr = InlineStringStart;
      using var emitter = Emitter(DataType.U8);

      foreach (var str in InlineStrings)
        emitter.EmitString(str);
    }

    private readonly Stack<Value> evalStack = new();
    public Value EvalExpr(int index, ValueMode mode)
    {
      var range = ConstRanges[index];
      if (Const.TryEvaluate(new ConstEval(this, ConstNodes[range]), mode, out var val) is Token err)
        throw Invalid(err);
      return val;
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

    private ref struct ConstEval(Context ctx, ReadOnlySpan<ExprNode> nodes) : IConstEvaluator
    {
      private readonly Context ctx = ctx;
      private ReadOnlySpan<ExprNode> nodes = nodes;

      public bool NextOp(out ConstOp op, out Token token)
      {
        if (nodes.Length == 0)
        {
          op = default;
          token = default;
          return false;
        }
        var node = nodes[0];
        nodes = nodes[1..];
        op = node.Op;
        token = ctx.Buffer[node.Token];
        return true;
      }

      public ReadOnlySpan<char> TokenData(Token token) => ctx.Buffer[token];

      public bool TryGetName(ReadOnlySpan<char> name, ValueMode mode, out Value val)
      {
        if (!ctx.Labels.TryGetValue(new(name), out var addr))
        {
          val = default;
          return false;
        }
        val = new() { Unsigned = (ulong)addr };
        val.Convert(ValueMode.Unsigned, mode);
        return true;
      }
    }
  }
}