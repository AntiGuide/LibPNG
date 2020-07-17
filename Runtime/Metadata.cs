using System.IO;

namespace LibPNG {
    public class Metadata {
        public uint Width;
        public uint Height;
        public byte BitDepth;
        public ColourTypeEnum ColourType;
        public InterlaceMethodEnum InterlaceMethod;
        public readonly MemoryStream DataStream;

        public enum ColourTypeEnum : byte {
            GREYSCALE = 0,
            TRUECOLOUR = 2,
            INDEXED_COLOUR = 3,
            GREYSCALE_WITH_ALPHA = 4,
            TRUECOLOUR_WITH_ALPHA = 6,
        }

        public enum InterlaceMethodEnum : byte {
            NO_INTERLACE = 0,
            ADAM7_INTERLACE = 1,
        }

        public Metadata(int dataStreamCapacity) {
            DataStream = new MemoryStream(dataStreamCapacity);
        }
    }
}