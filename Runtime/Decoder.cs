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
        private static long posR;

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

            var tmpScanlineColor = new Color32[metadata.Width];
            
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

                uint bytesPerPixel;
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
                Profiler.BeginSample("Filtering1");
                
                for (uint aktLine = 0; aktLine < metadata.Height; aktLine++) {
                    var firstRowBytePosition = aktLine * (metadata.Width * bytesPerPixel + 1);
                    Color32[] lineColors = null;
                    switch ((FilterType) uncompressedData[firstRowBytePosition]) {
                        case FilterType.NONE:
                            FilterLineNone(uncompressedData, firstRowBytePosition, metadata.Width, bytesPerPixel, ref tmpScanlineColor);
                            break;
                        case FilterType.SUB:
                            FilterLineSub(uncompressedData, firstRowBytePosition, metadata.Width, bytesPerPixel, ref tmpScanlineColor);
                            lineColors = tmpScanlineColor;
                            break;
                        case FilterType.UP:
                            FilterLineUp(uncompressedData, firstRowBytePosition, metadata.Width, bytesPerPixel, ref tmpScanlineColor);
                            lineColors = tmpScanlineColor;
                            break;
                        case FilterType.AVERAGE:
                            FilterLineAverage(uncompressedData, firstRowBytePosition, metadata.Width, bytesPerPixel, ref tmpScanlineColor);
                            lineColors = tmpScanlineColor;
                            break;
                        case FilterType.PAETH:
                            FilterLinePaeth(uncompressedData, firstRowBytePosition, metadata.Width, bytesPerPixel, ref tmpScanlineColor);
                            lineColors = tmpScanlineColor;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    
                    Debug.Assert(lineColors != null);
                    tex.SetPixels32(0, checked((int)(metadata.Height - 1 - aktLine)), checked((int)metadata.Width), 1, lineColors);
                }

                Profiler.EndSample();
                Profiler.BeginSample("Applying");
                tex.Apply();
                Profiler.EndSample();
                return true;
            }
        }

        private static void FilterLineNone(in byte[] data, uint offset, uint width, uint bpp, ref Color32[] tmpScanlineColor) {
            for (var position = 0; position < width; position++) {
                posR = offset + 1 + position * bpp;
                tmpScanlineColor[position] = new Color32(data[posR], data[posR + 1], data[posR + 2], bpp == 4 ? data[posR + 3] : byte.MaxValue);
            }
        }
        
        private static void FilterLineSub(in byte[] data, uint offset, uint width, uint bpp, ref Color32[] tmpScanlineColor) {
            for (var position = offset + 1; position <= width * bpp + offset; position++) {
                var left = position - offset > bpp ? data[position - bpp] : 0;
                data[position] = (byte) (data[position] + left);
                if ((position - offset) % bpp != 0 || position == offset + 1) continue;
                
                var posR = position - bpp + 1;
                tmpScanlineColor[(posR - offset - 1) / bpp] = new Color32(data[posR], data[posR + 1], data[posR + 2], bpp == 4 ? data[posR + 3] : byte.MaxValue);
            }
        }
        
        private static void FilterLineUp(in byte[] data, uint offset, uint width, uint bpp, ref Color32[] tmpScanlineColor) {
            for (var position = offset + 1; position <= width * bpp + offset; position++) {
                var current = data[position];
                var back = width * bpp + 1;
                var above = position > back ? data[position - back] : 0;
                data[position] = (byte) (current + above);
                if ((position - offset) % bpp != 0 || position == offset + 1) continue;
                
                var posR = position - bpp + 1;
                tmpScanlineColor[(posR - offset - 1) / bpp] = new Color32(data[posR], data[posR + 1], data[posR + 2], bpp == 4 ? data[posR + 3] : byte.MaxValue);
            }
        }
        
        private static void FilterLineAverage(in byte[] data, uint offset, uint width, uint bpp, ref Color32[] tmpScanlineColor) {
            for (var position = offset + 1; position <= width * bpp + offset; position++) {
                var current = data[position];
                var left = position - offset > bpp ? data[position - bpp] : 0;
                var back = width * bpp + 1;
                var above = position > back ? data[position - back] : 0;
                data[position] = (byte) (current + (byte) ((left + above) / 2));
                if ((position - offset) % bpp != 0 || position == offset + 1) continue;
                
                var posR = position - bpp + 1;
                tmpScanlineColor[(posR - offset - 1) / bpp] = new Color32(data[posR], data[posR + 1], data[posR + 2], bpp == 4 ? data[posR + 3] : byte.MaxValue);
            }
        }
        
        private static void FilterLinePaeth(in byte[] data, uint offset, uint width, uint bpp, ref Color32[] tmpScanlineColor) {
            for (var position = offset + 1; position <= width * bpp + offset; position++) {
                var current = data[position];
                var left = position - offset > bpp ? data[position - bpp] : 0;
                var back = width * bpp + 1;
                var above = position > back ? data[position - back] : 0;
                //back += bpp;
                var aboveleft = position - offset > bpp ? data[position - back - bpp] : 0;
                int tmp;
                tmp = above - aboveleft;
                var pa = -tmp > tmp ? -tmp : tmp;
                tmp = left - aboveleft;
                var pb = -tmp > tmp ? -tmp : tmp;
                tmp = left + above - aboveleft - aboveleft;
                var pc = -tmp > tmp ? -tmp : tmp;
                if (pa <= pb && pa <= pc) {
                    data[position] = (byte)(current + left);
                } else if (pb <= pc) {
                    data[position] = (byte)(current + above);
                }else {
                    data[position] = (byte)(current + aboveleft);
                }
                
                if ((position - offset) % bpp != 0 || position == offset + 1) continue;
                
                var posR = position - bpp + 1;
                tmpScanlineColor[(posR - offset - 1) / bpp] = new Color32(data[posR], data[posR + 1], data[posR + 2], bpp == 4 ? data[posR + 3] : byte.MaxValue);
            }
        }
    }
}