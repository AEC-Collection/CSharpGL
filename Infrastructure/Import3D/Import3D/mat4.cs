using System.Security.Cryptography;
using System.Text;

namespace Import3D {
    /// <summary>
    /// column-major matrix4x4
    /// </summary>
    public unsafe struct mat4 : IEquatable<mat4> {
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
