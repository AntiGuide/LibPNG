using System;
using System.IO;

namespace LibPNG {
    class Program {
        private static void Main() {
            var filePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/Dokumente/LibPNG/Test_RGBA_8BPC.png";
            FileStream fileStream;
            try {
                fileStream = File.Open(filePath, FileMode.Open);
            } catch (FileNotFoundException e) {
                Console.WriteLine(e);
                throw;
            }

            var bitmap = Decoder.Decode(fileStream);
        }
    }
}