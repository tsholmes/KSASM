
using System;
using System.Collections.Generic;
using System.Text;

namespace KSASM
{
  public partial class Processor
  {
    private delegate void UnaryOp(ValueOps ops, ref Value b);
    private delegate double FloatUnaryOp(double b);
    private delegate void BinaryOp(ValueOps ops, ref Value a, Value b);

    private void ReadAt(int addr, ValuePointer ptr, ValArray val)
    {
      ptr.Address = addr & ADDR_MASK;
      Memory.Read(ptr, val);
    }

    private void WriteAt(int addr, ValuePointer ptr, ValArray val)
    {
      ptr.Address = addr & ADDR_MASK;
      Memory.Write(ptr, val);
    }

    private void SetupOp(int idx, DataType type, int width)
    {
      var op = Op(idx);
      ref var ptr = ref Ptr(idx);
      op.Mode = type.VMode();
      op.Width = width;
      ptr.Type = type;
      ptr.Width = (byte)width;
    }

    private void Unary(UnaryOp op)
    {
      var ops = A.Mode.Ops();
      var bmode = B.Mode;
      B.Mode = A.Mode;
      for (var i = 0; i < A.Width; i++)
      {
        ref var val = ref B.Values[i];
        val = A.Values[i];
        op(ops, ref val);
      }
      B.Convert(bmode);
    }

    private void UnaryFloat(FloatUnaryOp op)
    {
      A.Convert(ValueMode.Float);
      var bmode = B.Mode;
      B.Mode = ValueMode.Float;
      for (var i = 0; i < A.Width; i++)
        B.Values[i].Float = op(A.Values[i].Float);
      B.Convert(bmode);
    }

    private void Binary(BinaryOp op)
    {
      var ops = B.Mode.Ops();
      A.Convert(B.Mode);
      var cmode = C.Mode;
      C.Mode = B.Mode;
      for (var i = 0; i < A.Width; i++)
      {
        ref var val = ref C.Values[i];
        val = B.Values[i];
        op(ops, ref val, A.Values[i]);
      }
      C.Convert(cmode);
    }

    private void BinaryShift(BinaryOp op)
    {
      var ops = B.Mode.Ops();
      A.Convert(ValueMode.Unsigned);
      var cmode = C.Mode;
      C.Mode = B.Mode;
      for (var i = 0; i < A.Width; i++)
      {
        ref var val = ref C.Values[i];
        val = B.Values[i];
        op(ops, ref val, A.Values[i]);
      }
      C.Convert(cmode);
    }

    private void Reduce(BinaryOp op)
    {
      var ops = A.Mode.Ops();
      var bmode = B.Mode;
      B.Mode = A.Mode;
      ref var val = ref B.Values[0];
      val = A.Values[0];
      for (var i = 1; i < A.Width; i++)
        op(ops, ref val, A.Values[i]);
      B.Convert(bmode);
    }

    private void BranchSign(int sign)
    {
      if (B.Mode.Ops().GetSign(B.Values[0]) == sign)
        PC = (int)A.Values[0].Unsigned & ADDR_MASK;
    }

    private void BranchCompare(Compare tgt)
    {
      B.Convert(C.Mode);
      if (C.Mode.Ops().Compare(C.Values[0], B.Values[0]) switch
      {
        < 0 => tgt.HasFlag(Compare.Less),
        0 => tgt.HasFlag(Compare.Equal),
        > 0 => tgt.HasFlag(Compare.Greater),
      })
        PC = (int)A.Values[0].Unsigned & ADDR_MASK;
    }

    [Flags]
    private enum Compare
    {
      Less = 1, Equal = 2, Greater = 4,
      LessEqual = Less | Equal, GreaterEqual = Greater | Equal, NotEqual = Less | Greater,
    }

