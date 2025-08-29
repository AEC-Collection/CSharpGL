using System.Diagnostics;

namespace Import3D {
    /**
     *  @brief  This class represents a quaternion as a 4D vector.
     */
    public unsafe struct aiQuaternion : IEquatable<aiQuaternion> {

        //! w,x,y,z components of the quaternion
        public float w, x, y, z;

        public aiQuaternion() {
            this.w = 1.0f; this.x = this.y = this.z = 0.0f;
        }
        public aiQuaternion(float pw, float px, float py, float pz) {
            this.w = pw;
            this.x = px;
            this.y = py;
            this.z = pz;
        }

        /**
         *  @brief  Construct from rotation matrix. Result is undefined if the matrix is not orthonormal.
         */
        public aiQuaternion(mat3 pRotMatrix) {
            float t = pRotMatrix.values[0 + 0 * 3] + pRotMatrix.values[1 + 1 * 3] + pRotMatrix.values[2 + 2 * 3];

            // large enough
            if (t > 0) {
                var s = Math.Sqrt(1 + t) * (2.0);
                x = (float)((pRotMatrix.values[2 + 1 * 3] - pRotMatrix.values[1 + 2 * 3]) / s);
                y = (float)((pRotMatrix.values[0 + 2 * 3] - pRotMatrix.values[2 + 0 * 3]) / s);
                z = (float)((pRotMatrix.values[1 + 0 * 3] - pRotMatrix.values[0 + 1 * 3]) / s);
                w = (float)((0.25) * s);
            } // else we have to check several cases
            else if (pRotMatrix.values[0 + 0 * 3] > pRotMatrix.values[1 + 1 * 3] && pRotMatrix.values[0 + 0 * 3] > pRotMatrix.values[2 + 2 * 3]) {
                // Column 0:
                var s = Math.Sqrt(1.0 + pRotMatrix.values[0 + 0 * 3] - pRotMatrix.values[1 + 1 * 3] - pRotMatrix.values[2 + 2 * 3]) * 2.0;
                x = (float)((0.25) * s);
                y = (float)((pRotMatrix.values[1 + 0 * 3] + pRotMatrix.values[0 + 1 * 3]) / s);
                z = (float)((pRotMatrix.values[0 + 2 * 3] + pRotMatrix.values[2 + 0 * 3]) / s);
                w = (float)((pRotMatrix.values[2 + 1 * 3] - pRotMatrix.values[1 + 2 * 3]) / s);
            }
            else if (pRotMatrix.values[1 + 1 * 3] > pRotMatrix.values[2 + 2 * 3]) {
                // Column 1:
                var s = Math.Sqrt(1.0 + pRotMatrix.values[1 + 1 * 3] - pRotMatrix.values[0 + 0 * 3] - pRotMatrix.values[2 + 2 * 3]) * 2.0;
                x = (float)((pRotMatrix.values[1 + 0 * 3] + pRotMatrix.values[0 + 1 * 3]) / s);
                y = (float)((0.25) * s);
                z = (float)((pRotMatrix.values[2 + 1 * 3] + pRotMatrix.values[1 + 2 * 3]) / s);
                w = (float)((pRotMatrix.values[0 + 2 * 3] - pRotMatrix.values[2 + 0 * 3]) / s);
            }
            else {
                // Column 2:
                var s = Math.Sqrt(1.0 + pRotMatrix.values[2 + 2 * 3] - pRotMatrix.values[0 + 0 * 3] - pRotMatrix.values[1 + 1 * 3]) * 2.0;
                x = (float)((pRotMatrix.values[0 + 2 * 3] + pRotMatrix.values[2 + 0 * 3]) / s);
                y = (float)((pRotMatrix.values[2 + 1 * 3] + pRotMatrix.values[1 + 2 * 3]) / s);
                z = (float)((0.25) * s);
                w = (float)((pRotMatrix.values[1 + 0 * 3] - pRotMatrix.values[0 + 1 * 3]) / s);
            }
        }

