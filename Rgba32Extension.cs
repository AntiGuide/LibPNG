using SixLabors.ImageSharp.PixelFormats;

namespace LibPNG {
    public static class Rgba32Extension {
        public static Rgba32 Add(this Rgba32 a, Rgba32 b) {
            a.R += b.R;
            a.G += b.G;
            a.B += b.B;
            a.A += b.A;
            return a;
        }
    }
}