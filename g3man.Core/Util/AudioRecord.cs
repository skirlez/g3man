using System.Security.Cryptography;
using System.Text;
using UndertaleModLib;
using UndertaleModLib.Models;

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
		
		return s.ToArray();
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

	
	private const int RECORD_SIZE = 5 + 2 + 4 + 16;
	private static AudioRecord? ReadInternal(byte[] bytes) {
		BinaryReader binaryReader = new(new MemoryStream(bytes), new ASCIIEncoding());
		string header = new(binaryReader.ReadChars(5));
		if (header != "g3man")
			return null;
		
		AudioRecord record = new(); 
		record.Version = binaryReader.ReadUInt16();
		if (record.Version < VERSION) {
			// TODO we need to report this properly
			return null;
		}
		record.OriginalEntriesCount = binaryReader.ReadUInt32();
		record.OriginalHash = binaryReader.ReadBytes(16);
		return record;
	}


	public static byte[] Hash(IList<UndertaleEmbeddedAudio> audio) {
		IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
		foreach (UndertaleEmbeddedAudio entry in audio)
			md5.AppendData(entry.Data);
		return md5.GetCurrentHash();
	}
}