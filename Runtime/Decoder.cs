using System;
using System.IO;
using Ionic.Zlib;
using Unity.Burst;
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

                lastChunk = ChunkGenerator.GenerateChunk(FileData.GetSubArray(offset + 4, signedLength + 8), signedLength, ref metadata);
                offset += 12 + signedLength;
            } while (!lastChunk);

            //var tmpScanlineColor = new Color32[metadata.Width];
            
            NativeArray<byte> uncompressedData;
            
            var metadataData = metadata.Data;
            var buffer = metadataData.ToArray();
            using (var zlibStream = new ZlibStream(new MemoryStream(buffer), Ionic.Zlib.CompressionMode.Decompress)) {
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
                //Color32[] lineColors = null;
                switch ((FilterType) uncompressedData[firstRowBytePosition]) {
                    case FilterType.NONE:
                        break;
                    case FilterType.SUB:
                        FilterLineSub(ref uncompressedData, firstRowBytePosition, Width[0], bytesPerPixel);
                        break;
                    case FilterType.UP:
                        FilterLineUp(ref uncompressedData, firstRowBytePosition, Width[0], bytesPerPixel);
                        break;
                    case FilterType.AVERAGE:
                        FilterLineAverage(ref uncompressedData, firstRowBytePosition, Width[0], bytesPerPixel);
                        break;
                    case FilterType.PAETH:
                        FilterLinePaeth(ref uncompressedData, firstRowBytePosition, Width[0], bytesPerPixel);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                for (var i = 0; i < Width[0]; i++) {
                    RawTextureData[(int)((metadata.Height - 1 - aktLine) * metadata.Width * 4 + i * 4)] = uncompressedData[firstRowBytePosition + 1 + i * bytesPerPixel];
                    RawTextureData[(int)((metadata.Height - 1 - aktLine) * metadata.Width * 4 + i * 4 + 1)] = uncompressedData[firstRowBytePosition + 2 + i * bytesPerPixel];
                    RawTextureData[(int)((metadata.Height - 1 - aktLine) * metadata.Width * 4 + i * 4 + 2)] = uncompressedData[firstRowBytePosition + 3 + i * bytesPerPixel];
                    RawTextureData[(int)((metadata.Height - 1 - aktLine) * metadata.Width * 4 + i * 4 + 3)] = bytesPerPixel == 4 ? uncompressedData[firstRowBytePosition + 4 + i * bytesPerPixel] : byte.MaxValue;
                }
            }
        }

        [BurstCompile]
        private void FilterLineSub(ref NativeArray<byte> data, int offset, int width, int bpp) {
            for (var position = offset + 1; position <= width * bpp + offset; position++) {
                var left = position - offset > bpp ? data[position - bpp] : 0;
                data[position] = (byte) (data[position] + left);
            }
        }
        
        [BurstCompile]
        private void FilterLineUp(ref NativeArray<byte> data, int offset, int width, int bpp) {
            for (var position = offset + 1; position <= width * bpp + offset; position++) {
                var current = data[position];
                var back = width * bpp + 1;
                var above = position > back ? data[position - back] : 0;
                data[position] = (byte) (current + above);
            }
        }
        
        [BurstCompile]
        private void FilterLineAverage(ref NativeArray<byte> data, int offset, int width, int bpp) {
            for (var position = offset + 1; position <= width * bpp + offset; position++) {
                var current = data[position];
                var left = position - offset > bpp ? data[position - bpp] : 0;
                var back = width * bpp + 1;
                var above = position > back ? data[position - back] : 0;
                data[position] = (byte) (current + (byte) ((left + above) / 2));
            }
        }
        
        [BurstCompile]
        private void FilterLinePaeth(ref NativeArray<byte> data, int offset, int width, int bpp) {
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
            }
        }
    }
}