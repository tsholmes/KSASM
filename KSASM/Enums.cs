
using System;

namespace KSASM
{
  public enum DataType : byte
  {
    U8 = 0,
    U16 = 1,
    U32 = 2,
    U64 = 3,
    I8 = 4,
    I16 = 5,
    I32 = 6,
    I64 = 7,
    F64 = 8,
    C128 = 9,
    P24 = 10,
    S48 = 11,
    // 12-15 Reserved
  }

  // TODO: assign static opcode values once these are more finalized
  public enum OpCode : byte
  {
    // Stack Manipulation
    push,
    pop,
    dup,
    swz,
    // Memory Load/Store
    ld,
    st,
    // Offset Load/Store
    ldf,
    lds,
    stf,
    sts,
    // Register Manipulation
    ldfp,
    stfp,
    modfp,
    ldsp,
    stsp,
    modsp,
    // Bitwise
    not,
    and,
    or,
    xor,
    shl,
    shr,
    // Math
    neg,
    sign,
    abs,
    add,
    sub,
    mul,
    div,
    rem,
    mod,
    pow,
    max,
    min,
    // Float Math
    floor,
    ceil,
    round,
    trunc,
    sqrt,
    exp,
    log,
    log2,
    log10,
    sin,
    cos,
    tan,
    sinh,
    cosh,
    tanh,
    asin,
    acos,
    atan,
    asinh,
    acosh,
    atanh,
    // Complex Math
    conj,
    // Reduce
    andr,
    orr,
    xorr,
    addr,
    mulr,
    minr,
    maxr,
    // Branching
    jump,
    bzero,
    bpos,
    bneg,
    blt,
    ble,
    beq,
    bne,
    bge,
    bgt,
    sw,
    // Function
    call,
    adjf,
    ret,
    // Misc
    rand,
    sleep,
    devmap,
    debug = 0x7F,
  }

  public class OpCodeInfo
  {
    private static readonly OpCodeInfo[] infos = new OpCodeInfo[256];

    public static OpCodeInfo For(OpCode op) => infos[(byte)op];

    public readonly int InOps;
    public readonly int OutOps;
    public int TotalOps => InOps + OutOps;

    public OperandInfo A { get; init; } = new();
    public OperandInfo B { get; init; } = new();
    public OperandInfo C { get; init; } = new();

    private OpCodeInfo(int inOps, int outOps)
    {
      InOps = inOps;
      OutOps = outOps;
    }

    public OperandInfo this[int index] => index switch
    {
      0 => A,
      1 => B,
      2 => C,
      _ => throw new IndexOutOfRangeException($"{index}"),
    };

