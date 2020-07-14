using System;
using System.IO;
using System.Linq;
using Ionic.Zlib;
using UnityEngine;
using CompressionMode = System.IO.Compression.CompressionMode;

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
        public static Texture2D Decode(Stream fileStream) {
            var fileStreamLength = checked((int) fileStream.Length);
            var fileStreamBuffer = new byte[fileStreamLength];
            fileStream.Read(fileStreamBuffer, 0, 8);

            // This signature indicates that the remainder of the datastream contains a single PNG image, consisting of a series of chunks beginning with an IHDR chunk and ending with an IEND chunk.
            var isPNG = fileStreamBuffer[0] == 137 && fileStreamBuffer[1] == 80 && fileStreamBuffer[2] == 78 && fileStreamBuffer[3] == 71 && fileStreamBuffer[4] == 13 && fileStreamBuffer[5] == 10 && fileStreamBuffer[6] == 26 && fileStreamBuffer[7] == 10;
            Debug.Assert(isPNG, "This doesn't seem to be a PNG");
            bool lastChunk;
            var metadata = new Metadata();

            do {
                fileStreamBuffer = new byte[4];
                fileStream.Read(fileStreamBuffer, 0, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(fileStreamBuffer);

                var length = BitConverter.ToUInt32(fileStreamBuffer, 0);
                var signedLength = checked((int) length);

                lastChunk = ChunkGenerator.GenerateChunk(fileStream, signedLength, metadata);
            } while (!lastChunk);

            var memoryStream = new MemoryStream(metadata.Data.ToArray());
            using (var zlibStream = new ZlibStream(memoryStream, Ionic.Zlib.CompressionMode.Decompress)) {
                var buffer = new byte[4096];
                var uncompressedData = new byte[0];
                while (zlibStream.Read(buffer, 0, 4096) != 0) {
                    uncompressedData = uncompressedData.Concat(buffer).ToArray();
                }

                Debug.Assert(uncompressedData.Length >= 1);
                var bmp = new Texture2D(checked((int) metadata.Width), checked((int) metadata.Height));

                int bytesPerPixel;
                switch (metadata.ColourType) {
                    case Metadata.ColourTypeEnum.GREYSCALE:
                        bytesPerPixel = 1;
                        throw new NotImplementedException();
                    case Metadata.ColourTypeEnum.TRUECOLOUR:
                        bytesPerPixel = 3;
                        break;
                    case Metadata.ColourTypeEnum.INDEXED_COLOUR:
                        bytesPerPixel = 1;
                        throw new NotImplementedException();
                    case Metadata.ColourTypeEnum.GREYSCALE_WITH_ALPHA:
                        bytesPerPixel = 2;
                        throw new NotImplementedException();
                    case Metadata.ColourTypeEnum.TRUECOLOUR_WITH_ALPHA:
                        bytesPerPixel = 4;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                for (var aktLine = 0; aktLine < metadata.Height; aktLine++) {
                    var filterTypeMarker = aktLine * (metadata.Width * bytesPerPixel + 1);
                    Debug.Assert(uncompressedData[filterTypeMarker] <= 4);
                    switch ((FilterType) uncompressedData[filterTypeMarker]) {
                        case FilterType.NONE:
                            for (var i = 0; i < metadata.Width; i++) {
                                bmp.SetPixel(i, (int) metadata.Height - 1 - aktLine,new Color32(uncompressedData[filterTypeMarker + i * bytesPerPixel + 1], uncompressedData[filterTypeMarker + i * bytesPerPixel + 2], uncompressedData[filterTypeMarker + i * bytesPerPixel + 3], bytesPerPixel == 4 ? uncompressedData[filterTypeMarker + i * bytesPerPixel + 4] : byte.MaxValue));
                            }

                            break;
                        case FilterType.SUB:
                            for (var i = 0; i < metadata.Width; i++) {
                                var col = new Color32(uncompressedData[filterTypeMarker + i * bytesPerPixel + 1], uncompressedData[filterTypeMarker + i * bytesPerPixel + 2], uncompressedData[filterTypeMarker + i * bytesPerPixel + 3], bytesPerPixel == 4 ? uncompressedData[filterTypeMarker + i * bytesPerPixel + 4] : byte.MinValue);
                                var oldColor = i == 0 ? new Color32(0, 0, 0, (bytesPerPixel == 4 ? (byte) 0 : Byte.MaxValue)) : (Color32)bmp.GetPixel(i - 1, (int) metadata.Height - 1 - aktLine);

                                var tmp = col.Add(oldColor);
                                if (bytesPerPixel != 4) tmp.a = byte.MaxValue;
                                bmp.SetPixel(i, (int) metadata.Height - 1 - aktLine, tmp);
                            }

                            break;
                        case FilterType.UP:
                            for (var i = 0; i < metadata.Width; i++) {
                                var col = new Color32(uncompressedData[filterTypeMarker + i * bytesPerPixel + 1], uncompressedData[filterTypeMarker + i * bytesPerPixel + 2], uncompressedData[filterTypeMarker + i * bytesPerPixel + 3], bytesPerPixel == 4 ? uncompressedData[filterTypeMarker + i * bytesPerPixel + 4] : byte.MaxValue);
                                var oldColor = aktLine == 0 ? new Color32(0, 0, 0, 0) : (Color32)bmp.GetPixel(i, (int) metadata.Height - 1 -  (aktLine - 1));
                                var tmp = col.Add(oldColor);
                                if (bytesPerPixel != 4) tmp.a = byte.MaxValue;
                                bmp.SetPixel(i, (int) metadata.Height - 1 - aktLine, tmp);
                            }

                            break;
                        case FilterType.AVERAGE:
                            for (var i = 0; i < metadata.Width; i++) {
                                var x = new Color32(uncompressedData[filterTypeMarker + i * bytesPerPixel + 1], uncompressedData[filterTypeMarker + i * bytesPerPixel + 2], uncompressedData[filterTypeMarker + i * bytesPerPixel + 3], bytesPerPixel == 4 ? uncompressedData[filterTypeMarker + i * bytesPerPixel + 4] : byte.MaxValue);
                                var a = i == 0 ? new Color32(0, 0, 0, 0) : (Color32)bmp.GetPixel(i - 1, (int) metadata.Height - 1 - aktLine);
                                var b = aktLine == 0 ? new Color32(0, 0, 0, 0) : (Color32)bmp.GetPixel(i, (int) metadata.Height - 1 -  (aktLine - 1));
                                var tmp = x.Add(a.Average(b));
                                if (bytesPerPixel != 4) tmp.a = byte.MaxValue;
                                bmp.SetPixel(i, (int) metadata.Height - 1 - aktLine, tmp);
                            }

                            break;
                        case FilterType.PAETH:
                            // TODO Better architecture. Operate on a byte basis
                            for (var i = 0; i < metadata.Width; i++) {
                                var col = new Color32();
                                for (var k = 0; k < bytesPerPixel; k++) {
                                    var x = uncompressedData[filterTypeMarker + i * bytesPerPixel + k + 1];
                                    var a = i == 0 ? (byte) 0 : ((Color32) bmp.GetPixel(i - 1, (int) metadata.Height - 1 - aktLine))[k];
                                    var b = aktLine == 0 ? (byte) 0 : ((Color32)bmp.GetPixel(i, (int) metadata.Height - 1 -  (aktLine - 1)))[k];
                                    var c = aktLine == 0 || i == 0 ? (byte) 0 : ((Color32)bmp.GetPixel(i - 1, (int) metadata.Height - 1 -  (aktLine - 1)))[k];
                                    
                                    var p = a + b - c;
                                    var pa = Rgba32Extension.Abs(p - a);
                                    var pb = Rgba32Extension.Abs(p - b);
                                    var pc = Rgba32Extension.Abs(p - c);

                                    if (pa <= pb && pa <= pc) {
                                        col[k] = (byte)(x + a);
                                    } else if (pb <= pc) {
                                        col[k] = (byte)(x + b);
                                    }else {
                                        col[k] = (byte)(x + c);
                                    }
                                }

                                if (bytesPerPixel != 4) col.a = byte.MaxValue;
                                bmp.SetPixel(i, (int) metadata.Height - 1 - aktLine, col);
                            }
                            
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Unknown: {uncompressedData[filterTypeMarker]}");
                    }
                }
                
                bmp.Apply();
                return bmp;
            }
        }
    }
}