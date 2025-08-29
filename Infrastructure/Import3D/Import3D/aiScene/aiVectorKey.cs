using System;

namespace Import3D {
    /** A time-value pair specifying a certain 3D vector for the given time. */
    public struct aiVectorKey : IEquatable<aiVectorKey> {
        /** The time of this key */
        public double mTime;

        /** The value of this key */
        /*C_STRUCT*/
        public vec3 mValue;

        /** The interpolation setting of this key */
        public aiAnimInterpolation mInterpolation = aiAnimInterpolation.aiAnimInterpolation_Linear;

        //# ifdef __cplusplus

        /// @brief  The default constructor.
        public aiVectorKey()
                //: mTime(0.0), mValue(), mInterpolation(aiAnimInterpolation_Linear)
                { }

        /// @brief  Construction from a given time and key value.
        public aiVectorKey(double time, vec3 value) {
            this.mTime = time;
            this.mValue = value;
            this.mInterpolation = aiAnimInterpolation.aiAnimInterpolation_Linear;
        }

        public override bool Equals(object? obj) {
            return obj is aiVectorKey key && Equals(key);
        }

        public bool Equals(aiVectorKey other) {
            return mTime == other.mTime &&
                   mValue.Equals(other.mValue) &&
                   mInterpolation == other.mInterpolation;
        }

        public override int GetHashCode() {
            return HashCode.Combine(mTime, mValue, mInterpolation);
        }

        public static bool operator ==(aiVectorKey left, aiVectorKey right) {
            return left.Equals(right);
        }

        public static bool operator !=(aiVectorKey left, aiVectorKey right) {
            return !(left == right);
        }
        public static bool operator <(aiVectorKey left, aiVectorKey right) {
            return left.mTime < right.mTime;
        }
        public static bool operator >(aiVectorKey left, aiVectorKey right) {
            return left.mTime > right.mTime;
        }

        //typedef vec3 elem_type;

        //// Comparison operators. For use with std::find();
        //bool operator ==(aiVectorKey &rhs) {
        //    return rhs.mValue == this->mValue;
        //}

        //bool operator !=(aiVectorKey &rhs) {
        //    return rhs.mValue != this->mValue;
        //}

        //// Relational operators. For use with std::sort();
        //bool operator <(aiVectorKey &rhs) {
        //    return mTime < rhs.mTime;
        //}

        //bool operator >(aiVectorKey &rhs) {
        //    return mTime > rhs.mTime;
        //}
        //#endif // __cplusplus

    }

    public enum aiAnimInterpolation {
        /** */
        aiAnimInterpolation_Step,

        /** */
        aiAnimInterpolation_Linear,

        /** */
        aiAnimInterpolation_Spherical_Linear,

        /** */
        aiAnimInterpolation_Cubic_Spline,

        /** */
        //# ifndef SWIG
        //_aiAnimInterpolation_Force32Bit = INT_MAX
        //#endif
    };

}