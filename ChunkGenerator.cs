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

        public static ChunkType GenerateChunk(ReadOnlySpan<byte> data, int length) {
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
                    new IHDR(chunkData);
                    return ChunkType.IHDR;
                case ChunkType.PLTE:
                    new PLTE(chunkData);
                    return ChunkType.PLTE;
                case ChunkType.IDAT:
                    new IDAT(chunkData);
                    return ChunkType.IDAT;
                case ChunkType.IEND:
                    new IEND(chunkData);
                    return ChunkType.IEND;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}