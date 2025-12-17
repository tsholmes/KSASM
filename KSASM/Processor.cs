
using System;
using System.Collections.Generic;

namespace KSASM
{
  public partial class Processor
  {
    public const int MAIN_MEM_SIZE = 1 << 24;
    public const int ADDR_MASK = MAIN_MEM_SIZE - 1;

    public static bool DebugOps = false;
    public static bool DebugOperands = false;

    public readonly ByteArrayMemory MainMemory;
    public readonly MappedMemory MappedMemory;
    public readonly MemoryAccessor Memory;
    public int PC = 0;
    public int SP = 0;
    public int FP = 0;
    public ulong SleepTime = 0;

    private readonly ValArray A = new();
    private ValuePointer Aptr;
    private readonly ValArray B = new();
    private ValuePointer Bptr;
    private readonly ValArray C = new();
    private ValuePointer Cptr;

    public Action<ValArray, ValArray> OnDebug;
    public Action<string> OnDebugStr;

    public readonly IDevice[] Devices;

    private readonly Dictionary<ulong, IDevice> deviceMap = [];
    private readonly IDevice defaultDevice = new NullDevice();

    public Processor(params IDevice[] devices)
    {
      MainMemory = new(MAIN_MEM_SIZE);
      MappedMemory = new();
      Memory = new(MappedMemory);

      MappedMemory.MapRange(0, MainMemory, 0, MAIN_MEM_SIZE);

      Devices = devices;
      foreach (var device in devices)
        deviceMap.Add(device.Id, device);
    }

    private void ModPC(int off) => PC = (PC + off) & ADDR_MASK;
    private void ModSP(int off) => SP = (SP + off) & ADDR_MASK;
    private void ModFP(int off) => FP = (FP + off) & ADDR_MASK;

    public void Step()
    {
      var inst = Instruction.Decode(Memory.Read(PC, DataType.P24).Unsigned);
      ModPC(DataType.P24.SizeBytes());

      Exec(ref inst);
    }

    private ValArray Op(int idx) => idx switch
    {
      0 => A,
      1 => B,
      2 => C,
      _ => throw new IndexOutOfRangeException($"{idx}"),
    };

    private ref ValuePointer Ptr(int idx)
    {
      switch (idx)
      {
        case 0: return ref Aptr;
        case 1: return ref Bptr;
        case 2: return ref Cptr;
        default: throw new IndexOutOfRangeException($"{idx}");
      }
    }

    private int Push(ref ValuePointer ptr)
    {
      ModSP(-ptr.Type.SizeBytes() * ptr.Width);
      return SP;
    }

    private int Pop(ref ValuePointer ptr)
    {
      var addr = SP;
      ModSP(ptr.Type.SizeBytes() * ptr.Width);
      return addr;
    }

    private void Exec(ref Instruction inst)
    {
      var initPC = PC;
      var info = OpCodeInfo.For(inst.OpCode);

      // fill op format
      for (var i = info.TotalOps; --i >= 0;)
      {
        ref var op = ref Ptr(i);
        op.Type = info[i].Type ?? inst.Type(i);
        op.Width = (byte)(info[i].Width ?? inst.Width);
      }
      // fill immediate pointers
      for (var i = 0; i < inst.ImmCount; i++)
      {
        ref var op = ref Ptr(i);
        op.Address = PC;
        ModPC(op.Type.SizeBytes() * op.Width);
      }
      // fill stack pointers for non-immediate
      for (var i = inst.ImmCount; i < info.InOps; i++)
      {
        ref var op = ref Ptr(i);
        op.Address = Pop(ref op);
      }
      // read input ops
      for (var i = info.InOps; --i >= 0;)
        Memory.Read(Ptr(i), Op(i));
      // prep out ops
      for (var i = info.OutOps; --i >= 0;)
      {
        ref var op = ref Ptr(i + info.InOps);
        var opv = Op(i + info.InOps);
        opv.Init(op.Type.VMode(), op.Width);
      }

      // if (DebugOps)
      //   Console.WriteLine($"{PC - 8}: {op}*{opA.Width} {opA.Address},{opA.Type} {opB.Address},{opB.Type}");

      if (DebugOps)
        Console.WriteLine($"{initPC - 3:X6}: {inst.OpCode}*{inst.Width} {Aptr.Type}*{Aptr.Width} {Bptr.Type}*{Bptr.Width} {Cptr.Type}*{Cptr.Width} (FP {FP:X6}, SP {SP:X6})");
      if (DebugOperands)
      {
        for (var i = 0; i < info.InOps; i++)
          Console.WriteLine($"  IN {"ABC"[i]}: {Op(i)}");
      }

      ExecOp(inst.OpCode);

      // write out ops
      for (var i = 0; i < info.OutOps; i++)
      {
        var idx = i + info.InOps;
        ref var op = ref Ptr(idx);
        op.Address = Push(ref op);
        Memory.Write(op, Op(idx));
        if (DebugOperands)
          Console.WriteLine($"  OUT {"ABC"[idx]}: {Op(idx)}");
      }
    }

