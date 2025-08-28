using System.Xml.Linq;
using System;
using System.Diagnostics;

namespace Import3D.FBX {
    /** DOM class for generic FBX NoteAttribute blocks. NoteAttribute's just hold a property table,
 *  fixed members are added by deriving classes. */
    public unsafe class FBXNodeAttribute : FBXObject {
        public FBXNodeAttribute(UInt64 id, FBXElement element, FBXDocument doc, string name)
            : base(id, element, name) {
            FBXScope sc = GetRequiredScope(element);

            string &classname = ParseTokenAsString(GetRequiredToken(element, 2));

            // hack on the deriving type but Null/LimbNode attributes are the only case in which
            // the property table is by design absent and no warning should be generated
            // for it.
            bool is_null_or_limb = !strcmp(classname.c_str(), "Null") || !strcmp(classname.c_str(), "LimbNode");
            props = GetPropertyTable(doc, "NodeAttribute.Fbx" + classname, element, sc, is_null_or_limb);

        }

        FBXPropertyTable Props() {
            Debug.Assert(props.get());
            return *props;
        }


        FBXPropertyTable props;
    }
}
