using System.Text;

namespace Deluau.Reader;

public class Buffer(Stream stream)
{
    private readonly BinaryReader _reader = new BinaryReader(stream);
    
    public short ReadInt16()
    {
        return _reader.ReadInt16();
    }

    public int ReadInt32()
    {
        return _reader.ReadInt32();
    }

    public ushort ReadUInt16()
    {
        return _reader.ReadUInt16();
    }

    public uint ReadUInt32()
    {
        return _reader.ReadUInt32();
    }

    public float ReadFloat32()
    {
        return _reader.ReadSingle();
    }
    
    public double ReadFloat64()
    {
        return _reader.ReadDouble();
    }

    public byte ReadByte()
    {
        return _reader.ReadByte();
    }

    public bool ReadBool()
    {
        return _reader.ReadBoolean();
    }

    public uint ReadVarInt()
    {
        uint result = 0;
        int shift = 0;

        byte b;
        do
        {
            b = ReadByte();
            result |= (uint)((b & 127) << shift);
            shift += 7;
        } while ((b & 128) > 0);

        return result;
    }

    public string ReadString(int length)
    {
        return Encoding.ASCII.GetString(_reader.ReadBytes(length));
    }
}