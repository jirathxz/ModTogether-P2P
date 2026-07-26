using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ModTogether.Plugins.MHWilds.Services
{
    public static class MurmurHash3
    {
        public static uint Hash32(byte[] data, uint seed = 0xFFFFFFFF)
        {
            uint h1 = seed;
            int length = data.Length;
            int nblocks = length / 4;

            for (int i = 0; i < nblocks; i++)
            {
                uint k1 = BitConverter.ToUInt32(data, i * 4);
                k1 *= 0xcc9e2d51;
                k1 = (k1 << 15) | (k1 >> 17);
                k1 *= 0x1b873593;

                h1 ^= k1;
                h1 = (h1 << 13) | (h1 >> 19);
                h1 = h1 * 5 + 0xe6546b64;
            }

            int tailOffset = nblocks * 4;
            uint k2 = 0;
            int tailLength = length & 3;

            if (tailLength >= 3) k2 ^= (uint)data[tailOffset + 2] << 16;
            if (tailLength >= 2) k2 ^= (uint)data[tailOffset + 1] << 8;
            if (tailLength >= 1)
            {
                k2 ^= (uint)data[tailOffset];
                k2 *= 0xcc9e2d51;
                k2 = (k2 << 15) | (k2 >> 17);
                k2 *= 0x1b873593;
                h1 ^= k2;
            }

            h1 ^= (uint)length;
            h1 ^= h1 >> 16;
            h1 *= 0x85ebca6b;
            h1 ^= h1 >> 13;
            h1 *= 0xc2b2ae35;
            h1 ^= h1 >> 16;

            return h1;
        }
    }

    public class RePakPacker
    {
        private class EntryInfo
        {
            public string SourcePath { get; set; } = string.Empty;
            public string RelativePath { get; set; } = string.Empty;
            public uint HashLower { get; set; }
            public uint HashUpper { get; set; }
            public long Offset { get; set; }
            public long Size { get; set; }
        }

        public static void CreatePakFromDirectory(string sourceFolder, string outputPath)
        {
            var files = Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories);
            var entries = new List<EntryInfo>();

            foreach (var file in files)
            {
                string relPath = file.Substring(sourceFolder.Length).TrimStart('\\', '/');
                int nativesIdx = relPath.IndexOf("natives", StringComparison.OrdinalIgnoreCase);
                if (nativesIdx >= 0)
                {
                    relPath = relPath.Substring(nativesIdx);
                }
                relPath = relPath.Replace('\\', '/');

                string lowerPath = relPath.ToLowerInvariant();
                string upperPath = relPath.ToUpperInvariant();

                byte[] lowerBytes = Encoding.Unicode.GetBytes(lowerPath);
                byte[] upperBytes = Encoding.Unicode.GetBytes(upperPath);

                var fi = new FileInfo(file);

                entries.Add(new EntryInfo
                {
                    SourcePath = file,
                    RelativePath = relPath,
                    HashLower = MurmurHash3.Hash32(lowerBytes),
                    HashUpper = MurmurHash3.Hash32(upperBytes),
                    Size = fi.Length
                });
            }

            // Sort entries by HashLower for fast binary search inside RE Engine
            entries = entries.OrderBy(e => e.HashLower).ThenBy(e => e.HashUpper).ToList();

            // Calculate offsets
            // Header: 16 bytes (Magic 4B, Major 4B, Minor 4B, Flags 4B)
            // Index Header: 8 bytes (EntryCount 4B, Reserved 4B)
            // Entries: entries.Count * 48 bytes
            long dataOffset = 16 + 8 + (entries.Count * 48);

            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].Offset = dataOffset;
                dataOffset += entries[i].Size;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs);

            // 1. Write Header (16 bytes)
            bw.Write(new char[] { 'K', 'P', 'K', 'A' }); // Magic
            bw.Write((uint)4); // Major version 4 (RE Engine / MH Wilds)
            bw.Write((uint)0); // Minor version 0
            bw.Write((uint)0); // Feature flags

            // 2. Write Index Header (8 bytes)
            bw.Write((uint)entries.Count);
            bw.Write((uint)0);

            // 3. Write Entries Table (48 bytes per entry)
            foreach (var entry in entries)
            {
                bw.Write(entry.HashLower);
                bw.Write(entry.HashUpper);
                bw.Write(entry.Offset);
                bw.Write(entry.Size); // CompressedSize
                bw.Write(entry.Size); // UncompressedSize
                bw.Write((long)0);    // CompressionType: 0 = Uncompressed
                bw.Write((ulong)0);   // Checksum
            }

            // 4. Write Data Payload
            foreach (var entry in entries)
            {
                using var input = new FileStream(entry.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                input.CopyTo(fs);
            }

            bw.Flush();
        }
    }
}
