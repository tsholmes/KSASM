
using System;

namespace KSASM.Assembly
{
  public static class Lexer
  {
    public static SourceIndex LexSource(ParseBuffer buffer, SourceString source, TokenIndex producer)
    {
      SourceIndex sourceIndex;

      var src = source.Source.AsSpan();
      var index = 0;

      using (var s = buffer.NewRawSource(source.Name, src, producer))
      {
        sourceIndex = s.Source;
        while (src.Length > 0)
        {
          var ws = SkipWS(src);
          src = src[ws..];
          index += ws;
          if (src.Length == 0)
            break;

          var (type, length) = Next(src);
          src = src[length..];
          s.AddToken(type, new(index, length));
          index += length;
        }
        s.AddToken(TokenType.EOL, new(index, 0));
      }

      return sourceIndex;
    }

    private static char At(ReadOnlySpan<char> src, int index)
    {
      if (index >= src.Length)
        return default;
      return src[index];
    }

    private static int SkipWS(ReadOnlySpan<char> src)
    {
      var index = 0;
      var len = src.Length;
      while (index < len)
      {
        var c = At(src, index);
        if (c == '\\' && At(src, index + 1) == '\n')
        {
          index += 2;
          continue;
        }
        else if (c == '\\' && At(src, index + 1) == '\r' && At(src, index + 2) == '\n')
        {
          index += 3;
          continue;
        }
        if (c == '\n' || !char.IsWhiteSpace(c))
          break;
        index++;
      }
      if (At(src, index) != '#')
        return index;
      while (index < len && At(src, index) != '\n')
        index++;
      return index;
    }

    public static (TokenType, int) Next(ReadOnlySpan<char> src)
    {
      var c = src[0];

      return c switch
      {
        '\n' => (TokenType.EOL, 1),
        // Placeholder handled in WordLike
        // Word handled in WordLine
        // Label handled in WordLike
        '@' => TakePosition(src),
        '-' when At(src, 1) == '>' => (TokenType.Result, 2),
        ',' => (TokenType.Comma, 1),
        ':' => TakeType(src),
        _ when IsDigit(c) => TakeNumber(src),
        '(' => (TokenType.POpen, 1),
        ')' => (TokenType.PClose, 1),
        '.' => TakeMacro(src),
        '"' => TakeString(src),
        '{' => (TokenType.BOpen, 1),
        '}' => (TokenType.BClose, 1),
        '+' => (TokenType.Plus, 1),
        '-' => (TokenType.Minus, 1),
        '*' => (TokenType.Mult, 1),
        '/' => (TokenType.Div, 1),
        '~' => (TokenType.Not, 1),
        _ when IsWordStart(c) => TakeWordLike(src),
        _ => (TokenType.Invalid, 1),
      };
    }

    private static (TokenType, int) TakeWordLike(ReadOnlySpan<char> src)
    {
      var len = 1;
      while (IsWordChar(At(src, len)))
        len++;

      if (len == 1 && src[0] == '_')
        return (TokenType.Placeholder, 1);
      else if (At(src, len) == ':' && IsBoundary(At(src, len + 1)))
        return (TokenType.Label, len + 1);
      else
        return (TokenType.Word, len);
    }

    private static (TokenType, int) TakeMacro(ReadOnlySpan<char> src)
    {
      var len = 1;
      while (IsWordChar(At(src, len)))
        len++;

      if (len == 1)
        return (TokenType.Invalid, 1);
      else
        return (TokenType.Macro, len);
    }

    private static (TokenType, int) TakeString(ReadOnlySpan<char> src)
    {
      var len = 1;
      while (len < src.Length && At(src, len) is not '"' and not '\n')
        len++;

      len++;

      if (At(src, len - 1) != '"')
        return (TokenType.Invalid, len);
      else
        return (TokenType.String, len);
    }

    private static (TokenType, int) TakePosition(ReadOnlySpan<char> src)
    {
      var len = 1;
      // TODO: support hex positions?
      while (IsDigit(At(src, len)))
        len++;

      if (len == 1)
        return (TokenType.Invalid, 1);
      else
        return (TokenType.Position, len);
    }

    private static (TokenType, int) TakeType(ReadOnlySpan<char> src)
    {
      var len = 1;
      while (IsWordChar(At(src, len)))
        len++;

      if (len == 1)
        return (TokenType.Invalid, 1);
      else
        return (TokenType.Type, len);
    }

    private static (TokenType, int) TakeNumber(ReadOnlySpan<char> src)
    {
      var len = 1;
      var minLen = 1;
      if ((src[0], At(src, 1)) is ('0', 'x') or ('0', 'X'))
      {
        // hex
        len = 2;
        minLen = 3;
        while (char.IsAsciiHexDigit(At(src, len)))
          len++;
      }
      else if ((src[0], At(src, 1)) is ('0', 'b') or ('0', 'B'))
      {
        // binary
        len = 2;
        minLen = 3;
        while (At(src, len) is '0' or '1')
          len++;
      }
      else
      {
        // decimal

        while (IsDigit(At(src, len)))
          len++;

        if (At(src, len) is '.')
        {
          len++;
          minLen = len + 1;
          while (IsDigit(At(src, len)))
            len++;
        }

        if (At(src, len) is 'e' or 'E')
        {
          len++;
          if (At(src, len) is '+' or '-')
            len++;
          minLen = len + 1;
          while (IsDigit(At(src, len)))
            len++;
        }
      }
      if (len < minLen)
        return (TokenType.Invalid, len);
      else
        return (TokenType.Number, len);
    }

    public static bool IsWordStart(char c) => c switch
    {
      '_' => true,
      >= 'A' and <= 'Z' => true,
      >= 'a' and <= 'z' => true,
      _ => false,
    };
    public static bool IsDigit(char c) => char.IsAsciiDigit(c);
    public static bool IsWordChar(char c) => IsDigit(c) || IsWordStart(c) || c == '.';
    public static bool IsBoundary(char c) => char.IsWhiteSpace(c) || c == 0;

    // TODO: switch this to just take in firstEnd and secondStart
    // TODO: move Is* character methods to extension properties
    public static bool NeedsSpace(TokenType firstType, char firstEnd, TokenType secondType, char secondStart) =>
      (firstType, firstEnd, secondType, secondStart) switch
      {
        (TokenType.Label or TokenType.Comma, _, _, _) => true,
        _ when IsWordChar(firstEnd) && IsWordChar(secondStart) => true,
        (_, '(', TokenType.POpen, _) => false,
        (_, _, TokenType.POpen, _) when IsWordChar(firstEnd) => false,
        (_, _, TokenType.POpen, _) => true,
        (TokenType.PClose, _, _, ')') => false,
        (TokenType.PClose, _, _, _) => true,
        _ => false,
      };
  }
}