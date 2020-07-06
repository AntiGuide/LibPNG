using System;
using System.Buffers.Binary;
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
        tEXt,
        zTXt,
        iTXt,
    }
    
    public enum FilterType : byte {
        NONE = 0,
        SUB = 1,
        UP = 2,
        AVERAGE = 3,
        PAETH = 4,
    }
    
    public static class Decoder {
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
            bool lastChunk;
            var metadata = new Metadata();
            
            do {
                var length = BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(fileStreamBuffer, offset, 4));
                var signedLength = checked((int)length);
                offset += 4;

                lastChunk = ChunkGenerator.GenerateChunk(new ReadOnlySpan<byte>(fileStreamBuffer, offset, 8 + signedLength), signedLength, metadata);
                offset += 8 + signedLength;
            } while (!lastChunk);
            
            var memoryStream = new MemoryStream(metadata.Data.ToArray());
            using var zlibStream = new ZlibStream(memoryStream, CompressionMode.Decompress);
            var buffer = new byte[64];
            var uncompressedData = new byte[0];
            while (zlibStream.Read(buffer, 0, 64) != 0) {
                uncompressedData = uncompressedData.Concat(buffer).ToArray();
            }

            Debug.Assert(uncompressedData.Length >= 1);
            var bmp = new Image<Rgba32>(checked((int)metadata.Width), checked((int)metadata.Height));

            for (var aktLine = 0; aktLine < metadata.Height; aktLine++) {
                var filterTypeMarker = aktLine * (metadata.Width * 4 + 1);
                Debug.Assert(uncompressedData[filterTypeMarker] <= 4);
                switch ((FilterType)uncompressedData[filterTypeMarker]) {
                    case FilterType.NONE:
                        for (var i = 0; i < metadata.Width; i++) {
                            bmp[i, aktLine] = new Rgba32(uncompressedData[filterTypeMarker+i*4+1], uncompressedData[filterTypeMarker+i*4+2], uncompressedData[filterTypeMarker+i*4+3], uncompressedData[filterTypeMarker+i*4+4]);
                        }
                        break;
                    case FilterType.SUB:
                        for (var i = 0; i < metadata.Width; i++) {
                            var col = new Rgba32(uncompressedData[filterTypeMarker+i*4+1], uncompressedData[filterTypeMarker+i*4+2], uncompressedData[filterTypeMarker+i*4+3], uncompressedData[filterTypeMarker+i*4+4]);
                            var oldColor = i == 0 ? new Rgba32(0,0,0,0) : bmp[i - 1, aktLine];
                            bmp[i, aktLine] = col.Add(oldColor);
                        }
                        break;
                    case FilterType.UP:
                        for (var i = 0; i < metadata.Width; i++) {
                            var col = new Rgba32(uncompressedData[filterTypeMarker+i*4+1], uncompressedData[filterTypeMarker+i*4+2], uncompressedData[filterTypeMarker+i*4+3], uncompressedData[filterTypeMarker+i*4+4]);
                            var oldColor = aktLine == 0 ? new Rgba32(0,0,0,0) : bmp[i, aktLine - 1];
                            bmp[i, aktLine] = col.Add(oldColor);
                        }
                        break;
                    case FilterType.AVERAGE:
                        for (var i = 0; i < metadata.Width; i++) {
                            var x = new Rgba32(uncompressedData[filterTypeMarker+i*4+1], uncompressedData[filterTypeMarker+i*4+2], uncompressedData[filterTypeMarker+i*4+3], uncompressedData[filterTypeMarker+i*4+4]);
                            var a = i == 0 ? new Rgba32(0,0,0,0) : bmp[i - 1, aktLine];
                            var b = aktLine == 0 ? new Rgba32(0,0,0,0) : bmp[i, aktLine - 1];
                            bmp[i, aktLine] = x.Add(a.Average(b));
                        }
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
    }
}