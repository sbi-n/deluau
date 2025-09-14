namespace Deluau.Luau;

// Luau/Luau-Lang에서 일부 발췌
// https://github.com/luau-lang/luau/blob/b066e4c8f8851d9727d079935cfa2e0f9b108531/Common/include/Luau/Bytecode.h

// This header contains the bytecode definition for Luau interpreter
// Creating the bytecode is outside the scope of this file and is handled by bytecode builder (BytecodeBuilder.h) and bytecode compiler (Compiler.h)
// Note that ALL enums declared in this file are order-sensitive since the values are baked into bytecode that needs to be processed by legacy clients.

// Bytecode definitions
// Bytecode instructions are using "word code" - each instruction is one or many 32-bit words.
// The first word in the instruction is always the instruction header, and *must* contain the opcode (enum below) in the least significant byte.
//
// Instruction word can be encoded using one of the following encodings:
//     ABC - least-significant byte for the opcode, followed by three bytes, A, B and C; each byte declares a register index, small index into some other table or an unsigned integral value
//     AD - least-significant byte for the opcode, followed by A byte, followed by D half-word (16-bit integer). D is a signed integer that commonly specifies constant table index or jump offset
//     E - least-significant byte for the opcode, followed by E (24-bit integer). E is a signed integer that commonly specifies a jump offset
//
// Instruction word is sometimes followed by one extra word, indicated as AUX - this is just a 32-bit word and is decoded according to the specification for each opcode.
// For each opcode the encoding is *static* - that is, based on the opcode you know a-priory how large the instruction is, with the exception of NEWCLOSURE

// Bytecode indices
// Bytecode instructions commonly refer to integer values that define offsets or indices for various entities. For each type, there's a maximum encodable value.
// Note that in some cases, the compiler will set a lower limit than the maximum encodable value is to prevent fragile code into bumping against the limits whenever we change the compilation details.
// Additionally, in some specific instructions such as ANDK, the limit on the encoded value is smaller; this means that if a value is larger, a different instruction must be selected.
//
// Registers: 0-254. Registers refer to the values on the function's stack frame, including arguments.
// Upvalues: 0-254. Upvalues refer to the values stored in the closure object.
// Constants: 0-2^23-1. Constants are stored in a table allocated with each proto; to allow for future bytecode tweaks the encodable value is limited to 23 bits.
// Closures: 0-2^15-1. Closures are created from child protos via a child index; the limit is for the number of closures immediately referenced in each function.
// Jumps: -2^23..2^23. Jump offsets are specified in word increments, so jumping over an instruction may sometimes require an offset of 2 or more.
public enum LuauOpcode
{
    // NOP: noop
    NOP,

    // BREAK: debugger break
    BREAK,

    // LOADNIL: sets register to nil
    // A: target register
    LOADNIL,

    // LOADB: sets register to boolean and jumps to a given short offset (used to compile comparison results into a boolean)
    // A: target register
    // B: value (0/1)
    // C: jump offset
    LOADB,

    // LOADN: sets register to a number literal
    // A: target register
    // D: value (-32768..32767)
    LOADN,

    // LOADK: sets register to an entry from the constant table from the proto (number/string)
    // A: target register
    // D: constant table index (0..32767)
    LOADK,

    // MOVE: move (copy) value from one register to another
    // A: target register
    // B: source register
    MOVE,

    // GETGLOBAL: load value from global table using constant string as a key
    // A: target register
    // C: predicted slot index (based on hash)
    // AUX: constant table index
    GETGLOBAL,

    // SETGLOBAL: set value in global table using constant string as a key
    // A: source register
    // C: predicted slot index (based on hash)
    // AUX: constant table index
    SETGLOBAL,

    // GETUPVAL: load upvalue from the upvalue table for the current function
    // A: target register
    // B: upvalue index (0..255)
    GETUPVAL,

    // SETUPVAL: store value into the upvalue table for the current function
    // A: target register
    // B: upvalue index (0..255)
    SETUPVAL,

    // CLOSEUPVALS: close (migrate to heap) all upvalues that were captured for registers >= target
    // A: target register
    CLOSEUPVALS,

    // GETIMPORT: load imported global table global from the constant table
    // A: target register
    // D: constant table index (0..32767); we assume that imports are loaded into the constant table
    // AUX: 3 10-bit indices of constant strings that, combined, constitute an import path; length of the path is set by the top 2 bits (1,2,3)
    GETIMPORT,

    // GETTABLE: load value from table into target register using key from register
    // A: target register
    // B: table register
    // C: index register
    GETTABLE,

    // SETTABLE: store source register into table using key from register
    // A: source register
    // B: table register
    // C: index register
    SETTABLE,

    // GETTABLEKS: load value from table into target register using constant string as a key
    // A: target register
    // B: table register
    // C: predicted slot index (based on hash)
    // AUX: constant table index
    GETTABLEKS,

    // SETTABLEKS: store source register into table using constant string as a key
    // A: source register
    // B: table register
    // C: predicted slot index (based on hash)
    // AUX: constant table index
    SETTABLEKS,

