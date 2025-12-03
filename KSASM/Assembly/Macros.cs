
using System;
using System.Collections.Generic;

namespace KSASM.Assembly
{
  public class MacroParser : TokenProcessor
  {
    public static bool DebugMacros = false;

    private readonly RefStack<ParseBuffer.TokenReader> readerStack = new();
    private readonly Stack<AppendBuffer<TokenIndex>> tempStack = new();
    private readonly Dictionary<string, MacroDef> macros = [];
    private readonly AppendBuffer<char> nsbuf = new();
    private readonly Stack<int> nslens = new();

    private int regionPos = 0x00100000;
    private int ifDepth = 0;
    private int eolCount = 0;

    private ref ParseBuffer.TokenReader Reader
    {
      get
      {
        while (true)
        {
          ref var top = ref readerStack.Top;
          if (readerStack.Length == 1 || !top.EOF)
            return ref top;
          readerStack.Pop();
        }
      }
    }

    public MacroParser(ParseBuffer buffer, SourceIndex source) : base(buffer)
    {
      readerStack.Push(buffer.SourceReader(source));
    }

    public bool Next(out Token token)
    {
      while (NextInner(out token))
      {
        if (token.Type == TokenType.EOL)
        {
          if (++eolCount > 2)
            continue;
        }
        else
          eolCount = 0;
        // if (Assembler.Debug)
        //   Console.WriteLine($"{token.Type} {ctx.StackPos(token)}");
        return true;
      }
      return false;
    }

    private bool NextInner(out Token token)
    {
      while (true)
      {
        if (!Reader.Take(out token))
          return false;

        if (token.Type == TokenType.Macro)
          ParseMacro(token, buffer[token][1..]);
        else if (token.Type == TokenType.BClose)
          EndIf(token);
        else
        {
          if (token.Type == TokenType.Label)
            token = AddNs(token, token.Index, out _);
          return true;
        }
      }
    }

    protected override bool Peek(out Token token) => Reader.Peek(out token);

    // fetches the next expanded token, throwing an error if it is not the expected type
    private Token NextInnerTyped(TokenType type)
    {
      if (!NextInner(out var token))
        throw Invalid();
      else if (token.Type != type)
        throw Invalid(token);
      return token;
    }

    private void PushSource(SourceIndex source) => readerStack.Push(buffer.SourceReader(source));

    private SynthSourcePush PushSynthSource(string name, TokenIndex producer) => new(this, name, producer);

    private void ParseMacro(Token token, ReadOnlySpan<char> name)
    {
      switch (name)
      {
        case "macro": MacroDefine(); break;
        case "unmacro": MacroUndefine(); break;
        case "import": MacroImport(token); break;
        case "region": MacroRegion(token); break;
        case "add": MacroAdd(token); break;
        case "ifdef": MacroIfDef(false); break;
        case "ifndef": MacroIfDef(true); break;
        case "if": MacroIfAny(); break;
        case "ns": MacroNs(); break;
        case "endns": MacroEndNs(token); break;
        case "addns": MacroAddNs(token); break;
        case "tomacro": MacroToMacro(token); break;
        case "concat": MacroConcat(token); break;
        case "label": MacroLabel(token); break;
        default: MacroExpand(name.ToString(), token); break;
      }
    }

    private Token AddNs(Token token, TokenIndex producer, out SourceIndex source, bool force = false)
    {
      if (nsbuf.Length == 0 && !force)
      {
        source = new(-1);
        return token;
      }
      using var s = buffer.NewSynthSource(".addns", producer);
      source = s.Source;
      var tdata = buffer[token];
      using (var t = s.MakeToken(token.Type, token.Index))
      {
        if (token.Type == TokenType.Macro)
        {
          t.AddData('.');
          tdata = tdata[1..];
        }
        t.AddData(nsbuf[..]);
        t.AddData(tdata);
      }
      return buffer[s.LastToken];
    }

    private void MacroNs()
    {
      // expand arg
      var wtoken = NextInnerTyped(TokenType.Word);

      var ns = buffer[wtoken];
      nsbuf.AddRange(ns);
      nslens.Push(ns.Length);
    }

