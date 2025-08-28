using Import3D.FBX;
using System;
using System.Diagnostics;

namespace Import3D.FBX {
    /** DOM class for global document settings, a single instance per document can
 *  be accessed via Document.Globals(). */
    public unsafe class FBXFileGlobalSettings {
        public FBXFileGlobalSettings(FBXDocument doc, FBXPropertyTable props) {
            this.doc = doc; this.props = props;
        }


        FBXPropertyTable Props() {
            System.Diagnostics.Debug.Assert(props.get());
            return *props;
        }

        FBXDocument GetDocument() {
            return doc;
        }

        int UpAxis() {
            return PropertyGet<int>(Props(), "UpAxis", (1));
        }
        int UpAxisSign() {
            return PropertyGet<int>(Props(), "UpAxisSign", (1));
        }
        int FrontAxis() {
            return PropertyGet<int>(Props(), "FrontAxis", (2));
        }
        int FrontAxisSign() {
            return PropertyGet<int>(Props(), "FrontAxisSign", (1));
        }
        int CoordAxis() {
            return PropertyGet<int>(Props(), "CoordAxis", (0));
        }
        int CoordAxisSign() {
            return PropertyGet<int>(Props(), "CoordAxisSign", (1));
        }
        int OriginalUpAxis() {
            return PropertyGet<int>(Props(), "OriginalUpAxis", (0));
        }
        int OriginalUpAxisSign() {
            return PropertyGet<int>(Props(), "OriginalUpAxisSign", (1));
        }
        float UnitScaleFactor() {
            return PropertyGet<float>(Props(), "UnitScaleFactor", (1));
        }
        float OriginalUnitScaleFactor() {
            return PropertyGet<float>(Props(), "OriginalUnitScaleFactor", (1));
        }
        vec3 AmbientColor() {
            return PropertyGet<vec3>(Props(), "AmbientColor", (vec3(0, 0, 0)));
        }
        string DefaultCamera() {
            return PropertyGet<string>(Props(), "DefaultCamera", (""));
        }

        public enum FrameRate {
            FrameRate_DEFAULT = 0,
            FrameRate_120 = 1,
            FrameRate_100 = 2,
            FrameRate_60 = 3,
            FrameRate_50 = 4,
            FrameRate_48 = 5,
            FrameRate_30 = 6,
            FrameRate_30_DROP = 7,
            FrameRate_NTSC_DROP_FRAME = 8,
            FrameRate_NTSC_FULL_FRAME = 9,
            FrameRate_PAL = 10,
            FrameRate_CINEMA = 11,
            FrameRate_1000 = 12,
            FrameRate_CINEMA_ND = 13,
            FrameRate_CUSTOM = 14,

            FrameRate_MAX // end-of-enum sentinel
        };

        FrameRate TimeMode() {
            int ival = PropertyGet<int>(Props(), "TimeMode", (int)(FrameRate.FrameRate_DEFAULT));
            if (ival < 0 || ival >= (int)FrameRate.FrameRate_MAX) {
                //(void)((!!((int)(FrameRate.FrameRate_DEFAULT) >= 0)) || (Assimp::aiAssertViolation("(int)(FrameRate.FrameRate_DEFAULT) >= 0", "D:\\Projects\\assimp\\code\\AssetLib\\FBX\\FBXDocument.h", 1286), 0));
                //(void)((!!((int)(FrameRate.FrameRate_DEFAULT) < FrameRate.FrameRate_MAX)) || (Assimp::aiAssertViolation("(int)(FrameRate.FrameRate_DEFAULT) < AI_CONCAT(FrameRate, _MAX)", "D:\\Projects\\assimp\\code\\AssetLib\\FBX\\FBXDocument.h", 1286), 0));
                return (FrameRate)(FrameRate.FrameRate_DEFAULT);
            }
            return (FrameRate)(ival);
        }
        UInt64 TimeSpanStart() {
            return PropertyGet<UInt64>(Props(), "TimeSpanStart", (0L));
        }
        UInt64 TimeSpanStop() {
            return PropertyGet<UInt64>(Props(), "TimeSpanStop", (0L));
        }
        float CustomFrameRate() {
            return PropertyGet<float>(Props(), "CustomFrameRate", (-1.0f));
        }

        FBXPropertyTable props;
        FBXDocument doc;

    }
}
