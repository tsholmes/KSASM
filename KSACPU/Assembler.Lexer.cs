

namespace KSACPU
{
  public partial class Assembler
  {
    public enum TokenType
    {
      Invalid,
      EOL, // non-escaped newline, or end of file
      Placeholder, // _
      Word, // [\w][\w\d]*
      Label, // word:\b
      Position, // @integer
      Width, // *integer
      IOpen, // [
      IClose, // ]
      Comma, // ,
      Type, // :typename
      Offset, // + or -
      Number, // decimal, hex with 0x prefix, binary with 0b prefix
      COpen, // $(
      CClose, // )
    }

    public struct Token
    {
      public TokenType Type;
      public int Pos;
      public int Len;
    }

    public class Lexer
    {
      private readonly string source;
      private int index = 0;

      private bool takenEOF = false;

      private bool hasNext = false;
      private Token next;

      public Lexer(string source)
      {
        this.source = source;
      }

      public string this[Token token] => source[token.Pos..(token.Pos + token.Len)];

      public bool EOF() => takenEOF && !hasNext;

      public bool Peek(out Token token)
      {
        if (!FillNext())
        {
          token = default;
          return false;
        }
        token = next;
        return true;
      }

      public bool Take(out Token token)
      {
        if (!FillNext())
        {
          token = default;
          return false;
        }
        token = next;
        hasNext = false;
        return true;
      }

      private void SetNext(TokenType type, int len)
      {
        next = new() { Type = type, Pos = index, Len = len };
        hasNext = true;
        index += len;
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

      private bool FillNext()
      {
        if (hasNext)
          return true;

        SkipWS();

        if (index == source.Length)
        {
          if (takenEOF)
            return false;
          takenEOF = true;
          SetNext(TokenType.EOL, 0);
          return true;
        }

        var c = At(index);

        if (c == '\n')
          SetNext(TokenType.EOL, 1);
        else if (c == '[')
          SetNext(TokenType.IOpen, 1);
        else if (c == ']')
          SetNext(TokenType.IClose, 1);
        else if (c == '+' || c == '-')
          SetNext(TokenType.Offset, 1);
        else if (c == ',')
          SetNext(TokenType.Comma, 1);
        else if (c == '$' && At(index + 1) == '(')
          SetNext(TokenType.COpen, 2);
        else if (c == ')')
          SetNext(TokenType.CClose, 1);
        else if (IsWordStart(c))
          TakeWordLike();
        else if (c == '@')
          TakePosition();
        else if (c == '*')
          TakeWidth();
        else if (c == ':')
          TakeType();
        else if (IsDigit(c))
          TakeNumber();
        else
          SetNext(TokenType.Invalid, 1);

        return true;
      }

      private void TakeWordLike()
      {
        var len = 1;
        while (IsWordChar(At(index + len)))
          len++;

        if (len == 1 && At(index) == '_')
          SetNext(TokenType.Placeholder, 1);
        else if (At(index + len) == ':' && IsBoundary(At(index + len + 1)))
          SetNext(TokenType.Label, len + 1);
        else
          SetNext(TokenType.Word, len);
      }

      private void TakePosition()
      {
        var len = 1;
        // TODO: support hex positions?
        while (IsDigit(At(index + len)))
          len++;

        if (len == 1)
          SetNext(TokenType.Invalid, 1);
        else
          SetNext(TokenType.Position, len);
      }

      private void TakeWidth()
      {
        var len = 1;
        while (IsDigit(At(index + len)))
          len++;

        if (len == 1)
          SetNext(TokenType.Invalid, 1);
        else
          SetNext(TokenType.Width, len);
      }

      private void TakeType()
      {
        var len = 1;
        while (IsWordChar(At(index + len)))
          len++;

        if (len == 1)
          SetNext(TokenType.Invalid, 1);
        else
          SetNext(TokenType.Type, len);
      }

      private void TakeNumber()
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
          SetNext(TokenType.Invalid, len);
        else
          SetNext(TokenType.Number, len);
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