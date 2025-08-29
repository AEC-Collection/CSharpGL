using System.Text;

namespace Import3D {
    /// <summary>
    /// column-major matrix4x4
    /// </summary>
    unsafe partial struct mat4 {
        /// <summary>
        /// return the matrix that represents a translation from (0, 0, 0) to <paramref name="position"/>
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public static mat4 Translate(vec3 position) {
            var result = new mat4();
            result.values[0 + 3 * 4] = position.x;
            result.values[1 + 3 * 4] = position.x;
            result.values[2 + 3 * 4] = position.x;
            return result;
        }

        public static mat4 FromEulerAnglesXYZ(vec3 xyz) {
            var result = new mat4();

            float cx = (float)Math.Cos(xyz.x);
            float sx = (float)Math.Sin(xyz.x);
            float cy = (float)Math.Cos(xyz.y);
            float sy = (float)Math.Sin(xyz.y);
            float cz = (float)Math.Cos(xyz.z);
            float sz = (float)Math.Sin(xyz.z);

            // mz*my*mx
            result.values[0 + 0 * 4] = cz * cy;
            result.values[0 + 1 * 4] = cz * sy * sx - sz * cx;
            result.values[0 + 2 * 4] = sz * sx + cz * sy * cx;

            result.values[1 + 0 * 4] = sz * cy;
            result.values[1 + 1 * 4] = cz * cx + sz * sy * sx;
            result.values[1 + 2 * 4] = sz * sy * cx - cz * sx;

            result.values[2 + 0 * 4] = -sy;
            result.values[2 + 1 * 4] = cy * sx;
            result.values[2 + 2 * 4] = cy * cx;

            return result;
        }
    }
}
