namespace Import3D {
    /** Describes vertex-based animations for a single mesh or a group of
     *  meshes. Meshes carry the animation data for each frame in their
     *  aiMesh::mAnimMeshes array. The purpose of aiMeshAnim is to
     *  define keyframes linking each mesh attachment to a particular
     *  point in time. */
    public class aiMeshAnim {
        /** Name of the mesh to be animated. An empty string is not allowed,
     *  animated meshes need to be named (not necessarily uniquely,
     *  the name can basically serve as wild-card to select a group
     *  of meshes with similar animation setup)*/
        /*C_STRUCT*/
        public string mName;

        /** Size of the #mKeys array. Must be 1, at least. */
        public uint mNumKeys;

        /** Key frames of the animation. May not be nullptr. */
        public aiMeshKey[] mKeys;

        //# ifdef __cplusplus

        //aiMeshAnim() 
        //    : mNumKeys(),
        //      mKeys() { }

        //~aiMeshAnim() {
        //    delete[] mKeys;
        //}

        //#endif

    }
}