    static OpCodeInfo()
    {
      var p24 = new OperandInfo { Type = DataType.P24 };
      var p24x1 = new OperandInfo { Type = DataType.P24, Width = 1 };
      var u8 = new OperandInfo { Type = DataType.U8 };
      var x1 = new OperandInfo { Width = 1 };

      var in1 = new OpCodeInfo(1, 0);
      var addrRead = new OpCodeInfo(1, 1) { A = p24x1 };
      var addrWrite = new OpCodeInfo(2, 0) { A = p24x1 };
      var regRead = new OpCodeInfo(0, 1) { A = p24x1 };
      var regWrite = new OpCodeInfo(1, 0) { A = p24x1 };
      var unary = new OpCodeInfo(1, 1);
      var binary = new OpCodeInfo(2, 1);
      var reduce = new OpCodeInfo(1, 1) { B = x1 };
      var br0 = regWrite;
      var br1 = new OpCodeInfo(2, 0) { A = p24x1, B = x1 };
      var br2 = new OpCodeInfo(3, 0) { A = p24x1, B = x1, C = x1 };

      Array.Fill(infos, new(0, 0));

      infos[(int)OpCode.push] =
        infos[(int)OpCode.pop] = in1;
      infos[(int)OpCode.dup] = new(2, 0) { A = new() { Type = DataType.U8, Width = 1 } };
      infos[(int)OpCode.swz] = new(2, 1) { A = u8 };
      infos[(int)OpCode.ld] =
        infos[(int)OpCode.ldf] =
        infos[(int)OpCode.lds] = addrRead;
      infos[(int)OpCode.st] =
        infos[(int)OpCode.stf] =
        infos[(int)OpCode.sts] = addrWrite;
      infos[(int)OpCode.ldfp] =
        infos[(int)OpCode.ldsp] = regRead;
      infos[(int)OpCode.stfp] =
        infos[(int)OpCode.modfp] =
        infos[(int)OpCode.stsp] =
        infos[(int)OpCode.modsp] = regWrite;
      infos[(int)OpCode.not] = unary;
      infos[(int)OpCode.and] =
        infos[(int)OpCode.or] =
        infos[(int)OpCode.xor] =
        infos[(int)OpCode.shl] =
        infos[(int)OpCode.shr] = binary;
      infos[(int)OpCode.neg] =
        infos[(int)OpCode.sign] =
        infos[(int)OpCode.abs] = unary;
      infos[(int)OpCode.add] =
        infos[(int)OpCode.sub] =
        infos[(int)OpCode.mul] =
        infos[(int)OpCode.div] =
        infos[(int)OpCode.rem] =
        infos[(int)OpCode.mod] =
        infos[(int)OpCode.pow] =
        infos[(int)OpCode.max] =
        infos[(int)OpCode.min] = binary;
      infos[(int)OpCode.floor] =
        infos[(int)OpCode.ceil] =
        infos[(int)OpCode.round] =
        infos[(int)OpCode.trunc] =
        infos[(int)OpCode.sqrt] =
        infos[(int)OpCode.exp] =
        infos[(int)OpCode.log] =
        infos[(int)OpCode.log2] =
        infos[(int)OpCode.log10] =
        infos[(int)OpCode.sin] =
        infos[(int)OpCode.cos] =
        infos[(int)OpCode.tan] =
        infos[(int)OpCode.sinh] =
        infos[(int)OpCode.cosh] =
        infos[(int)OpCode.tanh] =
        infos[(int)OpCode.asin] =
        infos[(int)OpCode.acos] =
        infos[(int)OpCode.atan] =
        infos[(int)OpCode.asinh] =
        infos[(int)OpCode.acosh] =
        infos[(int)OpCode.atanh] =
        infos[(int)OpCode.conj] = unary;
      infos[(int)OpCode.andr] =
        infos[(int)OpCode.orr] =
        infos[(int)OpCode.xorr] =
        infos[(int)OpCode.addr] =
        infos[(int)OpCode.mulr] =
        infos[(int)OpCode.minr] =
        infos[(int)OpCode.maxr] = reduce;
      infos[(int)OpCode.jump] = br0;
      infos[(int)OpCode.bzero] =
        infos[(int)OpCode.bpos] =
        infos[(int)OpCode.bneg] = br1;
      infos[(int)OpCode.blt] =
        infos[(int)OpCode.ble] =
        infos[(int)OpCode.beq] =
        infos[(int)OpCode.bne] =
        infos[(int)OpCode.bge] =
        infos[(int)OpCode.bgt] = br2;
      infos[(int)OpCode.sw] = new(2, 0) { A = p24, B = x1 };
      infos[(int)OpCode.call] = br0;
      infos[(int)OpCode.adjf] = br0;
      infos[(int)OpCode.ret] = new(0, 0);
      infos[(int)OpCode.rand] = unary;
      infos[(int)OpCode.sleep] = new(1, 0) { A = x1 };
      infos[(int)OpCode.devmap] = new(1, 0) { A = new() { Type = DataType.P24, Width = 4 } };
      infos[(int)OpCode.debug] = new(1, 0);
    }

    public class OperandInfo
    {
      public DataType? Type { get; init; }
      public int? Width { get; init; }

      public OperandInfo() { }
    }
  }
}