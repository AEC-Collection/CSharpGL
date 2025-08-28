using System.Xml.Linq;
using System;

namespace Import3D.FBX {
    /** DOM base class for FBX lights attached to a node */
    public unsafe class FBXLight : FBXNodeAttribute {
        public FBXLight(UInt64 id, FBXElement element, FBXDocument doc, string name)
            : base(id, element, doc, name) {
        }

        public enum Type {
            Type_Point,
            Type_Directional,
            Type_Spot,
            Type_Area,
            Type_Volume,

            Type_MAX // end-of-enum sentinel
        };

        public enum Decay {
            Decay_None,
            Decay_Linear,
            Decay_Quadratic,
            Decay_Cubic,

            Decay_MAX // end-of-enum sentinel
        };

        vec3 Color() {
            return PropertyGet<vec3>(Props(), "Color", (vec3(1, 1, 1)));
        }
        Type LightType() {
            int ival = PropertyGet<int>(Props(), "LightType", (int)(0));
            if (ival < 0 || ival >= (int)Type.Type_MAX) {
                //(void)((!!((int)(0) >= 0)) || (Assimp::aiAssertViolation("(int)(0) >= 0", "D:\\Projects\\assimp\\code\\AssetLib\\FBX\\FBXDocument.h", 322), 0));
                //(void)((!!((int)(0) < Type_MAX)) || (Assimp::aiAssertViolation("(int)(0) < AI_CONCAT(Type, _MAX)", "D:\\Projects\\assimp\\code\\AssetLib\\FBX\\FBXDocument.h", 322), 0));
                return (Type)(0);
            }
            return (Type)(ival);
        }
        bool CastLightOnObject() {
            return PropertyGet<bool>(Props(), "CastLightOnObject", (false));
        }
        bool DrawVolumetricLight() {
            return PropertyGet<bool>(Props(), "DrawVolumetricLight", (true));
        }
        bool DrawGroundProjection() {
            return PropertyGet<bool>(Props(), "DrawGroundProjection", (true));
        }
        bool DrawFrontFacingVolumetricLight() {
            return PropertyGet<bool>(Props(), "DrawFrontFacingVolumetricLight", (false));
        }
        float Intensity() {
            return PropertyGet<float>(Props(), "Intensity", (100.0f));
        }
        float InnerAngle() {
            return PropertyGet<float>(Props(), "InnerAngle", (0.0f));
        }
        float OuterAngle() {
            return PropertyGet<float>(Props(), "OuterAngle", (45.0f));
        }
        int Fog() {
            return PropertyGet<int>(Props(), "Fog", (50));
        }
        Decay DecayType() {
            int ival = PropertyGet<int>(Props(), "DecayType", (int)(2));
            if (ival < 0 || ival >= (int)Decay.Decay_MAX) {
                //(void)((!!((int)(2) >= 0)) || (Assimp::aiAssertViolation("(int)(2) >= 0", "D:\\Projects\\assimp\\code\\AssetLib\\FBX\\FBXDocument.h", 332), 0));
                //(void)((!!((int)(2) < Decay_MAX)) || (Assimp::aiAssertViolation("(int)(2) < AI_CONCAT(Decay, _MAX)", "D:\\Projects\\assimp\\code\\AssetLib\\FBX\\FBXDocument.h", 332), 0));
                return (Decay)(2);
            }
            return (Decay)(ival);
        }
        float DecayStart() {
            return PropertyGet<float>(Props(), "DecayStart", (1.0f));
        }
        string FileName() {
            return PropertyGet<string>(Props(), "FileName", (""));
        }

        bool EnableNearAttenuation() {
            return PropertyGet<bool>(Props(), "EnableNearAttenuation", (false));
        }
        float NearAttenuationStart() {
            return PropertyGet<float>(Props(), "NearAttenuationStart", (0.0f));
        }
        float NearAttenuationEnd() {
            return PropertyGet<float>(Props(), "NearAttenuationEnd", (0.0f));
        }
        bool EnableFarAttenuation() {
            return PropertyGet<bool>(Props(), "EnableFarAttenuation", (false));
        }
        float FarAttenuationStart() {
            return PropertyGet<float>(Props(), "FarAttenuationStart", (0.0f));
        }
        float FarAttenuationEnd() {
            return PropertyGet<float>(Props(), "FarAttenuationEnd", (0.0f));
        }

        bool CastShadows() {
            return PropertyGet<bool>(Props(), "CastShadows", (true));
        }
        vec3 ShadowColor() {
            return PropertyGet<vec3>(Props(), "ShadowColor", (vec3(0, 0, 0)));
        }

        int AreaLightShape() {
            return PropertyGet<int>(Props(), "AreaLightShape", (0));
        }

        float LeftBarnDoor() {
            return PropertyGet<float>(Props(), "LeftBarnDoor", (20.0f));
        }
        float RightBarnDoor() {
            return PropertyGet<float>(Props(), "RightBarnDoor", (20.0f));
        }
        float TopBarnDoor() {
            return PropertyGet<float>(Props(), "TopBarnDoor", (20.0f));
        }
        float BottomBarnDoor() {
            return PropertyGet<float>(Props(), "BottomBarnDoor", (20.0f));
        }
        bool EnableBarnDoor() {
            return PropertyGet<bool>(Props(), "EnableBarnDoor", (true));
        }

    }
}
