using System.Xml.Linq;
using System;

namespace Import3D.FBX {
    /** DOM base class for FBX limb node markers attached to a node */
    public unsafe class FBXLimbNode : FBXNodeAttribute {
        public FBXLimbNode(UInt64 id, FBXElement element, FBXDocument doc, string name)
            : base(id, element, doc, name) {
        }

    }
}
