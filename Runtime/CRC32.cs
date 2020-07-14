using System.Collections.Generic;

namespace LibPNG {
    public static class CRC32 {
        private const uint CRC_POLYNOM = 0xEDB88320;
        private static readonly uint[] crc_tab32 = new uint[256];
        
        static CRC32() {
            for (uint i = 0; i < 256; i++) {
                var crc = i;
                for (uint j = 0; j < 8; j++) {
                    if ((crc & 0x00000001) != 0) {
                        crc = (crc >> 1) ^ CRC_POLYNOM;
                    } else { 
                        crc = crc >> 1;
                    }
                }

                crc_tab32[i] = crc;
            }
        }

        public static uint Calculate(in IEnumerable<byte> data) {
            var crc = 0xFFFFFFFF;
            if (data == null) return crc ^ 0xFFFFFFFF;
            
            foreach (var b in data) {
                var x = 0x000000FF & b;
                var y = crc ^ x;
                crc = (crc >> 8) ^ crc_tab32[ y & 0xFF ];
            }

            return crc ^ 0xFFFFFFFF;
        }
    }
}