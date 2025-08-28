using System.Diagnostics;
using System.Xml.Linq;
using System;

namespace Import3D.FBX {
    /** Represents a FBX animation curve (i.e. a mapping from single animation curves to nodes) */
    public unsafe class FBXAnimationCurveNode : FBXObject {

        /* the optional white list specifies a list of property names for which the caller
        wants animations for. If the curve node does not match one of these, std::range_error
        will be thrown. */
        public FBXAnimationCurveNode(UInt64 id, FBXElement element, string name, FBXDocument doc,
        char** target_prop_whitelist = null, int whitelist_size = 0) {
            FBXScope sc = GetRequiredScope(element);

            // find target node
            char* whitelist[] = { "Model", "NodeAttribute", "Deformer" };
            List<FBXConnection> & conns = doc.GetConnectionsBySourceSequenced(ID(), whitelist, 3);

            for (FBXConnection con : conns) {

                // link should go for a property
                if (!con.PropertyName().length()) {
                    continue;
                }

                if (target_prop_whitelist) {
                    char* s = con.PropertyName().c_str();
                    bool ok = false;
                    for (int i = 0; i < whitelist_size; ++i) {
                        if (!strcmp(s, target_prop_whitelist[i])) {
                            ok = true;
                            break;
                        }
                    }

                    if (!ok) {
                        throw std::range_error("AnimationCurveNode target property is not in whitelist");
                    }
                }

                FBXObject* ob = con.DestinationObject();
                if (!ob) {
                    DOMWarning("failed to read destination object for AnimationCurveNode.Model link, ignoring", &element);
                    continue;
                }

                target = ob;
                if (!target) {
                    continue;
                }

                prop = con.PropertyName();
                break;
            }

            if (!target) {
                DOMWarning("failed to resolve target Model/NodeAttribute/Constraint for AnimationCurveNode", &element);
            }

            props = GetPropertyTable(doc, "AnimationCurveNode.FbxAnimCurveNode", element, sc, false);

        }


        public FBXPropertyTable Props() {
            Debug.Assert(props.get());
            return *props;
        }

        public Dictionary<string, FBXAnimationCurve> Curves() {
            if (!curves.empty()) {
                return curves;
            }

            // resolve attached animation curves
            List<FBXConnection> & conns = doc.GetConnectionsByDestinationSequenced(ID(), "AnimationCurve");

            for (FBXConnection con : conns) {

                // link should go for a property
                if (!con.PropertyName().length()) {
                    continue;
                }

                FBXObject* ob = con.SourceObject();
                if (null == ob) {
                    DOMWarning("failed to read source object for AnimationCurve.AnimationCurveNode link, ignoring", &element);
                    continue;
                }

                FBXAnimationCurve* anim = dynamic_cast<FBXAnimationCurve*>(ob);
                if (null == anim) {
                    DOMWarning("source object for .AnimationCurveNode link is not an AnimationCurve", &element);
                    continue;
                }

                curves[con.PropertyName()] = anim;
            }

            return curves;

        }

        /** Object the curve is assigned to, this can be null if the
         *  target object has no DOM representation or could not
         *  be read for other reasons.*/
        public FBXObject Target() {
            return target;
        }

        public FBXModel TargetAsModel() {
            return (FBXModel)(target);
        }

        public FBXNodeAttribute TargetAsNodeAttribute() {
            return (FBXNodeAttribute)(target);
        }

        /** FBXProperty of Target() that is being animated*/
        public string TargetProperty() {
            return prop;
        }


        FBXObject target;
        FBXPropertyTable props;
        Dictionary<string, FBXAnimationCurve> curves;

        string prop;
        FBXDocument doc;
    }
}
