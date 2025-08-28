
namespace Import3D {
    public struct vec2 : IEquatable<vec2> {
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

        public override bool Equals(object? obj) {
            return obj is vec2 vec && Equals(vec);
        }

        public bool Equals(vec2 other) {
            return x == other.x &&
                   y == other.y;
        }

        public override int GetHashCode() {
            return HashCode.Combine(x, y);
        }


        public static bool operator ==(vec2 left, vec2 right) {
            return left.Equals(right);
        }

        public static bool operator !=(vec2 left, vec2 right) {
            return !(left == right);
        }
    }
}
