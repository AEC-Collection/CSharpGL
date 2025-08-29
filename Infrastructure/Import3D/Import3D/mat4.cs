using System.Security.Cryptography;
using System.Text;

namespace Import3D {
    /// <summary>
    /// column-major matrix4x4
    /// </summary>
    public unsafe partial struct mat4 : IEquatable<mat4> {
        /// <summary>
        /// column-major matrix4x4
        /// </summary>
        public fixed float values[16];

        /// <summary>
        /// 
        /// </summary>
        /// <param name="row">row index(0 - 3)</param>
        /// <param name="column">column index(0 - 3)</param>
        /// <returns></returns>
        public float this[int row, int column] {
            get { return this.values[row + column * 4]; }
            set { this.values[row + column * 4] = value; }
        }

        public mat4() {
            // identity matrix
            for (int i = 0; i < 4; i++) {
                values[i * 5] = 1.0f;
            }
        }

        public mat4(vec4 col0, vec4 col1, vec4 col2, vec4 col3) {
            this.values[0 + 0 * 4] = col0.x;
            this.values[1 + 0 * 4] = col0.y;
            this.values[2 + 0 * 4] = col0.z;
            this.values[3 + 0 * 4] = col0.w;

            this.values[0 + 1 * 4] = col1.x;
            this.values[1 + 1 * 4] = col1.y;
            this.values[2 + 1 * 4] = col1.z;
            this.values[3 + 1 * 4] = col1.w;

            this.values[0 + 2 * 4] = col2.x;
            this.values[1 + 2 * 4] = col2.y;
            this.values[2 + 2 * 4] = col2.z;
            this.values[3 + 2 * 4] = col2.w;

            this.values[0 + 3 * 4] = col3.x;
            this.values[1 + 3 * 4] = col3.y;
            this.values[2 + 3 * 4] = col3.z;
            this.values[3 + 3 * 4] = col3.w;

        }

        //TODO: strange thing: in debug mode, this shows { 1 0 0 0 ; 0 0 0 0 ; 0 0 0 0 ; 0 0 0 0 }
        // while this.ToString() shows 1 0 0 0 ; 0 1 0 0 ; 0 0 1 0 ; 0 0 0 1 ; 
        public override string ToString() {
            var builder = new StringBuilder();
            for (int row = 0; row < 4; row++) {
                var first = true;
                for (int column = 0; column < 4; column++) {
                    if (first) { first = false; }
                    else { builder.Append(" "); }
                    //var value = this[row, column];
                    var value = this.values[row + column * 4];
                    builder.Append(value);
                }
                builder.Append(" ; ");
            }
            return builder.ToString();
        }

        public override bool Equals(object? obj) {
            return obj is mat4 mat && Equals(mat);
        }

        public bool Equals(mat4 other) {
            //return EqualityComparer<float*>.Default.Equals(values, other.values);
            for (int i = 0; i < 16; i++) {
                if (this.values[i] != other.values[i]) { return false; }
            }
            return true;
        }

        public override int GetHashCode() {
            return
                HashCode.Combine(
                HashCode.Combine(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]),
                HashCode.Combine(values[8], values[9], values[10], values[11], values[12], values[13], values[14], values[15]));
        }

        public static bool operator ==(mat4 left, mat4 right) {
            return left.Equals(right);
        }

        public static bool operator !=(mat4 left, mat4 right) {
            return !(left == right);
        }

        //public string ToString2() {
        //    var builder = new StringBuilder();
        //    for (int i = 0; i < 16; i++) {
        //        builder.Append(this.values[i]);
        //        builder.Append(" ");
        //        if (i % 4 == 3) { builder.Append("| "); }
        //    }
        //    //for (int row = 0; row < 4; row++) {
        //    //    for (int column = 0; column < 4; column++) {
        //    //        var value = this[row, column];
        //    //        builder.Append(value);
        //    //        builder.Append(", ");
        //    //    }
        //    //    builder.AppendLine();
        //    //}
        //    return builder.ToString();
        //}

    }
}
