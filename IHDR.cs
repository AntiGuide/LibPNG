using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;

namespace LibPNG {
    public struct IHDR {
        // Data
        public readonly uint Width;
        public readonly uint Height;
        public readonly byte BitDepth;
        public readonly ColourTypeEnum ColourType;
        private readonly CompressionMethodEnum CompressionMethod;
        private readonly FilterMethodEnum FilterMethod;
        public readonly InterlaceMethodEnum InterlaceMethod;
        
        // Calculated Data
        public readonly byte SampleDepth;
        
        public enum ColourTypeEnum : byte {
            GREYSCALE = 0,
            TRUECOLOUR = 2,
            INDEXED_COLOUR = 3,
            GREYSCALE_WITH_ALPHA = 4,
            TRUECOLOUR_WITH_ALPHA = 6,
        }
        
        private enum CompressionMethodEnum : byte {
            DEFLATE = 0, // deflate/inflate compression with a sliding window of at most 32768 bytes
        }
        
        private enum FilterMethodEnum : byte {
            ADAPTIVE = 0, // adaptive filtering with five basic filter types
        }
        
        public enum InterlaceMethodEnum : byte {
            NO_INTERLACE = 0,
            ADAM7_INTERLACE = 1,
        }
        
        public IHDR(in ReadOnlySpan<byte> chunkData) {
            Width = BinaryPrimitives.ReadUInt32BigEndian(chunkData.Slice(0, 4)); // Width of the image in pixels
            if (Width == 0) throw new Exception($"{nameof(Width)} may not be 0");
            
            Height = BinaryPrimitives.ReadUInt32BigEndian(chunkData.Slice(4, 4)); // Height of the image in pixels
            if (Height == 0) throw new Exception($"{nameof(Height)} may not be 0");
            
            BitDepth = chunkData[8]; // number of bits per sample or per palette index (not per pixel)
            if (BitDepth != 1 && BitDepth != 2 && BitDepth != 4 && BitDepth != 8 && BitDepth != 16) throw new Exception($"{nameof(BitDepth)} may only be 1, 2, 4, 8 or 16 but was {BitDepth}");
            
            var colourType = chunkData[9];
            switch (colourType) {
                case 0:
                    // Each pixel is a greyscale sample
                    Debug.Assert(BitDepth == 1 || BitDepth == 2 || BitDepth == 4 || BitDepth == 8 || BitDepth == 16,
                        $"{nameof(BitDepth)} may only be 1, 2, 4, 8 or 16 when {nameof(ColourType)} is {(byte)ColourTypeEnum.GREYSCALE} ({nameof(ColourTypeEnum.GREYSCALE)}) but was {BitDepth}");
                    break;
                case 2:
                    // Each pixel is an R,G,B triple
                    Debug.Assert(BitDepth == 8 || BitDepth == 16,
                        $"{nameof(BitDepth)} may only be 8 or 16 when {nameof(ColourType)} is {(byte)ColourTypeEnum.TRUECOLOUR} ({nameof(ColourTypeEnum.TRUECOLOUR)}) but was {BitDepth}");
                    break;
                case 3:
                    // Each pixel is a palette index; a PLTE chunk shall appear
                    Debug.Assert(BitDepth == 1 || BitDepth == 2 || BitDepth == 4 || BitDepth == 8,
                        $"{nameof(BitDepth)} may only be 1, 2, 4 or 8 when {nameof(ColourType)} is {(byte)ColourTypeEnum.INDEXED_COLOUR} ({nameof(ColourTypeEnum.INDEXED_COLOUR)}) but was {BitDepth}");
                    break;
                case 4:
                    // Each pixel is a greyscale sample followed by an alpha sample
                    Debug.Assert(BitDepth == 8 || BitDepth == 16,
                        $"{nameof(BitDepth)} may only be 8 or 16 when {nameof(ColourType)} is {(byte)ColourTypeEnum.GREYSCALE_WITH_ALPHA} ({nameof(ColourTypeEnum.GREYSCALE_WITH_ALPHA)}) but was {BitDepth}");
                    break;
                case 6:
                    // Each pixel is an R,G,B triple followed by an alpha sample
                    Debug.Assert(BitDepth == 8 || BitDepth == 16,
                        $"{nameof(BitDepth)} may only be 8 or 16 when {nameof(ColourType)} is {(byte)ColourTypeEnum.TRUECOLOUR_WITH_ALPHA} ({nameof(ColourTypeEnum.TRUECOLOUR_WITH_ALPHA)}) but was {BitDepth}");
                    break;
                default:
                    throw new Exception($"{nameof(colourType)} may only be 0, 2, 3, 4 or 6 but was {colourType}");
            }

            ColourType = (ColourTypeEnum) colourType;

            SampleDepth = ColourType == ColourTypeEnum.INDEXED_COLOUR ? (byte) 8 : BitDepth;
            
            var compressionMethod = chunkData[10];
            Debug.Assert(compressionMethod == 0, $"{nameof(compressionMethod)} may only be 0 but was {compressionMethod}");
            CompressionMethod = (CompressionMethodEnum) compressionMethod;
            
            var filterMethod = chunkData[11];
            Debug.Assert(filterMethod == 0, $"{nameof(filterMethod)} may only be 0 but was {filterMethod}");
            FilterMethod = (FilterMethodEnum) filterMethod;
            
            var interlaceMethod = chunkData[12];
            Debug.Assert(interlaceMethod == (int) InterlaceMethodEnum.NO_INTERLACE || interlaceMethod == (int) InterlaceMethodEnum.ADAM7_INTERLACE, $"{nameof(interlaceMethod)} may only be {InterlaceMethodEnum.NO_INTERLACE} or {InterlaceMethodEnum.ADAM7_INTERLACE} but was {interlaceMethod}");
            InterlaceMethod = (InterlaceMethodEnum) interlaceMethod;
        }

        public override string ToString() {
            var sb = new StringBuilder();
            sb.AppendLine($"{nameof(Width)}: {Width}");
            sb.AppendLine($"{nameof(Height)}: {Height}");
            sb.AppendLine($"{nameof(BitDepth)}: {BitDepth}");
            sb.AppendLine($"{nameof(ColourType)}: {ColourType}");
            sb.AppendLine($"{nameof(CompressionMethod)}: {CompressionMethod}");
            sb.AppendLine($"{nameof(FilterMethod)}: {FilterMethod}");
            sb.AppendLine($"{nameof(InterlaceMethod)}: {InterlaceMethod}");
            return sb.ToString();
        }
    }
}