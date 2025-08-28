using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

namespace Import3D.FBX {
    /// Loads the Autodesk FBX file format.
    ///
    /// See http://en.wikipedia.org/wiki/FBX
    public partial class FBXImporter {
        FBXImportSettings mSettings;

        string magicHeader = "Kaydara FBX Binary";
        public unsafe static void InternReadFile(string pFile, aiScene pScene) {
            var allBytes = File.ReadAllBytes(pFile);
            Debug.Assert(allBytes != null);
            fixed (byte* pBytes = allBytes) {
                List<FBXToken> tokenList;
                bool is_binary = false;
                if (Import3D.Utility.strncmp(pBytes, magicHeader) == 0) {
                    is_binary = true;
                    tokenList = TokenizeBinary(pBytes, allBytes.Length);
                }
                else {
                    tokenList = Tokenize(pBytes, allBytes.Length);
                }

                // use this information to construct a very rudimentary
                // parse-tree representing the FBX scope structure
                FBXParser parser = new FBXParser(tokenList, is_binary);

                // take the raw parse-tree and convert it to a FBX DOM
                var mSettings = new FBXImportSettings();
                var doc = new FBXDocument(parser, mSettings);

                // convert the FBX DOM to aiScene
                ConvertToAssimpScene(pScene, doc, mSettings.removeEmptyBones);

                // size relative to cm
                float size_relative_to_cm = doc.GlobalSettings().UnitScaleFactor();
                if (size_relative_to_cm == 0.0) {
                    // BaseImporter later asserts that fileScale is non-zero.
                    throw new Exception("The UnitScaleFactor must be non-zero");
                }

                // Set FBX file scale is relative to CM must be converted to M for
                // assimp universal format (M)
                SetFileScale(size_relative_to_cm * 0.01f);

                // This collection does not own the memory for the tokens, but we need to call their d'tor
                std::for_each(tokens.begin(), tokens.end(), Util::destructor_fun<Token>());

            }

        }

        private static void ConvertToAssimpScene(aiScene pScene, FBXDocument doc, bool removeEmptyBones) {
            throw new NotImplementedException();
        }

        private static unsafe void TokenizeError(string v, byte* input, byte* cursor) {
            var offset = cursor - input;
            TokenizeError(v, offset);
        }

        private static void TokenizeError(string msg, long offset) {
            throw new Exception($"{msg} @ 0x{Convert.ToString(offset, 16)}");
        }
    }
}
