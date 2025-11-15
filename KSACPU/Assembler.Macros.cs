
using System;
using System.Collections.Generic;

namespace KSACPU
{
  public partial class Assembler
  {
    public class MacroParser : ITokenStream
    {
      private readonly SourceString source;
      private readonly LexerReader baseLexer;
      private readonly List<LexerReader> macroStack = [];

      private readonly Dictionary<string, MacroDef> macros = new();

      public MacroParser(SourceString source, ITokenStream stream)
      {
        this.source = source;
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
            if (Debug)
              Console.WriteLine($"{token.Type} '{token.Str()}' at {token.PosStr()}");
            return true;
          }

          ParseMacro(token);
        }
      }

      private void Invalid()
      {
        lexer.Peek(out var token);
        Invalid(token);
      }

      private void Invalid(Token token) => throw new InvalidOperationException(
        $"Invalid token {token.Type} '{token.Str()}' at {token.PosStr()}");

      private void ParseMacro(Token token)
      {
        var name = token.Str()[1..];
        switch (name.ToLowerInvariant())
        {
          case "macro": DefineMacro(); break;
          case "import": RunImport(); break;
          default: ExpandMacro(name, token); break;
        }
      }

      private void RunImport()
      {
        if (!lexer.TakeType(TokenType.Word, out var ntoken))
          Invalid();

        var source = Library.LoadImport(ntoken.Str());
        macroStack.Add(new(new Lexer(source)));
      }

      private void DefineMacro()
      {
        if (!lexer.TakeType(TokenType.Word, out var ntoken))
          Invalid();

        var macro = new MacroDef { Name = ntoken.Str() };

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

      private void ExpandMacro(string name, Token nameToken)
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