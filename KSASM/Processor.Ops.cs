
using System;
using System.Collections.Generic;

namespace KSASM
{
  public partial class Processor
  {
    private void UnaryBToA(ValuePointer opA, ValuePointer opB, out ValueOps ops)
    {
      var mode = opA.Type.VMode();
      ops = mode.Ops();

      Memory.Read(opB, B);
      B.Convert(mode);
    }

    private void BinaryBToA(ValuePointer opA, ValuePointer opB, out ValueOps ops)
    {
      var mode = opA.Type.VMode();
      ops = mode.Ops();

      Memory.Read(opA, A);
      Memory.Read(opB, B);
      B.Convert(mode);
    }

    private void ReduceAAtB(ValuePointer opA, ValuePointer opB, out ValueOps ops)
    {
      var mode = opA.Type.VMode();
      ops = mode.Ops();

      Memory.Read(opA, A);
      Memory.Read(opB, B);
      B.Convert(ValueMode.Unsigned);
      C.Init(mode, A.Width);
    }

    private void OpCopy(ValuePointer opA, ValuePointer opB)
    {
      UnaryBToA(opA, opB, out _);

      Memory.Write(opA, B);
    }

    private void OpReorder(ValuePointer opA, ValuePointer opB)
    {
      ReduceAAtB(opA, opB, out var ops);

      for (var i = 0; i < A.Width; i++)
        ops.Add(ref C.Values[i], A.Values[(int)B.Values[i].Unsigned]);

      Memory.Write(opA, C);
    }

    private void OpBitNot(ValuePointer opA, ValuePointer opB)
    {
      UnaryBToA(opA, opB, out var ops);

      for (var i = 0; i < B.Width; i++)
        ops.BitNot(ref B.Values[i]);

      Memory.Write(opA, B);
    }

    private void OpNegate(ValuePointer opA, ValuePointer opB)
    {
      UnaryBToA(opA, opB, out var ops);

      for (var i = 0; i < B.Width; i++)
        ops.Negate(ref B.Values[i]);

      Memory.Write(opA, B);
    }

    private void OpConjugate(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();

    private void OpSign(ValuePointer opA, ValuePointer opB)
    {
      UnaryBToA(opA, opB, out var ops);

      for (var i = 0; i < B.Width; i++)
        ops.Sign(ref B.Values[i]);

      Memory.Write(opA, B);
    }

    private void OpAbs(ValuePointer opA, ValuePointer opB)
    {
      UnaryBToA(opA, opB, out var ops);

      for (var i = 0; i < B.Width; i++)
        ops.Abs(ref B.Values[i]);

      Memory.Write(opA, B);
    }

    private void OpBitAnd(ValuePointer opA, ValuePointer opB)
    {
      BinaryBToA(opA, opB, out var ops);

      for (var i = 0; i < B.Width; i++)
        ops.BitAnd(ref A.Values[i], B.Values[i]);

      Memory.Write(opA, A);
    }

    private void OpBitOr(ValuePointer opA, ValuePointer opB)
    {
      BinaryBToA(opA, opB, out var ops);

      for (var i = 0; i < B.Width; i++)
        ops.BitOr(ref A.Values[i], B.Values[i]);

      Memory.Write(opA, A);
    }

    private void OpBitXor(ValuePointer opA, ValuePointer opB)
    {
      BinaryBToA(opA, opB, out var ops);

      for (var i = 0; i < B.Width; i++)
        ops.BitXor(ref A.Values[i], B.Values[i]);

      Memory.Write(opA, A);
    }

    private void OpShiftLeft(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpShiftRight(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();

    private void OpAdd(ValuePointer opA, ValuePointer opB)
    {
      BinaryBToA(opA, opB, out var ops);

      for (var i = 0; i < A.Width; i++)
        ops.Add(ref A.Values[i], B.Values[i]);

      Memory.Write(opA, A);
    }

    private void OpSubtract(ValuePointer opA, ValuePointer opB)
    {
      BinaryBToA(opA, opB, out var ops);

      for (var i = 0; i < A.Width; i++)
        ops.Sub(ref A.Values[i], B.Values[i]);

      Memory.Write(opA, A);
    }

    private void OpMultiply(ValuePointer opA, ValuePointer opB)
    {
      BinaryBToA(opA, opB, out var ops);

      for (var i = 0; i < A.Width; i++)
        ops.Mul(ref A.Values[i], B.Values[i]);

      Memory.Write(opA, A);
    }

    private void OpDivide(ValuePointer opA, ValuePointer opB)
    {
      BinaryBToA(opA, opB, out var ops);

      for (var i = 0; i < A.Width; i++)
        ops.Div(ref A.Values[i], B.Values[i]);
    }

    private void OpRemainder(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpModulus(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpPower(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpMax(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpMin(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpUFpu(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpAll(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpAny(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpParity(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpSum(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpProduct(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpMinAll(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpMaxAll(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();

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

    private void OpSwitch(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();

    private void OpSleep(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opB, B);
      B.Convert(ValueMode.Unsigned);
      SleepTime = B.Values[0].Unsigned;
      if (SleepTime == 0)
        SleepTime = 1;
    }

    private void OpDevID(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpDevType(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();

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

    private void OpIHandler(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpIData(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();
    private void OpIReturn(ValuePointer opA, ValuePointer opB) => throw new NotImplementedException();

    private void OpDebug(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opA, A);
      Memory.Read(opB, B);
      OnDebug?.Invoke(A, B);
    }
  }
}