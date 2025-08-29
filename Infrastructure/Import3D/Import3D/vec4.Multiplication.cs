
namespace Import3D {
    partial struct vec4 {

        /// <summary>
        ///
        /// </summary>
        /// <param name="lhs"></param>
        /// <returns></returns>
        public static vec4 operator -(vec4 lhs) {
            return new vec4(-lhs.x, -lhs.y, -lhs.z, -lhs.w);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static vec4 operator +(vec4 lhs, vec4 rhs) {
            return new vec4(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z, lhs.w + rhs.w);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static vec4 operator -(vec4 lhs, vec4 rhs) {
            return new vec4(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z, lhs.w - rhs.w);
        }

        //public static vec4 operator -(vec4 lhs, float rhs)
        //{
        //    return new vec4(lhs.x - rhs, lhs.y - rhs, lhs.z - rhs, lhs.w - rhs);
        //}
        /// <summary>
        ///
        /// </summary>
        /// <param name="self"></param>
        /// <param name="s"></param>
        /// <returns></returns>
        public static vec4 operator *(vec4 self, float s) {
            return new vec4(self.x * s, self.y * s, self.z * s, self.w * s);
        }

        //public static vec4 operator +(vec4 lhs, float rhs)
        //{
        //    return new vec4(lhs.x + rhs, lhs.y + rhs, lhs.z + rhs, lhs.w + rhs);
        //}
        /// <summary>
        ///
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static vec4 operator *(float lhs, vec4 rhs) {
            return new vec4(rhs.x * lhs, rhs.y * lhs, rhs.z * lhs, rhs.w * lhs);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static vec4 operator *(vec4 lhs, vec4 rhs) {
            return new vec4(rhs.x * lhs.x, rhs.y * lhs.y, rhs.z * lhs.z, rhs.w * lhs.w);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static vec4 operator /(vec4 lhs, float rhs) {
            return new vec4(lhs.x / rhs, lhs.y / rhs, lhs.z / rhs, lhs.w / rhs);
        }


    }
}