    // GETTABLEN: load value from table into target register using small integer index as a key
    // A: target register
    // B: table register
    // C: index-1 (index is 1..256)
    GETTABLEN,

    // SETTABLEN: store source register into table using small integer index as a key
    // A: source register
    // B: table register
    // C: index-1 (index is 1..256)
    SETTABLEN,

    // NEWCLOSURE: create closure from a child proto; followed by a CAPTURE instruction for each upvalue
    // A: target register
    // D: child proto index (0..32767)
    NEWCLOSURE,

    // NAMECALL: prepare to call specified method by name by loading function from source register using constant index into target register and copying source register into target register + 1
    // A: target register
    // B: source register
    // C: predicted slot index (based on hash)
    // AUX: constant table index
    // Note that this instruction must be followed directly by CALL; it prepares the arguments
    // This instruction is roughly equivalent to GETTABLEKS + MOVE pair, but we need a special instruction to support custom __namecall metamethod
    NAMECALL,

    // CALL: call specified function
    // A: register where the function object lives, followed by arguments; results are placed starting from the same register
    // B: argument count + 1, or 0 to preserve all arguments up to top (MULTRET)
    // C: result count + 1, or 0 to preserve all values and adjust top (MULTRET)
    CALL,

    // RETURN: returns specified values from the function
    // A: register where the returned values start
    // B: number of returned values + 1, or 0 to return all values up to top (MULTRET)
    RETURN,

    // JUMP: jumps to target offset
    // D: jump offset (-32768..32767; 0 means "next instruction" aka "don't jump")
    JUMP,

    // JUMPBACK: jumps to target offset; this is equivalent to JUMP but is used as a safepoint to be able to interrupt while/repeat loops
    // D: jump offset (-32768..32767; 0 means "next instruction" aka "don't jump")
    JUMPBACK,

    // JUMPIF: jumps to target offset if register is not nil/false
    // A: source register
    // D: jump offset (-32768..32767; 0 means "next instruction" aka "don't jump")
    JUMPIF,

    // JUMPIFNOT: jumps to target offset if register is nil/false
    // A: source register
    // D: jump offset (-32768..32767; 0 means "next instruction" aka "don't jump")
    JUMPIFNOT,

    // JUMPIFEQ, JUMPIFLE, JUMPIFLT, JUMPIFNOTEQ, JUMPIFNOTLE, JUMPIFNOTLT: jumps to target offset if the comparison is true (or false, for NOT variants)
    // A: source register 1
    // D: jump offset (-32768..32767; 0 means "next instruction" aka "don't jump")
    // AUX: source register 2
    JUMPIFEQ,
    JUMPIFLE,
    JUMPIFLT,
    JUMPIFNOTEQ,
    JUMPIFNOTLE,
    JUMPIFNOTLT,

    // ADD, SUB, MUL, DIV, MOD, POW: compute arithmetic operation between two source registers and put the result into target register
    // A: target register
    // B: source register 1
    // C: source register 2
    ADD,
    SUB,
    MUL,
    DIV,
    MOD,
    POW,

    // ADDK, SUBK, MULK, DIVK, MODK, POWK: compute arithmetic operation between the source register and a constant and put the result into target register
    // A: target register
    // B: source register
    // C: constant table index (0..255)
    ADDK,
    SUBK,
    MULK,
    DIVK,
    MODK,
    POWK,

    // AND, OR: perform `and` or `or` operation (selecting first or second register based on whether the first one is truthy) and put the result into target register
    // A: target register
    // B: source register 1
    // C: source register 2
    AND,
    OR,

    // ANDK, ORK: perform `and` or `or` operation (selecting source register or constant based on whether the source register is truthy) and put the result into target register
    // A: target register
    // B: source register
    // C: constant table index (0..255)
    ANDK,
    ORK,

    // CONCAT: concatenate all strings between B and C (inclusive) and put the result into A
    // A: target register
    // B: source register start
    // C: source register end
    CONCAT,

    // NOT, MINUS, LENGTH: compute unary operation for source register and put the result into target register
    // A: target register
    // B: source register
    NOT,
    MINUS,
    LENGTH,

    // NEWTABLE: create table in target register
    // A: target register
    // B: table size, stored as 0 for v=0 and ceil(log2(v))+1 for v!=0
    // AUX: array size
    NEWTABLE,

    // DUPTABLE: duplicate table using the constant table template to target register
    // A: target register
    // D: constant table index (0..32767)
    DUPTABLE,

    // SETLIST: set a list of values to table in target register
    // A: target register
    // B: source register start
    // C: value count + 1, or 0 to use all values up to top (MULTRET)
    // AUX: table index to start from
    SETLIST,

    // FORNPREP: prepare a numeric for loop, jump over the loop if first iteration doesn't need to run
    // A: target register; numeric for loops assume a register layout [limit, step, index, variable]
    // D: jump offset (-32768..32767)
    // limit/step are immutable, index isn't visible to user code since it's copied into variable
    FORNPREP,

