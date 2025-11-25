
using System;
using System.Collections.Generic;
using System.Text;

namespace KSASM.Assembly
{
  public class MacroParser : TokenProcessor, ITokenStream
  {
    public static bool DebugMacros = false;

    private readonly LexerReader baseLexer;
    private readonly Stack<(LexerReader, List<Token>)> macroStack = [];
    private readonly ListPool<Token> tokenListPool = new();
    private readonly ListPool<Range> rangeListPool = new();
    private readonly List<string> nsStack = [];
    private readonly Dictionary<string, MacroDef> macros = [];

    private int regionPos = 0x00100000;
    private int ifDepth = 0;
    private string curNs = "";

    private int eolCount = 0;

    public MacroParser(ITokenStream stream, Context ctx) : base(ctx)
    {
      this.baseLexer = new(stream, -1);
    }

    private LexerReader lexer
    {
      get
      {
        (LexerReader lexer, List<Token> tokens) macro;
        while (macroStack.TryPeek(out macro) && macro.lexer.EOF())
        {
          macro = macroStack.Pop();
          tokenListPool.Return(macro.tokens);
        }
        if (macroStack.TryPeek(out macro))
          return macro.lexer;
        return baseLexer;
      }
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
        if (Assembler.Debug)
          Console.WriteLine($"{token.Type} {ctx.StackPos(token)}");
        return true;
      }
      return false;
    }

    private bool NextInner(out Token token)
    {
      while (true)
      {
        if (!lexer.Take(out token))
          return false;

        if (token.Type == TokenType.Macro)
          ParseMacro(token);
        else if (token.Type == TokenType.BClose)
          EndIf(token);
        else
        {
          if (token.Type == TokenType.Label && curNs.Length > 0)
            AddNs(ref token);
          return true;
        }
      }
    }

    // fetches the next expanded token, throwing an error if it is not the expected type
    private Token NextInnerTyped(TokenType type)
    {
      if (!NextInner(out var token))
        throw Invalid();
      else if (token.Type != type)
        throw Invalid(token);
      return token;
    }

    private void PushLexer(LexerReader lexer, List<Token> tokens) =>
      macroStack.Push((lexer, tokens));

    private void PushTokens(Token parent, List<Token> tokens)
    {
      PushLexer(new(new ListTokenStream(tokens), ctx.AddFrame(parent)), tokens);
      if (DebugMacros && parent.Span() != ".import")
      {
        Console.WriteLine($">>> PUSH {parent.Str()}");
        foreach (var token in tokens)
          Console.WriteLine($"    {token.Type} {ctx.StackPos(token)}");
        Console.WriteLine("<<<");
      }
    }

    private void PushTokens(Token parent, params Span<Token> tokens) =>
      PushTokens(parent, tokenListPool.Take(tokens));

    private readonly StringBuilder nsb = new();
    private void AddNs(ref Token token)
    {
      nsb.Clear();
      if (token.Type == TokenType.Macro)
      {
        nsb.Append('.');
        nsb.Append(curNs);
        nsb.Append(token[1..]);
      }
      else
      {
        nsb.Append(curNs);
        nsb.Append(token.Span());
      }
      token.OverrideStr = nsb.ToString();
    }

