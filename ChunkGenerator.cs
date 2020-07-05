using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;

namespace LibPNG {
    public static class ChunkGenerator {
        private static readonly Dictionary<byte[], ChunkType> chunkTypeAssociation = new Dictionary<byte[], ChunkType> {
            {new byte[] {73, 72, 68, 82}, ChunkType.IHDR},
            {new byte[] {80, 76, 84, 69}, ChunkType.PLTE},
            {new byte[] {73, 68, 65, 84}, ChunkType.IDAT},
            {new byte[] {73, 69, 78, 68}, ChunkType.IEND},
        };

        /// <returns>Returns if this was the last chunk</returns>
        public static bool GenerateChunk(ReadOnlySpan<byte> data, int length, Metadata metadata) {
            //Chunk Type
            var chunkTypeSpan = data.Slice(0, 4);
            
            ChunkType? chunkType = null;
            foreach (var (key, value) in chunkTypeAssociation) {
                if (chunkTypeSpan.SequenceEqual(key)) chunkType = value;
            }
            
            Debug.Assert(chunkType != null, $"{nameof(chunkType)} matched no known types");

            var isCriticalChunk = (chunkTypeSpan[0] & 32) == 0;
            var isPublicChunk = (chunkTypeSpan[1] & 32) == 0;
            var isCompatiblePNGVersion = (chunkTypeSpan[2] & 32) == 0;
            var isSafeToCopy = (chunkTypeSpan[3] & 32) == 1;
            
            // CRC
            var expectedCRC = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4 + length, 4));
            var calculatedCRC = CRC32.Calculate(data.Slice(0, 4 + length));
            Debug.Assert(expectedCRC == calculatedCRC, "CRC check failed. The data seems to be damaged.");
            
            // Chunk Data
            var chunkData = data.Slice(4, length);
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
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}