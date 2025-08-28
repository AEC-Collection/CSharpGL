
namespace Import3D {
    public struct vec3 : IEquatable<vec3> {
        public float x;
        public float y;
        public float z;

        public vec3(float value) {
            this.x = value;
            this.y = value;
            this.z = value;
        }
        public vec3(float x, float y, float z) {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        public override string ToString() {
            return $"{x}, {y}, {z}";
        }

        public override bool Equals(object? obj) {
            return obj is vec3 vec && Equals(vec);
        }

        public bool Equals(vec3 other) {
            return x == other.x &&
                   y == other.y &&
                   z == other.z;
        }

        public override int GetHashCode() {
            return HashCode.Combine(x, y, z);
        }


        public static bool operator ==(vec3 left, vec3 right) {
            return left.Equals(right);
        }

        public static bool operator !=(vec3 left, vec3 right) {
            return !(left == right);
        }
    }
}
