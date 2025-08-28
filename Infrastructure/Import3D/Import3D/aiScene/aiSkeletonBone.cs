namespace Import3D {
    /**
 * @brief  A skeleton bone represents a single bone is a skeleton structure.
 *
 * Skeleton-Animations can be represented via a skeleton struct, which describes
 * a hierarchical tree assembled from skeleton bones. A bone is linked to a mesh.
 * The bone knows its parent bone. If there is no parent bone the parent id is
 * marked with -1.
 * The skeleton-bone stores a pointer to its used armature. If there is no
 * armature this value if set to null.
 * A skeleton bone stores its offset-matrix, which is the absolute transformation
 * for the bone. The bone stores the locale transformation to its parent as well.
 * You can compute the offset matrix by multiplying the hierarchy like:
 * Tree: s1 -> s2 -> s3
 * Offset-Matrix s3 = locale-s3 * locale-s2 * locale-s1
 */

    public unsafe class aiSkeletonBone {
        /// The parent bone index, is -1 one if this bone represents the root bone.
        public int mParent;


        //# ifndef ASSIMP_BUILD_NO_ARMATUREPOPULATE_PROCESS
        /// @brief The bone armature node - used for skeleton conversion
        /// you must enable aiProcess_PopulateArmatureData to populate this
        public aiNode mArmature;

        /// @brief The bone node in the scene - used for skeleton conversion
        /// you must enable aiProcess_PopulateArmatureData to populate this
        public aiNode* mNode;

        //#endif
        /// @brief The number of weights
        public uint mNumnWeights;

        /// The mesh index, which will get influenced by the weight.
        public aiMesh mMeshId;

        /// The influence weights of this bone, by vertex index.
        public aiVertexWeight[] mWeights;

        /** Matrix that transforms from bone space to mesh space in bind pose.
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
        public mat4 mOffsetMatrix = new mat4();

        /// Matrix that transforms the locale bone in bind pose.
        public mat4 mLocalMatrix = new mat4();

        //# ifdef __cplusplus
        ///	@brief The class constructor.
        public aiSkeletonBone()
             //            mParent(-1),
             ////#ifndef ASSIMP_BUILD_NO_ARMATUREPOPULATE_PROCESS
             //            mArmature(null),
             //            mNode(null),
             ////#endif
             //            mNumnWeights(0),
             //            mMeshId(null),
             //            mWeights(null),
             //            mOffsetMatrix(),
             //            mLocalMatrix() 
             {
            // empty
            this.mParent = -1;
        }

        /// @brief The class constructor with its parent
        /// @param  parent      The parent node index.
        public aiSkeletonBone(uint parent)
             //            mParent(parent),
             //#ifndef ASSIMP_BUILD_NO_ARMATUREPOPULATE_PROCESS
             //            mArmature(null),
             //            mNode(null),
             //#endif
             //            mNumnWeights(0),
             //            mMeshId(null),
             //            mWeights(null),
             //            mOffsetMatrix(),
             //            mLocalMatrix()
             {
            // empty
            this.mParent = (int)parent;
        }
        //        /// @brief The class destructor.
        //        ~aiSkeletonBone() {
        //            delete[] mWeights;
        //            mWeights = null;
        //        }
        //#endif // __cplusplus

    }
}