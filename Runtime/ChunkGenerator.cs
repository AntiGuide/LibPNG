using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LibPNG {
    public static class ChunkGenerator {
        private static readonly Dictionary<byte[], ChunkType> chunkTypeAssociation = new Dictionary<byte[], ChunkType> {
            {new byte[] {73, 72, 68, 82}, ChunkType.IHDR},
            {new byte[] {80, 76, 84, 69}, ChunkType.PLTE},
            {new byte[] {73, 68, 65, 84}, ChunkType.IDAT},
            {new byte[] {73, 69, 78, 68}, ChunkType.IEND},
        };

        /// <returns>Returns if this was the last chunk</returns>
        public static bool GenerateChunk(Stream stream, int length, Metadata metadata) {
            //Chunk Type
            var chunkTypeSpan = new byte[4];
            stream.Read(chunkTypeSpan, 0, 4);
            var crcData = new List<byte>();
            crcData.AddRange(chunkTypeSpan);
            
            ChunkType? chunkType = null;
            foreach (var cta in chunkTypeAssociation.Where(cta => chunkTypeSpan.SequenceEqual(cta.Key))) {
                chunkType = cta.Value;
                break;
            }

            var isCriticalChunk = (chunkTypeSpan[0] & 32) == 0;
            if (!isCriticalChunk) {
                stream.Seek(length + 4, SeekOrigin.Current);
                return false;
            }
            
            //var isPublicChunk = (chunkTypeSpan[1] & 32) == 0;
            //var isCompatiblePNGVersion = (chunkTypeSpan[2] & 32) == 0;
            //var isSafeToCopy = (chunkTypeSpan[3] & 32) == 1;

            var chunkData = new byte[length];
            stream.Read(chunkData, 0, length);
            crcData.AddRange(chunkData);
            
            // CRC
            var crcValueData = new byte[4];
            stream.Read(crcValueData, 0, 4);
            if (BitConverter.IsLittleEndian) Array.Reverse(crcValueData);
            var expectedCRC = BitConverter.ToUInt32(crcValueData, 0);
            var calculatedCRC = CRC32.Calculate(crcData);
            Debug.Assert(expectedCRC == calculatedCRC, "CRC check failed. The data seems to be damaged.");
            
            // Chunk Data
            switch (chunkType) {
                case ChunkType.IHDR:
                    ReadIHDR(chunkData, metadata);
                    return false;
                case ChunkType.IDAT:
                    metadata.DataStream.Write(chunkData, 0, chunkData.Length);
                    return false;
                case ChunkType.IEND:
                    return true;
                default:
                    throw new Exception($"{nameof(chunkType)} matched no known types. Type was {System.Text.Encoding.ASCII.GetString(chunkTypeSpan)}");
            }
        }
        
        public static void ReadIHDR(in byte[] chunkData, Metadata metadata) {
            var subArray = new byte[4];
            Array.Copy(chunkData, 0, subArray, 0, 4);
            if (BitConverter.IsLittleEndian) Array.Reverse(subArray);
            
            metadata.Width = BitConverter.ToUInt32(subArray, 0); // Width of the image in pixels
            if (metadata.Width == 0) throw new Exception($"{nameof(metadata.Width)} may not be 0");
            
            subArray = new byte[4];
            Array.Copy(chunkData, 4, subArray, 0, 4);
            if (BitConverter.IsLittleEndian) Array.Reverse(subArray);
            
            metadata.Height = BitConverter.ToUInt32(subArray, 0); // Height of the image in pixels
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
    }
}