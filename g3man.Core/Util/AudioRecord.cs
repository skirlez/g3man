using System.Text;
using UndertaleModLib;

namespace g3man.Core.Util;

public class AudioRecord {
	public uint Version;
	public uint OriginalEntriesCount;
	public byte[] OriginalHash = null!;

	
	private const uint VERSION = 1;
	
	public static byte[] Write(uint originalEntriesCount, byte[] originalHash) {
		MemoryStream s = new();
		BinaryWriter writer = new(s, new ASCIIEncoding());
		writer.Write(['g', '3', 'm', 'a', 'n']);
		writer.Write((short)VERSION);
		writer.Write(originalEntriesCount);
		writer.Write(originalHash);
		return s.GetBuffer();
	}
	
	public static AudioRecord? Read(byte[] bytes) {
		if (bytes.Length < RECORD_SIZE)
			return null;
		try {
			return ReadInternal(bytes);
		}
		catch {
			return null;
		}
	}

	
	private const int RECORD_SIZE = 5 + 2 + 4 + 32;
	private static AudioRecord? ReadInternal(byte[] bytes) {
		BinaryReader binaryReader = new(new MemoryStream(bytes), new ASCIIEncoding());
		string header = new string(binaryReader.ReadChars(5));
		if (header != "g3man")
			return null;
		
		AudioRecord record = new(); 
		record.Version = binaryReader.ReadUInt16();
		if (record.Version < VERSION) {
			// TODO we need to report this properly
			return null;
		}
		record.OriginalEntriesCount = binaryReader.ReadUInt32();
		record.OriginalHash = binaryReader.ReadBytes(32);
		return record;
	}
}