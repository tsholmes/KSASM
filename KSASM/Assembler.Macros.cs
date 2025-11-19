
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
        if (NextInner(out token))
        {
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

        if (token.Type is not TokenType.Word and not TokenType.Macro)
          throw Invalid(token);

        while (lexer.TakeType(TokenType.Word, out var itoken) || lexer.TakeType(TokenType.Macro, out itoken))
          token = token with { OverrideStr = $"{token.Str()}{itoken.Str()}" };

        if (!lexer.TakeType(TokenType.PClose, out _))
          throw Invalid();

        PushTokens(token);
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

        var skipDepth = 1;
        while (skipDepth > 0)
        {
          if (lexer.TakeType(TokenType.Macro, out var mtoken))
          {
            skipDepth += mtoken.Str()[1..] switch
            {
              "ifdef" or "ifndef" or "if" => 1,
              "endif" => -1,
              _ => 0,
            };
          }
          else if (!lexer.Take(out _))
            throw Invalid();
        }
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

        if (lexer.TakeType(TokenType.BOpen, out _))
        {
          // take tokens until we find unmatched }
          var depth = 0;
          while (true)
          {
            if (!lexer.Take(out var token))
              throw Invalid();
            if (token.Type == TokenType.BOpen)
              depth++;
            else if (token.Type == TokenType.BClose)
            {
              if (depth == 0)
                break;
              depth--;
            }
            macro.Tokens.Add(token);
          }
        }
        else
        {
          // take tokens until we find unescaped EOL
          while (lexer.Peek(out var token) && token.Type != TokenType.EOL)
          {
            if (!lexer.Take(out token))
              throw new InvalidOperationException();
            if (token.Type == TokenType.EscapedEOL)
              token.Type = TokenType.EOL;
            macro.Tokens.Add(token);
          }
        }

        macros[macro.Name] = macro;
      }

      private void MacroExpand(string name, Token nameToken)
      {
        if (!macros.TryGetValue(name, out var macro))
          Invalid(nameToken);

        var args = new List<List<Token>>();

        if (lexer.TakeType(TokenType.POpen, out _))
        {
          var done = false;
          while (!done)
          {
            var arg = new List<Token>();
            var pdepth = 0;
            while (lexer.Take(out var token))
            {
              if (token.Type == TokenType.Comma)
                break;
              else if (token.Type is TokenType.POpen or TokenType.COpen)
                pdepth++;
              else if (token.Type == TokenType.PClose)
              {
                pdepth--;
                if (pdepth < 0)
                {
                  done = true;
                  break;
                }
              }
              else if (token.Type == TokenType.EscapedEOL)
                token.Type = TokenType.EOL;
              arg.Add(token);
            }
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
          else if (token.Type == TokenType.EscapedEOL)
            token.Type = TokenType.EOL;
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
    }

    public class ListTokenStream : ITokenStream
    {
      private readonly List<Token> tokens;
      private int index = 0;

      public ListTokenStream(List<Token> tokens)
      {
        this.tokens = tokens;
      }

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