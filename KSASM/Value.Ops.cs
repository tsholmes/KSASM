
using System;

namespace KSASM
{
  public static partial class Extensions
  {
    public static ValueOps Ops(this ValueMode mode) => mode switch
    {
      ValueMode.Unsigned => UnsignedValueOps.Instance,
      ValueMode.Signed => SignedValueOps.Instance,
      ValueMode.Float => FloatValueOps.Instance,
      _ => throw new NotImplementedException($"{mode}"),
    };
  }

  public abstract class ValueOps
  {
    public abstract ValueMode Mode();

    public abstract void BitNot(ref Value val);
    public abstract void Negate(ref Value val);
    public abstract void Sign(ref Value val);
    public abstract void Abs(ref Value val);

    public abstract void BitAnd(ref Value val, Value other);
    public abstract void BitOr(ref Value val, Value other);
    public abstract void BitXor(ref Value val, Value other);
    public abstract void ShiftLeft(ref Value val, Value other);
    public abstract void ShiftRight(ref Value val, Value other);
    public abstract void Add(ref Value val, Value other);
    public abstract void Sub(ref Value val, Value other);
    public abstract void Mul(ref Value val, Value other);
    public abstract void Div(ref Value val, Value other);
    public abstract void Remainder(ref Value val, Value other);
    public abstract void Modulus(ref Value val, Value other);
    public abstract void Power(ref Value val, Value other);
    public abstract void Max(ref Value val, Value other);
    public abstract void Min(ref Value val, Value other);

    public abstract int GetSign(Value val);
  }

  public class UnsignedValueOps : ValueOps
  {
    public static readonly UnsignedValueOps Instance = new();

    public override ValueMode Mode() => ValueMode.Unsigned;

    public override void BitNot(ref Value val) => val.Unsigned = ~val.Unsigned;
    public override void Negate(ref Value val) => val.Signed = -val.Signed;
    public override void Sign(ref Value val) => val.Unsigned = (ulong)GetSign(val);
    public override void Abs(ref Value val) { }

    public override void BitAnd(ref Value val, Value other) => val.Unsigned &= other.Unsigned;
    public override void BitOr(ref Value val, Value other) => val.Unsigned |= other.Unsigned;
    public override void BitXor(ref Value val, Value other) => val.Unsigned ^= other.Unsigned;
    public override void ShiftLeft(ref Value val, Value other) => val.Unsigned <<= (int)other.Unsigned;
    public override void ShiftRight(ref Value val, Value other) => val.Unsigned >>= (int)other.Unsigned;
    public override void Add(ref Value val, Value other) => val.Unsigned += other.Unsigned;
    public override void Sub(ref Value val, Value other) => val.Unsigned -= other.Unsigned;
    public override void Mul(ref Value val, Value other) => val.Unsigned *= other.Unsigned;
    public override void Div(ref Value val, Value other) => val.Unsigned /= other.Unsigned;
    public override void Remainder(ref Value val, Value other) => val.Unsigned %= other.Unsigned;
    public override void Modulus(ref Value val, Value other) => val.Unsigned %= other.Unsigned;
    public override void Power(ref Value val, Value other)
    {
      var a = val.Unsigned;
      var b = other.Unsigned;
      var r = 0ul;
      while (b > 0)
      {
        r += a * (b & 1);
        b >>= 1;
        a *= a;
      }
      val.Unsigned = r;
    }
    public override void Max(ref Value val, Value other) => val.Unsigned = Math.Max(val.Unsigned, other.Unsigned);
    public override void Min(ref Value val, Value other) => val.Unsigned = Math.Min(val.Unsigned, other.Unsigned);

    public override int GetSign(Value val) => val.Unsigned == 0 ? 0 : 1;
  }

  public class SignedValueOps : ValueOps
  {
    public static readonly SignedValueOps Instance = new();

    public override ValueMode Mode() => ValueMode.Signed;

