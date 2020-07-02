using System;

namespace LibPNG {
    public class IDAT {
        public readonly byte[] Data;
        
        public IDAT(in ReadOnlySpan<byte> chunkData) {
            Data = chunkData.ToArray();
        }
    }
}