    private void ParseMacro(Token token)
    {
      var name = token[1..];
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

    private void MacroNs()
    {
      // expand arg
      var wtoken = NextInnerTyped(TokenType.Word);

      nsStack.Add(wtoken.Str());

      curNs = string.Join("", nsStack);
    }

    private void MacroEndNs(Token token)
    {
      if (nsStack.Count == 0)
        throw Invalid(token);
      nsStack.RemoveAt(nsStack.Count - 1);

      curNs = string.Join("", nsStack);
    }

    private void MacroAddNs(Token macro)
    {
      if (!lexer.TakeType(TokenType.POpen, out _))
        throw Invalid();

      // expand arg
      if (!NextInner(out var token))
        throw Invalid();

      if (token.Type == TokenType.Word || token.Type == TokenType.Macro)
        AddNs(ref token);
      else
        throw Invalid(token);

      if (!lexer.TakeType(TokenType.PClose, out _))
        throw Invalid();

      PushTokens(macro, token);
    }

    private void MacroToMacro(Token macro)
    {
      if (!lexer.TakeType(TokenType.POpen, out _))
        throw Invalid();

      // expand arg
      var token = NextInnerTyped(TokenType.Word);

      if (!lexer.TakeType(TokenType.PClose, out _))
        throw Invalid();

      PushTokens(macro, token with { Type = TokenType.Macro, OverrideStr = $".{token.Span()}" });
    }

    private void MacroConcat(Token macro)
    {
      if (!lexer.TakeType(TokenType.POpen, out _))
        throw Invalid();

      if (!lexer.Take(out var token))
        throw Invalid();

      if (token.Type is not TokenType.Word and not TokenType.Macro and not TokenType.Placeholder)
        throw Invalid(token);

      while (lexer.TakeType(TokenType.Word, out var itoken)
          || lexer.TakeType(TokenType.Macro, out itoken)
          || lexer.TakeType(TokenType.Placeholder, out itoken)
          || (lexer.TakeType(TokenType.Number, out itoken) && int.TryParse(itoken.Span(), out _)))
        token = token with { OverrideStr = $"{token.Span()}{itoken.Span()}" };

      if (!lexer.TakeType(TokenType.PClose, out _))
        throw Invalid();

      if (token.Type == TokenType.Placeholder && token.Span().Length > 1)
        token.Type = TokenType.Word;

      PushTokens(macro, token);
    }

    private void MacroLabel(Token macro)
    {
      if (!lexer.TakeType(TokenType.POpen, out _))
        throw Invalid();

      var token = NextInnerTyped(TokenType.Word);

      if (!lexer.TakeType(TokenType.PClose, out _))
        throw Invalid();

      PushTokens(macro, token with { Type = TokenType.Label, OverrideStr = $"{token.Span()}:" });
    }

    private void MacroIfDef(bool not)
    {
      if (!lexer.TakeType(TokenType.Word, out var wtoken))
        throw Invalid();

      var name = wtoken.Str();
      MacroDoIf(macros.ContainsKey(name) == not);
    }

    private void MacroIfAny()
    {
      if (!lexer.TakeType(TokenType.POpen, out _))
        throw Invalid();

      var any = false;
      var chunk = new ChunkReader { P = this, EndPClose = true, TrackP = true };
      while (chunk.Take(out _))
        any = true;
      if (!lexer.TakeType(TokenType.PClose, out _))
        throw Invalid();

      MacroDoIf(!any);
    }

    private void MacroDoIf(bool skip)
    {
      if (!lexer.TakeType(TokenType.BOpen, out _))
        throw Invalid();

      if (!skip)
      {
        ifDepth++;
        return;
      }

      var chunk = new ChunkReader { P = this, TrackB = true, EndBClose = true };

      while (chunk.Take(out _)) ;

      if (!lexer.TakeType(TokenType.BClose, out _))
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
      if (!lexer.TakeType(TokenType.POpen, out _))
        Invalid();

      if (!lexer.Take(out var first))
        Invalid();

      Value val;
      ValueMode mode;

      if (first.Type == TokenType.Width)
      {
        if (!Values.TryParseValue(first[1..], out val, out mode))
          Invalid(first);
      }
      else if (first.Type == TokenType.Number)
      {
        if (!Values.TryParseValue(first, out val, out mode))
          Invalid(first);
      }
      else
        throw Invalid(first);

      while (lexer.TakeType(TokenType.Comma, out _))
      {
        if (!lexer.TakeType(TokenType.Number, out var ntoken))
          Invalid();
        if (!Values.TryParseValue(ntoken, out var nval, out var nmode))
          Invalid(ntoken);
        nval.Convert(nmode, mode);
        mode.Ops().Add(ref val, nval);
      }

      if (!lexer.TakeType(TokenType.PClose, out _))
        Invalid();

      PushTokens(
        macro,
        first with { OverrideStr = first.Type == TokenType.Width ? $"*{val.Get(mode)}" : $"{val.Get(mode)}" }
      );
    }

    private void MacroRegion(Token macro)
    {
      var ntoken = NextInnerTyped(TokenType.Word);

      var name = ntoken.Span();

      var endLabel = lexer.TakeType(TokenType.Offset, out var otoken) && otoken.Str() == "-";

      if (!lexer.TakeType(TokenType.Number, out var sztoken))
        Invalid();

      if (!int.TryParse(sztoken, out var size))
        Invalid(sztoken);

      var startPos = regionPos - size;
      var endPos = regionPos;
      regionPos = startPos;

      if (endLabel)
        PushTokens(
          macro,
          ntoken with { Type = TokenType.Position, OverrideStr = $"@{endPos}" },
          ntoken with { Type = TokenType.Label, OverrideStr = $"{name}:" },
          ntoken with { Type = TokenType.Position, OverrideStr = $"@{startPos}" }
        );
      else
        PushTokens(
          macro,
          ntoken with { Type = TokenType.Position, OverrideStr = $"@{startPos}" },
          ntoken with { Type = TokenType.Label, OverrideStr = $"{name}:" }
        );
    }

    private void MacroImport(Token macro)
    {
      if (!lexer.TakeType(TokenType.Word, out var ntoken))
        Invalid();

      var source = Library.LoadImport(ntoken.Str());
      PushLexer(new(Lexer.LexTokens(source).AsStream(), ctx.AddFrame(macro)), null);
    }

    private void MacroUndefine()
    {
      // expand macros for name
      var ntoken = NextInnerTyped(TokenType.Word);

      var name = curNs + ntoken.Str();
      if (!macros.Remove(name))
        throw Invalid(ntoken);
    }

    private void MacroDefine()
    {
      // expand macros for name
      var ntoken = NextInnerTyped(TokenType.Word);

      var macro = new MacroDef { Name = curNs + ntoken.Str() };

      if (lexer.TakeType(TokenType.POpen, out _) && !lexer.TakeType(TokenType.PClose, out _))
      {
        while (true)
        {
          if (lexer.TakeType(TokenType.Macro, out var mtoken))
          {
            var mstr = mtoken.Span();
            if (!mstr.StartsWith("..."))
              throw Invalid(mtoken);
            macro.Args[mstr[3..].ToString()] = macro.Args.Count;
            macro.HasRest = true;
            if (!lexer.TakeType(TokenType.PClose, out _))
              throw Invalid();
            break;
          }
          if (!lexer.TakeType(TokenType.Word, out var atoken))
            Invalid();
          macro.Args[atoken.Str()] = macro.Args.Count;
          if (lexer.TakeType(TokenType.Comma, out _))
            continue;
          else if (lexer.TakeType(TokenType.PClose, out _))
            break;
          else
            Invalid();
        }
      }

      var chunk = new ChunkReader { P = this };
      if (lexer.TakeType(TokenType.BOpen, out _))
        chunk.TrackB = chunk.EndBClose = true;
      else
        chunk.EndLine = true;

      while (chunk.Take(out var token))
        macro.Tokens.Add(token);

      if (chunk.TrackB && !lexer.TakeType(TokenType.BClose, out _))
        throw Invalid();
      else if (!chunk.TrackB && !lexer.TakeType(TokenType.EOL, out _))
        throw Invalid();

      macros[macro.Name] = macro;
    }

    private void MacroExpand(string name, Token nameToken)
    {
      if (!macros.TryGetValue(name, out var macro) && !macros.TryGetValue(curNs + name, out macro))
        Invalid(nameToken);

      using var tokenLease = tokenListPool.Lease();
      using var rangeLease = rangeListPool.Lease();

      var argTokens = tokenLease.List;
      var argRanges = rangeLease.List;
      var argStart = 0;

      if (lexer.TakeType(TokenType.POpen, out _))
      {
        var done = false;
        while (!done)
        {
          var isRest = macro.HasRest && argRanges.Count == macro.Args.Count - 1;
          var chunk = new ChunkReader { P = this, TrackP = true, TrackB = true };
          // don't consume brackets for rest
          if (!isRest && lexer.TakeType(TokenType.BOpen, out _))
            chunk.EndBClose = true;
          else
          {
            chunk.EndPClose = chunk.EndLine = true;
            if (!macro.HasRest || argRanges.Count < macro.Args.Count - 1)
              chunk.EndComma = true; // only end on comma if we aren't in the ...rest arg
          }

          while (chunk.Take(out var token))
            argTokens.Add(token);

          argRanges.Add(argStart..argTokens.Count);
          argStart = argTokens.Count;

          if (chunk.EndBClose && !lexer.TakeType(TokenType.BClose, out _))
            throw Invalid();

          if (lexer.TakeType(TokenType.PClose, out _))
            done = true;
          else if (isRest)
            throw Invalid(); // if we had rest args, the last param should run all the way until )
          else if (!lexer.TakeType(TokenType.Comma, out _))
            throw Invalid();
        }
      }

      var expanded = tokenListPool.Take();
      foreach (var tk in macro.Tokens)
      {
        var token = tk;
        if (token.Type == TokenType.Word && macro.Args.TryGetValue(token.Str(), out var argIdx))
        {
          if (argIdx >= argRanges.Count)
            continue;

          var range = argRanges[argIdx];
          for (var i = range.Start.Value; i < range.End.Value; i++)
            expanded.AddRange(argTokens[i]);
          continue;
        }
        expanded.Add(token);
      }

      PushExpanded(nameToken, expanded);
    }

    private void PushExpanded(Token macro, List<Token> tokens)
    {
      var prevLexer = lexer;
      PushTokens(macro, tokens);

      var any = false;
      foreach (var tok in tokens)
      {
        if (tok.Type != TokenType.Macro)
          continue;
        var str = tok.Span();
        if (str.Length > 2 && str[1] == '.')
        {
          any = true;
          break;
        }
      }
      if (!any)
        return;

      var startIfs = ifDepth;

      var expanded = tokenListPool.Take();
      while (lexer != prevLexer)
      {
        if (!lexer.Take(out var token))
          throw Invalid();

        if (token.Type == TokenType.Macro)
        {
          var tstr = token.Span();
          if (tstr.Length > 2 && tstr[1] == '.')
          {
            ParseMacro(token with { OverrideStr = tstr[1..].ToString() });
            continue;
          }
        }
        else if (token.Type is TokenType.BClose && ifDepth > startIfs)
        {
          // if we have active inner ifs, close them now instead of passing the tokens on
          ifDepth--;
          continue;
        }
        expanded.Add(token);
      }
      PushTokens(macro, expanded);
    }

    protected override bool Peek(out Token token) => lexer.Peek(out token);

    private class MacroDef
    {
      public string Name;
      public Dictionary<string, int> Args = [];
      public bool HasRest;
      public List<Token> Tokens = [];
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
          if (EndComma && P.lexer.PeekType(TokenType.Comma, out token))
            return false;
          if (EndPClose && P.lexer.PeekType(TokenType.PClose, out token))
            return false;
          if (EndBClose && P.lexer.PeekType(TokenType.BClose, out token))
            return false;
          if (EndLine && P.lexer.PeekType(TokenType.EOL, out token))
            return false;
        }
        if (!P.lexer.Take(out token))
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

  public class ListTokenStream(List<Token> tokens) : ITokenStream
  {
    private int index = 0;

    public bool Next(out Token token)
    {
      if (index >= tokens.Count)
      {
        token = default;
        return false;
      }
      token = tokens[index++];
      return true;
    }
  }
}