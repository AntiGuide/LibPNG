using System.Runtime.CompilerServices;
using Unity.Collections;

public static class BitConverterBigEndian {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ToUInt32(in byte[] data) {
        return (uint)(data[0] << 24 | data[1] << 16 | data[2] << 8 | data[3]);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ToUInt32(in NativeSlice<byte> data) {
        return (uint)(data[0] << 24 | data[1] << 16 | data[2] << 8 | data[3]);
    }
}
