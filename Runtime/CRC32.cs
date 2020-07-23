using Unity.Collections;

namespace LibPNG {
    public static class CRC32 {
        private const uint CRC_POLYNOM = 0xEDB88320;
        //private static readonly NativeArray<uint> crc_tab32 = new NativeArray<uint>(256, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        private static readonly uint[] crc_tab32 = new uint[256];
        
        static CRC32() {
            for (var i = 0; i < 256; i++) {
                var crc = (uint)i;
                for (uint j = 0; j < 8; j++) {
                    if ((crc & 0x00000001) != 0) {
                        crc = crc >> 1 ^ CRC_POLYNOM;
                    } else { 
                        crc = crc >> 1;
                    }
                }

                crc_tab32[i] = (uint) crc;
            }
        }

        public static uint Calculate(in NativeSlice<byte> data) {
            var crc = 0xFFFFFFFF;
            
            foreach (var b in data) {
                var x = 0x000000FF & b;
                var y = crc ^ x;
                crc = (crc >> 8) ^ crc_tab32[ (int) y & 0xFF ];
            }

            return crc ^ 0xFFFFFFFF;
        }
    }
}