        /** Construct from euler angles */
        public aiQuaternion(float fPitch, float fYaw, float fRoll/*float roty, float rotz, float rotx*/) {
            float fSinPitch = ((float)Math.Sin(fPitch * (0.5)));
            float fCosPitch = ((float)Math.Cos(fPitch * (0.5)));
            float fSinYaw = ((float)Math.Sin(fYaw * (0.5)));
            float fCosYaw = ((float)Math.Cos(fYaw * (0.5)));
            float fSinRoll = ((float)Math.Sin(fRoll * (0.5)));
            float fCosRoll = ((float)Math.Cos(fRoll * (0.5)));
            float fCosPitchCosYaw = (fCosPitch * fCosYaw);
            float fSinPitchSinYaw = (fSinPitch * fSinYaw);
            x = fSinRoll * fCosPitchCosYaw - fCosRoll * fSinPitchSinYaw;
            y = fCosRoll * fSinPitch * fCosYaw + fSinRoll * fCosPitch * fSinYaw;
            z = fCosRoll * fCosPitch * fSinYaw - fSinRoll * fSinPitch * fCosYaw;
            w = fCosRoll * fCosPitchCosYaw + fSinRoll * fSinPitchSinYaw;
        }

        /** Construct from an axis-angle pair */
        public aiQuaternion(vec3 axis, float angle) {
            axis.Normalize();

            float sin_a = (float)Math.Sin(angle / 2);
            float cos_a = (float)Math.Cos(angle / 2);
            x = axis.x * sin_a;
            y = axis.y * sin_a;
            z = axis.z * sin_a;
            w = cos_a;
        }

        /** Construct from a normalized quaternion stored in a vec3 */
        public aiQuaternion(vec3 normalized) {
            x = normalized.x;
            y = normalized.y;
            z = normalized.z;

            float t = (1.0f) - (x * x) - (y * y) - (z * z);

            if (t < 0) {
                w = (0.0f);
            }
            else {
                w = (float)Math.Sqrt(t);
            }
        }

        /** Returns a matrix representation of the quaternion */
        public mat3 GetMatrix() {
            mat3 resMatrix;
            resMatrix.values[0 + 0 * 3] = (1.0f) - (2.0f) * (y * y + z * z);
            resMatrix.values[0 + 1 * 3] = (2.0f) * (x * y - z * w);
            resMatrix.values[0 + 2 * 3] = (2.0f) * (x * z + y * w);
            resMatrix.values[1 + 0 * 3] = (2.0f) * (x * y + z * w);
            resMatrix.values[1 + 1 * 3] = (1.0f) - (2.0f) * (x * x + z * z);
            resMatrix.values[1 + 2 * 3] = (2.0f) * (y * z - x * w);
            resMatrix.values[2 + 0 * 3] = (2.0f) * (x * z - y * w);
            resMatrix.values[2 + 1 * 3] = (2.0f) * (y * z + x * w);
            resMatrix.values[2 + 2 * 3] = (1.0f) - (2.0f) * (x * x + y * y);

            return resMatrix;
        }

        //public bool operator ==(aiQuaternion o);
        //public bool operator !=(aiQuaternion o);

        //// transform vector by matrix
        //public aiQuaternion operator *= (mat4 mat);

        public bool Equal(aiQuaternion o, float epsilon = 1e-6f) {
            return Math.Abs(x - o.x) <= epsilon
                && Math.Abs(y - o.y) <= epsilon
                && Math.Abs(z - o.z) <= epsilon
                && Math.Abs(w - o.w) <= epsilon;
        }

        /**
         *  @brief  Will normalize the quaternion representation.
         */
        public aiQuaternion Normalize() {
            // compute the magnitude and divide through it
            float mag = (float)Math.Sqrt(x * x + y * y + z * z + w * w);
            if (mag != 0) {
                float invMag = (1.0f) / mag;
                return new aiQuaternion(w * invMag, x * invMag, y * invMag, z * invMag);
            }
            else {
                Debug.Assert(false);
                return this;
            }
        }

        /**
         *  @brief  Will compute the quaternion conjugate. The result will be stored in the instance.
         */
        public aiQuaternion Conjugate() {
            return new aiQuaternion(w, -x, -y, -z);
        }

        /**
         *  @brief  Rotate a point by this quaternion
         */
        public vec3 Rotate(vec3 v) {
            var q2 = new aiQuaternion(0.0f, v.x, v.y, v.z);
            var q = this;
            var qinv = q;
            qinv.Conjugate();

            q = q * q2 * qinv;
            return new vec3(q.x, q.y, q.z);
        }

        /**
         *  @brief Multiply two quaternions
         *  @param  two   The other quaternion.
         *  @return The result of the multiplication.
         */
        public static aiQuaternion operator *(aiQuaternion left, aiQuaternion right) {
            return new aiQuaternion(
                left.w * right.w - left.x * right.x - left.y * right.y - left.z * right.z,
                left.w * right.x + left.x * right.w + left.y * right.z - left.z * right.y,
                left.w * right.y + left.y * right.w + left.z * right.x - left.x * right.z,
                left.w * right.z + left.z * right.w + left.x * right.y - left.y * right.x);
        }

