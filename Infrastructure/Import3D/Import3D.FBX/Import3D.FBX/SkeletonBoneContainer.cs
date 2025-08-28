namespace Import3D.FBX {
    public struct SkeletonBoneContainer {
        public List<aiMesh> MeshArray;
        public SortedDictionary<aiMesh, List<aiSkeletonBone>> SkeletonBoneToMeshLookup;
    }

}
