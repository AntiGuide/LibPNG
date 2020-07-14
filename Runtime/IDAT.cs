namespace LibPNG {
    public static class IDAT {
        public static void Read(in byte[] chunkData, Metadata metadata) {
            metadata.Data.AddRange(chunkData);
        }
    }
}