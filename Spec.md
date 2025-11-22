# KSASM

- no registers (other than PC)
- all instructions take memory addrs/offsets
- all instructions encode data types of arguments
- all (applicable) instructions can operate on up to 8 sequential values
- 64 bit wide instructions
- 24 bit addresses

## Instruction Encoding
| Opcode | Data Width | B Data Type | A Data Type | Operand Mode | Operands |
| :---: | :---: | :---: | :---: | :---: | :---: |
| 63-57 | 56-54 | 53-51 | 50-48 | 47-45 | 44-0 |

### Opcode (7 bits)
Specifies which operation to perform

### Data Width (3 bits)
Specifies the number of adjacent values to operate on, offset by 1.
* `000` data width 1
* `001` data width 2
* ...
* `111` data width 8

### Data Type (3 bits)
Specifies the data type of an input or output

* `000` u8 : unsigned byte
* `001` i16: signed int16
* `010` i32: signed int32
* `011` i64: signed int64
* `100` u64: unsigned int64
* `101` f64: double-precision floating point
* `110` p24: 3-byte pointer. unsigned for math operations
* `111` c128: 128-bit complex. real part as f64 in lower bits, imaginary part as f64 in higher bits

### Operand Mode (3 Bits)
Specifies how to interpret the operands
* `0xx` addrAOffB: full address of A, wide offset address of B from A
* `1xx` addrBaseOffAB: full base address, narrow offsets of A,B from base
* `xx0` directA: first operand address points to the value
* `xx1` indirectA: first operand address points to a `p24` address of the value
* `x0x` directB: second operand address points to the value
* `x1x` indirectB: second operand address points to a `p24` address of the value

### addrAOffB Operands (45 bits)
* bits 0-23 `addrA`: full 24-bit address of operand A
* bits 24-44 `offB`: signed 21-bit offset of operand B from operand A
  * offset is calculated from literal addrA value, prior to indirection

### addrBaseOffAB Operands (45 bits)
* bits 0-23 `addrBase`
* bits 24-33 `offA`: signed 10-bit offset of operand A from base
* bits 34-43 `offB`: signed 10-bit offset of operand B from base
* bit 44 baseIndirect flag
  * `0` directBase: operands A,B are offset from `baseAddress`
  * `1` indirectBase: operands A,B are offset from `p24` value in memory at `baseAddress`

## Instruction Execution

### Operands
Operands are resolved to full addresses prior to instruction execution.
`[x]` is the `p24` value at memory address `x`

|               |              | A                 |                     | B                 |                     |
| ------------- | ------------ | ----------------- | ------------------- | ----------------- | ------------------- |
|               |              | directA           | indirectA           | directB           | indirectB           |
| addrAOffB     |              | `addrA`           | `[addrA]`           | `addrB`           | `[addrB]`           |
| addrBaseOffAB | directBase   | `addrBase+offA`   | `[addrBase+offA]`   | `addrBase+offB`   | `[addrBase+offB]`   |
|               | indirectBase | `[addrBase]+offA` | `[[addrBase]+offA]` | `[addrBase]+offB` | `[[addrBase]+offB]` |

### Value Modes
When executing, operand values are read as the data type specified in the instruction and expanded into the largest similar type:
- u8, u64, p24: Unsigned (64-bit unsigned integer)
- i16, i32, i64: Signed (64-bit signed integer)
- f64: Float (64-bit double precision floating point)
- c128: Complex (128-bit pair of double precision floating points)

The operands are then converted between value modes depending on the form of instruction
- `A = op(B)` convert B to A's value mode prior to execution
- `A = op(A, B)` convert B to A's value mode prior to execution
- `An = op(A_Bn)` convert B to Unsigned prior to execution
- `An = reduce(Ai where Bi=n)` convert B to Unsigned prior to execution
- `if (condition(A)) PC = B` convert B to Unsigned prior to execution

Some operations noted below always convert to `Float` before execution, converting back to A's value mode before storing the result.

## Opcodes

* TODO: arithmetic vs logical shift
* TODO: real/imag for getting complex parts
* TODO: device enumeration and info commands? when we have dynamic devices from parts
* TODO: interrupts if they end up being necessary
* TODO: assign static opcode numbers once instruction list is finalized

