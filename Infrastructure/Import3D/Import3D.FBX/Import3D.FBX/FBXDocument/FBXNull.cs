using System.Xml.Linq;
using System;

namespace Import3D.FBX {
    /** DOM base class for FBX null markers attached to a node */
    public unsafe class FBXNull : FBXNodeAttribute {
        public FBXNull(UInt64 id, FBXElement element, FBXDocument doc, string name)
            : base(id, element, doc, name) {
        }

    }
}
