
namespace KSACPU
{
  public partial class Assembler
  {
    public class LexerReader
    {
      private readonly ITokenStream stream;

      private bool hasNext = false;
      private Token next;
      private bool eof = false;

      public LexerReader(ITokenStream stream)
      {
        this.stream = stream;
      }

      public bool Peek(out Token token)
      {
        FillNext();
        token = next;
        return hasNext;
      }

      public bool PeekType(TokenType type, out Token token)
      {
        if (!Peek(out token))
          return false;
        return type == token.Type;
      }

      public bool Take(out Token token)
      {
        FillNext();
        token = next;
        var has = hasNext;
        hasNext = false;
        return has;
      }

      public bool TakeType(TokenType type, out Token token) =>
        PeekType(type, out token) && Take(out token);

      public bool EOF()
      {
        FillNext();
        return eof;
      }

      private void FillNext()
      {
        if (!hasNext && !eof)
          hasNext = stream.Next(out next);
        eof = !hasNext;
      }
    }

    public class Lexer : ITokenStream
    {
      private readonly SourceString source;
      private int index = 0;

      private bool takenEOF = false;

      public Lexer(SourceString source)
      {
        this.source = source;
      }

      public bool Next(out Token token)
      {
        SkipWS();

        if (index == source.Length)
        {
          if (takenEOF)
          {
            token = default;
            return false;
          }
          takenEOF = true;
          return TakeNext(TokenType.EOL, 0, out token);
        }

        var c = At(index);

        return c switch
        {
          '\n' => TakeNext(TokenType.EOL, 1, out token),
          '\\' when At(index + 1) == '\n' => TakeNext(TokenType.EscapedEOL, 2, out token),
          '\\' when At(index + 1) == '\r' && At(index + 2) == '\n' => TakeNext(TokenType.EscapedEOL, 3, out token),
          '[' => TakeNext(TokenType.IOpen, 1, out token),
          ']' => TakeNext(TokenType.IClose, 1, out token),
          '+' or '-' => TakeNext(TokenType.Offset, 1, out token),
          ',' => TakeNext(TokenType.Comma, 1, out token),
          '$' when At(index + 1) == '(' => TakeNext(TokenType.COpen, 2, out token),
          '(' => TakeNext(TokenType.POpen, 1, out token),
          ')' => TakeNext(TokenType.PClose, 1, out token),
          _ when IsWordStart(c) => TakeWordLike(out token),
          '@' => TakePosition(out token),
          '*' => TakeWidth(out token),
          ':' => TakeType(out token),
          _ when IsDigit(c) => TakeNumber(out token),
          '.' => TakeMacro(out token),
          _ => TakeNext(TokenType.Invalid, 1, out token),
        };
      }

      private void SkipWS()
      {
        while (index < source.Length)
        {
          var c = At(index);
          if (c == '\n' || !char.IsWhiteSpace(c))
            break;
          index++;
        }
        if (At(index) != '#')
          return;
        while (index < source.Length && At(index) != '\n')
          index++;
      }

      private bool TakeNext(TokenType type, int len, out Token token)
      {
        token = source.Token(type, index, len);
        index += len;
        return true;
      }

      private bool TakeWordLike(out Token token)
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

      private bool TakeMacro(out Token token)
      {
        var len = 1;
        while (IsWordChar(At(index + len)))
          len++;

        if (len == 1)
          return TakeNext(TokenType.Invalid, 1, out token);
        else
          return TakeNext(TokenType.Macro, len, out token);
      }

      private bool TakePosition(out Token token)
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

      private bool TakeWidth(out Token token)
      {
        var len = 1;
        while (IsDigit(At(index + len)))
          len++;

        if (len == 1)
          return TakeNext(TokenType.Invalid, 1, out token);
        else
          return TakeNext(TokenType.Width, len, out token);
      }

      private bool TakeType(out Token token)
      {
        var len = 1;
        while (IsWordChar(At(index + len)))
          len++;

        if (len == 1)
          return TakeNext(TokenType.Invalid, 1, out token);
        else
          return TakeNext(TokenType.Type, len, out token);
      }

      private bool TakeNumber(out Token token)
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

      private static bool IsWordChar(char c) => IsDigit(c) || IsWordStart(c);

      private static bool IsBoundary(char c) => char.IsWhiteSpace(c) || c == 0;
    }
  }
}