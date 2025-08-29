
namespace Import3D {
    /** Binds a anim-mesh to a specific point in time. */
    public struct aiMeshKey : IEquatable<aiMeshKey> {
        /** The time of this key */
        public double mTime;

        /** Index into the aiMesh::mAnimMeshes array of the
         *  mesh corresponding to the #aiMeshAnim hosting this
         *  key frame. The referenced anim mesh is evaluated
         *  according to the rules defined in the docs for #aiAnimMesh.*/
        public uint mValue;

        //# ifdef __cplusplus

        //aiMeshKey() 
        //    : mTime(0.0),
        //      mValue(0) {
        //}

        /** Construction from a given time and key value */
        aiMeshKey(double time, uint value) {
            this.mTime = time;
            this.mValue = value;
        }

        public override bool Equals(object? obj) {
            return obj is aiMeshKey key && Equals(key);
        }

        public bool Equals(aiMeshKey other) {
            return mTime == other.mTime &&
                   mValue == other.mValue;
        }

        public override int GetHashCode() {
            return HashCode.Combine(mTime, mValue);
        }

        public static bool operator ==(aiMeshKey left, aiMeshKey right) {
            return left.Equals(right);
        }

        public static bool operator !=(aiMeshKey left, aiMeshKey right) {
            return !(left == right);
        }

        //typedef uint elem_type;

        //// Comparison operators. For use with std::find();
        //bool operator ==(aiMeshKey &o) {
        //    return o.mValue == this->mValue;
        //}
        //bool operator !=(aiMeshKey &o) {
        //    return o.mValue != this->mValue;
        //}

        //// Relational operators. For use with std::sort();
        //bool operator <(aiMeshKey &o) {
        //    return mTime < o.mTime;
        //}
        //bool operator >(aiMeshKey &o) {
        //    return mTime > o.mTime;
        //}
        public static bool operator <(aiMeshKey left, aiMeshKey right) {
            return left.mTime < right.mTime;
        }
        public static bool operator >(aiMeshKey left, aiMeshKey right) {
            return left.mTime > right.mTime;
        }

        //#endif

    }
}