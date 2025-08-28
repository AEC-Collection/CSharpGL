using System.Diagnostics;
using System.Xml.Linq;
using System;

namespace Import3D.FBX {
    /** DOM class for generic FBX textures */
    public unsafe class FBXTexture : FBXObject {
        public FBXTexture(UInt64 id, FBXElement element, FBXDocument doc, string name)
            : base(id, element, name) {
            this.uvTrans = new vec2(0, 0); this.uvScaling = new vec2(1, 1);
            this.uvRotation = 0.0f;
            FBXScope sc = GetRequiredScope(element);

            FBXElement* Type = sc["Type"];
            FBXElement* FileName = sc["FileName"];
            FBXElement* RelativeFilename = sc["RelativeFilename"];
            FBXElement* ModelUVTranslation = sc["ModelUVTranslation"];
            FBXElement* ModelUVScaling = sc["ModelUVScaling"];
            FBXElement* Texture_Alpha_Source = sc["Texture_Alpha_Source"];
            FBXElement* Cropping = sc["Cropping"];

            if (Type) {
                type = ParseTokenAsString(GetRequiredToken(*Type, 0));
            }

            if (FileName) {
                fileName = ParseTokenAsString(GetRequiredToken(*FileName, 0));
            }

            if (RelativeFilename) {
                relativeFileName = ParseTokenAsString(GetRequiredToken(*RelativeFilename, 0));
            }

            if (ModelUVTranslation) {
                uvTrans = aiVector2D(ParseTokenAsFloat(GetRequiredToken(*ModelUVTranslation, 0)),
                        ParseTokenAsFloat(GetRequiredToken(*ModelUVTranslation, 1)));
            }

            if (ModelUVScaling) {
                uvScaling = aiVector2D(ParseTokenAsFloat(GetRequiredToken(*ModelUVScaling, 0)),
                        ParseTokenAsFloat(GetRequiredToken(*ModelUVScaling, 1)));
            }

            if (Cropping) {
                crop[0] = ParseTokenAsInt(GetRequiredToken(*Cropping, 0));
                crop[1] = ParseTokenAsInt(GetRequiredToken(*Cropping, 1));
                crop[2] = ParseTokenAsInt(GetRequiredToken(*Cropping, 2));
                crop[3] = ParseTokenAsInt(GetRequiredToken(*Cropping, 3));
            }
            else {
                // vc8 doesn't support the crop() syntax in initialization lists
                // (and vc9 WARNS about the new (i.e. compliant) behaviour).
                crop[0] = crop[1] = crop[2] = crop[3] = 0;
            }

            if (Texture_Alpha_Source) {
                alphaSource = ParseTokenAsString(GetRequiredToken(*Texture_Alpha_Source, 0));
            }

            props = GetPropertyTable(doc, "Texture.FbxFileTexture", element, sc);

            // 3DS Max and FBX SDK use "Scaling" and "Translation" instead of "ModelUVScaling" and "ModelUVTranslation". Use these properties if available.
            bool ok;
            vec3 & scaling = PropertyGet<vec3>(*props, "Scaling", ok);
            if (ok) {
                uvScaling.x = scaling.x;
                uvScaling.y = scaling.y;
            }

            vec3 & trans = PropertyGet<vec3>(*props, "Translation", ok);
            if (ok) {
                uvTrans.x = trans.x;
                uvTrans.y = trans.y;
            }

            vec3 & rotation = PropertyGet<vec3>(*props, "Rotation", ok);
            if (ok) {
                uvRotation = rotation.z;
            }

            // resolve video links
            if (doc.Settings().readTextures) {
                List<FBXConnection> & conns = doc.GetConnectionsByDestinationSequenced(ID());
                for (FBXConnection con : conns) {
                    FBXObject* ob = con.SourceObject();
                    if (null == ob) {
                        DOMWarning("failed to read source object for texture link, ignoring", &element);
                        continue;
                    }

                    FBXVideo* video = dynamic_cast<FBXVideo*>(ob);
                    if (video) {
                        media = video;
                    }
                }
            }

        }
        string Type() {
            return type;
        }
        string FileName() {
            return fileName;
        }

        string RelativeFilename() {
            return relativeFileName;
        }

        string AlphaSource() {
            return alphaSource;
        }

        vec2 UVTranslation() {
            return uvTrans;
        }

        vec2 UVScaling() {
            return uvScaling;
        }

        float UVRotation() {
            return uvRotation;
        }

        FBXPropertyTable Props() {
            Debug.Assert(props.get());
            return *props;
        }

        // return a 4-tuple
        uint* Crop() {
            return crop;
        }

        FBXVideo Media() {
            return media;
        }


        vec2 uvTrans;
        vec2 uvScaling;
        float uvRotation;

        string type;
        string relativeFileName;
        string fileName;
        string alphaSource;
        FBXPropertyTable props;

        uint[] crop = new uint[4];

        FBXVideo media;
    }
}
