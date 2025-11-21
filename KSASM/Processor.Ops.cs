
using System;
using System.Collections.Generic;

namespace KSASM
{
  public partial class Processor
  {
    private delegate void UnaryOp(ValueOps ops, ref Value b);
    private void UnaryBToA(ValuePointer opA, ValuePointer opB, UnaryOp op)
    {
      var mode = opA.Type.VMode();
      var ops = mode.Ops();

      Memory.Read(opB, B);
      B.Convert(mode);

      for (var i = 0; i < B.Width; i++)
        op(ops, ref B.Values[i]);

      Memory.Write(opA, B);
    }

    private delegate void BinaryOp(ValueOps ops, ref Value a, Value b);
    private void BinaryBToA(ValuePointer opA, ValuePointer opB, BinaryOp op)
    {
      var mode = opA.Type.VMode();
      var ops = mode.Ops();

      Memory.Read(opA, A);
      Memory.Read(opB, B);
      B.Convert(mode);

      for (var i = 0; i < A.Width; i++)
        op(ops, ref A.Values[i], B.Values[i]);

      Memory.Write(opA, A);
    }

    private void ReduceAAtB(ValuePointer opA, ValuePointer opB, BinaryOp op)
    {
      var mode = opA.Type.VMode();
      var ops = mode.Ops();

      Memory.Read(opA, A);
      Memory.Read(opB, B);
      B.Convert(ValueMode.Unsigned);
      C.Init(mode, A.Width);

      Span<bool> used = stackalloc bool[8];

      for (var i = 0; i < A.Width; i++)
      {
        var n = (int)(B.Values[i].Unsigned & 0x7);
        if (used[n])
          op(ops, ref C.Values[n], A.Values[i]);
        else
        {
          used[n] = true;
          C.Values[n] = A.Values[i];
        }
      }

      Memory.Write(opA, C);
    }

    private void OpCopy(ValuePointer opA, ValuePointer opB) => UnaryBToA(opA, opB, (ops, ref b) => { });

    private void OpReorder(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opA, A);
      Memory.Read(opB, B);
      B.Convert(ValueMode.Unsigned);
      C.Init(A.Mode, A.Width);

      for (var i = 0; i < A.Width; i++)
        C.Values[i] = A.Values[(int)B.Values[i].Unsigned & 0x7];

      Memory.Write(opA, C);
    }

    private void OpBitNot(ValuePointer opA, ValuePointer opB) =>
      UnaryBToA(opA, opB, (ops, ref b) => ops.BitNot(ref b));
    private void OpNegate(ValuePointer opA, ValuePointer opB) =>
      UnaryBToA(opA, opB, (ops, ref b) => ops.Negate(ref b));

