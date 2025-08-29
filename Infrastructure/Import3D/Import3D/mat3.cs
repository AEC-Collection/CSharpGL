using System.Security.Cryptography;
using System.Text;

namespace Import3D {
    /// <summary>
    /// column-major matrix3x3
    /// </summary>
    public unsafe partial struct mat3 : IEquatable<mat3> {
        /// <summary>
        /// column-major matrix3x3
        /// </summary>
        public fixed float values[9];

        /// <summary>
        /// 
        /// </summary>
        /// <param name="row">row index(0 - 2)</param>
        /// <param name="column">column index(0 - 2)</param>
        /// <returns></returns>
        public float this[int row, int column] {
            get { return this.values[row + column * 3]; }
            set { this.values[row + column * 3] = value; }
        }

        public mat3() {
            // identity matrix
            for (int i = 0; i < 3; i++) {
                values[i * 4] = 1.0f;
            }
        }

        public mat3(vec3 col0, vec3 col1, vec3 col2) {
            this.values[0 + 0 * 3] = col0.x;
            this.values[1 + 0 * 3] = col0.y;
            this.values[2 + 0 * 3] = col0.z;

            this.values[0 + 1 * 3] = col1.x;
            this.values[1 + 1 * 3] = col1.y;
            this.values[2 + 1 * 3] = col1.z;

            this.values[0 + 2 * 3] = col2.x;
            this.values[1 + 2 * 3] = col2.y;
            this.values[2 + 2 * 3] = col2.z;

        }

        public mat3(mat4 mat) {
            this.values[0 + 0 * 3] = mat.values[0 + 0 * 4];
            this.values[1 + 0 * 3] = mat.values[1 + 0 * 4];
            this.values[2 + 0 * 3] = mat.values[2 + 0 * 4];

            this.values[0 + 1 * 3] = mat.values[0 + 1 * 4];
            this.values[1 + 1 * 3] = mat.values[1 + 1 * 4];
            this.values[2 + 1 * 3] = mat.values[2 + 1 * 4];

            this.values[0 + 2 * 3] = mat.values[0 + 2 * 4];
            this.values[1 + 2 * 3] = mat.values[1 + 2 * 4];
            this.values[2 + 2 * 3] = mat.values[2 + 2 * 4];

        }

        //TODO: strange thing: in debug mode, this shows { 1 0 0 ; 0 0 0 ; 0 0 0 ; 0 0 0 }
        // while this.ToString() shows 1 0 0 ; 0 1 0 ; 0 0 1 ; 
        public override string ToString() {
            var builder = new StringBuilder();
            for (int row = 0; row < 3; row++) {
                var first = true;
                for (int column = 0; column < 3; column++) {
                    if (first) { first = false; }
                    else { builder.Append(" "); }
                    //var value = this[row, column];
                    var value = this.values[row + column * 3];
                    builder.Append(value);
                }
                builder.Append(" ; ");
            }
            return builder.ToString();
        }

        public override bool Equals(object? obj) {
            return obj is mat3 mat && Equals(mat);
        }

        public bool Equals(mat3 other) {
            //return EqualityComparer<float*>.Default.Equals(values, other.values);
            for (int i = 0; i < 9; i++) {
                if (this.values[i] != other.values[i]) { return false; }
            }
            return true;
        }

        public override int GetHashCode() {
            return
                HashCode.Combine(
                HashCode.Combine(values[0], values[1], values[2]),
                HashCode.Combine(values[3], values[4], values[5]),
                HashCode.Combine(values[6], values[7], values[8]));
        }

        public static bool operator ==(mat3 left, mat3 right) {
            return left.Equals(right);
        }

        public static bool operator !=(mat3 left, mat3 right) {
            return !(left == right);
        }

    }
}
