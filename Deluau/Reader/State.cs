using System.Runtime.InteropServices;
using Deluau.Luau;
using Type = Deluau.Luau.Type;

namespace Deluau.Reader;

public class State
{
    public readonly byte Version;
    public readonly byte TypesVersion;
    
    public readonly Proto[] Protos;
    public readonly uint MainProtoID;
    
    private readonly Buffer _buffer;
    private readonly string[] _strings;

    private string ReadSource()
    {
        int source_index = (int)_buffer.ReadVarInt()-1;
        Console.WriteLine("Source index " + source_index);
        try { return _strings[source_index]; }
        catch { return "unknown"; }
    }
    private Constant ReadConstant()
    {
        Console.WriteLine("Reading constant");
        Constant k = new Constant();
        k.Type = (Type)_buffer.ReadByte();

        switch (k.Type)
        {
            case Type.BOOL:
                k.Value = _buffer.ReadBool();
                break;
            case Type.NUMBER:
                k.Value = _buffer.ReadFloat64();
                break;
            case Type.STRING:
                k.Value = _strings[_buffer.ReadVarInt()-1];
                break;
            case Type.IMPORT:
                k.Value = _buffer.ReadUInt32();
                break;
            case Type.TABLE:
                uint length = _buffer.ReadVarInt();
                uint[] keys = new uint[length];
                for (int table_index = 0; table_index < length; table_index++)
                {
                    keys[table_index] = _buffer.ReadVarInt();
                }

                k.Value = keys;
                break;
            case Type.CLOSURE:
                k.Value = _buffer.ReadVarInt();
                break;
            case Type.VECTOR:
                float[] vector = new float[4];
                for (int vector_index = 0; vector_index < 4; vector_index++)
                {
                    vector[vector_index] = _buffer.ReadFloat32();
                }

                k.Value = vector;
                break;
        }
        
        return k;
    }