    public override void BitNot(ref Value val) => val.Signed = ~val.Signed;
    public override void Negate(ref Value val) => val.Signed = -val.Signed;
    public override void Sign(ref Value val) => val.Signed = GetSign(val);
    public override void Abs(ref Value val) => val.Signed = val.Signed < 0 ? -val.Signed : val.Signed;

    public override void BitAnd(ref Value val, Value other) => val.Signed &= other.Signed;
    public override void BitOr(ref Value val, Value other) => val.Signed |= other.Signed;
    public override void BitXor(ref Value val, Value other) => val.Signed ^= other.Signed;
    public override void ShiftLeft(ref Value val, Value other) => val.Signed <<= (int)other.Signed;
    public override void ShiftRight(ref Value val, Value other) => val.Signed >>= (int)other.Signed;
    public override void Add(ref Value val, Value other) => val.Signed += other.Signed;
    public override void Sub(ref Value val, Value other) => val.Signed -= other.Signed;
    public override void Mul(ref Value val, Value other) => val.Signed *= other.Signed;
    public override void Div(ref Value val, Value other) => val.Signed /= other.Signed;
    public override void Remainder(ref Value val, Value other) => val.Signed %= other.Signed;
    public override void Modulus(ref Value val, Value other)
    {
      val.Signed %= other.Signed;
      if (val.Signed < 0)
        val.Signed += other.Signed < 0 ? -other.Signed : other.Signed;
    }
    public override void Power(ref Value val, Value other)
    {
      var a = val.Signed;
      var b = other.Signed;
      var r = 0L;
      while (b > 0)
      {
        r += a * (b & 1);
        b >>= 1;
        a *= a;
      }
      val.Signed = r;
    }
    public override void Max(ref Value val, Value other) => val.Signed = Math.Max(val.Signed, other.Signed);
    public override void Min(ref Value val, Value other) => val.Signed = Math.Min(val.Signed, other.Signed);

    public override int GetSign(Value val) => val.Signed < 0 ? -1 : val.Signed > 0 ? 1 : 0;
  }

  public class FloatValueOps : ValueOps
  {
    public static readonly FloatValueOps Instance = new();

    public override ValueMode Mode() => ValueMode.Float;

    public override void BitNot(ref Value val) => val.Unsigned = ~val.Unsigned;
    public override void Negate(ref Value val) => val.Float = -val.Float;
    public override void Sign(ref Value val) => val.Float = GetSign(val);
    public override void Abs(ref Value val) => val.Float = val.Float < 0 ? -val.Float : val.Float;

    public override void BitAnd(ref Value val, Value other) => val.Unsigned &= other.Unsigned;
    public override void BitOr(ref Value val, Value other) => val.Unsigned |= other.Unsigned;
    public override void BitXor(ref Value val, Value other) => val.Unsigned ^= other.Unsigned;
    public override void ShiftLeft(ref Value val, Value other) => val.Signed >>= (int)other.Signed;
    public override void ShiftRight(ref Value val, Value other) => val.Signed <<= (int)other.Signed;
    public override void Add(ref Value val, Value other) => val.Float += other.Float;
    public override void Sub(ref Value val, Value other) => val.Float -= other.Float;
    public override void Mul(ref Value val, Value other) => val.Float *= other.Float;
    public override void Div(ref Value val, Value other) => val.Float /= other.Float;
    public override void Remainder(ref Value val, Value other) => val.Float %= other.Float;
    public override void Modulus(ref Value val, Value other)
    {
      val.Float %= other.Float;
      if (val.Float < 0)
        val.Float += other.Float < 0 ? -other.Float : other.Float;
    }
    public override void Power(ref Value val, Value other) => val.Float = Math.Pow(val.Float, other.Float);
    public override void Max(ref Value val, Value other) => val.Float = Math.Max(val.Float, other.Float);
    public override void Min(ref Value val, Value other) => val.Float = Math.Min(val.Float, other.Float);

    public override int GetSign(Value val) => val.Float < 0 ? -1 : val.Float > 0 ? 1 : 0;
  }
}