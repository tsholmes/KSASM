
using System;
using System.Collections.Generic;
using System.Linq;

namespace KSASM
{
  public partial class Assembler
  {
    public class MacroParser : ITokenStream
    {
      public static bool DebugMacros = false;

      private readonly LexerReader baseLexer;
      private readonly List<LexerReader> macroStack = [];
      private readonly List<string> nsStack = [];

      private readonly Dictionary<string, MacroDef> macros = [];

      private int regionPos = 0x00100000;
      private int ifDepth = 0;
      private string curNs = "";

      public MacroParser(ITokenStream stream)
      {
        this.baseLexer = new(stream);
      }

      private LexerReader lexer
      {
        get
        {
          while (macroStack.Count > 0 && macroStack[^1].EOF())
            macroStack.RemoveAt(macroStack.Count - 1);
          if (macroStack.Count > 0)
            return macroStack[^1];
          return baseLexer;
        }
      }

      public bool Next(out Token token)
      {
        while (NextInner(out token))
        {
          if (token.Type == TokenType.EscapedEOL)
            continue;
          if (Debug)
            Console.WriteLine($"{token.Type} '{token.Str()}' at {token.PosStr()}");
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

          if (token.Type != TokenType.Macro)
          {
            if (token.Type == TokenType.Label && curNs.Length > 0)
              token.OverrideStr = curNs + token.Str();
            return true;
          }

          ParseMacro(token);
        }
      }

      private void PushTokens(params List<Token> tokens)
      {
        macroStack.Add(new(new ListTokenStream(tokens)));
        if (DebugMacros)
        {
          Console.WriteLine($">>> PUSH");
          foreach (var token in tokens)
            Console.WriteLine($"    {token.Type} '{token.Str()}'");
          Console.WriteLine("<<<");
        }
      }

      private Exception Invalid()
      {
        lexer.Peek(out var token);
        return Invalid(token);
      }

      private Exception Invalid(Token token) => throw new InvalidOperationException(
        $"Invalid token {token.Type} '{token.Str()}' at {token.PosStr()}");

      private void ParseMacro(Token token)
      {
        var name = token.Str()[1..];
        switch (name.ToLowerInvariant())
        {
          case "macro": MacroDefine(); break;
          case "unmacro": MacroUndefine(); break;
          case "import": MacroImport(); break;
          case "region": MacroRegion(); break;
          case "add": MacroAdd(); break;
          case "ifdef": MacroIf(false); break;
          case "ifndef": MacroIf(true); break;
          case "if": MacroIfAny(); break;
          case "endif": MacroEndIf(token); break;
          case "ns": MacroNs(); break;
          case "endns": MacroEndNs(token); break;
          case "addns": MacroAddNs(); break;
          case "tomacro": MacroToMacro(); break;
          case "concat": MacroConcat(); break;
          case "label": MacroLabel(); break;
          default: MacroExpand(name, token); break;
        }
      }

