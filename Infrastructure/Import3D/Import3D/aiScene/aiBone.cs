using Import3D;
using System.Numerics;
using System.Xml.Linq;

namespace Import3D {
    // ---------------------------------------------------------------------------
    /** @brief A single bone of a mesh.
     *
     *  A bone has a name by which it can be found in the frame hierarchy and by
     *  which it can be addressed by animations. In addition it has a number of
     *  influences on vertices, and a matrix relating the mesh position to the
     *  position of the bone at the time of binding.
     */

    public unsafe class aiBone : IEquatable<aiBone> {
        /**
    * The name of the bone.
    */
        public string mName;

        /**
         * The number of vertices affected by this bone.
         * The maximum value for this member is #AI_MAX_BONE_WEIGHTS.
         */
        public uint mNumWeights;

        //# ifndef ASSIMP_BUILD_NO_ARMATUREPOPULATE_PROCESS
        /**
         * The bone armature node - used for skeleton conversion
         * you must enable aiProcess_PopulateArmatureData to populate this
         */
        aiNode* mArmature;

        /**
         * The bone node in the scene - used for skeleton conversion
         * you must enable aiProcess_PopulateArmatureData to populate this
         */
        public aiNode* mNode;

        //#endif
        /**
         * The influence weights of this bone, by vertex index.
         */
        public aiVertexWeight[] mWeights;

        /**
         * Matrix that transforms from mesh space to bone space in bind pose.
         *
         * This matrix describes the position of the mesh
         * in the local space of this bone when the skeleton was bound.
         * Thus it can be used directly to determine a desired vertex position,
         * given the world-space transform of the bone when animated,
         * and the position of the vertex in mesh space.
         *
         * It is sometimes called an inverse-bind matrix,
         * or inverse bind pose matrix.
         */
        public mat4 mOffsetMatrix;

        //# ifdef __cplusplus

        ///	@brief  Default constructor
        public aiBone()
               //            : mName(),
               //              mNumWeights(0),
               //#ifndef ASSIMP_BUILD_NO_ARMATUREPOPULATE_PROCESS
               //              mArmature(null),
               //              mNode(null),
               //#endif
               //              mWeights(null),
               //              mOffsetMatrix() 
               {
            // empty
        }

        /// @brief  Copy constructor
        public aiBone(aiBone other)
            //            :
            //            mName(other.mName),
            //            mNumWeights(other.mNumWeights),
            //#ifndef ASSIMP_BUILD_NO_ARMATUREPOPULATE_PROCESS
            //            mArmature(null),
            //            mNode(null),
            //#endif
            //            mWeights(null),
            //            mOffsetMatrix(other.mOffsetMatrix) 
            {
            this.mName = other.mName;
            this.mNumWeights = other.mNumWeights;
            this.mOffsetMatrix = other.mOffsetMatrix;
            copyVertexWeights(other);
        }

        void copyVertexWeights(aiBone other) {
            if (other.mWeights == null || other.mNumWeights == 0) {
                mWeights = null;
                mNumWeights = 0;
                return;
            }

            mNumWeights = other.mNumWeights;
            //        if (mWeights) {
            //            delete[] mWeights;
            //}

            mWeights = new aiVertexWeight[mNumWeights];
            //memcpy(mWeights, other.mWeights, mNumWeights * sizeof(aiVertexWeight));
            Array.Copy(other.mWeights, mWeights, mWeights.Length);
        }

        public override bool Equals(object? obj) {
            return obj is aiBone bone && Equals(bone);
        }

        public bool Equals(aiBone other) {
            //return mName == other.mName &&
            //mNumWeights == other.mNumWeights &&
            //EqualityComparer<aiNode*>.Default.Equals(mArmature, other.mArmature) &&
            //EqualityComparer<aiNode*>.Default.Equals(mNode, other.mNode) &&
            //EqualityComparer<aiVertexWeight[]>.Default.Equals(mWeights, other.mWeights) &&
            //EqualityComparer<mat4>.Default.Equals(mOffsetMatrix, other.mOffsetMatrix);
            if (this.mName != other.mName
                || this.mNumWeights != other.mNumWeights
                || this.mOffsetMatrix != other.mOffsetMatrix
                ) return false;
            for (var i = 0; i < mNumWeights; ++i) {
                if (this.mWeights[i] != other.mWeights[i]) {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode() {
            return HashCode.Combine(mName, mNumWeights/*, mArmature, mNode*/, mWeights, mOffsetMatrix);
        }

        public static bool operator ==(aiBone left, aiBone right) {
            return left.Equals(right);
        }

        public static bool operator !=(aiBone left, aiBone right) {
            return !(left == right);
        }

        ////! @brief Assignment operator
        //aiBone operator=(aiBone other) {
        //    if (this == other) {
        //        return *this;
        //    }

        //    mName = other.mName;
        //    mNumWeights = other.mNumWeights;
        //    mOffsetMatrix = other.mOffsetMatrix;
        //    copyVertexWeights(other);

        //    return *this;
        //}

        ///// @brief Compare operator.
        //bool operator ==(aiBone rhs) {
        //    if (mName != rhs.mName || mNumWeights != rhs.mNumWeights) {
        //        return false;
        //    }

        //    for (var i = 0; i < mNumWeights; ++i) {
        //        if (mWeights[i] != rhs.mWeights[i]) {
        //            return false;
        //        }
        //    }

        //    return true;
        //}
        //! @brief Destructor - deletes the array of vertex weights
        //        ~aiBone() {
        //            delete[] mWeights;
        //        }
        //#endif // __cplusplus

    }
}