        /**
         * @brief Performs a spherical interpolation between two quaternions and writes the result into the third.
         * @param pOut Target object to received the interpolated rotation.
         * @param pStart Start rotation of the interpolation at factor == 0.
         * @param pEnd End rotation, factor == 1.
         * @param pFactor Interpolation factor between 0 and 1. Values outside of this range yield undefined results.
         */
        public static void Interpolate(aiQuaternion pOut, aiQuaternion pStart, aiQuaternion pEnd, float pFactor) {
            // calc cosine theta
            float cosom = pStart.x * pEnd.x + pStart.y * pEnd.y + pStart.z * pEnd.z + pStart.w * pEnd.w;

            // adjust signs (if necessary)
            aiQuaternion end = pEnd;
            if (cosom < (0.0)) {
                cosom = -cosom;
                end.x = -end.x; // Reverse all signs
                end.y = -end.y;
                end.z = -end.z;
                end.w = -end.w;
            }

            // Calculate coefficients
            float sclp, sclq;

            if (((1.0) - cosom) > 1e-6/*ai_epsilon*/) // 0.0001 -> some epsillon
            {
                // Standard case (slerp)
                float omega, sinom;
                omega = (float)Math.Acos(cosom); // extract theta from dot product's cos theta
                sinom = (float)Math.Sin(omega);
                sclp = (float)Math.Sin(((1.0) - pFactor) * omega) / sinom;
                sclq = (float)Math.Sin(pFactor * omega) / sinom;
            }
            else {
                // Very close, do linear interp (because it's faster)
                sclp = (1.0f) - pFactor;
                sclq = pFactor;
            }

            pOut.x = sclp * pStart.x + sclq * end.x;
            pOut.y = sclp * pStart.y + sclq * end.y;
            pOut.z = sclp * pStart.z + sclq * end.z;
            pOut.w = sclp * pStart.w + sclq * end.w;
        }

        public override bool Equals(object? obj) {
            return obj is aiQuaternion quaternion && Equals(quaternion);
        }

        public bool Equals(aiQuaternion other) {
            return w == other.w &&
                   x == other.x &&
                   y == other.y &&
                   z == other.z;
        }

        public override int GetHashCode() {
            return HashCode.Combine(w, x, y, z);
        }

        public static aiQuaternion operator *(mat4 mat, aiQuaternion quat) {
            var x = mat.values[0 + 0 * 4] * quat.x + mat.values[0 + 1 * 4] * quat.y + mat.values[0 + 2 * 4] * quat.z + mat.values[0 + 3 * 4] * quat.w;
            var y = mat.values[1 + 0 * 4] * quat.x + mat.values[1 + 1 * 4] * quat.y + mat.values[1 + 2 * 4] * quat.z + mat.values[1 + 3 * 4] * quat.w;
            var z = mat.values[2 + 0 * 4] * quat.x + mat.values[2 + 1 * 4] * quat.y + mat.values[2 + 2 * 4] * quat.z + mat.values[2 + 3 * 4] * quat.w;
            var w = mat.values[3 + 0 * 4] * quat.x + mat.values[3 + 1 * 4] * quat.y + mat.values[3 + 2 * 4] * quat.z + mat.values[3 + 3 * 4] * quat.w;
            return new aiQuaternion(w, x, y, z);
            //aiQuaterniont<float> res;
            //res.x = pMatrix.a1 * pQuaternion.x + pMatrix.a2 * pQuaternion.y + pMatrix.a3 * pQuaternion.z + pMatrix.a4 * pQuaternion.w;
            //res.y = pMatrix.b1 * pQuaternion.x + pMatrix.b2 * pQuaternion.y + pMatrix.b3 * pQuaternion.z + pMatrix.b4 * pQuaternion.w;
            //res.z = pMatrix.c1 * pQuaternion.x + pMatrix.c2 * pQuaternion.y + pMatrix.c3 * pQuaternion.z + pMatrix.c4 * pQuaternion.w;
            //res.w = pMatrix.d1 * pQuaternion.x + pMatrix.d2 * pQuaternion.y + pMatrix.d3 * pQuaternion.z + pMatrix.d4 * pQuaternion.w;
            //return res;
        }

        public static bool operator ==(aiQuaternion left, aiQuaternion right) {
            return left.Equals(right);
        }

        public static bool operator !=(aiQuaternion left, aiQuaternion right) {
            return !(left == right);
        }
    }
}