    private void OpPush() => WriteAt(Push(ref Aptr), Aptr, A);
    private void OpPop() { }
    private void OpDup()
    {
      var count = Math.Clamp((int)A.Values[0].Unsigned, 2, 8);
      for (var i = 0; i < count; i++)
        WriteAt(Push(ref Bptr), Bptr, B);
    }
    private void OpSwz()
    {
      var cmode = C.Mode;
      C.Mode = B.Mode;
      var width = B.Width;
      for (var i = 0; i < width; i++)
        C.Values[i] = B.Values[A.Values[i].Unsigned % (ulong)width];
      C.Convert(cmode);
    }
    private void OpLd() => ReadAt((int)A.Values[0].Unsigned, Bptr, B);
    private void OpSt() => WriteAt((int)A.Values[0].Unsigned, Bptr, B);
    private void OpLdf() => ReadAt(FP + (int)A.Values[0].Unsigned, Bptr, B);
    private void OpLds() => ReadAt(SP + (int)A.Values[0].Unsigned, Bptr, B);
    private void OpStf() => WriteAt(FP + (int)A.Values[0].Unsigned, Bptr, B);
    private void OpSts() => WriteAt(SP + (int)A.Values[0].Unsigned, Bptr, B);
    private void OpLdfp() => A.Values[0].Unsigned = (ulong)FP;
    private void OpStfp() => FP = (int)A.Values[0].Unsigned & ADDR_MASK;
    private void OpModfp() => FP = (FP + (int)A.Values[0].Unsigned) & ADDR_MASK;
    private void OpLdsp() => A.Values[0].Unsigned = (ulong)SP;
    private void OpStsp() => SP = (int)A.Values[0].Unsigned & ADDR_MASK;
    private void OpModsp() => SP = (SP + (int)A.Values[0].Unsigned) & ADDR_MASK;
    private void OpNot() => Unary((ops, ref v) => ops.BitNot(ref v));
    private void OpAnd() => Binary((ops, ref a, b) => ops.BitAnd(ref a, b));
    private void OpOr() => Binary((ops, ref a, b) => ops.BitOr(ref a, b));
    private void OpXor() => Binary((ops, ref a, b) => ops.BitXor(ref a, b));
    private void OpShl() => BinaryShift((ops, ref a, b) => ops.ShiftLeft(ref a, b));
    private void OpShr() => BinaryShift((ops, ref a, b) => ops.ShiftRight(ref a, b));
    private void OpNeg() => Unary((ops, ref v) => ops.Negate(ref v));
    private void OpSign() => Unary((ops, ref v) => ops.Sign(ref v));
    private void OpAbs() => Unary((ops, ref v) => ops.Abs(ref v));
    private void OpAdd() => Binary((ops, ref a, b) => ops.Add(ref a, b));
    private void OpSub() => Binary((ops, ref a, b) => ops.Sub(ref a, b));
    private void OpMul() => Binary((ops, ref a, b) => ops.Mul(ref a, b));
    private void OpDiv() => Binary((ops, ref a, b) => ops.Div(ref a, b));
    private void OpRem() => Binary((ops, ref a, b) => ops.Remainder(ref a, b));
    private void OpMod() => Binary((ops, ref a, b) => ops.Modulus(ref a, b));
    private void OpPow() => Binary((ops, ref a, b) => ops.Power(ref a, b));
    private void OpMax() => Binary((ops, ref a, b) => ops.Max(ref a, b));
    private void OpMin() => Binary((ops, ref a, b) => ops.Min(ref a, b));
    private void OpFloor() => UnaryFloat(Math.Floor);
    private void OpCeil() => UnaryFloat(Math.Ceiling);
    private void OpRound() => UnaryFloat(Math.Round);
    private void OpTrunc() => UnaryFloat(Math.Truncate);
    private void OpSqrt() => UnaryFloat(Math.Sqrt);
    private void OpExp() => UnaryFloat(Math.Exp);
    private void OpLog() => UnaryFloat(Math.Log);
    private void OpLog2() => UnaryFloat(Math.Log2);
    private void OpLog10() => UnaryFloat(Math.Log10);
    private void OpSin() => UnaryFloat(Math.Sin);
    private void OpCos() => UnaryFloat(Math.Cos);
    private void OpTan() => UnaryFloat(Math.Tan);
    private void OpSinh() => UnaryFloat(Math.Sinh);
    private void OpCosh() => UnaryFloat(Math.Cosh);
    private void OpTanh() => UnaryFloat(Math.Tanh);
    private void OpAsin() => UnaryFloat(Math.Asin);
    private void OpAcos() => UnaryFloat(Math.Acos);
    private void OpAtan() => UnaryFloat(Math.Atan);
    private void OpAsinh() => UnaryFloat(Math.Asinh);
    private void OpAcosh() => UnaryFloat(Math.Acosh);
    private void OpAtanh() => UnaryFloat(Math.Atanh);
    private void OpConj() => throw new NotImplementedException();
    private void OpAndr() => Reduce((ops, ref a, b) => ops.BitAnd(ref a, b));
    private void OpOrr() => Reduce((ops, ref a, b) => ops.BitOr(ref a, b));
    private void OpXorr() => Reduce((ops, ref a, b) => ops.BitXor(ref a, b));
    private void OpAddr() => Reduce((ops, ref a, b) => ops.Add(ref a, b));
    private void OpMulr() => Reduce((ops, ref a, b) => ops.Mul(ref a, b));
    private void OpMinr() => Reduce((ops, ref a, b) => ops.Min(ref a, b));
    private void OpMaxr() => Reduce((ops, ref a, b) => ops.Max(ref a, b));
    private void OpJump() => PC = (int)A.Values[0].Unsigned & ADDR_MASK;
    private void OpBzero() => BranchSign(0);
    private void OpBpos() => BranchSign(+1);
    private void OpBneg() => BranchSign(-1);
    private void OpBlt() => BranchCompare(Compare.Less);
    private void OpBle() => BranchCompare(Compare.LessEqual);
    private void OpBeq() => BranchCompare(Compare.Equal);
    private void OpBne() => BranchCompare(Compare.NotEqual);
    private void OpBge() => BranchCompare(Compare.GreaterEqual);
    private void OpBgt() => BranchCompare(Compare.Greater);
    private void OpSw()
    {
      B.Convert(ValueMode.Unsigned);
      var idx = B.Values[0].Unsigned;
      if (idx < (ulong)A.Width)
        PC = (int)A.Values[(int)idx].Unsigned & ADDR_MASK;
    }
    private void OpCall()
    {
      if (DebugOps)
        Console.WriteLine($"  CALL {A.Values[0].Unsigned & ADDR_MASK:X6} (rFP: {FP:X6}, rPC: {PC:X6}, SP: {SP - 6:X6})");
      var newFP = SP;
      SetupOp(1, DataType.P24, 2);
      B.Values[0].Unsigned = (ulong)FP;
      B.Values[1].Unsigned = (ulong)PC;
      WriteAt(Push(ref Bptr), Bptr, B);
      FP = newFP;
      PC = (int)A.Values[0].Unsigned & ADDR_MASK;
    }
    private void OpAdjf()
    {
      var origFP = FP;
      var origSP = SP;
      SetupOp(1, DataType.P24, 2);
      ReadAt(Pop(ref Bptr), Bptr, B);
      FP = SP = (SP + (int)A.Values[0].Unsigned) & ADDR_MASK;
      WriteAt(Push(ref Bptr), Bptr, B);
      if (DebugOps)
        Console.WriteLine($"  ADJF {A.Values[0].Unsigned & ADDR_MASK:X6} (FP: {origFP:X6}->{FP:X6}, SP: {origSP:X6}->{SP:X6})");
    }
    private void OpRet()
    {
      var origFP = FP;
      SetupOp(0, DataType.P24, 2);
      ReadAt(Pop(ref Aptr), Aptr, A);
      FP = (int)A.Values[0].Unsigned & ADDR_MASK;
      PC = (int)A.Values[1].Unsigned & ADDR_MASK;
      if (DebugOps)
        Console.WriteLine($"  RET {A.Values[0].Unsigned & ADDR_MASK:X6} (FP: {origFP:X6}->{FP:X6}, PC: {PC:X6}, SP: {SP:X6})");
    }
    private void OpRand()
    {
      var bmode = B.Mode;
      B.Mode = A.Mode;
      var val = new Encoding.EVal();
      for (var i = 0; i < A.Width; i++)
      {
        switch (A.Mode)
        {
          case ValueMode.Unsigned:
            rand.NextBytes(val.Bytes);
            B.Values[i].Unsigned = val.U64 % A.Values[i].Unsigned;
            break;
          case ValueMode.Signed:
            rand.NextBytes(val.Bytes);
            B.Values[i].Signed = val.I64 % A.Values[i].Signed;
            break;
          case ValueMode.Float:
            val.F64 = rand.NextDouble();
            if (A.Values[i].Float < 0)
              val.F64 = (val.F64 - 0.5) * 2.0 * A.Values[i].Float;
            else
              val.F64 *= A.Values[i].Float;
            break;
        }
      }
      B.Convert(bmode);
    }
    private void OpSleep()
    {
      A.Convert(ValueMode.Unsigned);
      SleepTime = A.Values[0].Unsigned;
      if (SleepTime == 0)
        SleepTime = 1;
    }
    private void OpDevmap()
    {
      var memAddr = (int)A.Values[0].Unsigned;
      var memLen = (int)A.Values[1].Unsigned;
      var devId = A.Values[2].Unsigned;
      var devAddr = (int)B.Values[3].Unsigned;

      if (devId == 0)
        MappedMemory.MapRange(memAddr, MainMemory, memAddr, memLen);
      else
      {
        var device = deviceMap.GetValueOrDefault(devId) ?? defaultDevice;
        MappedMemory.MapRange(memAddr, device, devAddr, memLen);
      }
    }
    private void OpDebug()
    {
      if (Aptr.Type == DataType.S48)
      {
        Span<byte> buf = stackalloc byte[256];
        var sb = new StringBuilder();
        if (A.Width > 1)
          sb.Append('(');
        for (var i = 0; i < A.Width; i++)
        {
          if (i > 0) sb.Append(',');
          var s48 = A.Values[i].Unsigned;
          var addr = (int)s48 & ADDR_MASK;
          var len = (int)(s48 >> 24) & 0xFF;
          MappedMemory.Read(buf[..len], addr);
          sb.Append('"');
          foreach (var b in buf[..len])
            sb.Append((char)b);
          sb.Append('"');
        }
        if (A.Width > 1)
          sb.Append(')');
        OnDebugStr(sb.ToString());
      }
      else
        OnDebug(A);
    }
  }
}