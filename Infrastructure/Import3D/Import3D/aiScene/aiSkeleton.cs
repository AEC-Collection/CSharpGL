using System.Numerics;

namespace Import3D {
    /**
   * @brief A skeleton represents the bone hierarchy of an animation.
   *
   * Skeleton animations can be described as a tree of bones:
   *                  root
   *                    |
   *                  node1
   *                  /   \
   *               node3  node4
   * If you want to calculate the transformation of node three you need to compute the
   * transformation hierarchy for the transformation chain of node3:
   * root->node1->node3
   * Each node is represented as a skeleton instance.
   */
    public unsafe class aiSkeleton {
        /**
     *  @brief The name of the skeleton instance.
     */
        public string mName;

        /**
         *  @brief  The number of bones in the skeleton.
         */
        public uint mNumBones;

        /**
         *  @brief The bone instance in the skeleton.
         */
        /*C_STRUCT*/
        public aiSkeletonBone[] mBones;

        //# ifdef __cplusplus
        /**
         *  @brief The class constructor.
         */
        //public aiSkeleton()
        //: mName(), mNumBones(0), mBones(null) 
        //{
        // empty
        //}

        ///**
        //         *  @brief  The class destructor.
        //         */
        //        ~aiSkeleton() {
        //            delete[] mBones;
        //        }
        //#endif // __cplusplus


    }
}