| Hex | Name         | Alias | Description                             | Notes |
| --- | ------------ | ----- | --------------------------------------- | ----- |
|     | copy         |       | An = Bn                                 | . |
|     | reorder      | swz   | An = A_Bn                               | B -> Unsigned mod n |
|     | bitnot       | not   | An = ~Bn                                | 0-padded when sizeof(AType) > sizeof(Btype) |
|     | negate       | neg   | An = -Bn                                | Unsigned B -> Signed |
|     | conjugate    | conj  | An = conj(Bn)                           | `x + y i` to `x - y i` |
|     | sign         |       | An = sign(Bn)                           | complex takes sign of real part |
|     | abs          |       | An =\|Bn\|                              | . |
|     | bitand       | and   | An = An & Bn                            | . |
|     | bitor        | or    | An = An \| Bn                           | . |
|     | bitxor       | xor   | An = An ^ Bn                            | . |
|     | shiftleft    | shl   | An = An << Bn                           | . |
|     | shiftright   | shr   | An = An >> Bn                           | . |
|     | add          |       | An = An + Bn                            | . |
|     | subtract     | sub   | An = An - Bn                            | . |
|     | multiply     | mul   | An = An * Bn                            | . |
|     | divide       | div   | An = An / Bn                            | . |
|     | remainder    | rem   | An = remainder(An / Bn)                 | `\|An\|` in `[0, \|Bn\|)` with sign `An/Bn` |
|     | modulus      | mod   | An = An % Bn                            | `[0, \|Bn\|)` |
|     | power        | pow   | An = An ** Bn                           | . |
|     | max          |       | An = max(An, Bn)                        | . |
|     | min          |       | An = min(An, Bn)                        | . |
|     | floor        |       | An = floor(Bn)                          | B -> Float |
|     | ceil         |       | An = ceil(x)                            | B -> Float |
|     | round        |       | An = round(Bn)                          | B -> Float |
|     | trunc        |       | An = trunc(Bn)                          | B -> Float |
|     | sqrt         |       | An = sqrt(Bn)                           | B -> Float |
|     | exp          |       | An = e^Bn                               | B -> Float |
|     | pow2         |       | An = 2^Bn                               | B -> Float |
|     | pow10        |       | An = 10^Bn                              | B -> Float |
|     | log          |       | An = log(Bn)                            | B -> Float |
|     | log2         |       | An = log2(Bn)                           | B -> Float |
|     | log10        |       | An = log10(Bn)                          | B -> Float |
|     | sin          |       | An = sin(Bn)                            | B -> Float |
|     | cos          |       | An = cos(Bn)                            | B -> Float |
|     | tan          |       | An = tan(Bn)                            | B -> Float |
|     | sinh         |       | An = sinh(Bn)                           | B -> Float |
|     | cosh         |       | An = cosh(Bn)                           | B -> Float |
|     | tanh         |       | An = tanh(Bn)                           | B -> Float |
|     | asin         |       | An = asin(Bn)                           | B -> Float |
|     | acos         |       | An = acos(Bn)                           | B -> Float |
|     | atan         |       | An = atan(Bn)                           | B -> Float |
|     | asinh        |       | An = asinh(Bn)                          | B -> Float |
|     | acosh        |       | An = acosh(Bn)                          | B -> Float |
|     | atanh        |       | An = atanh(Bn)                          | B -> Float |
|     | rand         |       | An = rand(Bn)                           | - `[0, x)` when `x>0`, `(x, \|x\|)` when `x<0` |
|     | all          | andr  | An = reduce(bitand Ai where Bi=n)       | . |
|     | any          | orr   | An = reduce(bitor Ai where Bi=n)        | . |
|     | parity       | xorr  | An = reduce(bitxor Ai where Bi=n)       | . |
|     | sum          | addr  | An = reduce(add Ai where Bi=n)          | . |
|     | product      | mulr  | An = reduce(multiply Ai where Bi=n)     | . |
|     | minall       | minr  | An = reduce(min Ai where Bi=n)          | . |
|     | maxall       | maxr  | An = reduce(max Ai where Bi=n)          | . |
|     | jump         |       | PC = B0                                 | . |
|     | call         |       | A0 = PC + 8, PC = B0                    | . |
|     | branchifzero | bzero | if(A0 == 0) PC = B0                     | . |
|     | branchifpos  | bpos  | if (A0 > 0) PC = B0                     | . |
|     | branchifneg  | bneg  | if (A0 < 0) PC = B0                     | . |
|     | switch       |       | if (0 <= A0 < n) PC = B_A0              | . |
|     | sleep        |       | pause for B0 cycles (min 1 frame)       | . |
|     | devmap       |       | mem[A0..A0+A1] => devices[B0].mem[B1..] | set B0=0 to unmap range (B1 ignored) |
| 7E  | debug        |       | debug output An, Bn                     | . |
| 7F  | debugstr     |       | output string mem[An..An+Bn]            | . |
