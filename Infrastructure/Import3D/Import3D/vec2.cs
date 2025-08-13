namespace Import3D {
    public struct vec2 {
        public float x;
        public float y;

        public vec2(float value) {
            this.x = value;
            this.y = value;
        }
        public vec2(float x, float y) {
            this.x = x;
            this.y = y;
        }

        public override string ToString() {
            return $"{x}, {y}";
        }
    }
}
