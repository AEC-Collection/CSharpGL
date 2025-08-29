namespace Import3D {
    /** An animation consists of key-frame data for a number of nodes. For
 *  each node affected by the animation a separate series of data is given.*/

    public unsafe struct aiAnimation {
        /** The name of the animation. If the modeling package this data was
     *  exported from does support only a single animation channel, this
     *  name is usually empty (length is zero). */
        /*C_STRUCT*/
        public string mName;

        /** Duration of the animation in ticks.  */
        public double mDuration;

        /** Ticks per second. 0 if not specified in the imported file */
        public double mTicksPerSecond;

        /** The number of bone animation channels. Each channel affects
         *  a single node. */
        public uint mNumChannels;

        /** The node animation channels. Each channel affects a single node.
         *  The array is mNumChannels in size. */
        /*C_STRUCT*/
        public aiNodeAnim[] mChannels;

        /** The number of mesh animation channels. Each channel affects
         *  a single mesh and defines vertex-based animation. */
        public uint mNumMeshChannels;

        /** The mesh animation channels. Each channel affects a single mesh.
         *  The array is mNumMeshChannels in size. */
        /*C_STRUCT*/
        public aiMeshAnim[] mMeshChannels;

        /** The number of mesh animation channels. Each channel affects
         *  a single mesh and defines morphing animation. */
        public uint mNumMorphMeshChannels;

        /** The morph mesh animation channels. Each channel affects a single mesh.
         *  The array is mNumMorphMeshChannels in size. */
        /*C_STRUCT*/
        public aiMeshMorphAnim[] mMorphMeshChannels;

        //# ifdef __cplusplus
        public aiAnimation()
            //: mDuration(-1.),
            //  mTicksPerSecond(0.),
            //  mNumChannels(0),
            //  mChannels(nullptr),
            //  mNumMeshChannels(0),
            //  mMeshChannels(nullptr),
            //  mNumMorphMeshChannels(0),
            //  mMorphMeshChannels(nullptr)
            {
            // empty
        }

        //~aiAnimation() {
        //    // DO NOT REMOVE THIS ADDITIONAL CHECK
        //    if (mNumChannels && mChannels) {
        //        for (uint a = 0; a < mNumChannels; a++) {
        //            delete mChannels[a];
        //        }

        //        delete[] mChannels;
        //    }
        //    if (mNumMeshChannels && mMeshChannels) {
        //        for (uint a = 0; a < mNumMeshChannels; a++) {
        //            delete mMeshChannels[a];
        //        }

        //        delete[] mMeshChannels;
        //    }
        //    if (mNumMorphMeshChannels && mMorphMeshChannels) {
        //        for (uint a = 0; a < mNumMorphMeshChannels; a++) {
        //            delete mMorphMeshChannels[a];
        //        }

        //        delete[] mMorphMeshChannels;
        //    }
        //}
        //#endif // __cplusplus

    }
}