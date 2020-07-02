using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Ionic.Zlib;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LibPNG {
    public enum ChunkType {
        // Critical Chunks
        IHDR,
        PLTE,
        IDAT,
        IEND,
    }
    
    public enum FilterType : byte {
        NONE = 0,
        SUB = 1,
        UP = 2,
        AVERAGE = 3,
        PAETH = 4,
    }
    
    public static class Decoder {
        private static IHDR ihdr;
        private static List<PLTE> plteChunks = new List<PLTE>();
        private static byte[] data = {};
        private static List<IEND> iendChunks = new List<IEND>();

        public static Image<Rgba32> Decode(FileStream fileStream) {
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
            } while (chunkType != ChunkType.IEND);
            
            Debug.Assert(iendChunks.Count == 1);
            
            Console.WriteLine(ihdr.ToString());
            
            var memoryStream = new MemoryStream(data);
            using var zlibStream = new ZlibStream(memoryStream, CompressionMode.Decompress);
            var buffer = new byte[64];
            var uncompressedData = new byte[0];
            while (zlibStream.Read(buffer, 0, 64) != 0) {
                uncompressedData = uncompressedData.Concat(buffer).ToArray();
            }

            Debug.Assert(uncompressedData.Length >= 1);
            var bmp = new Image<Rgba32>(checked((int)ihdr.Width), checked((int)ihdr.Height));

            for (var aktLine = 0; aktLine < ihdr.Height; aktLine++) {
                var filterTypeMarker = aktLine * (ihdr.Width * 4 + 1);
                Debug.Assert(uncompressedData[filterTypeMarker] <= 4);
                switch ((FilterType)uncompressedData[filterTypeMarker]) {
                    case FilterType.NONE:
                        for (var i = 0; i < ihdr.Width; i++) {
                            bmp[i, aktLine] = new Rgba32(uncompressedData[filterTypeMarker+i*4+1], uncompressedData[filterTypeMarker+i*4+2], uncompressedData[filterTypeMarker+i*4+3], uncompressedData[filterTypeMarker+i*4+4]);
                        }
                        break;
                    case FilterType.SUB:
                        for (var i = 0; i < ihdr.Width; i++) {
                            var col = new Rgba32(uncompressedData[filterTypeMarker+i*4+1], uncompressedData[filterTypeMarker+i*4+2], uncompressedData[filterTypeMarker+i*4+3], uncompressedData[filterTypeMarker+i*4+4]);
                            var oldColor = i == 0 ? new Rgba32(0,0,0,0) : bmp[i - 1, aktLine];
                            bmp[i, aktLine] = col.Add(oldColor);
                        }
                        break;
                    case FilterType.UP:
                        for (var i = 0; i < ihdr.Width; i++) {
                            var col = new Rgba32(uncompressedData[filterTypeMarker+i*4+1], uncompressedData[filterTypeMarker+i*4+2], uncompressedData[filterTypeMarker+i*4+3], uncompressedData[filterTypeMarker+i*4+4]);
                            var oldColor = aktLine == 0 ? new Rgba32(0,0,0,0) : bmp[i, aktLine - 1];
                            bmp[i, aktLine] = col.Add(oldColor);
                        }
                        break;
                    case FilterType.AVERAGE:
                        throw new NotImplementedException($"{FilterType.AVERAGE} is not implemented at the moment");
                        break;
                    case FilterType.PAETH:
                        throw new NotImplementedException($"{FilterType.PAETH} is not implemented at the moment");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return bmp;
        }
        
        public static void AddChunk(IHDR ihdr) => Decoder.ihdr = ihdr;
        public static void AddChunk(PLTE plte) => plteChunks.Add(plte);
        public static void AddChunk(IDAT idat) => data = data.Concat(idat.Data).ToArray();
        public static void AddChunk(IEND iend) => iendChunks.Add(iend);
    }
}