
using System;

namespace KSASM.Assembly
{
  public class Lexer : IDisposable
  {
    public static SourceIndex LexSource(ParseBuffer buffer, SourceString source, TokenIndex producer)
    {
      SourceIndex sourceIndex;

      using (var lexer = new Lexer(buffer, source, producer))
      {
        sourceIndex = lexer.SourceIndex;
        while (lexer.Next()) ;
      }

      return sourceIndex;
    }

    private readonly SourceString source;
    private readonly ParseBuffer.RawSourceBuilder s;
    private int index = 0;

    private SourceIndex SourceIndex => s.Source;

    private Lexer(ParseBuffer buffer, SourceString source, TokenIndex producer)
    {
      this.source = source;
      s = buffer.NewRawSource(source.Name, source.Source, producer);
    }

    private bool Next()
    {
      SkipWS();

      if (index == source.Length)
        return false;

      var c = At(index);

      return c switch
      {
        '\n' => Add(TokenType.EOL, 1),
        '[' => Add(TokenType.IOpen, 1),
        ']' => Add(TokenType.IClose, 1),
        '+' or '-' => Add(TokenType.Offset, 1),
        ',' => Add(TokenType.Comma, 1),
        '$' when At(index + 1) == '(' => Add(TokenType.COpen, 2),
        '$' when At(index + 1) == '[' => Add(TokenType.CIOpen, 2),
        '(' => Add(TokenType.POpen, 1),
        ')' => Add(TokenType.PClose, 1),
        '{' => Add(TokenType.BOpen, 1),
        '}' => Add(TokenType.BClose, 1),
        '/' => Add(TokenType.Div, 1),
        '~' => Add(TokenType.Not, 1),
        _ when IsWordStart(c) => TakeWordLike(),
        '@' => TakePosition(),
        '*' => TakeWidth(),
        ':' => TakeType(),
        _ when IsDigit(c) => TakeNumber(),
        '.' => TakeMacro(),
        '"' => TakeString(),
        _ => Add(TokenType.Invalid, 1),
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

    private bool Add(TokenType type, int len)
    {
      s.AddToken(type, new(index, len));
      index += len;
      return true;
    }

    private bool TakeWordLike()
    {
      var len = 1;
      while (IsWordChar(At(index + len)))
        len++;

      if (len == 1 && At(index) == '_')
        return Add(TokenType.Placeholder, 1);
      else if (At(index + len) == ':' && IsBoundary(At(index + len + 1)))
        return Add(TokenType.Label, len + 1);
      else
        return Add(TokenType.Word, len);
    }

    private bool TakeMacro()
    {
      var len = 1;
      while (IsWordChar(At(index + len)))
        len++;

      if (len == 1)
        return Add(TokenType.Invalid, 1);
      else
        return Add(TokenType.Macro, len);
    }

    private bool TakeString()
    {
      var len = 1;
      while (index + len < source.Length && At(index + len) is not '"' and not '\n')
        len++;

      len++;

      if (At(index + len - 1) != '"')
        return Add(TokenType.Invalid, len);
      else
        return Add(TokenType.String, len);
    }

    private bool TakePosition()
    {
      var len = 1;
      // TODO: support hex positions?
      while (IsDigit(At(index + len)))
        len++;

      if (len == 1)
        return Add(TokenType.Invalid, 1);
      else
        return Add(TokenType.Position, len);
    }

    private bool TakeWidth()
    {
      var len = 1;
      while (IsDigit(At(index + len)))
        len++;

      if (len == 1)
        return Add(TokenType.Mult, 1);
      else
        return Add(TokenType.Width, len);
    }

    private bool TakeType()
    {
      var len = 1;
      while (IsWordChar(At(index + len)))
        len++;

      if (len == 1)
        return Add(TokenType.Invalid, 1);
      else
        return Add(TokenType.Type, len);
    }

    private bool TakeNumber()
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
        return Add(TokenType.Invalid, len);
      else
        return Add(TokenType.Number, len);
    }

    private char At(int index)
    {
      if (index < 0 || index >= source.Length)
        return (char)0;
      return source[index];
    }

    public void Dispose()
    {
      s.AddToken(TokenType.EOL, new(source.Length, 0));
      s.Dispose();
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

    public static bool NeedsSpace(TokenType firstType, char firstEnd, TokenType secondType, char secondStart) =>
      (firstType, firstEnd, secondType, secondStart) switch
      {
        (TokenType.Label or TokenType.Comma, _, _, _) => true,
        (_, _, TokenType.Mult, _) => true,
        _ when IsWordChar(firstEnd) && IsWordChar(secondStart) => true,
        (_, '(' or '[', TokenType.POpen or TokenType.COpen or TokenType.CIOpen, _) => false,
        (_, _, TokenType.POpen, _) when IsWordChar(firstEnd) => false,
        (_, _, TokenType.POpen or TokenType.COpen or TokenType.CIOpen, _) => true,
        (TokenType.PClose, _, _, ')' or ']') => false,
        (TokenType.PClose, _, _, _) => true,
        _ => false,
      };
  }
}