using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Import3D.MS3D {
    public class MS3DImporter {
        /** Returns whether the class can handle the format of the given file.
            * See BaseImporter::CanRead() for details.  */
        public bool CanRead(string pFile, bool checkSig) {
            throw new NotImplementedException();
        }



        public vec4 ReadColor(System.IO.BinaryReader stream) {
            // aiColor4D is packed on gcc, implicit binding to float& fails therefore.
            //stream >> (float &)ambient.r >> (float &)ambient.g >> (float &)ambient.b >> (float &)ambient.a;
            var color = new vec4(
                stream.ReadSingle(),
                stream.ReadSingle(),
                stream.ReadSingle(),
                stream.ReadSingle());
            return color;
        }

        // ------------------------------------------------------------------------------------------------
        public vec3 ReadVector(System.IO.BinaryReader stream) {
            // See note in ReadColor()
            //stream >> (float &)pos.x >> (float &)pos.y >> (float &)pos.z;
            var vector = new vec3(
                       stream.ReadSingle(),
                       stream.ReadSingle(),
                       stream.ReadSingle());
            return vector;
        }
        public void ReadComments<T>(System.IO.BinaryReader stream, List<T> outp)
            where T : IHasComment {
            UInt16 cnt = stream.ReadUInt16();

            for (uint i = 0; i < cnt; ++i) {
                UInt32 index = stream.ReadUInt32();
                UInt32 clength = stream.ReadUInt32();

                if (index >= outp.Count) {
                    Log.WriteLine("MS3D: Invalid index in comment section");
                }
                else if (clength > stream.BaseStream.Length - stream.BaseStream.Position) {
                    throw new Exception("MS3D: Failure reading comment, length field is out of range");
                }
                else {
                    var commentBytes = stream.ReadBytes((int)clength);
                    var comment = Encoding.ASCII.GetString(commentBytes);// string(reinterpret_cast<char*>(stream.GetPtr()), clength);
                    outp[(int)index].SetComment(comment);
                }
                //stream.IncPtr(clength);
            }
        }

        // ------------------------------------------------------------------------------------------------
        public void CollectChildJoints(List<TempJoint> joints,
                List<bool> hadit,
                aiNode nd,
                mat4 absTrafo) {
            uint cnt = 0;
            for (var i = 0; i < joints.Count; ++i) {
                if (!hadit[i] && joints[i].parentName == nd.mName) {
                    ++cnt;
                }
            }

            nd.mNumChildren = (int)cnt;
            nd.mChildren = new aiNode[cnt];
            cnt = 0;
            for (var i = 0; i < joints.Count; ++i) {
                if (!hadit[i] && joints[i].parentName == nd.mName) {
                    aiNode ch = nd.mChildren[cnt++] = new aiNode(joints[i].name);
                    ch.mParent = nd;

                    ch.mTransformation = mat4.Translate(joints[i].position) * mat4.FromEulerAnglesXYZ(joints[i].rotation);

                    mat4 abs = absTrafo * ch.mTransformation;
                    for (uint a = 0; a < mScene.mNumMeshes; ++a) {
                        aiMesh msh = mScene.mMeshes[a];
                        for (uint n = 0; n < msh.mNumBones; ++n) {
                            aiBone bone = msh.mBones[n];

                            if (bone.mName == ch.mName) {
                                bone.mOffsetMatrix = abs.Inverse();
                            }
                        }
                    }

                    hadit[i] = true;
                    CollectChildJoints(joints, hadit, ch, abs);
                }
            }
        }
        public void CollectChildJoints(List<TempJoint> joints, aiNode nd) {
            var hadit = new List<bool>(joints.Count);
            mat4 trafo;

            CollectChildJoints(joints, hadit, nd, trafo);
        }

        /** Imports the given file into the given scene structure.
   * See BaseImporter::InternReadFile() for details */
        // Imports the given file into the given scene structure.
        public unsafe void InternReadFile(string pFile, aiScene pScene) {
            using (var fileStream = new FileStream(pFile, FileMode.Open, FileAccess.Read))
            using (var stream = new BinaryReader(fileStream)) {

                mScene = pScene;

                // CanRead() should have done this already
                // 1 ------------ read into temporary data structures mirroring the original file
                //stream.CopyAndAdvance(head, 10);
                //byte[] head = new byte[10];
                byte[] head = stream.ReadBytes(10); var strHead = Encoding.ASCII.GetString(head);
                //stream >> version;
                Int32 version = stream.ReadInt32();

                if (strHead != "MS3D000000") {
                    throw new Exception($"{pFile} is Not a MS3D file, magic string MS3D000000 not found: ");
                }

                if (version != 4) {
                    throw new Exception("MS3D: Unsupported file format version, 4 was expected");
                }

                UInt16 verts = stream.ReadUInt16();
                //stream >> verts;

                var vertices = new List<TempVertex>(verts);
                for (var i = 0; i < verts; ++i) {
                    TempVertex v = vertices[i];

                    //stream.IncPtr(1);
                    var tmp = stream.ReadByte();
                    v.pos = ReadVector(stream);
                    v.bone_id[0] = stream.ReadByte();// stream.GetI1();
                    v.ref_cnt = stream.ReadByte();// stream.GetI1();

                    v.bone_id[1] = v.bone_id[2] = v.bone_id[3] = UInt32.MaxValue;// UINT_MAX;
                    v.weights[1] = v.weights[2] = v.weights[3] = 0.0f;
                    v.weights[0] = 1.0f;
                }

                UInt16 tris = stream.ReadUInt16();
                //stream >> tris;

                var triangles = new List<TempTriangle>(tris);
                for (var i = 0; i < tris; ++i) {
                    TempTriangle t = triangles[i];

                    //stream.IncPtr(2);
                    var tmp = stream.ReadBytes(2);
                    for (uint j = 0; j < 3; ++j) {
                        t.indices[j] = stream.ReadUInt16();// stream.GetI2();
                    }

                    for (uint j = 0; j < 3; ++j) {
                        t.normals[j] = ReadVector(stream);
                    }

                    for (uint j = 0; j < 3; ++j) {
                        //stream >> (float &)(t.uv[j].x); // see note in ReadColor()
                        t.uv[j].x = stream.ReadSingle();
                    }
                    for (uint j = 0; j < 3; ++j) {
                        //stream >> (float &)(t.uv[j].y);
                        t.uv[j].y = stream.ReadSingle();
                    }

                    t.sg = stream.ReadByte();// stream.GetI1();
                    t.group = stream.ReadByte();// stream.GetI1();
                }

                UInt16 grp = stream.ReadUInt16();
                //stream >> grp;

                bool need_default = false;
                var groups = new List<TempGroup>(grp);
                for (var i = 0; i < grp; ++i) {
                    TempGroup t = groups[i];

                    //stream.IncPtr(1);
                    var tmp = stream.ReadByte();
                    //stream.CopyAndAdvance(t.name, 32);
                    byte[] name = stream.ReadBytes(32); var strName = Encoding.ASCII.GetString(name);

                    //t.name[32] = '\0';
                    t.name = strName;

                    UInt16 num = stream.ReadUInt16();
                    //stream >> num;

                    t.triangles.Capacity = (num);
                    for (var j = 0; j < num; ++j) {
                        t.triangles[j] = stream.ReadUInt16();// stream.GetI2();
                    }
                    t.mat = stream.ReadByte();// stream.GetI1();
                    if (t.mat == UInt32.MaxValue) {
                        need_default = true;
                    }
                }

                UInt16 mat = stream.ReadUInt16();
                //stream >> mat;

                var materials = new List<TempMaterial>(mat);
                for (var j = 0; j < mat; ++j) {
                    TempMaterial t = materials[j];

                    {
                        //stream.CopyAndAdvance(t.name, 32);
                        //t.name[32] = '\0';
                        var name = stream.ReadBytes(32); var strName = Encoding.ASCII.GetString(name);
                        t.name = strName;
                    }

                    t.ambient = ReadColor(stream);
                    t.diffuse = ReadColor(stream);
                    t.specular = ReadColor(stream);
                    t.emissive = ReadColor(stream);
                    //stream >> t.shininess >> t.transparency;
                    t.shininess = stream.ReadSingle();
                    t.transparency = stream.ReadSingle();

                    //stream.IncPtr(1);
                    var tmp = stream.ReadByte();

                    {
                        //stream.CopyAndAdvance(t.texture, 128);
                        //t.texture[128] = '\0';
                        var name = stream.ReadBytes(128); var strName = Encoding.ASCII.GetString(name);
                        t.texture = strName;
                    }

                    {
                        //stream.CopyAndAdvance(t.alphamap, 128);
                        //t.alphamap[128] = '\0';
                        var name = stream.ReadBytes(128); var strName = Encoding.ASCII.GetString(name);
                        t.alphamap = strName;
                    }
                }

                float animfps = stream.ReadSingle();
                float currenttime = stream.ReadSingle();
                UInt32 totalframes = stream.ReadUInt32();
                //stream >> animfps >> currenttime >> totalframes;

                UInt16 joint = stream.ReadUInt16();
                //stream >> joint;

                var joints = new List<TempJoint>(joint);
                for (var ii = 0; ii < joint; ++ii) {
                    TempJoint j = joints[ii];

                    //stream.IncPtr(1);
                    var tmp = stream.ReadByte();
                    {
                        //stream.CopyAndAdvance(j.name, 32);
                        //j.name[32] = '\0';
                        var name = stream.ReadBytes(128); var strName = Encoding.ASCII.GetString(name);
                        j.name = strName;
                    }

                    {
                        //stream.CopyAndAdvance(j.parentName, 32);
                        //j.parentName[32] = '\0';
                        var name = stream.ReadBytes(128); var strName = Encoding.ASCII.GetString(name);
                        j.parentName = strName;
                    }

                    j.rotation = ReadVector(stream);
                    j.position = ReadVector(stream);

                    {
                        var capacity = stream.ReadUInt16();
                        j.rotFrames = new TempKeyFrame[capacity];
                    }
                    {
                        var capacity = stream.ReadUInt16();
                        j.posFrames = new TempKeyFrame[capacity];
                    }

                    for (var a = 0; a < j.rotFrames.Length; ++a) {
                        //TempKeyFrame kf = j.rotFrames[a];
                        //stream >> kf.time;
                        j.rotFrames[a].time = stream.ReadSingle();
                        j.rotFrames[a].value = ReadVector(stream);
                    }
                    for (uint a = 0; a < j.posFrames.Length; ++a) {
                        //TempKeyFrame kf = j.posFrames[a];
                        //stream >> kf.time;
                        j.posFrames[a].time = stream.ReadSingle();
                        j.posFrames[a].value = ReadVector(stream);
                    }
                }

                if (stream.BaseStream.Length - stream.BaseStream.Position > 4) {
                    UInt32 subversion = stream.ReadUInt32();
                    //stream >> subversion;
                    if (subversion == 1) {
                        ReadComments(stream, groups);
                        ReadComments(stream, materials);
                        ReadComments(stream, joints);

                        // model comment - print it for we have such a nice log.
                        var tmp = stream.ReadInt32();
                        if (tmp != 0) {
                            int len = stream.ReadInt32();
                            if (len > stream.BaseStream.Length - stream.BaseStream.Position) {
                                throw new Exception("MS3D: Model comment is too long");
                            }

                            {
                                //string s = string(reinterpret_cast<char*>(stream.GetPtr()), len);
                                //Log.WriteLine("MS3D: Model comment: ", s);
                                var bytes = stream.ReadBytes(len); var comment = Encoding.ASCII.GetString(bytes);
                                Log.WriteLine($"MS3D: Model comment: {comment}");
                            }
                        }

                        if (stream.BaseStream.Length - stream.BaseStream.Position > 4) {
                            subversion = stream.ReadUInt32();
                            if (1u < subversion && subversion <= 3u) {
                                for (var i = 0; i < verts; ++i) {
                                    TempVertex v = vertices[i];
                                    v.weights[3] = 1.0f;
                                    for (uint n = 0; n < 3; v.weights[3] -= v.weights[n++]) {
                                        v.bone_id[n + 1] = stream.ReadByte();//.GetI1();
                                        var weight = stream.ReadByte();
                                        v.weights[n] = weight / 255.0f;
                                    }
                                    //stream.IncPtr((subversion - 1) << 2u);
                                    var move = (subversion - 1) << 2;
                                    stream.BaseStream.Position += move;
                                }

                                // even further extra data is not of interest for us, at least now now.
                            }
                        }
                    }
                }

                // 2 ------------ convert to proper aiXX data structures -----------------------------------

                if (need_default && materials.Count == 0) {
                    Log.WriteLine("MS3D: Found group with no material assigned, spawning default material");
                    // if one of the groups has no material assigned, but there are other
                    // groups with materials, a default material needs to be added (
                    // scenepreprocessor adds a default material only if nummat==0).
                    var m = new TempMaterial(); m.name = "<MS3D_DefaultMat>";
                    materials.Add(m);//.emplace_back();
                    //TempMaterial m = materials.back();

                    //strcpy(m.name, "<MS3D_DefaultMat>");
                    m.diffuse = new vec4(0.6f, 0.6f, 0.6f, 1.0f);
                    m.transparency = 1.0f;
                    m.shininess = 0.0f;

                    // this is because these TempXXX struct's have no c'tors.
                    //m.texture[0] = m.alphamap[0] = '\0';
                    m.texture = ""; m.alphamap = "";

                    for (var i = 0; i < groups.Count; ++i) {
                        TempGroup g = groups[i];
                        if (g.mat == UInt32.MaxValue) {
                            g.mat = (uint)(materials.Count - 1);// static_cast<uint>(materials.size() - 1);
                        }
                    }
                }

                // convert materials to our generic key-value dict-alike
                if (materials.Count > 0) {
                    pScene.mMaterials = new aiMaterial[materials.Count];
                    for (int i = 0; i < materials.Count; ++i) {
                        var mo = new aiMaterial();
                        pScene.mMaterials[pScene.mNumMaterials++] = mo;

                        TempMaterial mi = materials[i];

                        if (!string.IsNullOrEmpty(mi.alphamap)) {
                            //tmp = aiString(mi.alphamap);
                            mo.AddProperty(mi.alphamap, "$tex.file", aiTextureType.aiTextureType_OPACITY, 0);
                        }
                        if (!string.IsNullOrEmpty(mi.texture)) {
                            //tmp = aiString(mi.texture);
                            mo.AddProperty(mi.texture, "$tex.file", aiTextureType.aiTextureType_DIFFUSE, 0);
                        }
                        if (!string.IsNullOrEmpty(mi.name)) {
                            //tmp = aiString(mi.name);
                            mo.AddProperty(mi.name, "?mat.name", 0, 0);
                        }

                        mo.AddProperty(mi.ambient, 1, "$clr.ambient", 0, 0);
                        mo.AddProperty(mi.diffuse, 1, "$clr.diffuse", 0, 0);
                        mo.AddProperty(mi.specular, 1, "$clr.specular", 0, 0);
                        mo.AddProperty(mi.emissive, 1, "$clr.emissive", 0, 0);

                        mo.AddProperty(mi.shininess, 1, "$mat.shininess", 0, 0);
                        mo.AddProperty(mi.transparency, 1, "$mat.opacity", 0, 0);

                        var sm = mi.shininess > 0.0f ? aiShadingMode.aiShadingMode_Phong : aiShadingMode.aiShadingMode_Gouraud;
                        mo.AddProperty((int)sm, 1, "$mat.shadingm", 0, 0);
                    }
                }

                // convert groups to meshes
                if (groups.Count == 0) {
                    throw new Exception("MS3D: Didn't get any group records, file is malformed");
                }

                pScene.mNumMeshes = groups.Count;// static_cast<uint>(groups.size());
                pScene.mMeshes = new aiMesh[groups.Count];
                for (var i = 0; i < pScene.mNumMeshes; ++i) {
                    aiMesh m = new aiMesh(); pScene.mMeshes[i] = m;
                    TempGroup g = groups[i];

                    if (pScene.mNumMaterials != 0 && g.mat > pScene.mNumMaterials) {
                        throw new Exception("MS3D: Encountered invalid material index, file is malformed");
                    } // no error if no materials at all - scenepreprocessor adds one then

                    m.mMaterialIndex = g.mat;
                    m.mPrimitiveTypes = aiPrimitiveType.aiPrimitiveType_TRIANGLE;

                    m.mNumFaces = g.triangles.Count;// static_cast<uint>(g.triangles.size());
                    m.mFaces = new aiFace[g.triangles.Count];
                    m.mNumVertices = m.mNumFaces * 3;

                    // storage for vertices - verbose format, as requested by the postprocessing pipeline
                    m.mVertices = new vec3[m.mNumVertices];
                    m.mNormals = new vec3[m.mNumVertices];
                    m.mTextureCoords[0] = new vec3[m.mNumVertices];
                    m.mNumUVComponents[0] = 2;

                    //typedef std::map<uint, uint> BoneSet;
                    var mybones = new Dictionary<uint, uint>();

                    for (int j = 0, n = 0; j < m.mNumFaces; ++j) {
                        aiFace f = m.mFaces[j];
                        if (g.triangles[j] >= triangles.Count) {
                            throw new Exception("MS3D: Encountered invalid triangle index, file is malformed");
                        }

                        TempTriangle t = triangles[(int)g.triangles[j]];
                        f.mNumIndices = 3;
                        f.mIndices = new uint[3];

                        for (uint k = 0; k < 3; ++k, ++n) {
                            if (t.indices[k] >= vertices.Count) {
                                throw new Exception("MS3D: Encountered invalid vertex index, file is malformed");
                            }

                            TempVertex v = vertices[(int)t.indices[k]];
                            for (uint a = 0; a < 4; ++a) {
                                if (v.bone_id[a] != UInt32.MaxValue) {
                                    if (v.bone_id[a] >= joints.Count) {
                                        throw new Exception("MS3D: Encountered invalid bone index, file is malformed");
                                    }
                                    if (mybones.TryGetValue(v.bone_id[a], out var value)) {
                                        ++mybones[v.bone_id[a]];
                                    }
                                    else {
                                        mybones[v.bone_id[a]] = 1;
                                    }
                                }
                            }

                            // collect vertex components
                            m.mVertices[n] = v.pos;

                            m.mNormals[n] = t.normals[k];
                            m.mTextureCoords[0][n] = new vec3(t.uv[k].x, 1.0f - t.uv[k].y, 0.0f);
                            f.mIndices[k] = (uint)n;
                        }
                    }

                    // allocate storage for bones
                    if (mybones.Count > 0) {
                        var bmap = new List<uint>(joints.Count);
                        m.mBones = new aiBone[mybones.Count];
                        foreach (var pair in mybones) {
                            aiBone bn = m.mBones[m.mNumBones] = new aiBone();
                            TempJoint jnt = joints[(int)pair.Key];

                            bn.mName = jnt.name;//.Set(jnt.name);
                            bn.mWeights = new aiVertexWeight[(int)pair.Value];

                            bmap[(int)pair.Key] = m.mNumBones++;
                        }

                        // .. and collect bone weights
                        for (int j = 0, n = 0; j < m.mNumFaces; ++j) {
                            TempTriangle t = triangles[(int)g.triangles[j]];

                            for (uint k = 0; k < 3; ++k, ++n) {
                                TempVertex v = vertices[(int)t.indices[k]];
                                for (uint a = 0; a < 4; ++a) {
                                    uint bone = v.bone_id[a];
                                    if (bone == UInt32.MaxValue) {
                                        continue;
                                    }

                                    aiBone outbone = m.mBones[bmap[(int)bone]];
                                    aiVertexWeight outwght = outbone.mWeights[outbone.mNumWeights++];

                                    outwght.mVertexId = (uint)n;
                                    outwght.mWeight = v.weights[a];
                                }
                            }
                        }
                    }
                }

                // ... add dummy nodes under a single root, each holding a reference to one
                // mesh. If we didn't do this, we'd lose the group name.
                aiNode rt = pScene.mRootNode = new aiNode("<MS3DRoot>");

#if ASSIMP_BUILD_MS3D_ONE_NODE_PER_MESH
                rt.mChildren = new aiNode[rt.mNumChildren = pScene.mNumMeshes + (joints.Count != 0 ? 1 : 0)];

                for (var i = 0; i < pScene.mNumMeshes; ++i) {
                    TempGroup g = groups[i];

                    // we need to generate an unique name for all mesh nodes.
                    // since we want to keep the group name, a prefix is
                    // prepended.
                    aiNode nd = rt.mChildren[i] = new aiNode($"<MS3DMesh>_{g.name}");
                    nd.mParent = rt;

                    nd.mMeshes = new uint[nd.mNumMeshes = 1];
                    nd.mMeshes[0] = (uint)i;
                }
#else
                rt.mMeshes = new uint[pScene.mNumMeshes];
                for (uint i = 0; i < pScene.mNumMeshes; ++i) {
                    rt.mMeshes[rt.mNumMeshes++] = i;
                }
#endif

                // convert animations as well
                if (joints.Count > 0) {
#if ! ASSIMP_BUILD_MS3D_ONE_NODE_PER_MESH
                    rt.mChildren = new aiNode[1];
                    rt.mNumChildren = 1;

                    aiNode jt = rt.mChildren[0] = new aiNode("<MS3DJointRoot>");
#else
                    aiNode jt = rt.mChildren[pScene.mNumMeshes] = new aiNode("<MS3DJointRoot>");
#endif
                    jt.mParent = rt;
                    CollectChildJoints(joints, jt);
                    //jt.mName.Set("<MS3DJointRoot>");

                    pScene.mAnimations = new aiAnimation[pScene.mNumAnimations = 1];
                    aiAnimation anim = pScene.mAnimations[0] = new aiAnimation();

                    anim.mName = ("<MS3DMasterAnim>");

                    // carry the fps info to the user by scaling all times with it
                    anim.mTicksPerSecond = animfps;

                    // leave duration at its default, so ScenePreprocessor will fill an appropriate
                    // value (the values taken from some MS3D files seem to be too unreliable
                    // to pass the validation)
                    // anim.mDuration = totalframes/animfps;

                    anim.mChannels = new aiNodeAnim[joints.Count];
                    foreach (var aJoint in joints) {
                        //if ((*it).rotFrames.empty() && (*it).posFrames.empty()) { continue; }
                        if ((aJoint.rotFrames == null || aJoint.rotFrames.Length == 0)
                            && (aJoint.posFrames == null || aJoint.posFrames.Length == 0)) { continue; }

                        aiNodeAnim nd = anim.mChannels[anim.mNumChannels++] = new aiNodeAnim();
                        nd.mNodeName = aJoint.name;// ((*it).name);

                        if (aJoint.rotFrames != null && aJoint.rotFrames.Length > 0) {
                            nd.mRotationKeys = new aiQuatKey[aJoint.rotFrames.Length];
                            foreach (var rot in aJoint.rotFrames) {
                                aiQuatKey q = nd.mRotationKeys[nd.mNumRotationKeys++];

                                q.mTime = rot.time * animfps;
                                var _mat4 = mat4.FromEulerAnglesXYZ(aJoint.rotation)
                                        * mat4.FromEulerAnglesXYZ(rot.value);
                                q.mValue = new aiQuaternion(new mat3(_mat4));
                            }
                        }

                        if (aJoint.posFrames != null && aJoint.posFrames.Length > 0) {
                            nd.mPositionKeys = new aiVectorKey[aJoint.posFrames.Length];

                            var qu = 0;// = nd.mRotationKeys;
                            foreach (var pos in aJoint.posFrames) {
                                aiVectorKey v = nd.mPositionKeys[nd.mNumPositionKeys++];

                                v.mTime = pos.time * animfps;
                                v.mValue = aJoint.position + pos.value;

                                qu++;
                            }
                        }
                    }
                    // fixup to pass the validation if not a single animation channel is non-trivial
                    if (anim.mNumChannels == 0) {
                        anim.mChannels = new aiNodeAnim[0];//null;
                    }
                }
            }

        }

        aiScene mScene;

    }
    public unsafe struct TempVertex {
        public vec3 pos;
        public fixed uint bone_id[4];
        public uint ref_cnt;
        public fixed float weights[4];
    };

    public unsafe struct TempTriangle {
        public fixed uint indices[3];
        /// <summary>
        /// vec3[3]
        /// </summary>
        public vec3[] normals = new vec3[3];
        /// <summary>
        /// vec2[3]
        /// </summary>
        public vec2[] uv = new vec2[2];

        public uint sg, group;

        public TempTriangle() { }
    };

    public unsafe struct TempGroup : IHasComment {
        public string name;// = new byte[33]; // +0
        public List<uint> triangles;///= new List<uint>();
        public uint mat; // 0xff is no material
        public string comment;
        public TempGroup() {
            this.triangles = new List<uint>();
        }
        public void SetComment(string comment) {
            this.comment = comment;
        }
    };

    public interface IHasComment {
        void SetComment(string comment);
    }
    public struct TempMaterial : IHasComment {
        // again, add an extra 0 character to all strings -
        public string name;//=new byte[33];
        public string texture;//[129];
        public string alphamap;//[129];

        public vec4 diffuse, specular, ambient, emissive;
        public float shininess, transparency;
        public string comment;

        public void SetComment(string comment) {
            this.comment = comment;
        }
    };

    public struct TempKeyFrame {
        public float time;
        public vec3 value;
    };

    public struct TempJoint : IHasComment {
        public string name;//[33];
        public string parentName;//[33];
        public vec3 rotation, position;

        public TempKeyFrame[] rotFrames;
        public TempKeyFrame[] posFrames;
        public string comment;
        public void SetComment(string comment) {
            this.comment = comment;
        }
    };

    // struct TempModel {
    //   string comment;
    // };

}
