using Unity.Collections;

public static class NativeSliceExtension {
    public static bool SequenceEqual(this in NativeSlice<byte> slice, NativeSlice<byte> otherSlice) {
        // ReSharper disable once LoopCanBeConvertedToQuery
        for (var i = 0; i < slice.Length; i++) {
            if (slice[i] != otherSlice[i]) return false;
        }

        return true;
    }
    
    public static bool SequenceEqual(this in NativeSlice<byte> slice, byte[] other) {
        // ReSharper disable once LoopCanBeConvertedToQuery
        for (var i = 0; i < slice.Length; i++) {
            if (slice[i] != other[i]) return false;
        }

        return true;
    }
}
