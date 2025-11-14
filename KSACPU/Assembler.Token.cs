
namespace KSACPU
{
  public partial class Assembler
  {
    public enum TokenType
    {
      Invalid,
      EOL, // non-escaped newline, or end of file
      EscapedEOL, // \ newline
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
      POpen, // (
      PClose, // )
      Macro, // .macroname
    }

    public struct Token
    {
      public SourceString Source;
      public TokenType Type;
      public int Pos;
      public int Len;

      public string Str() => Source.TokenStr(this);

      public string PosStr() => Source.PosStr(Pos);
    }

    public interface ITokenStream
    {
      public bool Next(out Token token);
    }
  }
}