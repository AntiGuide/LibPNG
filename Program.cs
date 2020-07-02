using System;
using System.IO;
using SixLabors.ImageSharp;

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

            using var image = Decoder.Decode(fileStream);
            using var fs = new FileStream($"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/Dokumente/LibPNG/Test_RGBA_8BPC_Output.bmp", FileMode.Create, FileAccess.Write);
            image.SaveAsBmp(fs);
        }
    }
}