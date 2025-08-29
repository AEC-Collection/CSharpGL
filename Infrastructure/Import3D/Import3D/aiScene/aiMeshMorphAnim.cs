namespace Import3D {

    /** Describes a morphing animation of a given mesh. */
    public class aiMeshMorphAnim {
        /** Name of the mesh to be animated. An empty string is not allowed,
    *  animated meshes need to be named (not necessarily uniquely,
    *  the name can basically serve as wildcard to select a group
    *  of meshes with similar animation setup)*/
        /*C_STRUCT*/
        public struct mName;

        /** Size of the #mKeys array. Must be 1, at least. */
        public uint mNumKeys;

        /** Key frames of the animation. May not be nullptr. */
        /*C_STRUCT*/
        public aiMeshMorphKey[] mKeys;

        //# ifdef __cplusplus

        //aiMeshMorphAnim() 
        //    : mNumKeys(),
        //      mKeys() { }

        //~aiMeshMorphAnim() {
        //    delete[] mKeys;
        //}

        //#endif

    }
}