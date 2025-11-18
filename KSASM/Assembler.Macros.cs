
using System;
using System.Collections.Generic;
using System.Linq;

namespace KSASM
{
  public partial class Assembler
  {
    public class MacroParser : ITokenStream
    {
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
        while (true)
        {
          if (!lexer.Take(out token))
            return false;

          if (token.Type != TokenType.Macro)
          {
            if (token.Type == TokenType.Label && curNs.Length > 0)
              token.OverrideStr = curNs + token.Str();
            if (Debug)
              Console.WriteLine($"{token.Type} '{token.Str()}' at {token.PosStr()}");
            return true;
          }

          ParseMacro(token);
        }
      }

      private void PushTokens(params Token[] tokens) =>
        macroStack.Add(new(new ListTokenStream(tokens.ToList())));

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
          case "import": MacroImport(); break;
          case "region": MacroRegion(); break;
          case "add": MacroAdd(); break;
          case "ifdef": MacroIf(false); break;
          case "ifndef": MacroIf(true); break;
          case "endif": MacroEndIf(token); break;
          case "ns": MacroNs(); break;
          case "endns": MacroEndNs(token); break;
          default: MacroExpand(name, token); break;
        }
      }

      private void MacroNs()
      {
        if (!lexer.TakeType(TokenType.Word, out var wtoken))
          throw Invalid();
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

      private void MacroIf(bool not)
      {
        if (!lexer.TakeType(TokenType.Word, out var wtoken))
          throw Invalid();

        var name = wtoken.Str();
        if (macros.ContainsKey(name) != not)
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
              "ifdef" or "ifndef" => 1,
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
          val.Add(nval, mode);
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

        var name = curNs + ntoken.Str();

        var endLabel = lexer.TakeType(TokenType.Offset, out var otoken) && otoken.Str() == "-";

        if (!lexer.TakeType(TokenType.Number, out var sztoken))
          Invalid();

        if (!int.TryParse(sztoken.Str(), out var size))
          Invalid(sztoken);

        var pos = endLabel ? regionPos : regionPos - size;
        regionPos -= size;

        PushTokens(
          ntoken with { Len = 0, Type = TokenType.Position, OverrideStr = $"@{pos}" },
          ntoken with { Len = 0, Type = TokenType.Label, OverrideStr = $"{name}:" }
        );
      }

      private void MacroImport()
      {
        if (!lexer.TakeType(TokenType.Word, out var ntoken))
          Invalid();

        var source = Library.LoadImport(ntoken.Str());
        macroStack.Add(new(new Lexer(source)));
      }

      private void MacroDefine()
      {
        if (!lexer.TakeType(TokenType.Word, out var ntoken))
          Invalid();

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

        while (lexer.Peek(out var token) && token.Type != TokenType.EOL)
        {
          if (!lexer.Take(out token))
            throw new InvalidOperationException();
          if (token.Type == TokenType.EscapedEOL)
            token.Type = TokenType.EOL;
          macro.Tokens.Add(token);
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
              else if (token.Type == TokenType.EOL)
                Invalid(token);
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

        macroStack.Add(new LexerReader(new ListTokenStream(expanded)));
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