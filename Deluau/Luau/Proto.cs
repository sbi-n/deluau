using System.Runtime.InteropServices.JavaScript;

namespace Deluau.Luau;

public struct Local
{
    public string Name;
    public uint Start;
    public uint End;
    public byte Register;
}

public struct Constant
{
    public Type Type;
    public object Value;
}
public struct Proto
{
    public byte MaxStackSize;
    public byte NumParams;
    public byte NumUpvalues;
    public byte Flags;
    public bool IsVarArg;

    public uint LineDefined;
    public string Source;
    public byte[] TypeInfo;
    public int[] InstructionLines;
    
    public uint SizeOpcodes;
    public uint[] OpcodeTable;

    public uint SizeLocals;
    public Local[] Locals;
    
    public uint SizeConsts;
    public Constant[] KTable;

    public uint SizeProtos;
    public Proto[] PTable;

    public string[] UpvalueNames;
}