    private void MacroEndNs(Token token)
    {
      if (nslens.Count == 0)
        throw Invalid(token);
      var len = nslens.Pop();
      nsbuf.Length -= len;
    }

    private void MacroAddNs(Token macro)
    {
      if (!Reader.TakeType(TokenType.POpen, out _))
        throw Invalid();

      // expand arg
      if (!NextInner(out var token))
        throw Invalid();

      if (token.Type is not TokenType.Word and not TokenType.Macro)
        throw Invalid(token);

      if (!Reader.TakeType(TokenType.PClose, out _))
        throw Invalid();

      AddNs(token, macro.Index, out var sourceIndex, force: true);

      PushSource(sourceIndex);
    }

    private void MacroToMacro(Token macro)
    {
      if (!Reader.TakeType(TokenType.POpen, out _))
        throw Invalid();

      // expand arg
      var token = NextInnerTyped(TokenType.Word);

      if (!Reader.TakeType(TokenType.PClose, out _))
        throw Invalid();

      using var s = PushSynthSource(".tomacro", macro.Index);
      using var t = s.S.MakeToken(TokenType.Macro, token.Index);
      t.AddData('.');
      t.AddData(buffer[token]);
    }

    private void MacroConcat(Token macro)
    {
      if (!Reader.TakeType(TokenType.POpen, out _))
        throw Invalid();

      if (!Reader.Take(out var token))
        throw Invalid();

      if (token.Type is not TokenType.Word and not TokenType.Macro and not TokenType.Placeholder)
        throw Invalid(token);

      var type = token.Type == TokenType.Placeholder ? TokenType.Word : token.Type;

      using var s = PushSynthSource(".concat", macro.Index);
      using var t = s.S.MakeToken(type, token.Index);
      t.AddData(buffer[token]);

      while (Reader.TakeType(TokenType.Word, out var itoken)
          || Reader.TakeType(TokenType.Macro, out itoken)
          || Reader.TakeType(TokenType.Placeholder, out itoken)
          || Reader.TakeType(TokenType.Number, out itoken))
      {
        if (itoken.Type == TokenType.Number && !int.TryParse(buffer[itoken], out _))
          throw Invalid(itoken);

        t.AddData(buffer[itoken]);
      }

      if (!Reader.TakeType(TokenType.PClose, out _))
        throw Invalid();
    }

    private void MacroLabel(Token macro)
    {
      if (!Reader.TakeType(TokenType.POpen, out _))
        throw Invalid();

      var token = NextInnerTyped(TokenType.Word);

      if (!Reader.TakeType(TokenType.PClose, out _))
        throw Invalid();

      using var s = PushSynthSource(".label", macro.Index);
      s.MakeLabel(buffer[token], token.Index);
    }

    private void MacroIfDef(bool not)
    {
      if (!Reader.TakeType(TokenType.Word, out var wtoken))
        throw Invalid();

      var name = buffer[wtoken].ToString();
      MacroDoIf(macros.ContainsKey(name) == not);
    }

    private void MacroIfAny()
    {
      if (!Reader.TakeType(TokenType.POpen, out _))
        throw Invalid();

      var any = false;
      var chunk = new ChunkReader { P = this, EndPClose = true, TrackP = true };
      while (chunk.Take(out _))
        any = true;
      if (!Reader.TakeType(TokenType.PClose, out _))
        throw Invalid();

      MacroDoIf(!any);
    }

    private void MacroDoIf(bool skip)
    {
      if (!Reader.TakeType(TokenType.BOpen, out _))
        throw Invalid();

      if (!skip)
      {
        ifDepth++;
        return;
      }

      var chunk = new ChunkReader { P = this, TrackB = true, EndBClose = true };

      while (chunk.Take(out _)) ;

      if (!Reader.TakeType(TokenType.BClose, out _))
        throw Invalid();
    }

    private void EndIf(Token token)
    {
      if (ifDepth <= 0)
        throw Invalid(token);
      ifDepth--;
    }

