using System.Xml.Linq;
using System;
using static System.Formats.Asn1.AsnWriter;
using System.Reflection;

namespace Import3D.FBX {
    /** Represents a FBX animation curve (i.e. a 1-dimensional set of keyframes and values therefore) */
    public unsafe class FBXAnimationCurve : FBXObject {

        public FBXAnimationCurve(UInt64 id, FBXElement element, string name, FBXDocument doc)
            : base(id, element, name) {
            FBXScope sc = GetRequiredScope(element);
            FBXElement KeyTime = GetRequiredElement(sc, "KeyTime");
            FBXElement KeyValueFloat = GetRequiredElement(sc, "KeyValueFloat");

            ParseVectorDataArray(keys, KeyTime);
            ParseVectorDataArray(values, KeyValueFloat);

            if (keys.Count != values.Count) {
                throw new Exception($"the number of key times does not match the number of keyframe values {KeyTime}");
            }

            // check if the key times are well-ordered
            if (!std::equal(keys.begin(), keys.end() - 1, keys.begin() + 1, std::less<KeyTimeList::value_type>())) {
                throw new Exception($"the keyframes are not in ascending order {KeyTime}");
            }

            FBXElement? KeyAttrDataFloat = sc["KeyAttrDataFloat"];
            if (KeyAttrDataFloat != null) {
                ParseVectorDataArray(attributes, KeyAttrDataFloat);
            }

            FBXElement? KeyAttrFlags = sc["KeyAttrFlags"];
            if (KeyAttrFlags != null) {
                ParseVectorDataArray(flags, KeyAttrFlags);
            }

        }

        /** get list of keyframe positions (time).
         *  Invariant: |GetKeys()| > 0 */
        List<Int64> GetKeys() {
            return keys;
        }

        /** get list of keyframe values.
         * Invariant: |GetKeys()| == |GetValues()| && |GetKeys()| > 0*/
        List<float> GetValues() {
            return values;
        }

        List<float> GetAttributes() {
            return attributes;
        }

        List<uint> GetFlags() {
            return flags;
        }


        List<Int64> keys;
        List<float> values;
        List<float> attributes;
        List<uint> flags;

        // extract required compound scope
        FBXScope GetRequiredScope(FBXElement el) {
            FBXScope? s = el.Compound();
            if (s == null) {
                throw new Exception($"expected compound scope {el}");
            }

            return s;
        }
        // extract a required element from a scope, abort if the element cannot be found
        FBXElement GetRequiredElement(FBXScope sc, string index, FBXElement? element = null) {
            FBXElement? el = sc[index];
            if (el == null) {
                ParseError("did not find required element \"" + index + "\"", element);
            }
            return el;
        }
        // read an array of int64_ts
        public unsafe void ParseVectorDataArray(List<Int64> outValue, FBXElement el) {
            outValue.Clear();//.resize(0);
            var tok = el.Tokens();
            if (tok.Count == 0) {
                ParseError("unexpected empty element", el);
            }

            if (tok[0].IsBinary()) {
                var data = tok[0].begin(); var end = tok[0].end();

                byte type;
                UInt32 count;
                ReadBinaryDataArrayHead(data, end, ref type, ref count, el);

                if (count == 0) {
                    return;
                }

                if (type != 'l') {
                    ParseError("expected long array (binary)", el);
                }

                List<char> buff;
                ReadBinaryDataArray(type, count, data, end, buff, el);

                System.Diagnostics.Debug.Assert(data == end);
                UInt64 dataToRead = static_cast<UInt64>(count) * 8;
                if (dataToRead != buff.Length) {
                    ParseError("Invalid read size (binary)", el);
                }

                outValue.reserve(count);

                Int64* ip = reinterpret_cast<Int64*>(&buff[0]);
                for (uint i = 0; i < count; ++i, ++ip) {
                    Int64 val = *ip;
                    //AI_SWAP8(val);
                    outValue.push_back(val);
                }

                return;
            }

            int dim = ParseTokenAsDim(tok[0]);

            // see notes in ParseVectorDataArray()
            outValue.Capacity = dim;//.reserve(dim);

            FBXScope scope = GetRequiredScope(el);
            FBXElement a = GetRequiredElement(scope, "a", el);

            foreach (var item in a.Tokens()) {
                Int64 ival = ParseTokenAsInt64(**it++);

                outValue.push_back(ival);
            }
        }

    }
}
