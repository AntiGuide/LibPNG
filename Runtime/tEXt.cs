using System;
using System.Text;

namespace LibPNG {
    /// <summary>
    /// This is standard text
    /// </summary>
    public static class tEXt {
        public static void Read(in ReadOnlySpan<byte> chunkData, Metadata metadata) {
            var sb = new StringBuilder();
            var position = 0;
            while (chunkData[position] != 0) {
                sb.Append((char)chunkData[position]);
                position++;
            }
            
            var keyword = sb.ToString();
            position++;

            var text = System.Text.Encoding.GetEncoding("iso-8859-1").GetString(chunkData.Slice(position));
        }
    }
}