    private void MacroAdd(Token macro)
    {
      if (!Reader.TakeType(TokenType.POpen, out _))
        Invalid();

      if (!Reader.Take(out var first))
        Invalid();

      Value val;
      ValueMode mode;

      if (first.Type == TokenType.Width)
      {
        if (!Values.TryParseValue(buffer[first][1..], out val, out mode))
          Invalid(first);
      }
      else if (first.Type == TokenType.Number)
      {
        if (!Values.TryParseValue(buffer[first], out val, out mode))
          Invalid(first);
      }
      else
        throw Invalid(first);

      while (Reader.TakeType(TokenType.Comma, out _))
      {
        if (!Reader.TakeType(TokenType.Number, out var ntoken))
          Invalid();
        if (!Values.TryParseValue(buffer[ntoken], out var nval, out var nmode))
          Invalid(ntoken);
        nval.Convert(nmode, mode);
        mode.Ops().Add(ref val, nval);
      }

      if (!Reader.TakeType(TokenType.PClose, out _))
        Invalid();

      using var s = PushSynthSource(".add", macro.Index);
      if (first.Type == TokenType.Width)
        s.MakeWidth(val, mode, first.Index);
      else
        s.MakeNumber(val, mode, first.Index);
    }

    private void MacroRegion(Token macro)
    {
      var ntoken = NextInnerTyped(TokenType.Word);

      var endLabel = Reader.TakeType(TokenType.Offset, out var otoken) && buffer[otoken][0] == '-';

      if (!Reader.TakeType(TokenType.Number, out var sztoken))
        Invalid();

      if (!int.TryParse(buffer[sztoken], out var size))
        Invalid(sztoken);

      var startPos = regionPos - size;
      var endPos = regionPos;
      regionPos = startPos;

      using var s = PushSynthSource(".region", macro.Index);
      if (endLabel)
      {
        s.MakePosition(endPos, TokenIndex.Invalid);
        s.MakeLabel(buffer[ntoken], ntoken.Index);
        s.MakePosition(startPos, TokenIndex.Invalid);
      }
      else
      {
        s.MakePosition(startPos, TokenIndex.Invalid);
        s.MakeLabel(buffer[ntoken], ntoken.Index);
      }
    }

    private void MacroImport(Token macro)
    {
      if (!Reader.TakeType(TokenType.Word, out var ntoken))
        Invalid();

      var source = Library.LoadImport(buffer[ntoken].ToString());
      var sourceIndex = Lexer.LexSource(buffer, source, macro.Index);
      PushSource(sourceIndex);
    }

    private void MacroUndefine()
    {
      // expand macros for name
      var ntoken = NextInnerTyped(TokenType.Word);

      var tname = buffer[ntoken];
      Span<char> name = stackalloc char[tname.Length + nsbuf.Length];
      nsbuf[..].CopyTo(name);
      tname.CopyTo(name[nsbuf.Length..]);

      if (!macros.Remove(name.ToString()))
        throw Invalid(ntoken);
    }

