namespace Import3D.STL {
    public unsafe partial class STLImporter {
        class BinContext {
            public readonly FileStream file;
            public readonly aiScene scene;
            // the default vertex color is light gray.
            public vec4 mClrColorDefault = new vec4(0.6f);

            public BinContext(FileStream file, aiScene scene) {
                this.file = file;
                this.scene = scene;
            }
        }
    }
}
