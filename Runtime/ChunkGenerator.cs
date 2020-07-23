using System;
using System.Diagnostics;
using Unity.Collections;

namespace LibPNG {
    public static class ChunkGenerator {
        /// <returns>Returns if this was the last chunk</returns>
        public static bool GenerateChunk(NativeSlice<byte> data, int length, Metadata metadata) {
            ChunkType? chunkType = null;
            if (data[0] == 73 && data[1] == 72 && data[2] == 68 && data[3] == 82) chunkType = ChunkType.IHDR;
            else if (data[0] == 80 && data[1] == 76 && data[2] == 84 && data[3] == 69) chunkType = ChunkType.PLTE;
            else if (data[0] == 73 && data[1] == 68 && data[2] == 65 && data[3] == 84) chunkType = ChunkType.IDAT;
            else if (data[0] == 73 && data[1] == 69 && data[2] == 78 && data[3] == 68) chunkType = ChunkType.IEND;

            var isCriticalChunk = (data[0] & 32) == 0;
            if (chunkType == null) {
                if (isCriticalChunk) throw new NotImplementedException();
                
                return false;
            }

            //var isPublicChunk = (chunkTypeSpan[1] & 32) == 0;
            //var isCompatiblePNGVersion = (chunkTypeSpan[2] & 32) == 0;
            //var isSafeToCopy = (chunkTypeSpan[3] & 32) == 1;
            
            // CRC
            var expectedCRC = BitConverterBigEndian.ToUInt32(data.Slice(4 + length, 4));
            var calculatedCRC = CRC32.Calculate(data.Slice(0, 4 + length));
            Debug.Assert(expectedCRC == calculatedCRC, "CRC check failed. The data seems to be damaged.");
            
            // Chunk Data
            switch (chunkType) {
                case ChunkType.IHDR:
                    ReadIHDR(data.Slice(4, length), metadata);
                    return false;
                case ChunkType.IDAT:
                    metadata.Data.Write(data.Slice(4, length).ToArray(), 0, length); // TODO Replace with a native array
                    return false;
                case ChunkType.IEND:
                    return true;
                default:
                    throw new Exception($"{nameof(chunkType)} matched no known types. Type was {GetASCIIString(data.Slice(0,4))}");
            }
        }
        