    private void MacroDefine()
    {
      // expand macros for name
      var ntoken = NextInnerTyped(TokenType.Word);

      var tname = buffer[ntoken];
      Span<char> nbuf = stackalloc char[tname.Length + nsbuf.Length];
      nsbuf[..].CopyTo(nbuf);
      tname.CopyTo(nbuf[nsbuf.Length..]);
      var name = nbuf.ToString();

      var macro = new MacroDef { Name = name };

      if (Reader.TakeType(TokenType.POpen, out _) && !Reader.TakeType(TokenType.PClose, out _))
      {
        while (true)
        {
          if (Reader.TakeType(TokenType.Macro, out var mtoken))
          {
            var mstr = buffer[mtoken];
            if (!mstr.StartsWith("..."))
              throw Invalid(mtoken);
            macro.Args[mstr[3..].ToString()] = macro.Args.Count;
            macro.HasRest = true;
            if (!Reader.TakeType(TokenType.PClose, out _))
              throw Invalid();
            break;
          }
          if (!Reader.TakeType(TokenType.Word, out var atoken))
            Invalid();
          macro.Args[buffer[atoken].ToString()] = macro.Args.Count;
          if (Reader.TakeType(TokenType.Comma, out _))
            continue;
          else if (Reader.TakeType(TokenType.PClose, out _))
            break;
          else
            Invalid();
        }
      }

      if (macro.Args.Count > MacroDef.MAX_ARGS)
        throw new InvalidOperationException($"Too many macro args ${macro.Args.Count} > {MacroDef.MAX_ARGS}");

      using (var s = buffer.NewSynthSource($".macro {name}", ntoken.Index))
      {
        macro.Source = s.Source;

        var chunk = new ChunkReader { P = this };
        if (Reader.TakeType(TokenType.BOpen, out _))
          chunk.TrackB = chunk.EndBClose = true;
        else
          chunk.EndLine = true;

        while (chunk.Take(out var token))
          s.CopyToken(token.Index);

        if (chunk.TrackB && !Reader.TakeType(TokenType.BClose, out _))
          throw Invalid();
        else if (!chunk.TrackB && !Reader.TakeType(TokenType.EOL, out _))
          throw Invalid();
      }

      macros[macro.Name] = macro;
    }

    private void MacroExpand(string name, Token nameToken)
    {
      if (!macros.TryGetValue(name, out var macro) && !macros.TryGetValue($"{nsbuf[..]}{name}", out macro))
        throw Invalid(nameToken);

      Span<FixedRange> argRanges = stackalloc FixedRange[MacroDef.MAX_ARGS];
      var argCount = 0;

      if (Reader.TakeType(TokenType.POpen, out _))
      {
        using var s = buffer.NewSynthSource($".{macro.Name} args", nameToken.Index);

        var done = false;
        while (!done)
        {
          var isRest = macro.HasRest && argCount == macro.Args.Count - 1;
          var chunk = new ChunkReader { P = this, TrackP = true, TrackB = true };
          // don't consume brackets for rest
          if (!isRest && Reader.TakeType(TokenType.BOpen, out _))
            chunk.EndBClose = true;
          else
          {
            chunk.EndPClose = chunk.EndLine = true;
            if (!isRest)
              chunk.EndComma = true; // only end on comma if we aren't in the ...rest arg
          }

          var argStart = s.NextToken;

          while (chunk.Take(out var token))
            s.CopyToken(token.Index);

          var argEnd = s.NextToken;

          argRanges[argCount++] = new(argStart.Index, argEnd.Index - argStart.Index);

          if (chunk.EndBClose && !Reader.TakeType(TokenType.BClose, out _))
            throw Invalid();

          if (Reader.TakeType(TokenType.PClose, out _))
            done = true;
          else if (isRest)
            throw Invalid(); // if we had rest args, the last param should run all the way until )
          else if (!Reader.TakeType(TokenType.Comma, out _))
            throw Invalid();
        }
      }

      var rcount = readerStack.Length;

      SourceIndex source;
      using (var s = PushSynthSource($".{macro.Name} expand", nameToken.Index))
      {
        source = s.S.Source;
        foreach (var token in buffer.SourceTokens(macro.Source))
        {
          // TODO: make StringIndex that uses shared buffer with sorted list of ranges. binary search for index
          if (token.Type == TokenType.Word && macro.Args.TryGetValue(buffer[token].ToString(), out var argIdx))
          {
            if (argIdx >= argRanges.Length)
              continue;

            var range = argRanges[argIdx];
            foreach (var atoken in buffer.TokenRange(range))
              s.S.CopyToken(atoken.Index);
            continue;
          }
          s.S.CopyToken(token.Index);
        }
      }

      var anyImm = false;
      foreach (var token in buffer.SourceTokens(source))
      {
        if (token.Type != TokenType.Macro)
          continue;
        var str = buffer[token];
        if (str.Length > 2 && str.StartsWith(".."))
        {
          anyImm = true;
          break;
        }
      }

      if (!anyImm)
        return;

      // use a temporary buffer here to store the tokens, so temporary sources from expansion aren't embedded
      var expanded = tempStack.Count > 0 ? tempStack.Pop() : new();
      expanded.Clear();

      var startIfs = ifDepth;

      // consume tokens until the source we just added is completed
      // the EOF check should never be true, it just forces cleaning the reader stack
      while (!Reader.EOF && readerStack.Length > rcount)
      {
        if (!Reader.Take(out var token))
          throw Invalid();

        if (token.Type == TokenType.Macro)
        {
          var tstr = buffer[token];
          if (tstr.Length > 2 && tstr[1] == '.')
          {
            ParseMacro(token, buffer[token][2..]);
            continue;
          }
        }
        else if (token.Type is TokenType.BClose && ifDepth > startIfs)
        {
          // if we have active inner ifs, close them now instead of passing the tokens on
          ifDepth--;
          continue;
        }
        expanded.Add(token.Index);
      }

      using (var s = PushSynthSource($".{macro.Name} expand immediate", nameToken.Index))
      {
        foreach (var tokIdx in expanded)
          s.S.CopyToken(tokIdx);
      }

      expanded.Clear();
      tempStack.Push(expanded);
    }

