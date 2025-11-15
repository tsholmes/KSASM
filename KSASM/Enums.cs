
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
    C128 = 7,
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
    Reorder,

    // Unary operations
    BitNot,
    Negate,
    Conjugate,
    Sign,
    Abs,

    // Binary operations
    BitAnd,
    BitOr,
    BitXor,
    ShiftLeft,
    ShiftRight,
    Add,
    Subtract,
    Multiply,
    Divide,
    Remainder,
    Modulus,
    Power,
    Max,
    Min,
    UFpu,

    // Reduce operations
    All,
    Any,
    Parity,
    Sum,
    Product,
    MinAll,
    MaxAll,

    // Branch operations
    Jump,
    Call,
    BranchIfZero,
    BranchIfPos,
    BranchIfNeg,
    Switch,

    // Wait operations
    Sleep,

    // Device Operations
    // TODO: redesign after some use
    DevID,
    DevType,
    DevRead,
    DevWrite,

    // Interrupt Operations
    // TODO: redesign after some use
    IHandler,
    IData,
    IReturn,
  }

  // TODO: assign static values once more finalized
  public enum UFpuCode
  {
    // Rounding
    Floor,
    Ceil,
    Round,
    Trunc,

    // Exponential
    Sqrt,
    Exp,
    Pow2,
    Pow10,
    Log,
    Log2,
    Log10,

    // Trig
    Sin,
    Cos,
    Tan,
    Sinh,
    Cosh,
    Tanh,

    // Inverse Trig
    Asin,
    Acos,
    Atan,
    Asinh,
    Acosh,
    Atanh,

    // Random
    Rand,
  }
}