        public static void ReadIHDR(in NativeSlice<byte> chunkData, Metadata metadata) {
            metadata.Width = BitConverterBigEndian.ToUInt32(chunkData.Slice(0, 4)); // Width of the image in pixels
            if (metadata.Width == 0) throw new Exception($"{nameof(metadata.Width)} may not be 0");
            
            metadata.Height = BitConverterBigEndian.ToUInt32(chunkData.Slice(4, 4)); // Height of the image in pixels
            if (metadata.Height == 0) throw new Exception($"{nameof(metadata.Height)} may not be 0");
            
            metadata.BitDepth = chunkData[8]; // number of bits per sample or per palette index (not per pixel)
            if (metadata.BitDepth != 1 && metadata.BitDepth != 2 && metadata.BitDepth != 4 && metadata.BitDepth != 8 && metadata.BitDepth != 16) throw new Exception($"{nameof(metadata.BitDepth)} may only be 1, 2, 4, 8 or 16 but was {metadata.BitDepth}");
            
            var colourType = chunkData[9];
            switch (colourType) {
                case 0:
                    // Each pixel is a greyscale sample
                    Debug.Assert(metadata.BitDepth == 1 || metadata.BitDepth == 2 || metadata.BitDepth == 4 || metadata.BitDepth == 8 || metadata.BitDepth == 16,
                        $"{nameof(metadata.BitDepth)} may only be 1, 2, 4, 8 or 16 when {nameof(metadata.ColourType)} is {(byte)Metadata.ColourTypeEnum.GREYSCALE} ({nameof(Metadata.ColourTypeEnum.GREYSCALE)}) but was {metadata.BitDepth}");
                    break;
                case 2:
                    // Each pixel is an R,G,B triple
                    Debug.Assert(metadata.BitDepth == 8 || metadata.BitDepth == 16,
                        $"{nameof(metadata.BitDepth)} may only be 8 or 16 when {nameof(metadata.ColourType)} is {(byte)Metadata.ColourTypeEnum.TRUECOLOUR} ({nameof(Metadata.ColourTypeEnum.TRUECOLOUR)}) but was {metadata.BitDepth}");
                    break;
                case 3:
                    // Each pixel is a palette index; a PLTE chunk shall appear
                    Debug.Assert(metadata.BitDepth == 1 || metadata.BitDepth == 2 || metadata.BitDepth == 4 || metadata.BitDepth == 8,
                        $"{nameof(metadata.BitDepth)} may only be 1, 2, 4 or 8 when {nameof(metadata.ColourType)} is {(byte)Metadata.ColourTypeEnum.INDEXED_COLOUR} ({nameof(Metadata.ColourTypeEnum.INDEXED_COLOUR)}) but was {metadata.BitDepth}");
                    break;
                case 4:
                    // Each pixel is a greyscale sample followed by an alpha sample
                    Debug.Assert(metadata.BitDepth == 8 || metadata.BitDepth == 16,
                        $"{nameof(metadata.BitDepth)} may only be 8 or 16 when {nameof(metadata.ColourType)} is {(byte)Metadata.ColourTypeEnum.GREYSCALE_WITH_ALPHA} ({nameof(Metadata.ColourTypeEnum.GREYSCALE_WITH_ALPHA)}) but was {metadata.BitDepth}");
                    break;
                case 6:
                    // Each pixel is an R,G,B triple followed by an alpha sample
                    Debug.Assert(metadata.BitDepth == 8 || metadata.BitDepth == 16,
                        $"{nameof(metadata.BitDepth)} may only be 8 or 16 when {nameof(metadata.ColourType)} is {(byte)Metadata.ColourTypeEnum.TRUECOLOUR_WITH_ALPHA} ({nameof(Metadata.ColourTypeEnum.TRUECOLOUR_WITH_ALPHA)}) but was {metadata.BitDepth}");
                    break;
                default:
                    throw new Exception($"{nameof(colourType)} may only be 0, 2, 3, 4 or 6 but was {colourType}");
            }

            metadata.ColourType = (Metadata.ColourTypeEnum) colourType;

            //metadata.SampleDepth = metadata.ColourType == Metadata.ColourTypeEnum.INDEXED_COLOUR ? (byte) 8 : metadata.BitDepth;
            
            var compressionMethod = chunkData[10];
            Debug.Assert(compressionMethod == 0, $"{nameof(compressionMethod)} may only be 0 but was {compressionMethod}");
            
            var filterMethod = chunkData[11];
            Debug.Assert(filterMethod == 0, $"{nameof(filterMethod)} may only be 0 but was {filterMethod}");
            
            var interlaceMethod = chunkData[12];
            Debug.Assert(interlaceMethod == (int) Metadata.InterlaceMethodEnum.NO_INTERLACE || interlaceMethod == (int) Metadata.InterlaceMethodEnum.ADAM7_INTERLACE, $"{nameof(interlaceMethod)} may only be {Metadata.InterlaceMethodEnum.NO_INTERLACE} or {Metadata.InterlaceMethodEnum.ADAM7_INTERLACE} but was {interlaceMethod}");
            metadata.InterlaceMethod = (Metadata.InterlaceMethodEnum) interlaceMethod;

            if (metadata.InterlaceMethod != Metadata.InterlaceMethodEnum.NO_INTERLACE) throw new NotImplementedException();
        }
        
        public static string GetASCIIString(NativeSlice<byte> bytes) {
            var chars = new char[bytes.Length];
            for (var i = 0; i < bytes.Length; i++) {
                chars[i] = (char) bytes[i];
            }
            
            return new string(chars);
        }
    }
}