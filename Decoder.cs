using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;

namespace LibPNG {
    public static class Decoder {
        public static Bitmap Decode(FileStream fileStream) {
            var fileStreamLength = checked((int)fileStream.Length);
            var fileStreamBuffer = new byte[fileStreamLength];
            var fileStreamBytesReadCount = fileStream.Read(fileStreamBuffer, 0, fileStreamLength);
            
            // This signature indicates that the remainder of the datastream contains a single PNG image, consisting of a series of chunks beginning with an IHDR chunk and ending with an IEND chunk.
            var isPNG = fileStreamBuffer[0] == 137 &&
                        fileStreamBuffer[1] == 80 &&
                        fileStreamBuffer[2] == 78 &&
                        fileStreamBuffer[3] == 71 &&
                        fileStreamBuffer[4] == 13 &&
                        fileStreamBuffer[5] == 10 &&
                        fileStreamBuffer[6] == 26 &&
                        fileStreamBuffer[7] == 10;
            Debug.Assert(isPNG, "This doesn't seem to be a PNG");
            
            var offset = 8;
            ChunkType chunkType;
            
            do {
                var length = BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(fileStreamBuffer, offset, 4));
                var signedLength = checked((int)length);
                offset += 4;

                chunkType = ChunkGenerator.GenerateChunk(new ReadOnlySpan<byte>(fileStreamBuffer, offset, 8 + signedLength), signedLength);
                offset += 8 + signedLength;
                Console.WriteLine(chunkType);
            } while (chunkType != ChunkType.IEND);
            
            return new Bitmap();
        }
    }

    public enum ChunkType {
        // Critical Chunks
        IHDR,
        PLTE,
        IDAT,
        IEND,
    }
}