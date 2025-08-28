using System.Diagnostics;
using System.Xml.Linq;
using System;

namespace Import3D.FBX {
    /** DOM class for generic FBX materials */
    public unsafe class FBXMaterial : FBXObject {
        public FBXMaterial(UInt64 id, FBXElement element, FBXDocument doc, string name)
            : base(id, element, name) {
            FBXScope sc = GetRequiredScope(element);

            FBXElement* ShadingModel = sc["ShadingModel"];
            FBXElement* MultiLayer = sc["MultiLayer"];

            if (MultiLayer) {
                multilayer = !!ParseTokenAsInt(GetRequiredToken(*MultiLayer, 0));
            }

            if (ShadingModel) {
                shading = ParseTokenAsString(GetRequiredToken(*ShadingModel, 0));
            }
            else {
                DOMWarning("shading mode not specified, assuming phong", &element);
                shading = "phong";
            }

            // lower-case shading because Blender (for example) writes "Phong"
            for (int i = 0; i < shading.length(); ++i) {
                shading[i] = static_cast<char>(tolower(static_cast < unsigned char > (shading[i])));
            }
            string templateName;
            if (shading == "phong") {
                templateName = "Material.FbxSurfacePhong";
            }
            else if (shading == "lambert") {
                templateName = "Material.FbxSurfaceLambert";
            }
            else {
                DOMWarning("shading mode not recognized: " + shading, &element);
            }

            props = GetPropertyTable(doc, templateName, element, sc);

            // resolve texture links
            List<FBXConnection> & conns = doc.GetConnectionsByDestinationSequenced(ID());
            for (FBXConnection con : conns) {
                // texture link to properties, not objects
                if (0 == con.PropertyName().length()) {
                    continue;
                }

                FBXObject* ob = con.SourceObject();
                if (null == ob) {
                    DOMWarning("failed to read source object for texture link, ignoring", &element);
                    continue;
                }

                FBXTexture* tex = dynamic_cast<FBXTexture*>(ob);
                if (null == tex) {
                    FBXLayeredTexture* layeredTexture = dynamic_cast<FBXLayeredTexture*>(ob);
                    if (!layeredTexture) {
                        DOMWarning("source object for texture link is not a texture or layered texture, ignoring", &element);
                        continue;
                    }
                    string &prop = con.PropertyName();
                    if (layeredTextures.find(prop) != layeredTextures.end()) {
                        DOMWarning("duplicate layered texture link: " + prop, &element);
                    }

                    layeredTextures[prop] = layeredTexture;
                    ((FBXLayeredTexture*)layeredTexture).fillTexture(doc);
                }
                else {
                    string &prop = con.PropertyName();
                    if (textures.find(prop) != textures.end()) {
                        DOMWarning("duplicate texture link: " + prop, &element);
                    }

                    textures[prop] = tex;
                }
            }

        }


        string GetShadingModel() {
            return shading;
        }

        bool IsMultilayer() {
            return multilayer;
        }

        FBXPropertyTable Props() {
            Debug.Assert(props.get());
            return *props;
        }

        Dictionary<string, FBXTexture> Textures() {
            return textures;
        }

        Dictionary<string, FBXLayeredTexture> LayeredTextures() {
            return layeredTextures;
        }


        string shading;
        bool multilayer;
        FBXPropertyTable props;

        Dictionary<string, FBXTexture> textures;
        Dictionary<string, FBXLayeredTexture> layeredTextures;
    }
}