    private void OpConjugate(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();

    private void OpSign(ValuePointer opA, ValuePointer opB) =>
      UnaryBToA(opA, opB, (ops, ref b) => ops.Sign(ref b));
    private void OpAbs(ValuePointer opA, ValuePointer opB) =>
      UnaryBToA(opA, opB, (ops, ref b) => ops.Abs(ref b));
    private void OpBitAnd(ValuePointer opA, ValuePointer opB) =>
      BinaryBToA(opA, opB, (ops, ref A, B) => ops.BitAnd(ref A, B));
    private void OpBitOr(ValuePointer opA, ValuePointer opB) =>
      BinaryBToA(opA, opB, (ops, ref A, B) => ops.BitOr(ref A, B));
    private void OpBitXor(ValuePointer opA, ValuePointer opB) =>
      BinaryBToA(opA, opB, (ops, ref A, B) => ops.BitXor(ref A, B));

    private void OpShiftLeft(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpShiftRight(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();

    private void OpAdd(ValuePointer opA, ValuePointer opB) =>
      BinaryBToA(opA, opB, (ops, ref A, B) => ops.Add(ref A, B));
    private void OpSubtract(ValuePointer opA, ValuePointer opB) =>
      BinaryBToA(opA, opB, (ops, ref A, B) => ops.Sub(ref A, B));
    private void OpMultiply(ValuePointer opA, ValuePointer opB) =>
      BinaryBToA(opA, opB, (ops, ref A, B) => ops.Mul(ref A, B));
    private void OpDivide(ValuePointer opA, ValuePointer opB) =>
      BinaryBToA(opA, opB, (ops, ref A, B) => ops.Div(ref A, B));

    private void OpRemainder(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpModulus(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpPower(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();

    private void OpMax(ValuePointer opA, ValuePointer opB) =>
      BinaryBToA(opA, opB, (ops, ref A, B) => ops.Max(ref A, B));
    private void OpMin(ValuePointer opA, ValuePointer opB) =>
      BinaryBToA(opA, opB, (ops, ref A, B) => ops.Min(ref A, B));

    private void OpFloor(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpCeil(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpRound(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpTrunc(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpSqrt(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpExp(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpPow2(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpPow10(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpLog(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpLog2(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpLog10(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpSin(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpCos(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpTan(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpSinh(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpCosh(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpTanh(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpAsin(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpAcos(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpAtan(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpAsinh(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpAcosh(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpAtanh(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();

    private void OpRand(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();

    private void OpAll(ValuePointer opA, ValuePointer opB) =>
      ReduceAAtB(opA, opB, (ops, ref A, B) => ops.BitAnd(ref A, B));
    private void OpAny(ValuePointer opA, ValuePointer opB) =>
      ReduceAAtB(opA, opB, (ops, ref A, B) => ops.BitOr(ref A, B));
    private void OpParity(ValuePointer opA, ValuePointer opB) =>
      ReduceAAtB(opA, opB, (ops, ref A, B) => ops.BitXor(ref A, B));
    private void OpSum(ValuePointer opA, ValuePointer opB) =>
      ReduceAAtB(opA, opB, (ops, ref A, B) => ops.Add(ref A, B));
    private void OpProduct(ValuePointer opA, ValuePointer opB) =>
      ReduceAAtB(opA, opB, (ops, ref A, B) => ops.Mul(ref A, B));
    private void OpMinAll(ValuePointer opA, ValuePointer opB) =>
      ReduceAAtB(opA, opB, (ops, ref A, B) => ops.Min(ref A, B));
    private void OpMaxAll(ValuePointer opA, ValuePointer opB) =>
      ReduceAAtB(opA, opB, (ops, ref A, B) => ops.Max(ref A, B));

    private void OpJump(ValuePointer opA, ValuePointer opB)
    {
      PC = opB.Address;
    }

    private void OpCall(ValuePointer opA, ValuePointer opB)
    {
      opA.Width = 1;
      A.Init(ValueMode.Unsigned, 1);
      A.Values[0].Unsigned = (ulong)PC;
      Memory.Write(opA, A);

      PC = opB.Address;
    }

    private void OpBranchIfSign(ValuePointer opA, ValuePointer opB, int sign)
    {
      opA.Width = opB.Width = 1;
      Memory.Read(opA, A);

      if (A.Mode.Ops().GetSign(A.Values[0]) == sign)
        PC = opB.Address;
    }

    private void OpSwitch(ValuePointer opA, ValuePointer opB)
    {
      opA.Width = 1;
      Memory.Read(opA, A);
      Memory.Read(opB, B);
      A.Convert(ValueMode.Unsigned);
      B.Convert(ValueMode.Unsigned);
      var idx = A.Values[0].Unsigned;
      if (idx < (ulong)B.Width)
        PC = (int)B.Values[(int)idx].Unsigned;
    }

    private void OpSleep(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opB, B);
      B.Convert(ValueMode.Unsigned);
      SleepTime = B.Values[0].Unsigned;
      if (SleepTime == 0)
        SleepTime = 1;
    }

    private void OpDevMap(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opA, A);
      Memory.Read(opB, B);

      A.Convert(ValueMode.Unsigned);
      B.Convert(ValueMode.Unsigned);

      var memAddr = (int)A.Values[0].Unsigned;
      var memLen = (int)A.Values[1].Unsigned;
      var devId = B.Values[0].Unsigned;
      var devAddr = (int)B.Values[1].Unsigned;

      if (devId == 0)
        MappedMemory.MapRange(memAddr, MainMemory, memAddr, memLen);
      else
      {
        var device = deviceMap.GetValueOrDefault(devId) ?? defaultDevice;
        MappedMemory.MapRange(memAddr, device, devAddr, memLen);
      }
    }

    private void OpDebug(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opA, A);
      Memory.Read(opB, B);
      OnDebug?.Invoke(A, B);
    }

    private void OpDebugStr(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opA, A);
      Memory.Read(opB, B);
      A.Convert(ValueMode.Unsigned);
      B.Convert(ValueMode.Unsigned);

      Span<byte> buf = stackalloc byte[256];

      for (var i = 0; i < A.Width; i++)
      {
        var addr = (int)A.Values[i].Unsigned;
        var len = (int)(B.Values[i].Unsigned & 0xFF);

        MappedMemory.Read(buf[..len], addr);

        OnDebugStr?.Invoke(System.Text.Encoding.ASCII.GetString(buf[..len]));
      }
    }
  }
}