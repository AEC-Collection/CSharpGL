using System;

namespace Import3D.FBX {
    /**
 *  @brief DOM base class for all kinds of FBX geometry
 */
    public unsafe class FBXGeometry {
        /// @brief The class constructor with all parameters.
        /// @param id       The id.
        /// @param element  The element instance
        /// @param name     The name instance
        /// @param doc      The document instance
        public FBXGeometry(UInt64 id, FBXElement element, string name, FBXDocument doc)
            : base(id, element, name) {
            List<FBXConnection> & conns = doc.GetConnectionsByDestinationSequenced(ID(), "Deformer");
            for (FBXConnection con : conns) {
                FBXSkin* sk = ProcessSimpleConnection<FBXSkin>(*con, false, "Skin . Geometry", element);
                if (sk) {
                    skin = sk;
                }
                FBXBlendShape* bsp = ProcessSimpleConnection<FBXBlendShape>(*con, false, "BlendShape . Geometry", element);
                if (bsp) {
                    auto pr = blendShapes.insert(bsp);
                    if (!pr.second) {
                        FBXImporter::LogWarn("there is the same blendShape id ", bsp.ID());
                    }
                }
            }

        }


        /// @brief Get the Skin attached to this geometry or null.
        /// @return The deformer skip instance as a pointer, null if none.
        FBXSkin DeformerSkin() {
            return skin;

        }

        /// @brief Get the BlendShape attached to this geometry or null
        /// @return The blendshape arrays.
        HashSet<FBXBlendShape> GetBlendShapes() {
            return blendShapes;

        }

        FBXSkin skin;
        HashSet<FBXBlendShape> blendShapes;


    }
}