    // FORNLOOP: adjust loop variables for one iteration, jump back to the loop header if loop needs to continue
    // A: target register; see FORNPREP for register layout
    // D: jump offset (-32768..32767)
    FORNLOOP,

    // FORGLOOP: adjust loop variables for one iteration of a generic for loop, jump back to the loop header if loop needs to continue
    // A: target register; generic for loops assume a register layout [generator, state, index, variables...]
    // D: jump offset (-32768..32767)
    // AUX: variable count (1..255)
    // loop variables are adjusted by calling generator(state, index) and expecting it to return a tuple that's copied to the user variables
    // the first variable is then copied into index; generator/state are immutable, index isn't visible to user code
    FORGLOOP,

    // FORGPREP_INEXT/FORGLOOP_INEXT: FORGLOOP with 2 output variables (no AUX encoding), assuming generator is luaB_inext
    // FORGPREP_INEXT prepares the index variable and jumps to FORGLOOP_INEXT
    // FORGLOOP_INEXT has identical encoding and semantics to FORGLOOP (except for AUX encoding)
    FORGPREP_INEXT,
    FORGLOOP_INEXT,

    // FORGPREP_NEXT/FORGLOOP_NEXT: FORGLOOP with 2 output variables (no AUX encoding), assuming generator is luaB_next
    // FORGPREP_NEXT prepares the index variable and jumps to FORGLOOP_NEXT
    // FORGLOOP_NEXT has identical encoding and semantics to FORGLOOP (except for AUX encoding)
    FORGPREP_NEXT,
    FORGLOOP_NEXT,

    // GETVARARGS: copy variables into the target register from vararg storage for current function
    // A: target register
    // B: variable count + 1, or 0 to copy all variables and adjust top (MULTRET)
    GETVARARGS,

    // DUPCLOSURE: create closure from a pre-created function object (reusing it unless environments diverge)
    // A: target register
    // D: constant table index (0..32767)
    DUPCLOSURE,

    // PREPVARARGS: prepare stack for variadic functions so that GETVARARGS works correctly
    // A: number of fixed arguments
    PREPVARARGS,

    // LOADKX: sets register to an entry from the constant table from the proto (number/string)
    // A: target register
    // AUX: constant table index
    LOADKX,

    // JUMPX: jumps to the target offset; like JUMPBACK, supports interruption
    // E: jump offset (-2^23..2^23; 0 means "next instruction" aka "don't jump")
    JUMPX,

    // FASTCALL: perform a fast call of a built-in function
    // A: builtin function id (see LuauBuiltinFunction)
    // C: jump offset to get to following CALL
    // FASTCALL is followed by one of (GETIMPORT, MOVE, GETUPVAL) instructions and by CALL instruction
    // This is necessary so that if FASTCALL can't perform the call inline, it can continue normal execution
    // If FASTCALL *can* perform the call, it jumps over the instructions *and* over the next CALL
    // Note that FASTCALL will read the actual call arguments, such as argument/result registers and counts, from the CALL instruction
    FASTCALL,

    // COVERAGE: update coverage information stored in the instruction
    // E: hit count for the instruction (0..2^23-1)
    // The hit count is incremented by VM every time the instruction is executed, and saturates at 2^23-1
    COVERAGE,

    // CAPTURE: capture a local or an upvalue as an upvalue into a newly created closure; only valid after NEWCLOSURE
    // A: capture type, see LuauCaptureType
    // B: source register (for VAL/REF) or upvalue index (for UPVAL/UPREF)
    CAPTURE,

    // JUMPIFEQK, JUMPIFNOTEQK: jumps to target offset if the comparison with constant is true (or false, for NOT variants)
    // A: source register 1
    // D: jump offset (-32768..32767; 0 means "next instruction" aka "don't jump")
    // AUX: constant table index
    JUMPIFEQK,
    JUMPIFNOTEQK,

    // FASTCALL1: perform a fast call of a built-in function using 1 register argument
    // A: builtin function id (see LuauBuiltinFunction)
    // B: source argument register
    // C: jump offset to get to following CALL
    FASTCALL1,

    // FASTCALL2: perform a fast call of a built-in function using 2 register arguments
    // A: builtin function id (see LuauBuiltinFunction)
    // B: source argument register
    // C: jump offset to get to following CALL
    // AUX: source register 2 in least-significant byte
    FASTCALL2,

    // FASTCALL2K: perform a fast call of a built-in function using 1 register argument and 1 constant argument
    // A: builtin function id (see LuauBuiltinFunction)
    // B: source argument register
    // C: jump offset to get to following CALL
    // AUX: constant index
    FASTCALL2K,

    // FORGPREP: prepare loop variables for a generic for loop, jump to the loop backedge unconditionally
    // A: target register; generic for loops assume a register layout [generator, state, index, variables...]
    // D: jump offset (-32768..32767)
    FORGPREP,

    // Enum entry for number of opcodes, not a valid opcode by itself!
    COUNT
};