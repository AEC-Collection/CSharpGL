using System;

namespace Import3D {
    /** A time-value pair specifying a rotation for the given time.
 *  Rotations are expressed with quaternions. */
    public struct aiQuatKey : IEquatable<aiQuatKey> {
        /** The time of this key */
        public double mTime;

        /** The value of this key */
        /*C_STRUCT*/
        public aiQuaternion mValue = new aiQuaternion();

        /** The interpolation setting of this key */
        public aiAnimInterpolation mInterpolation = aiAnimInterpolation.aiAnimInterpolation_Linear;

        //#ifdef __cplusplus
        public aiQuatKey()
            //: mTime(0.0), mValue(), mInterpolation(aiAnimInterpolation_Linear)
            { }

        /** Construction from a given time and key value */
        public aiQuatKey(double time, aiQuaternion value) {
            this.mTime = time;
            this.mValue = value;
            this.mInterpolation = aiAnimInterpolation.aiAnimInterpolation_Linear;
        }

        public override bool Equals(object? obj) {
            return obj is aiQuatKey key && Equals(key);
        }

        public bool Equals(aiQuatKey other) {
            return mTime == other.mTime &&
                   mValue.Equals(other.mValue) &&
                   mInterpolation == other.mInterpolation;
        }

        public override int GetHashCode() {
            return HashCode.Combine(mTime, mValue, mInterpolation);
        }

        public static bool operator ==(aiQuatKey left, aiQuatKey right) {
            return left.Equals(right);
        }

        public static bool operator !=(aiQuatKey left, aiQuatKey right) {
            return !(left == right);
        }

        //typedef aiQuaternion elem_type;

        //    // Comparison operators. For use with std::find();
        //    bool operator ==(aiQuatKey &rhs) {
        //            return rhs.mValue == this->mValue;
        //        }

        //        bool operator !=(aiQuatKey &rhs) {
        //            return rhs.mValue != this->mValue;
        //        }

        //        // Relational operators. For use with std::sort();
        //        bool operator <(aiQuatKey &rhs) {
        //            return mTime < rhs.mTime;
        //        }

        //        bool operator >(aiQuatKey &rhs) {
        //            return mTime > rhs.mTime;
        //        }
        //#endif

    }
}