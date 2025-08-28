namespace Import3D.FBX {
    /** FBX import settings, parts of which are publicly accessible via their corresponding AI_CONFIG constants */
    internal class FBXImportSettings {

        /** enable strict mode:
         *   - only accept fbx 2012, 2013 files
         *   - on the slightest error, give up.
         *
         *  Basically, strict mode means that the fbx file will actually
         *  be validated. Strict mode is off by default. */
        bool strictMode = true;

        /** specifies whether all geometry layers are read and scanned for
          * usable data channels. The FBX spec indicates that many readers
          * will only read the first channel and that this is in some way
          * the recommended way- in reality, however, it happens a lot that
          * vertex data is spread among multiple layers. The default
          * value for this option is true.*/
        bool readAllLayers = true;

        /** specifies whether all materials are read, or only those that
         *  are referenced by at least one mesh. Reading all materials
         *  may make FBX reading a lot slower since all objects
         *  need to be processed .
         *  This bit is ignored unless readMaterials=true*/
        bool readAllMaterials;

        /** import materials (true) or skip them and assign a default
         *  material. The default value is true.*/
        bool readMaterials = true;

        /** import embedded textures? Default value is true.*/
        bool readTextures = true;

        /** import cameras? Default value is true.*/
        bool readCameras = true;

        /** import light sources? Default value is true.*/
        bool readLights = true;

        /** import animations (i.e. animation curves, the node
         *  skeleton is always imported). Default value is true. */
        bool readAnimations = true;

        /** read bones (vertex weights and deform info).
         *  Default value is true. */
        bool readWeights = true;

        /** will convert all animation data into a skeleton (experimental)
         *  Default value is false.
         */
        bool useSkeleton;

        /** preserve transformation pivots and offsets. Since these can
         *  not directly be represented in assimp, additional dummy
         *  nodes will be generated. Note that settings this to false
         *  can make animation import a lot slower. The default value
         *  is true.
         *
         *  The naming scheme for the generated nodes is:
         *    <OriginalName>_$AssimpFbx$_<TransformName>
         *
         *  where <TransformName> is one of
         *    RotationPivot
         *    RotationOffset
         *    PreRotation
         *    PostRotation
         *    ScalingPivot
         *    ScalingOffset
         *    Translation
         *    Scaling
         *    Rotation
         **/
        bool preservePivots = true;

        /** do not import animation curves that specify a constant
         *  values matching the corresponding node transformation.
         *  The default value is true. */
        bool optimizeEmptyAnimationCurves = true;

        /** use legacy naming for embedded textures eg: (*0, *1, *2)
        */
        bool useLegacyEmbeddedTextureNaming;

        /** Empty bones shall be removed
        */
        bool removeEmptyBones = true;

        /** Set to true to perform a conversion from cm to meter after the import
        */
        bool convertToMeters;

        // Set to true to ignore the axis configuration in the file
        bool ignoreUpDirection = false;


        public FBXImportSettings() {
        }
    }
}