using Unity.Burst;
using Unity.Collections;

namespace LibPNG {
    [BurstCompile]
    public static class CRC32 {
        private const uint CRC_POLYNOM = 0xEDB88320;

        public static uint Calculate(in NativeSlice<byte> data) {
            var crc_tab32 = new NativeArray<uint>(256, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
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
            
            var crc2 = 0xFFFFFFFF;
            
            foreach (var b in data) {
                var x = 0x000000FF & b;
                var y = crc2 ^ x;
                crc2 = (crc2 >> 8) ^ crc_tab32[ (int) y & 0xFF ];
            }

            return crc2 ^ 0xFFFFFFFF;
        }
    }
}