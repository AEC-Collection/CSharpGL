using System.Xml.Linq;
using System;

namespace Import3D.FBX {
    /** Base class for in-memory (DOM) representations of FBX objects */
    public class FBXObject {
        public const float kFovUnknown = -1.0f;

        FBXObject(UInt64 id, FBXElement element, string name) {
            this.id = id; this.element = element; this.name = name;
        }


        FBXElement SourceElement() {
            return element;
        }

        string Name() {
            return name;
        }

        UInt64 ID() {
            return id;
        }


        public readonly FBXElement element;
        public readonly string name;
        public readonly UInt64 id;
    }
}
