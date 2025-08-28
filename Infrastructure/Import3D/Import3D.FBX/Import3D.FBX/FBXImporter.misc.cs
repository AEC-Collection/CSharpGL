using System.Runtime;

namespace Import3D.FBX {
    partial class FBXImporter {
        // ------------------------------------------------------------------------------------------------
        // Setup configuration properties for the loader
        private static void SetupProperties(Importer pImp) {
            mSettings.readAllLayers = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_READ_ALL_GEOMETRY_LAYERS, true);
            mSettings.readAllMaterials = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_READ_ALL_MATERIALS, false);
            mSettings.readMaterials = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_READ_MATERIALS, true);
            mSettings.readTextures = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_READ_TEXTURES, true);
            mSettings.readCameras = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_READ_CAMERAS, true);
            mSettings.readLights = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_READ_LIGHTS, true);
            mSettings.readAnimations = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_READ_ANIMATIONS, true);
            mSettings.readWeights = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_READ_WEIGHTS, true);
            mSettings.strictMode = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_STRICT_MODE, false);
            mSettings.preservePivots = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_PRESERVE_PIVOTS, true);
            mSettings.optimizeEmptyAnimationCurves = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_OPTIMIZE_EMPTY_ANIMATION_CURVES, true);
            mSettings.useLegacyEmbeddedTextureNaming = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_EMBEDDED_TEXTURES_LEGACY_NAMING, false);
            mSettings.removeEmptyBones = pImp.GetPropertyBool(AI_CONFIG_IMPORT_REMOVE_EMPTY_BONES, true);
            mSettings.convertToMeters = pImp.GetPropertyBool(AI_CONFIG_FBX_CONVERT_TO_M, false);
            mSettings.ignoreUpDirection = pImp.GetPropertyBool(AI_CONFIG_IMPORT_FBX_IGNORE_UP_DIRECTION, false);
            mSettings.useSkeleton = pImp.GetPropertyBool(AI_CONFIG_FBX_USE_SKELETON_BONE_CONTAINER, false);
        }

        /// @brief Will check the file for readability.
        // Returns whether the class can handle the format of the given file.
        private static bool CanRead(string pFile, bool checkSig) {
            // at least ASCII-FBX files usually have a 'FBX' somewhere in their head
            static char* tokens[] = { " \n\r\n " };
            return SearchFileHeaderForToken(pIOHandler, pFile, tokens, AI_COUNT_OF(tokens));
        }

        private static bool IsSpaceOrNewLine(char c) {
            return c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\0' || c == '\f';
        }


        private static void TokenizeError(string msg, int line, int column) {
            throw new Exception($"{msg} @ line:{line}, column:{column}");
        }
        private static bool IsLineEnd(char c) {
            return (c == '\r' || c == '\n' || c == '\0' || c == '\f');
        }
    }
}
