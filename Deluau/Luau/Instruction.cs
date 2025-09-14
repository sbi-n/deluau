namespace Deluau.Luau;

public class Instruction(uint value)
{
    private readonly uint _value = value;
    
    public byte OP => (byte)((_value) & 0xFF);
    
    public byte A => (byte)((_value >> 8) & 0xFF);
    public byte B => (byte)((_value >> 16) & 0xFF);
    public byte C => (byte)((_value >> 24) & 0xFF);

    public int D => (int)_value >> 16;
    
    public int E => (int)_value >> 8;
}