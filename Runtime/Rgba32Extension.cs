using UnityEngine;

namespace LibPNG {
    public static class Rgba32Extension {
        public static Color32 Add(this Color32 a, Color32 b, bool withoutOverflow = false) {
            a.r = withoutOverflow && a.r + b.r > byte.MaxValue ? byte.MaxValue : (byte) (a.r + b.r);
            a.g = withoutOverflow && a.g + b.g > byte.MaxValue ? byte.MaxValue : (byte) (a.g + b.g);
            a.b = withoutOverflow && a.b + b.b > byte.MaxValue ? byte.MaxValue : (byte) (a.b + b.b);
            a.a = withoutOverflow && a.a + b.a > byte.MaxValue ? byte.MaxValue : (byte) (a.a + b.a);
            return a;
        }
        
        public static Color32 Subtract(this Color32 a, Color32 b, bool withoutOverflow = false) {
            a.r = withoutOverflow && a.r - b.r < byte.MinValue ? byte.MinValue : (byte) (a.r - b.r);
            a.g = withoutOverflow && a.g - b.g < byte.MinValue ? byte.MinValue : (byte) (a.g - b.g);
            a.b = withoutOverflow && a.b - b.b < byte.MinValue ? byte.MinValue : (byte) (a.b - b.b);
            a.a = withoutOverflow && a.a - b.a < byte.MinValue ? byte.MinValue : (byte) (a.a - b.a);
            return a;
        }
        
        public static Color32 SubtractAbs(this Color32 a, Color32 b) {
            a.r = Abs(a.r - b.r) > byte.MaxValue ? byte.MaxValue : (byte) Abs(a.r - b.r);
            a.g = Abs(a.g - b.g) > byte.MaxValue ? byte.MaxValue : (byte) Abs(a.g - b.g);
            a.b = Abs(a.b - b.b) > byte.MaxValue ? byte.MaxValue : (byte) Abs(a.b - b.b);
            a.a = Abs(a.a - b.a) > byte.MaxValue ? byte.MaxValue : (byte) Abs(a.a - b.a);
            
            return a;
        }
        
        public static Color32 Average(this Color32 a, Color32 b) {
            a.r = (byte)((a.r + b.r) / 2);
            a.g = (byte)((a.g + b.g) / 2);
            a.b = (byte)((a.b + b.b) / 2);
            var aA = a.a + b.a;
            var i = (aA / 2);
            a.a = (byte)i;
            return a;
        }
        
        public static int Value(this Color32 a) {
            return a.r + a.g + a.b + a.a;
        }
        
        public static int Abs(int x) => Max(-x, x);
        public static int Abs(byte x) => Max(-x, x);
        private static int Max(int x, int y) => x > y ? x : y;
    }
}