      private void MacroNs()
      {
        // expand arg
        if (!NextInner(out var wtoken))
          throw Invalid();
        else if (wtoken.Type != TokenType.Word)
          throw Invalid(wtoken);

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

      private void MacroAddNs()
      {
        if (!lexer.TakeType(TokenType.POpen, out _))
          throw Invalid();

        // expand arg
        if (!NextInner(out var token))
          throw Invalid();

        if (token.Type == TokenType.Word)
          token = token with { OverrideStr = curNs + token.Str() };
        else if (token.Type == TokenType.Macro)
          token = token with { OverrideStr = $".{curNs}{token.Str()[1..]}" };
        else
          throw Invalid(token);

        if (!lexer.TakeType(TokenType.PClose, out _))
          throw Invalid();

        PushTokens(token);
      }

      private void MacroToMacro()
      {
        if (!lexer.TakeType(TokenType.POpen, out _))
          throw Invalid();

        // expand arg
        if (!NextInner(out var token))
          throw Invalid();
        else if (token.Type != TokenType.Word)
          throw Invalid(token);

        if (!lexer.TakeType(TokenType.PClose, out _))
          throw Invalid();

        PushTokens(token with { Type = TokenType.Macro, OverrideStr = $".{token.Str()}" });
      }

      private void MacroConcat()
      {
        if (!lexer.TakeType(TokenType.POpen, out _))
          throw Invalid();

        if (!lexer.Take(out var token))
          throw Invalid();

        if (token.Type is not TokenType.Word and not TokenType.Macro and not TokenType.Placeholder)
          throw Invalid(token);

        while (lexer.TakeType(TokenType.Word, out var itoken)
            || lexer.TakeType(TokenType.Macro, out itoken)
            || lexer.TakeType(TokenType.Placeholder, out itoken))
          token = token with { OverrideStr = $"{token.Str()}{itoken.Str()}" };

        if (!lexer.TakeType(TokenType.PClose, out _))
          throw Invalid();

        if (token.Type == TokenType.Placeholder && token.Str().Length > 1)
          token.Type = TokenType.Word;

        PushTokens(token);
      }

      private void MacroLabel()
      {
        if (!lexer.TakeType(TokenType.POpen, out _))
          throw Invalid();

        if (!NextInner(out var token))
          throw Invalid();
        else if (token.Type != TokenType.Word)
          throw Invalid(token);

        if (!lexer.TakeType(TokenType.PClose, out _))
          throw Invalid();

        PushTokens(token with { Type = TokenType.Label, OverrideStr = $"{token.Str()}:" });
      }

      private void MacroIf(bool not)
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
        while (true)
        {
          if (!lexer.Take(out var token))
            throw Invalid();
          if (token.Type == TokenType.PClose)
            break;
          any = true;
        }

        MacroDoIf(!any);
      }

      private void MacroDoIf(bool skip)
      {
        if (!skip)
        {
          ifDepth++;
          return;
        }

        var chunk = new ChunkReader { P = this, TrackIf = true, EndIf = true };
        while (chunk.Take(out _)) ;
        if (!lexer.TakeType(TokenType.Macro, out var token))
          throw Invalid();
        else if (token.Str() != ".endif")
          throw Invalid(token);
      }

      private void MacroEndIf(Token token)
      {
        if (ifDepth <= 0)
          throw Invalid(token);
        ifDepth--;
      }

      private void MacroAdd()
      {
        if (!lexer.TakeType(TokenType.POpen, out _))
          Invalid();

        if (!lexer.Take(out var first))
          Invalid();

        Value val;
        ValueMode mode;

        if (first.Type == TokenType.Width)
        {
          if (!Parser.TryParseValue(first.Str()[1..], out val, out mode))
            Invalid(first);
        }
        else if (first.Type == TokenType.Number)
        {
          if (!Parser.TryParseValue(first.Str()[1..], out val, out mode))
            Invalid(first);
        }
        else
          throw Invalid(first);

        while (lexer.TakeType(TokenType.Comma, out _))
        {
          if (!lexer.TakeType(TokenType.Number, out var ntoken))
            Invalid();
          if (!Parser.TryParseValue(ntoken.Str(), out var nval, out var nmode))
            Invalid(ntoken);
          nval.Convert(nmode, mode);
          mode.Ops().Add(ref val, nval);
        }

        if (!lexer.TakeType(TokenType.PClose, out _))
          Invalid();

        PushTokens(
          first with { Len = 0, OverrideStr = first.Type == TokenType.Width ? $"*{val.Get(mode)}" : $"{val.Get(mode)}" }
        );
      }

      private void MacroRegion()
      {
        if (!lexer.TakeType(TokenType.Word, out var ntoken))
          Invalid();

        var name = ntoken.Str();

        var endLabel = lexer.TakeType(TokenType.Offset, out var otoken) && otoken.Str() == "-";

        if (!lexer.TakeType(TokenType.Number, out var sztoken))
          Invalid();

        if (!int.TryParse(sztoken.Str(), out var size))
          Invalid(sztoken);

        var startPos = regionPos - size;
        var endPos = regionPos;
        regionPos = startPos;

        if (endLabel)
          PushTokens(
            ntoken with { Type = TokenType.Position, OverrideStr = $"@{endPos}" },
            ntoken with { Type = TokenType.Label, OverrideStr = $"{name}:" },
            ntoken with { Type = TokenType.Position, OverrideStr = $"@{startPos}" }
          );
        else
          PushTokens(
            ntoken with { Type = TokenType.Position, OverrideStr = $"@{startPos}" },
            ntoken with { Type = TokenType.Label, OverrideStr = $"{name}:" }
          );
      }