    private readonly struct SynthSourcePush(MacroParser parser, string name, TokenIndex producer) : IDisposable
    {
      public readonly ParseBuffer.SyntheticSourceBuilder S = parser.buffer.NewSynthSource(name, producer);

      public TokenIndex MakeLabel(ReadOnlySpan<char> name, TokenIndex from)
      {
        using (var t = S.MakeToken(TokenType.Label, from))
        {
          t.AddData(name);
          t.AddData(':');
        }
        return S.LastToken;
      }

      public TokenIndex MakeNumber(Value val, ValueMode mode, TokenIndex from)
      {
        using (var t = S.MakeToken(TokenType.Number, from))
        {
          t.AddValue(val, mode);
        }
        return S.LastToken;
      }

      public TokenIndex MakeWidth(Value val, ValueMode mode, TokenIndex from)
      {
        using (var t = S.MakeToken(TokenType.Width, from))
        {
          val.Convert(mode, ValueMode.Unsigned);
          t.AddData('*');
          t.AddValue(val, ValueMode.Unsigned);
        }
        return S.LastToken;
      }

      public TokenIndex MakePosition(int address, TokenIndex from)
      {
        using (var t = S.MakeToken(TokenType.Position, from))
        {
          t.AddData('@');
          t.AddInt(address);
        }
        return S.LastToken;
      }

      public void Dispose()
      {
        S.Dispose();
        parser.PushSource(S.Source);
      }
    }

    private class MacroDef
    {
      public const int MAX_ARGS = 32;
      public string Name;
      public SourceIndex Source;
      public Dictionary<string, int> Args = [];
      public bool HasRest;
    }

    private struct ChunkReader
    {
      public MacroParser P;
      public bool EndComma;
      public bool EndPClose, TrackP;
      public bool EndBClose, TrackB;
      public bool EndLine;

      public int PDepth;
      public int BDepth;

      public bool Take(out Token token)
      {
        if (!IsInner)
        {
          if (EndComma && P.Reader.PeekType(TokenType.Comma, out token))
            return false;
          if (EndPClose && P.Reader.PeekType(TokenType.PClose, out token))
            return false;
          if (EndBClose && P.Reader.PeekType(TokenType.BClose, out token))
            return false;
          if (EndLine && P.Reader.PeekType(TokenType.EOL, out token))
            return false;
        }
        if (!P.Reader.Take(out token))
          throw P.Invalid();

        if (TrackP)
        {
          if (token.Type is TokenType.POpen or TokenType.COpen)
            PDepth++;
          else if (token.Type is TokenType.PClose && --PDepth < 0)
            throw P.Invalid(token);
        }
        if (TrackB)
        {
          if (token.Type is TokenType.BOpen)
            BDepth++;
          else if (token.Type is TokenType.BClose && --BDepth < 0)
            throw P.Invalid(token);
        }
        return true;
      }

      public bool IsInner => PDepth > 0 || BDepth > 0;
    }
  }
}