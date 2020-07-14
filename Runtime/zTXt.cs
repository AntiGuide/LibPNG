using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using CompressionMode = System.IO.Compression.CompressionMode;

namespace LibPNG {
    /// <summary>
    /// This is compressed text and may be EXIF data
    /// </summary>
    public static class zTXt {
        public static void Read(in byte[] chunkData, Metadata metadata) {
            var sb = new StringBuilder();
            var position = 0;
            while (chunkData[position] != 0) {
                sb.Append((char)chunkData[position]);
                position++;
            }
            
            var keyword = sb.ToString();
            position++;
            
            var compressionMethod = chunkData[position];
            position++;
            Debug.Assert(compressionMethod == 0);

            var subArray = new byte[4];
            Array.Copy(chunkData, position, subArray, 0, chunkData.Length - position);
            var zlibStream = new DeflateStream(new MemoryStream(subArray), CompressionMode.Decompress);
            var decompressed = new byte[4096];
            int bytesRead;
            sb.Clear();
            
            do {
                bytesRead = zlibStream.Read(decompressed, 0, 4096);
                sb.Append(System.Text.Encoding.GetEncoding("iso-8859-1").GetString(decompressed, 0, bytesRead));
            } while (bytesRead != 0);

            var text = sb.ToString();
        }
    }
}