      private void MacroImport()
      {
        if (!lexer.TakeType(TokenType.Word, out var ntoken))
          Invalid();

        var source = Library.LoadImport(ntoken.Str());
        macroStack.Add(new(new Lexer(source)));
      }

      private void MacroUndefine()
      {
        // expand macros for name
        if (!NextInner(out var ntoken))
          throw Invalid();
        else if (ntoken.Type != TokenType.Word)
          throw Invalid(ntoken);

        var name = curNs + ntoken.Str();
        if (!macros.Remove(name))
          throw Invalid(ntoken);
      }

      private void MacroDefine()
      {
        // expand macros for name
        if (!NextInner(out var ntoken))
          throw Invalid();
        else if (ntoken.Type != TokenType.Word)
          throw Invalid(ntoken);

        var macro = new MacroDef { Name = curNs + ntoken.Str() };

        if (lexer.TakeType(TokenType.POpen, out _))
        {
          while (true)
          {
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

        var args = new List<List<Token>>();

        if (lexer.TakeType(TokenType.POpen, out _))
        {
          var done = false;
          while (!done)
          {
            var arg = new List<Token>();
            var chunk = new ChunkReader { P = this, TrackP = true, TrackB = true };
            if (lexer.TakeType(TokenType.BOpen, out _))
              chunk.EndBClose = true;
            else
              chunk.EndComma = chunk.EndPClose = chunk.EndLine = true;

            while (chunk.Take(out var token))
              arg.Add(token);

            if (chunk.EndBClose && !lexer.TakeType(TokenType.BClose, out _))
              throw Invalid();

            if (lexer.TakeType(TokenType.PClose, out _))
              done = true;
            else if (!lexer.TakeType(TokenType.Comma, out _))
              throw Invalid();
            args.Add(arg);
          }
        }

        var expanded = new List<Token>();
        foreach (var tk in macro.Tokens)
        {
          var token = tk;
          if (token.Type == TokenType.Word && macro.Args.TryGetValue(token.Str(), out var argIdx))
          {
            if (argIdx >= args.Count)
              continue;
            expanded.AddRange(args[argIdx]);
            continue;
          }
          expanded.Add(token);
        }

        PushExpanded(expanded);
      }

      private void PushExpanded(List<Token> tokens)
      {
        var prevLexer = lexer;
        PushTokens(tokens);

        var expanded = new List<Token>();
        while (lexer != prevLexer)
        {
          if (!lexer.Take(out var token))
            throw Invalid();

          if (token.Type == TokenType.Macro)
          {
            var tstr = token.Str();
            if (tstr.Length > 2 && tstr[1] == '.')
            {
              ParseMacro(token with { OverrideStr = tstr[1..] });
              continue;
            }
          }
          expanded.Add(token);
        }
        PushTokens(expanded);
      }

      private class MacroDef
      {
        public string Name;
        public Dictionary<string, int> Args = [];
        public List<Token> Tokens = [];
      }

      private struct ChunkReader
      {
        public MacroParser P;
        public bool EndComma;
        public bool EndPClose, TrackP;
        public bool EndBClose, TrackB;
        public bool EndIf, TrackIf;
        public bool EndLine;

        public int IfDepth;
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
            if (EndIf && P.lexer.PeekType(TokenType.Macro, out token) && token.Str() is ".endif")
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
          if (TrackIf)
          {
            if (token.Type is TokenType.Macro && token.Str() is ".if" or ".ifdef" or ".ifndef")
              IfDepth++;
            else if (token.Type is TokenType.Macro && token.Str() is ".endif" && --IfDepth < 0)
              throw P.Invalid(token);
          }
          return true;
        }

        public bool IsInner => IfDepth > 0 || PDepth > 0 || BDepth > 0;
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
}