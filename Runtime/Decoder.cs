using System;
using System.IO;
using Ionic.Zlib;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

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

    public struct Decoder : IJob {
        //private readonly NativeArray<byte> pngSignature = new NativeArray<byte>(new byte[]{ 137, 80, 78, 71, 13, 10, 26, 10 }, Allocator.Temp);

        [ReadOnly] public NativeArray<byte> FileData;
        
        public NativeList<byte> RawTextureData;
        public NativeArray<int> Width;
        public NativeArray<int> Height;
        
        private int posR;

        public void Execute() {
            //var fileStreamBuffer = FileData.Slice(0, 8);
            
            // This signature indicates that the remainder of the datastream contains a single PNG image, consisting of a series of chunks beginning with an IHDR chunk and ending with an IEND chunk.
            //Debug.Assert(fileStreamBuffer.SequenceEqual(pngSignature), "This doesn't seem to be a PNG");
            bool lastChunk;
            var metadata = new Metadata(FileData.Length);
            var offset = 8;
            do {
                var length = BitConverterBigEndian.ToUInt32(FileData.Slice(offset,4));
                var signedLength = checked((int) length);

                var nativeSlice = FileData.Slice(offset + 4, signedLength + 8);
                lastChunk = ChunkGenerator.GenerateChunk(nativeSlice, signedLength, metadata);
                offset += 12 + signedLength;
            } while (!lastChunk);

            var tmpScanlineColor = new Color32[metadata.Width];
            
            metadata.Data.Position = 0;
            NativeArray<byte> uncompressedData;
            
            using (var zlibStream = new ZlibStream(metadata.Data, Ionic.Zlib.CompressionMode.Decompress)) {
                using (var memoryStream = new MemoryStream()) {
                    zlibStream.CopyTo(memoryStream);
                    uncompressedData = new NativeArray<byte>(memoryStream.ToArray(), Allocator.Temp);
                }
            }
            
            Debug.Assert(uncompressedData.Length >= 1);

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

            Width[0] = checked((int) metadata.Width);
            Height[0] = checked((int) metadata.Height);
            RawTextureData.Resize(Width[0] * Height[0] * 4, NativeArrayOptions.UninitializedMemory);
            
            for (var aktLine = 0; aktLine < metadata.Height; aktLine++) {
                var firstRowBytePosition = aktLine * (Width[0] * bytesPerPixel + 1);
                Color32[] lineColors = null;
                switch ((FilterType) uncompressedData[firstRowBytePosition]) {
                    case FilterType.NONE:
                        FilterLineNone(uncompressedData, firstRowBytePosition, Width[0], bytesPerPixel, ref tmpScanlineColor);
                        break;
                    case FilterType.SUB:
                        FilterLineSub(ref uncompressedData, firstRowBytePosition, Width[0], bytesPerPixel, ref tmpScanlineColor);
                        lineColors = tmpScanlineColor;
                        break;
                    case FilterType.UP:
                        FilterLineUp(ref uncompressedData, firstRowBytePosition, Width[0], bytesPerPixel, ref tmpScanlineColor);
                        lineColors = tmpScanlineColor;
                        break;
                    case FilterType.AVERAGE:
                        FilterLineAverage(ref uncompressedData, firstRowBytePosition, Width[0], bytesPerPixel, ref tmpScanlineColor);
                        lineColors = tmpScanlineColor;
                        break;
                    case FilterType.PAETH:
                        FilterLinePaeth(ref uncompressedData, firstRowBytePosition, Width[0], bytesPerPixel, ref tmpScanlineColor);
                        lineColors = tmpScanlineColor;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                Debug.Assert(lineColors != null);
                if (bytesPerPixel == 4) {
                    for (var i = 0; i < lineColors.Length; i++) {
                        RawTextureData[(int)((metadata.Height - 1 - aktLine) * metadata.Width * 4 + i * 4)] = lineColors[i].r;
                        RawTextureData[(int)((metadata.Height - 1 - aktLine) * metadata.Width * 4 + i * 4 + 1)] = lineColors[i].g;
                        RawTextureData[(int)((metadata.Height - 1 - aktLine) * metadata.Width * 4 + i * 4 + 2)] = lineColors[i].b;
                        RawTextureData[(int)((metadata.Height - 1 - aktLine) * metadata.Width * 4 + i * 4 + 3)] = lineColors[i].a;
                    }
                } else {
                    for (var i = 0; i < lineColors.Length; i++) {
                        RawTextureData[(int)((metadata.Height - 1 - aktLine) * metadata.Width * 4 + i * 4)] = lineColors[i].r;
                        RawTextureData[(int)((metadata.Height - 1 - aktLine) * metadata.Width * 4 + i * 4 + 1)] = lineColors[i].g;
                        RawTextureData[(int)((metadata.Height - 1 - aktLine) * metadata.Width * 4 + i * 4 + 2)] = lineColors[i].b;
                        RawTextureData[(int)((metadata.Height - 1 - aktLine) * metadata.Width * 4 + i * 4 + 3)] = byte.MaxValue;
                    }
                }
            }
        }

        private void FilterLineNone(in NativeArray<byte> data, int offset, int width, int bpp, ref Color32[] tmpScanlineColor) {
            for (var position = 0; position < width; position++) {
                posR = offset + 1 + position * bpp;
                tmpScanlineColor[position] = new Color32(data[posR], data[posR + 1], data[posR + 2], bpp == 4 ? data[posR + 3] : byte.MaxValue);
            }
        }
        
        private void FilterLineSub(ref NativeArray<byte> data, int offset, int width, int bpp, ref Color32[] tmpScanlineColor) {
            for (var position = offset + 1; position <= width * bpp + offset; position++) {
                var left = position - offset > bpp ? data[position - bpp] : 0;
                data[position] = (byte) (data[position] + left);
                if ((position - offset) % bpp != 0 || position == offset + 1) continue;
                
                var posR = position - bpp + 1;
                tmpScanlineColor[(posR - offset - 1) / bpp] = new Color32(data[posR], data[posR + 1], data[posR + 2], bpp == 4 ? data[posR + 3] : byte.MaxValue);
            }
        }
        
        private void FilterLineUp(ref NativeArray<byte> data, int offset, int width, int bpp, ref Color32[] tmpScanlineColor) {
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
        
        private void FilterLineAverage(ref NativeArray<byte> data, int offset, int width, int bpp, ref Color32[] tmpScanlineColor) {
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
        
        private void FilterLinePaeth(ref NativeArray<byte> data, int offset, int width, int bpp, ref Color32[] tmpScanlineColor) {
            for (var position = offset + 1; position <= width * bpp + offset; position++) {
                var current = data[position];
                var left = position - offset > bpp ? data[position - bpp] : 0;
                var back = width * bpp + 1;
                var above = position > back ? data[position - back] : 0;
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