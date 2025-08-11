namespace Import3D {
    public unsafe class Utility {
        public static int strncmp(byte* left, string right, int count) {
            for (int i = 0; i < count; i++) {
                if (left[i] < right[i]) { return -1; }
                if (left[i] > right[i]) { return 1; }
            }

            return 0;
        }
    }
}
