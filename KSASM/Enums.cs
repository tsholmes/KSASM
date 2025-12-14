
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
    ret,
    // Misc
    rand,
    sleep,
    devmap,
    debug = 0x7F,
  }
}