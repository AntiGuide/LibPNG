using System;

namespace LibPNG {
    public static class IDAT {
        public static void Read(in ReadOnlySpan<byte> chunkData, Metadata metadata) {
            metadata.Data.AddRange(chunkData.ToArray());
        }
    }
}