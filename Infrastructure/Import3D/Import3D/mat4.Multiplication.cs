using System.Text;

namespace Import3D {
    /// <summary>
    /// column-major matrix4x4
    /// </summary>
    unsafe partial struct mat4 {

        /// <summary>
        /// Multiplies the <paramref name="lhs"/> matrix by the <paramref name="rhs"/> vector.
        /// </summary>
        /// <param name="lhs">The LHS matrix.</param>
        /// <param name="rhs">The RHS vector.</param>
        /// <returns>The product of <paramref name="lhs"/> and <paramref name="rhs"/>.</returns>
        public static vec4 operator *(mat4 lhs, vec4 rhs) {
            //return new vec4(
            //    lhs[0, 0] * rhs.x + lhs[1, 0] * rhs.y + lhs[2, 0] * rhs.z + lhs[3, 0] * rhs.w,
            //    lhs[0, 1] * rhs.x + lhs[1, 1] * rhs.y + lhs[2, 1] * rhs.z + lhs[3, 1] * rhs.w,
            //    lhs[0, 2] * rhs.x + lhs[1, 2] * rhs.y + lhs[2, 2] * rhs.z + lhs[3, 2] * rhs.w,
            //    lhs[0, 3] * rhs.x + lhs[1, 3] * rhs.y + lhs[2, 3] * rhs.z + lhs[3, 3] * rhs.w
            //);
            var result = new vec4();
            // result.x/y/z/w = lhs.values[row + column * 4] * rhs.x/y/z/w
            result.x =
                lhs.values[0 + 0 * 4] * rhs.x
              + lhs.values[0 + 1 * 4] * rhs.y
              + lhs.values[0 + 2 * 4] * rhs.z
              + lhs.values[0 + 3 * 4] * rhs.w;
            result.y =
                  lhs.values[1 + 0 * 4] * rhs.x
                + lhs.values[1 + 1 * 4] * rhs.y
                + lhs.values[1 + 2 * 4] * rhs.z
                + lhs.values[1 + 3 * 4] * rhs.w;
            result.z =
                lhs.values[2 + 0 * 4] * rhs.x
              + lhs.values[2 + 1 * 4] * rhs.y
              + lhs.values[2 + 2 * 4] * rhs.z
              + lhs.values[2 + 3 * 4] * rhs.w;
            result.w =
                lhs.values[3 + 0 * 4] * rhs.x
              + lhs.values[3 + 1 * 4] * rhs.y
              + lhs.values[3 + 2 * 4] * rhs.z
              + lhs.values[3 + 3 * 4] * rhs.w;
            return result;
        }

        /// <summary>
        /// Multiplies the <paramref name="lhs"/> matrix by the <paramref name="rhs"/> matrix.
        /// </summary>
        /// <param name="lhs">The LHS matrix.</param>
        /// <param name="rhs">The RHS matrix.</param>
        /// <returns>The product of <paramref name="lhs"/> and <paramref name="rhs"/>.</returns>
        public static mat4 operator *(mat4 lhs, mat4 rhs) {
            mat4 result = new mat4();
            for (int row = 0; row < 4; row++) {
                for (int column = 0; column < 4; column++) {
                    result.values[row + column * 4] =
                        lhs.values[row + column * 0] * rhs.values[0 * column * 4]
                      + lhs.values[row + column * 1] * rhs.values[1 * column * 4]
                      + lhs.values[row + column * 2] * rhs.values[2 * column * 4]
                      + lhs.values[row + column * 3] * rhs.values[3 * column * 4];
                }
            }

            return result;
        }

        ///// <summary>
        /////
        ///// </summary>
        ///// <param name="lhs"></param>
        ///// <param name="s"></param>
        ///// <returns></returns>
        //public static mat4 operator *(mat4 lhs, float s) {
        //    return new mat4(new[]
        //    {
        //        lhs[0]*s,
        //        lhs[1]*s,
        //        lhs[2]*s,
        //        lhs[3]*s
        //    });
        //}


    }
}