    private void ExecOp(OpCode op)
    {
      switch (op)
      {
        case OpCode.push: OpPush(); break;
        case OpCode.pop: OpPop(); break;
        case OpCode.dup: OpDup(); break;
        case OpCode.swz: OpSwz(); break;
        case OpCode.ld: OpLd(); break;
        case OpCode.st: OpSt(); break;
        case OpCode.ldf: OpLdf(); break;
        case OpCode.lds: OpLds(); break;
        case OpCode.stf: OpStf(); break;
        case OpCode.sts: OpSts(); break;
        case OpCode.ldfp: OpLdfp(); break;
        case OpCode.stfp: OpStfp(); break;
        case OpCode.modfp: OpModfp(); break;
        case OpCode.ldsp: OpLdsp(); break;
        case OpCode.stsp: OpStsp(); break;
        case OpCode.modsp: OpModsp(); break;
        case OpCode.not: OpNot(); break;
        case OpCode.and: OpAnd(); break;
        case OpCode.or: OpOr(); break;
        case OpCode.xor: OpXor(); break;
        case OpCode.shl: OpShl(); break;
        case OpCode.shr: OpShr(); break;
        case OpCode.neg: OpNeg(); break;
        case OpCode.sign: OpSign(); break;
        case OpCode.abs: OpAbs(); break;
        case OpCode.add: OpAdd(); break;
        case OpCode.sub: OpSub(); break;
        case OpCode.mul: OpMul(); break;
        case OpCode.div: OpDiv(); break;
        case OpCode.rem: OpRem(); break;
        case OpCode.mod: OpMod(); break;
        case OpCode.pow: OpPow(); break;
        case OpCode.max: OpMax(); break;
        case OpCode.min: OpMin(); break;
        case OpCode.floor: OpFloor(); break;
        case OpCode.ceil: OpCeil(); break;
        case OpCode.round: OpRound(); break;
        case OpCode.trunc: OpTrunc(); break;
        case OpCode.sqrt: OpSqrt(); break;
        case OpCode.exp: OpExp(); break;
        case OpCode.log: OpLog(); break;
        case OpCode.log2: OpLog2(); break;
        case OpCode.log10: OpLog10(); break;
        case OpCode.sin: OpSin(); break;
        case OpCode.cos: OpCos(); break;
        case OpCode.tan: OpTan(); break;
        case OpCode.sinh: OpSinh(); break;
        case OpCode.cosh: OpCosh(); break;
        case OpCode.tanh: OpTanh(); break;
        case OpCode.asin: OpAsin(); break;
        case OpCode.acos: OpAcos(); break;
        case OpCode.atan: OpAtan(); break;
        case OpCode.asinh: OpAsinh(); break;
        case OpCode.acosh: OpAcosh(); break;
        case OpCode.atanh: OpAtanh(); break;
        case OpCode.conj: OpConj(); break;
        case OpCode.andr: OpAndr(); break;
        case OpCode.orr: OpOrr(); break;
        case OpCode.xorr: OpXorr(); break;
        case OpCode.addr: OpAddr(); break;
        case OpCode.mulr: OpMulr(); break;
        case OpCode.minr: OpMinr(); break;
        case OpCode.maxr: OpMaxr(); break;
        case OpCode.jump: OpJump(); break;
        case OpCode.bzero: OpBzero(); break;
        case OpCode.bpos: OpBpos(); break;
        case OpCode.bneg: OpBneg(); break;
        case OpCode.blt: OpBlt(); break;
        case OpCode.ble: OpBle(); break;
        case OpCode.beq: OpBeq(); break;
        case OpCode.bne: OpBne(); break;
        case OpCode.bge: OpBge(); break;
        case OpCode.bgt: OpBgt(); break;
        case OpCode.sw: OpSw(); break;
        case OpCode.call: OpCall(); break;
        case OpCode.adjf: OpAdjf(); break;
        case OpCode.ret: OpRet(); break;
        case OpCode.rand: OpRand(); break;
        case OpCode.sleep: OpSleep(); break;
        case OpCode.devmap: OpDevmap(); break;
        case OpCode.debug: OpDebug(); break;
      }
    }
  }
}