using System.Xml.Linq;
using System;

namespace Import3D.FBX {
    public unsafe class FBXCamera : FBXNodeAttribute {
        public FBXCamera(UInt64 id, FBXElement element, FBXDocument doc, string name)
            : base(id, element, doc, name) {
        }


        vec3 Position() {
            return PropertyGet<vec3>(Props(), "Position", (vec3(0, 0, 0)));
        }
        vec3 UpVector() {
            return PropertyGet<vec3>(Props(), "UpVector", (vec3(0, 1, 0)));
        }
        vec3 InterestPosition() {
            return PropertyGet<vec3>(Props(), "InterestPosition", (vec3(0, 0, 0)));
        }

        float AspectWidth() {
            return PropertyGet<float>(Props(), "AspectWidth", (1.0f));
        }
        float AspectHeight() {
            return PropertyGet<float>(Props(), "AspectHeight", (1.0f));
        }
        float FilmWidth() {
            return PropertyGet<float>(Props(), "FilmWidth", (1.0f));
        }
        float FilmHeight() {
            return PropertyGet<float>(Props(), "FilmHeight", (1.0f));
        }

        float NearPlane() {
            return PropertyGet<float>(Props(), "NearPlane", (0.1f));
        }
        float FarPlane() {
            return PropertyGet<float>(Props(), "FarPlane", (100.0f));
        }

        float FilmAspectRatio() {
            return PropertyGet<float>(Props(), "FilmAspectRatio", (1.0f));
        }
        int ApertureMode() {
            return PropertyGet<int>(Props(), "ApertureMode", (0));
        }

        float FieldOfView() {
            return PropertyGet<float>(Props(), "FieldOfView", (kFovUnknown));
        }
        float FocalLength() {
            return PropertyGet<float>(Props(), "FocalLength", (1.0f));
        }
    }

}
