using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LibPNG {
    public static class ChunkGenerator {
        private static readonly Dictionary<byte[], ChunkType> chunkTypeAssociation = new Dictionary<byte[], ChunkType> {
            {new byte[] {73, 72, 68, 82}, ChunkType.IHDR},
            {new byte[] {80, 76, 84, 69}, ChunkType.PLTE},
            {new byte[] {73, 68, 65, 84}, ChunkType.IDAT},
            {new byte[] {73, 69, 78, 68}, ChunkType.IEND},
            {new byte[] {116, 69, 88, 116}, ChunkType.tEXt},
            {new byte[] {122, 84, 88, 116}, ChunkType.zTXt},
            {new byte[] {105, 84, 88, 116}, ChunkType.iTXt},
        };

        /// <returns>Returns if this was the last chunk</returns>
        public static bool GenerateChunk(Stream stream, int length, Metadata metadata) {
            //Chunk Type
            var chunkTypeSpan = new byte[4];
            stream.Read(chunkTypeSpan, 0, 4);
            var crcData = new List<byte>();
            crcData.AddRange(chunkTypeSpan);
            
            ChunkType? chunkType = null;
            foreach (var cta in chunkTypeAssociation.Where(cta => chunkTypeSpan.SequenceEqual(cta.Key))) {
                chunkType = cta.Value;
                break;
            }

            var isCriticalChunk = (chunkTypeSpan[0] & 32) == 0;
            var isPublicChunk = (chunkTypeSpan[1] & 32) == 0;
            var isCompatiblePNGVersion = (chunkTypeSpan[2] & 32) == 0;
            var isSafeToCopy = (chunkTypeSpan[3] & 32) == 1;

            var chunkData = new byte[length];
            stream.Read(chunkData, 0, length);
            crcData.AddRange(chunkData);
            
            // CRC
            var crcValueData = new byte[4];
            stream.Read(crcValueData, 0, 4);
            if (BitConverter.IsLittleEndian) Array.Reverse(crcValueData);
            var expectedCRC = BitConverter.ToUInt32(crcValueData, 0);
            var calculatedCRC = CRC32.Calculate(crcData);
            Debug.Assert(expectedCRC == calculatedCRC, "CRC check failed. The data seems to be damaged.");
            
            // Chunk Data
            switch (chunkType) {
                case ChunkType.IHDR:
                    IHDR.Read(chunkData, metadata);
                    return false;
                case ChunkType.PLTE:
                    PLTE.Read(chunkData, metadata);
                    return false;
                case ChunkType.IDAT:
                    IDAT.Read(chunkData, metadata);
                    return false;
                case ChunkType.IEND:
                    IEND.Read(chunkData, metadata);
                    return true;
                default:
                    if (isCriticalChunk) throw new Exception($"{nameof(chunkType)} matched no known types. Type was {System.Text.Encoding.ASCII.GetString(chunkTypeSpan)}");

                    return false;
            }
        }
    }
}