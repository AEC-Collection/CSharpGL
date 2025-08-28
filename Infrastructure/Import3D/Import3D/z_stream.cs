namespace Import3D {
    public unsafe struct z_stream {
        public byte* next_in;     /* next input byte */
        public uint avail_in;  /* number of bytes available at next_in */
        public ulong total_in;  /* total number of input bytes read so far */

        public byte* next_out; /* next output byte will go here */
        public uint avail_out; /* remaining free space at next_out */
        public ulong total_out; /* total number of bytes output so far */

        public string? msg;  /* last error message, NULL if no error */
        public deflate_state* state; /* not visible by applications */

        public Func<void*, void*, uint, uint> zalloc;  /* used to allocate the internal state */
        public Action<void*> zfree;   /* used to free the internal state */
        public Action opaque;  /* private data object passed to zalloc and zfree */

        public int data_type;  /* best guess about the data type: binary or text
                           for deflate, or the decoding state for inflate */
        public ulong adler;      /* Adler-32 or CRC-32 value of the uncompressed data */
        public ulong reserved;   /* reserved for future use */
    }
}
