using System.Security.Cryptography;

namespace Import3D {
    // ---------------------------------------------------------------------------
    /** @brief A single influence of a bone on a vertex.
     */

    public struct aiVertexWeight : IEquatable<aiVertexWeight> {
        //! Index of the vertex which is influenced by the bone.
        public uint mVertexId;

        //! The strength of the influence in the range (0...1).
        //! The influence from all bones at one vertex amounts to 1.
        public float mWeight;

        //# ifdef __cplusplus

        //! @brief Default constructor
        public aiVertexWeight()
            //: mVertexId(0),
            //  mWeight(0.0f)
            {
            // empty
        }

        //! @brief Initialization from a given index and vertex weight factor
        //! \param pID ID
        //! \param pWeight Vertex weight factor
        public aiVertexWeight(uint pID, float pWeight)
            //:
            //mVertexId(pID), mWeight(pWeight)
            {
            // empty
            this.mVertexId = pID; this.mWeight = pWeight;
        }

        public override bool Equals(object? obj) {
            return obj is aiVertexWeight weight && Equals(weight);
        }

        public bool Equals(aiVertexWeight other) {
            return mVertexId == other.mVertexId &&
                   mWeight == other.mWeight;
        }

        public override int GetHashCode() {
            return HashCode.Combine(mVertexId, mWeight);
        }

        public static bool operator ==(aiVertexWeight left, aiVertexWeight right) {
            return left.Equals(right);
        }

        public static bool operator !=(aiVertexWeight left, aiVertexWeight right) {
            return !(left == right);
        }

        //bool operator ==(aiVertexWeight rhs) {
        //    return (mVertexId == rhs.mVertexId && mWeight == rhs.mWeight);
        //}

        //bool operator !=(aiVertexWeight rhs) {
        //    return (*this == rhs);
        //}

        //#endif // __cplusplus

    }
}