using System;
using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LibPNG.Tests {
    public class DecoderTests {
        /// <summary>
        /// Test decoding for a simple RGBA 8 bit per channel picture with an IHDR, IDAT and IEND Chunk
        /// </summary>
        [UnityTest]
        public IEnumerator Decode_RGBA_8BPC() {
            var filePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/Dokumente/LibPNG/Tests/Test_RGBA_8BPC.png";
            var fileStream = File.Open(filePath, FileMode.Open);
            var image = new Texture2D(1, 1);
            yield return image.LoadImageAsync(fileStream);
            Assert.That(image, Is.Not.Null);
        }
        
        /// <summary>
        /// Test decoding for a simple but bigger RGBA 8 bit per channel picture with an IHDR, multiple IDAT and an IEND Chunk
        /// </summary>
        [UnityTest]
        public IEnumerator Decode_RGBA_8BPC_Big() {
            var filePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/Dokumente/LibPNG/Tests/Test_RGBA_8BPC_Big.png";
            var fileStream = File.Open(filePath, FileMode.Open);
            var image = new Texture2D(1, 1);
            yield return image.LoadImageAsync(fileStream);
            Assert.That(image, Is.Not.Null);
        }
        
        /// <summary>
        /// Test decoding for a simple but bigger RGBA 8 bit per channel picture with an IHDR, multiple IDAT and an IEND Chunk
        /// </summary>
        [UnityTest]
        public IEnumerator Decode_RGBA_8BPC_Meta() {
            var filePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/Dokumente/LibPNG/Tests/Test_RGBA_8BPC_Meta.png";
            var fileStream = File.Open(filePath, FileMode.Open);
            var image = new Texture2D(1, 1);
            yield return image.LoadImageAsync(fileStream);
            Assert.That(image, Is.Not.Null);
        }
    }
}
