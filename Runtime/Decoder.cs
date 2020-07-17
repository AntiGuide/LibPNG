using System;
using System.IO;
using System.Linq;
using Ionic.Zlib;
using UnityEngine;
using UnityEngine.Profiling;

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
        private static readonly byte[] pngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static bool Decode(this Texture2D tex, Stream fileStream) {
            Profiler.BeginSample("Decode");
            
            var fileStreamBuffer = new byte[8];
            fileStream.Read(fileStreamBuffer, 0, 8);

            // This signature indicates that the remainder of the datastream contains a single PNG image, consisting of a series of chunks beginning with an IHDR chunk and ending with an IEND chunk.
            Debug.Assert(fileStreamBuffer.SequenceEqual(pngSignature), "This doesn't seem to be a PNG");
            bool lastChunk;
            var metadata = new Metadata(checked((int) fileStream.Length));

            do {
                fileStreamBuffer = new byte[4];
                fileStream.Read(fileStreamBuffer, 0, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(fileStreamBuffer);

                var length = BitConverter.ToUInt32(fileStreamBuffer, 0);
                var signedLength = checked((int) length);

                lastChunk = ChunkGenerator.GenerateChunk(fileStream, signedLength, metadata);
            } while (!lastChunk);

            //var memoryStream = new MemoryStream(metadata.Data.ToArray());
            //using (var zlibStream = new ZlibStream(memoryStream, Ionic.Zlib.CompressionMode.Decompress)) {
            metadata.DataStream.Position = 0;
            using (var zlibStream = new ZlibStream(metadata.DataStream, Ionic.Zlib.CompressionMode.Decompress)) {
                var buffer = new byte[zlibStream.BufferSize];
                var uncompressedData = new byte[0];
                while (zlibStream.Read(buffer, 0, zlibStream.BufferSize) != 0) {
                    uncompressedData = uncompressedData.Concat(buffer).ToArray();
                }

                Debug.Assert(uncompressedData.Length >= 1);
                tex.Resize(checked((int) metadata.Width), checked((int) metadata.Height));

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
                
                Profiler.EndSample();
                Profiler.BeginSample("Filtering");
                for (var aktLine = 0; aktLine < metadata.Height; aktLine++) {
                    var filterTypeMarker = aktLine * (metadata.Width * bytesPerPixel + 1);
                    Debug.Assert(uncompressedData[filterTypeMarker] <= 4);
                    switch ((FilterType) uncompressedData[filterTypeMarker]) {
                        case FilterType.NONE:
                            for (var i = 0; i < metadata.Width; i++) {
                                tex.SetPixel(i, (int) metadata.Height - 1 - aktLine,new Color32(uncompressedData[filterTypeMarker + i * bytesPerPixel + 1], uncompressedData[filterTypeMarker + i * bytesPerPixel + 2], uncompressedData[filterTypeMarker + i * bytesPerPixel + 3], bytesPerPixel == 4 ? uncompressedData[filterTypeMarker + i * bytesPerPixel + 4] : byte.MaxValue));
                            }

                            break;
                        case FilterType.SUB:
                            for (var i = 0; i < metadata.Width; i++) {
                                var col = new Color32(uncompressedData[filterTypeMarker + i * bytesPerPixel + 1], uncompressedData[filterTypeMarker + i * bytesPerPixel + 2], uncompressedData[filterTypeMarker + i * bytesPerPixel + 3], bytesPerPixel == 4 ? uncompressedData[filterTypeMarker + i * bytesPerPixel + 4] : byte.MinValue);
                                var oldColor = i == 0 ? new Color32(0, 0, 0, (bytesPerPixel == 4 ? (byte) 0 : Byte.MaxValue)) : (Color32)tex.GetPixel(i - 1, (int) metadata.Height - 1 - aktLine);

                                var tmp = col.Add(oldColor);
                                if (bytesPerPixel != 4) tmp.a = byte.MaxValue;
                                tex.SetPixel(i, (int) metadata.Height - 1 - aktLine, tmp);
                            }

                            break;
                        case FilterType.UP:
                            for (var i = 0; i < metadata.Width; i++) {
                                var col = new Color32(uncompressedData[filterTypeMarker + i * bytesPerPixel + 1], uncompressedData[filterTypeMarker + i * bytesPerPixel + 2], uncompressedData[filterTypeMarker + i * bytesPerPixel + 3], bytesPerPixel == 4 ? uncompressedData[filterTypeMarker + i * bytesPerPixel + 4] : byte.MaxValue);
                                var oldColor = aktLine == 0 ? new Color32(0, 0, 0, 0) : (Color32)tex.GetPixel(i, (int) metadata.Height - 1 -  (aktLine - 1));
                                var tmp = col.Add(oldColor);
                                if (bytesPerPixel != 4) tmp.a = byte.MaxValue;
                                tex.SetPixel(i, (int) metadata.Height - 1 - aktLine, tmp);
                            }

                            break;
                        case FilterType.AVERAGE:
                            for (var i = 0; i < metadata.Width; i++) {
                                var x = new Color32(uncompressedData[filterTypeMarker + i * bytesPerPixel + 1], uncompressedData[filterTypeMarker + i * bytesPerPixel + 2], uncompressedData[filterTypeMarker + i * bytesPerPixel + 3], bytesPerPixel == 4 ? uncompressedData[filterTypeMarker + i * bytesPerPixel + 4] : byte.MaxValue);
                                var a = i == 0 ? new Color32(0, 0, 0, 0) : (Color32)tex.GetPixel(i - 1, (int) metadata.Height - 1 - aktLine);
                                var b = aktLine == 0 ? new Color32(0, 0, 0, 0) : (Color32)tex.GetPixel(i, (int) metadata.Height - 1 -  (aktLine - 1));
                                var tmp = x.Add(a.Average(b));
                                if (bytesPerPixel != 4) tmp.a = byte.MaxValue;
                                tex.SetPixel(i, (int) metadata.Height - 1 - aktLine, tmp);
                            }

                            break;
                        case FilterType.PAETH:
                            // TODO Better architecture. Operate on a byte basis
                            for (var i = 0; i < metadata.Width; i++) {
                                var col = new Color32();
                                for (var k = 0; k < bytesPerPixel; k++) {
                                    var x = uncompressedData[filterTypeMarker + i * bytesPerPixel + k + 1];
                                    var a = i == 0 ? (byte) 0 : ((Color32) tex.GetPixel(i - 1, (int) metadata.Height - 1 - aktLine))[k];
                                    var b = aktLine == 0 ? (byte) 0 : ((Color32)tex.GetPixel(i, (int) metadata.Height - 1 -  (aktLine - 1)))[k];
                                    var c = aktLine == 0 || i == 0 ? (byte) 0 : ((Color32)tex.GetPixel(i - 1, (int) metadata.Height - 1 -  (aktLine - 1)))[k];
                                    
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
                                tex.SetPixel(i, (int) metadata.Height - 1 - aktLine, col);
                            }
                            
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Unknown: {uncompressedData[filterTypeMarker]}");
                    }
                }
                
                Profiler.EndSample();
                Profiler.BeginSample("Applying");
                tex.Apply();
                Profiler.EndSample();
                return true;
            }
        }
    }
}