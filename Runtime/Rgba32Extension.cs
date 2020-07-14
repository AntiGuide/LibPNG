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
        
        public static Rgba32 Average(this Rgba32 a, Rgba32 b) {
            a.R = (byte)((a.R + b.R) / 2f);
            a.G = (byte)((a.G + b.G) / 2f);
            a.B = (byte)((a.B + b.B) / 2f);
            a.A = (byte)((a.A + b.A) / 2f);
            return a;
        }
    }
}