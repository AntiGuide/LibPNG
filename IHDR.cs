using System;
using System.Buffers.Binary;
using System.Diagnostics;

namespace LibPNG {
    public static class IHDR {
        public static void Read(in ReadOnlySpan<byte> chunkData, Metadata metadata) {
            metadata.Width = BinaryPrimitives.ReadUInt32BigEndian(chunkData.Slice(0, 4)); // Width of the image in pixels
            if (metadata.Width == 0) throw new Exception($"{nameof(metadata.Width)} may not be 0");
            
            metadata.Height = BinaryPrimitives.ReadUInt32BigEndian(chunkData.Slice(4, 4)); // Height of the image in pixels
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

            metadata.SampleDepth = metadata.ColourType == Metadata.ColourTypeEnum.INDEXED_COLOUR ? (byte) 8 : metadata.BitDepth;
            
            var compressionMethod = chunkData[10];
            Debug.Assert(compressionMethod == 0, $"{nameof(compressionMethod)} may only be 0 but was {compressionMethod}");
            
            var filterMethod = chunkData[11];
            Debug.Assert(filterMethod == 0, $"{nameof(filterMethod)} may only be 0 but was {filterMethod}");
            
            var interlaceMethod = chunkData[12];
            Debug.Assert(interlaceMethod == (int) Metadata.InterlaceMethodEnum.NO_INTERLACE || interlaceMethod == (int) Metadata.InterlaceMethodEnum.ADAM7_INTERLACE, $"{nameof(interlaceMethod)} may only be {Metadata.InterlaceMethodEnum.NO_INTERLACE} or {Metadata.InterlaceMethodEnum.ADAM7_INTERLACE} but was {interlaceMethod}");
            metadata.InterlaceMethod = (Metadata.InterlaceMethodEnum) interlaceMethod;
        }
    }
}