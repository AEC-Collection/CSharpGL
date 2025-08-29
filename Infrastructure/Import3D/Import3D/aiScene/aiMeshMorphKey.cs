namespace Import3D {

    /** Binds a morph anim mesh to a specific point in time. */
    public class aiMeshMorphKey {
        /** The time of this key */
        public double mTime;

        /** The values and weights at the time of this key
         *   - mValues: index of attachment mesh to apply weight at the same position in mWeights
         *   - mWeights: weight to apply to the blend shape index at the same position in mValues
         */
        public uint[] mValues;
        public double[] mWeights;

        /** The number of values and weights */
        public uint mNumValuesAndWeights;
        //# ifdef __cplusplus
        //aiMeshMorphKey() 
        //    : mTime(0.0),
        //      mValues(nullptr),
        //      mWeights(nullptr),
        //      mNumValuesAndWeights(0) {
        //}

        //~aiMeshMorphKey() {
        //    if (mNumValuesAndWeights && mValues && mWeights) {
        //        delete[] mValues;
        //        delete[] mWeights;
        //    }
        //}
        //#endif

    }
}