using System;

namespace Import3D {
    /** Describes the animation of a single node. The name specifies the
     *  bone/node which is affected by this animation channel. The keyframes
     *  are given in three separate series of values, one each for position,
     *  rotation and scaling. The transformation matrix computed from these
     *  values replaces the node's original transformation matrix at a
     *  specific time.
     *  This means all keys are absolute and not relative to the bone default pose.
     *  The order in which the transformations are applied is
     *  - as usual - scaling, rotation, translation.
     *
     *  @note All keys are returned in their correct, chronological order.
     *  Duplicate keys don't pass the validation step. Most likely there
     *  will be no negative time values, but they are not forbidden also ( so
     *  implementations need to cope with them! ) */

    public unsafe class aiNodeAnim {
        /** The name of the node affected by this animation. The node
      *  must exist and it must be unique.*/
        /*C_STRUCT*/
        public string mNodeName;

        /** The number of position keys */
        public uint mNumPositionKeys;

        /** The position keys of this animation channel. Positions are
         * specified as 3D vector. The array is mNumPositionKeys in size.
         *
         * If there are position keys, there will also be at least one
         * scaling and one rotation key.*/
        /*C_STRUCT*/
        public aiVectorKey[] mPositionKeys;

        /** The number of rotation keys */
        public uint mNumRotationKeys;

        /** The rotation keys of this animation channel. Rotations are
         *  given as quaternions,  which are 4D vectors. The array is
         *  mNumRotationKeys in size.
         *
         * If there are rotation keys, there will also be at least one
         * scaling and one position key. */
        /*C_STRUCT*/
        public aiQuatKey[] mRotationKeys;

        /** The number of scaling keys */
        public uint mNumScalingKeys;

        /** The scaling keys of this animation channel. Scalings are
         *  specified as 3D vector. The array is mNumScalingKeys in size.
         *
         * If there are scaling keys, there will also be at least one
         * position and one rotation key.*/
        /*C_STRUCT*/
        public aiVectorKey[] mScalingKeys;

        /** Defines how the animation behaves before the first
         *  key is encountered.
         *
         *  The default value is aiAnimBehaviour_DEFAULT (the original
         *  transformation matrix of the affected node is used).*/
        public aiAnimBehaviour mPreState = aiAnimBehaviour.aiAnimBehaviour_DEFAULT;

        /** Defines how the animation behaves after the last
         *  key was processed.
         *
         *  The default value is aiAnimBehaviour_DEFAULT (the original
         *  transformation matrix of the affected node is taken).*/
        public aiAnimBehaviour mPostState = aiAnimBehaviour.aiAnimBehaviour_DEFAULT;

        //#ifdef __cplusplus
        public aiNodeAnim()
                   //: mNumPositionKeys(0),
                   //  mPositionKeys(nullptr),
                   //  mNumRotationKeys(0),
                   //  mRotationKeys(nullptr),
                   //  mNumScalingKeys(0),
                   //  mScalingKeys(nullptr),
                   //  mPreState(aiAnimBehaviour_DEFAULT),
                   //  mPostState(aiAnimBehaviour_DEFAULT) 
                   {
            // empty
        }

        //        ~aiNodeAnim() {
        //            delete[] mPositionKeys;
        //            delete[] mRotationKeys;
        //            delete[] mScalingKeys;
        //        }
        //#endif // __cplusplus

    }

    /** Defines how an animation channel behaves outside the defined time
 *  range. This corresponds to aiNodeAnim::mPreState and
 *  aiNodeAnim::mPostState.*/
    public enum aiAnimBehaviour {
        /** The value from the default node transformation is taken*/
        aiAnimBehaviour_DEFAULT = 0x0,

        /** The nearest key value is used without interpolation */
        aiAnimBehaviour_CONSTANT = 0x1,

        /** The value of the nearest two keys is linearly
         *  extrapolated for the current time value.*/
        aiAnimBehaviour_LINEAR = 0x2,

        /** The animation is repeated.
         *
         *  If the animation key go from n to m and the current
         *  time is t, use the value at (t-n) % (|m-n|).*/
        aiAnimBehaviour_REPEAT = 0x3,

        /** This value is not used, it is just here to force the
             *  the compiler to map this enum to a 32 Bit integer  */
        //# ifndef SWIG
        //        _aiAnimBehaviour_Force32Bit = INT_MAX
        //#endif
    };

}