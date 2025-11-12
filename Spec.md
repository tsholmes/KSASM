# KSA CPU (name TBD)

- no registers (other than PC)
- all instructions take memory addrs/offsets
- all instructions encode data types of arguments
- all (applicable) instructions can operate on up to 8 sequential values
- 64 bit wide instructions?
- 24 bit addresses?

## Instruction Encoding
| Opcodes | Data Width | B Data Type | A Data Type | Operand Mode | Operands |
| :---: | :---: | :---: | :---: | :---: | :---: |
| 63-57 | 56-54 | 53-51 | 50-48 | 47-45 | 44-0 |

### Operand (7 bits)
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
* `0xx` addrA-offB: full address of A, wide offset address of B from A
* `1xx` addrBase-offAB: full base address, narrow offsets of A,B from base
* `xx0` direct-a: first operand address points to the value
* `xx1` indirect-a: first operand address points to a `p24` address of the value
* `x0x` direct-b: second operand address points to the value
* `x1x` indirect-b: second operand address points to a `p24` address of the value

### addrA-offB Operands (45 bits)
* bits 0-23 addrA: full 24-bit address of operand A
* bits 24-44 offB: signed 21-bit offset of operand B from operand A
  * offset is calculated from literal addrA value, prior to indirection

### addrBase-offAB Operands (45 bits)
* bits 0-23 base-address
* bits 24-33 a-offset: signed 10-bit offset of operand A from base
* bits 34-43 b-offset: signed 10-bit offset of operand B from base
* bit 44 base-indirect flag
  * `0` direct-base: operands A,B are offset from literal base-address value
  * `1` indirect-base: base-address is address of `p24` containing base from which A,B are offset

## Data Conversions
TODO

## Opcodes

### Move Operations
* copy: An = Bn
  * copies values at same index from B to A
  * performs type conversion when AType != BType
* reorder: An = A_Bn
  * reorders values in A using B as indices
  * complex B values take real part
  * floating point B values are truncated to integers
  * integers are indexes mod n

### Unary Operations
* bitnot: An = ~Bn
  * performs bitwise-not of values in B, storing result in A
  * if TypeA size is smaller then TypeB, values are truncated
  * if TypeA size is larger than TypeB, values are 0-padded
* negate: An = -Bn
  * performs arithmetic negation of values in B, storing result in A
  * unsigned values are treated as signed values of the same size
* conjugate: An = conj(Bn)
  * gets the complex conjugate of values in B, storing result in A
  * non-complex values are unchanged
* TODO: ipart?
* sign: An = sign(Bn)
  * takes sign of values in B, storing result in A
  * complex values take real part
  * if AType and BType are signed, result is -1 when <0, 0 when =0, 1 when >0
  * otherwise result is 0 when =0, 1 when !=0
* abs: An = |Bn|

### Binary Operations
* bitand: An = An & Bn
* bitor: An = An | Bn
* bitxor: An = An ^ Bn
* shiftleft: An = An << Bn
  * TODO: arith vs logical
* shiftright: An = An >> Bn
* add: An = An + Bn
* subtract: An = An - Bn
* multiply: An = An * Bn
* divide: An = An / Bn
* remainder: An = remainder(An / Bn)
  * value has same sign as An/Bn
* modulus: An = An % Bn
  * value is in range [0, |Bn|)
* power: An = An ** Bn
* max: An = max(An, Bn)
* min: An = min(An, Bn)
* ufpu: An = f_Bn(An)
  * calls a specialized unary function identified by Bn

### Reduce Operations
* all: An = reduce(x & y) for Ai where Bi = n
* any: An = reduce(x | y) for Ai where Bi = n
* parity: An = reduce(x ^ y) for Ai where Bi = n
* sum: An = reduce(x + y) for Ai where Bi = n
* product: An = reduce(x * y) for Ai where Bi = n
* minall An = reduce(min(x,y)) for Ai where Bi = n
* maxall An = reduce(max(x,y)) for Ai where Bi = n

### Branch Operations
* jump: PC = B0
* call: A0 = PC + 8, PC = B0
* branchifzero: if(A0 == 0) PC = B0
* branchifpos: if (A0 > 0) PC = B0
* branchifneg: if (A0 < 0) PC = B0
* switch: if (0 <= A0 < n) PC = B_A0

### Wait Operations
* sleep: pause for B0 cycles (min 1 frame)

### Device Operations
* TODO: figure out what this should actually look like once we have some devices to interact with
* devid: An = devices[Bn].id
* devtype: An = devices[Bn].type
* devread: An = devices[B0].data[An]
* devwrite: devices[B0].sendData(An)

### Interrupt Operations
* TODO: figure out what this should actually look like
* ihandler: Interrupts[Bn].handler = An
* icurrent: An = CurrentInterrupt.Data
  * A0 = interrupt id
  * A1..An = interrupt data
* ireturn: PC = CurrentInterrupt.return

## UFPU
### Rounding functions
- floor(x)
- ceil(x)
- round(x)
- trunc(x)
### Exponential functions
- sqrt(x)
- e^x
- 2^x
- 10^x
- log(x)
- log2(x)
- log10(x)
### Trig functions
- sin(x)
- cos(x)
- tan(x)
- sinh(x)
- cosh(x)
- tanh(x)
### Inverse Trig functions
- asin(x)
- acos(x)
- atan(x)
- asinh(x)
- acosh(x)
- atanh(x)
### Random functions
- rand(x)
  - when x is positive, [0, x)
  - when x is negative, (x, |x|)