    private Proto ReadProto()
    {
        Console.WriteLine("new Proto");
            Proto proto = new Proto();
            proto.MaxStackSize = _buffer.ReadByte();
            proto.NumParams = _buffer.ReadByte();
            proto.NumUpvalues = _buffer.ReadByte();
            proto.IsVarArg = _buffer.ReadBool();
            proto.Flags = _buffer.ReadByte();
            
            uint TypeSize = _buffer.ReadVarInt();
            Console.WriteLine("TypeSize" + TypeSize);
            proto.TypeInfo = new byte[TypeSize];
            for (int info_index = 0; info_index < TypeSize; info_index++)
            {
                proto.TypeInfo[info_index] = _buffer.ReadByte();
            }
            
            proto.SizeOpcodes = _buffer.ReadVarInt();
            Console.WriteLine("SizeOpcode" + proto.SizeOpcodes);
            proto.OpcodeTable = new uint[proto.SizeOpcodes];
            for (int code_index = 0; code_index < proto.SizeOpcodes; code_index++)
            {
                proto.OpcodeTable[code_index] = _buffer.ReadUInt32();
                Console.WriteLine(proto.OpcodeTable[code_index]);
            }
            
            proto.SizeConsts = _buffer.ReadVarInt();
            Console.WriteLine("SizeConsts" + proto.SizeConsts);
            proto.KTable = new Constant[proto.SizeConsts];
            for (int const_index = 0; const_index < proto.SizeConsts; const_index++)
            {
                proto.KTable[const_index] = ReadConstant();
                Console.WriteLine("const_index " + const_index);
                Console.WriteLine("const_type " + (byte)proto.KTable[const_index].Type);
                Console.WriteLine("const_value " + proto.KTable[const_index].Value);
            }
            
            proto.SizeProtos = _buffer.ReadVarInt();
            Console.WriteLine("SizeProtos " + proto.SizeProtos);
            proto.PTable = new Proto[proto.SizeProtos];
            for (int ref_index = 0; ref_index < proto.SizeProtos; ref_index++)
            {
                proto.PTable[ref_index] = Protos[_buffer.ReadVarInt()];
            }
            
            proto.LineDefined = _buffer.ReadVarInt();
            Console.WriteLine("LineDefined " + proto.LineDefined);
            proto.Source = ReadSource();
            Console.WriteLine("Source " + proto.Source);

            if (_buffer.ReadBool()) // read info
            {
                Console.WriteLine("Read info");
                int line_interval = 2 << _buffer.ReadByte()-1;
                byte[] small_info = new byte[proto.SizeOpcodes+1];
                byte last_offset = 0;
                Console.WriteLine("Line interval " + line_interval);
                
                Console.WriteLine("Size Code " + proto.SizeOpcodes);
                for (int info_index = 0; info_index < proto.SizeOpcodes; info_index++)
                {
                    last_offset = (byte) ((last_offset + _buffer.ReadByte()) & 0xFF);
                    Console.WriteLine("Last offset " + last_offset);
                    small_info[info_index] = last_offset;
                }

                int intervals = (int) Math.Floor((float) (proto.SizeOpcodes)/line_interval)+1;
                int[] large_info = new int[intervals];
                int last_line = 0;
                
                Console.WriteLine("interval " + intervals);

                for (int line_index = 0; line_index < intervals; line_index++)
                {
                    last_line = last_line + _buffer.ReadInt32();
                    large_info[line_index] = last_line;
                }


                Console.WriteLine("Size code " + proto.SizeOpcodes);
                proto.InstructionLines = new int[proto.SizeOpcodes];
                for (int info_index = 0; info_index < proto.SizeOpcodes; info_index++)
                {
                    int interval_index = (int) Math.Floor((float) info_index/line_interval);
                    Console.WriteLine("Info index " + info_index);
                    Console.WriteLine("Interval index " + interval_index);
                    proto.InstructionLines[info_index] = large_info[interval_index] + small_info[info_index];
                }
            }

            if (_buffer.ReadBool()) // read locals
            {
                Console.WriteLine("Read locals");
                proto.SizeLocals = _buffer.ReadVarInt();
                proto.Locals = new Local[proto.SizeLocals];
                for (int local_index = 0; local_index < proto.SizeLocals; local_index++)
                {
                    Local local = new Local();
                    local.Name = _strings[_buffer.ReadVarInt()-1];
                    local.Start = _buffer.ReadVarInt();
                    local.End = _buffer.ReadVarInt();
                    local.Register = _buffer.ReadByte();
                    
                    proto.Locals[local_index] = local;
                }

                uint upvalue_size = _buffer.ReadVarInt();
                proto.UpvalueNames = new string[upvalue_size];
                for (int upvalue_index = 0; upvalue_index < upvalue_size; upvalue_index++)
                {
                    proto.UpvalueNames[upvalue_index] = _strings[_buffer.ReadVarInt()-1];
                }
            }

            return proto;
    }
    public State(Stream stream)
    {
        _buffer = new Buffer(stream);
        
        Version = _buffer.ReadByte();
        TypesVersion = _buffer.ReadByte();

        if (Version == 0) throw new Exception("Invalid bytecode");
        if (Version != 6) throw new Exception($"Bytecode version mismatch (expected 6, but got {Version})");
        
        Console.WriteLine($"Version: {Version}");
        Console.WriteLine($"Types version: {TypesVersion}");

        uint string_size = _buffer.ReadVarInt();
        _strings = new string[string_size];
        
        Console.WriteLine(string_size);
        for (int string_index = 0; string_index < string_size; string_index++)
        {
            _strings[string_index] = _buffer.ReadString((int)_buffer.ReadVarInt());
            Console.WriteLine(string_index + _strings[string_index]);
        }
        
        if (TypesVersion >= 3) { while (_buffer.ReadBool()) { } }
        
        uint proto_size = _buffer.ReadVarInt();
        Protos = new Proto[proto_size];
        Console.WriteLine("Proto size: " + proto_size);
        for (int proto_index = 0; proto_index < proto_size; proto_index++)
        {
            Protos[proto_index] = ReadProto();
        }
        MainProtoID = _buffer.ReadVarInt();
        Console.WriteLine(MainProtoID);
    }
}