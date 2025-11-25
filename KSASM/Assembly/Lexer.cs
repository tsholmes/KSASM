
using System.Collections.Generic;

namespace KSASM.Assembly
{
  public class Lexer
  {
    public static TokenSource LexTokens(SourceString source)
    {
      var tokens = new List<SToken>();

      var lexer = new Lexer(source);
      while (lexer.Next(out var token))
        tokens.Add(token);

      if (tokens.Count == 0 || tokens[^1].Type != TokenType.EOL)
        tokens.Add(new(TokenType.EOL, ^0..^0));

      return new(source, tokens);
    }

    private readonly SourceString source;
    private int index = 0;

    private Lexer(SourceString source)
    {
      this.source = source;
    }

    private bool Next(out SToken token)
    {
      SkipWS();

      if (index == source.Length)
      {
        token = default;
        return false;
      }

      var c = At(index);

      return c switch
      {
        '\n' => TakeNext(TokenType.EOL, 1, out token),
        '[' => TakeNext(TokenType.IOpen, 1, out token),
        ']' => TakeNext(TokenType.IClose, 1, out token),
        '+' or '-' => TakeNext(TokenType.Offset, 1, out token),
        ',' => TakeNext(TokenType.Comma, 1, out token),
        '$' when At(index + 1) == '(' => TakeNext(TokenType.COpen, 2, out token),
        '$' when At(index + 1) == '[' => TakeNext(TokenType.CIOpen, 2, out token),
        '(' => TakeNext(TokenType.POpen, 1, out token),
        ')' => TakeNext(TokenType.PClose, 1, out token),
        '{' => TakeNext(TokenType.BOpen, 1, out token),
        '}' => TakeNext(TokenType.BClose, 1, out token),
        '/' => TakeNext(TokenType.Div, 1, out token),
        _ when IsWordStart(c) => TakeWordLike(out token),
        '@' => TakePosition(out token),
        '*' => TakeWidth(out token),
        ':' => TakeType(out token),
        _ when IsDigit(c) => TakeNumber(out token),
        '.' => TakeMacro(out token),
        '"' => TakeString(out token),
        _ => TakeNext(TokenType.Invalid, 1, out token),
      };
    }

    private void SkipWS()
    {
      while (index < source.Length)
      {
        var c = At(index);
        if (c == '\\' && At(index + 1) == '\n')
        {
          index += 2;
          continue;
        }
        else if (c == '\\' && At(index + 1) == '\r' && At(index + 2) == '\n')
        {
          index += 3;
          continue;
        }
        if (c == '\n' || !char.IsWhiteSpace(c))
          break;
        index++;
      }
      if (At(index) != '#')
        return;
      while (index < source.Length && At(index) != '\n')
        index++;
    }

    private bool TakeNext(TokenType type, int len, out SToken token)
    {
      token = new() { Type = type, Range = index..(index + len) };
      index += len;
      return true;
    }

    private bool TakeWordLike(out SToken token)
    {
      var len = 1;
      while (IsWordChar(At(index + len)))
        len++;

      if (len == 1 && At(index) == '_')
        return TakeNext(TokenType.Placeholder, 1, out token);
      else if (At(index + len) == ':' && IsBoundary(At(index + len + 1)))
        return TakeNext(TokenType.Label, len + 1, out token);
      else
        return TakeNext(TokenType.Word, len, out token);
    }

    private bool TakeMacro(out SToken token)
    {
      var len = 1;
      while (IsWordChar(At(index + len)))
        len++;

      if (len == 1)
        return TakeNext(TokenType.Invalid, 1, out token);
      else
        return TakeNext(TokenType.Macro, len, out token);
    }

    private bool TakeString(out SToken token)
    {
      var len = 1;
      while (index + len < source.Length && At(index + len) is not '"' and not '\n')
        len++;

      len++;

      if (At(index + len - 1) != '"')
        return TakeNext(TokenType.Invalid, len, out token);
      else
        return TakeNext(TokenType.String, len, out token);
    }

    private bool TakePosition(out SToken token)
    {
      var len = 1;
      // TODO: support hex positions?
      while (IsDigit(At(index + len)))
        len++;

      if (len == 1)
        return TakeNext(TokenType.Invalid, 1, out token);
      else
        return TakeNext(TokenType.Position, len, out token);
    }

    private bool TakeWidth(out SToken token)
    {
      var len = 1;
      while (IsDigit(At(index + len)))
        len++;

      if (len == 1)
        return TakeNext(TokenType.Mult, 1, out token);
      else
        return TakeNext(TokenType.Width, len, out token);
    }

    private bool TakeType(out SToken token)
    {
      var len = 1;
      while (IsWordChar(At(index + len)))
        len++;

      if (len == 1)
        return TakeNext(TokenType.Invalid, 1, out token);
      else
        return TakeNext(TokenType.Type, len, out token);
    }

    private bool TakeNumber(out SToken token)
    {
      var len = 1;
      var minLen = 1;
      if ((At(index), At(index + 1)) is ('0', 'x') or ('0', 'X'))
      {
        // hex
        len = 2;
        minLen = 3;
        while (char.IsAsciiHexDigit(At(index + len)))
          len++;
      }
      else if ((At(index), At(index + 1)) is ('0', 'b') or ('0', 'B'))
      {
        // binary
        len = 2;
        minLen = 3;
        while (At(index + len) is '0' or '1')
          len++;
      }
      else
      {
        // decimal

        while (IsDigit(At(index + len)))
          len++;

        if (At(index + len) is '.')
        {
          len++;
          minLen = len + 1;
          while (IsDigit(At(index + len)))
            len++;
        }

        if (At(index + len) is 'e' or 'E')
        {
          len++;
          if (At(index + len) is '+' or '-')
            len++;
          minLen = len + 1;
          while (IsDigit(At(index + len)))
            len++;
        }
      }
      if (len < minLen)
        return TakeNext(TokenType.Invalid, len, out token);
      else
        return TakeNext(TokenType.Number, len, out token);
    }

    private char At(int index)
    {
      if (index < 0 || index >= source.Length)
        return (char)0;
      return source[index];
    }

    private static bool IsWordStart(char c) => c switch
    {
      '_' => true,
      >= 'A' and <= 'Z' => true,
      >= 'a' and <= 'z' => true,
      _ => false,
    };

    private static bool IsDigit(char c) => char.IsAsciiDigit(c);

    private static bool IsWordChar(char c) => IsDigit(c) || IsWordStart(c) || c == '.';

    private static bool IsBoundary(char c) => char.IsWhiteSpace(c) || c == 0;
  }
}