
using System;

namespace KSASM
{
  public enum DataType : byte
  {
    U8 = 0,
    I16 = 1,
    I32 = 2,
    I64 = 3,
    U64 = 4,
    F64 = 5,
    P24 = 6,
    // C128 = 7,
  }

  [Flags]
  public enum OperandMode : byte
  {
    DirectA = 0,
    IndirectA = 1,

    DirectB = 0,
    IndirectB = 2,

    AddrAOffB = 0,
    AddrBaseOffAB = 4,
  }

  // TODO: assign static opcode values once these are more finalized
  public enum OpCode : byte
  {
    // Move operations
    Copy,
    Reorder, Swz = Reorder,

    // Unary operations
    BitNot, Not = BitNot,
    Negate, Neg = Negate,
    Conjugate, Conj = Conjugate,
    Sign,
    Abs,

    // Binary operations
    BitAnd, And = BitAnd,
    BitOr, Or = BitOr,
    BitXor, Xor = BitXor,
    ShiftLeft, Shl = ShiftLeft,
    ShiftRight, Shr = ShiftRight,
    Add,
    Subtract, Sub = Subtract,
    Multiply, Mul = Multiply,
    Divide, Div = Divide,
    Remainder, Rem = Remainder,
    Modulus, Mod = Modulus,
    Power, Pow = Power,
    Max,
    Min,

    // Unary Float operations
    Floor,
    Ceil,
    Round,
    Trunc,
    Sqrt,
    Exp,
    Pow2,
    Pow10,
    Log,
    Log2,
    Log10,
    Sin,
    Cos,
    Tan,
    Sinh,
    Cosh,
    Tanh,
    Asin,
    Acos,
    Atan,
    Asinh,
    Acosh,
    Atanh,

    // Random operations
    Rand,

    // Reduce operations
    All, AndR = All,
    Any, OrR = Any,
    Parity, XorR = Parity,
    Sum, AddR = Sum,
    Product, MulR = Product,
    MinAll, MinR = MinAll,
    MaxAll, MaxR = MaxAll,

    // Branch operations
    Jump,
    Call,
    BranchIfZero, BZero = BranchIfZero,
    BranchIfPos, BPos = BranchIfPos,
    BranchIfNeg, BNeg = BranchIfNeg,
    Switch,

    // Wait operations
    Sleep,

    // Device Operations
    DevMap,
    // TODO: device enumeration and info once dynamic devices for parts are added

    // TODO: Interrupt Operations if needed

    // debug instructions
    Debug = 126,
    DebugStr = 127,
  }
}