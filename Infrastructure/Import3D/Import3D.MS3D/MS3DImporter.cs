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
            var color = new vec4(//r g b a
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
            var vector = new vec3(// x y z
                       stream.ReadSingle(),
                       stream.ReadSingle(),
                       stream.ReadSingle());
            return vector;
        }
        public void ReadComments<T>(System.IO.BinaryReader stream, T[] outp)
            where T : IHasComment {
            UInt16 count = stream.ReadUInt16();

            for (uint i = 0; i < count; ++i) {
                UInt32 index = stream.ReadUInt32();
                UInt32 clength = stream.ReadUInt32();

                if (index >= outp.Length) {
                    Log.WriteLine("MS3D: Invalid index in comment section");
                }
                else if (clength > stream.BaseStream.Length - stream.BaseStream.Position) {
                    throw new Exception("MS3D: Failure reading comment, length field is out of range");
                }
                else {
                    var commentBytes = stream.ReadBytes((int)clength);
                    var comment = Encoding.ASCII.GetString(commentBytes); // string(reinterpret_cast<char*>(stream.GetPtr()), clength);
                    comment = comment.Substring(0, comment.IndexOf('\0'));
                    outp[(int)index].SetComment(comment);
                }
                //stream.IncPtr(clength);
            }
        }

        // ------------------------------------------------------------------------------------------------
        public void CollectChildJoints(TempJoint[] joints,
                List<bool> hadIt,
                aiNode node,
                mat4 absTransform) {
            uint count = 0;
            for (var i = 0; i < joints.Length; ++i) {
                if (!hadIt[i] && joints[i].parentName == node.mName) {
                    ++count;
                }
            }

            node.mNumChildren = (int)count;
            node.mChildren = new aiNode[count];
            count = 0;
            for (var i = 0; i < joints.Length; ++i) {
                if (!hadIt[i] && joints[i].parentName == node.mName) {
                    var childNode = new aiNode(joints[i].name);
                    node.mChildren[count++] = childNode;
                    childNode.mParent = node;

                    childNode.mTransformation = mat4.Translate(joints[i].position) * mat4.FromEulerAnglesXYZ(joints[i].rotation);

                    mat4 abs = absTransform * childNode.mTransformation;
                    for (uint a = 0; a < mScene.mNumMeshes; ++a) {
                        var mesh = mScene.mMeshes[a];
                        for (uint n = 0; n < mesh.mNumBones; ++n) {
                            aiBone bone = mesh.mBones[n];

                            if (bone.mName == childNode.mName) {
                                bone.mOffsetMatrix = abs.Inverse();
                            }
                        }
                    }

                    hadIt[i] = true;
                    CollectChildJoints(joints, hadIt, childNode, abs);
                }
            }
        }
        public void CollectChildJoints(TempJoint[] joints, aiNode node) {
            var hadIt = new List<bool>(joints.Length);
            mat4 transform = new mat4();// identity matrix

            CollectChildJoints(joints, hadIt, node, transform);
        }

        /** Imports the given file into the given scene structure.
   * See BaseImporter::InternReadFile() for details */
        // Imports the given file into the given scene structure.
        public unsafe void InternReadFile(string filename, aiScene scene) {
            using (var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
            using (var stream = new BinaryReader(fileStream)) {

                this.mScene = scene;

                // CanRead() should have done this already
                byte[] head = stream.ReadBytes(10); var strHead = Encoding.ASCII.GetString(head);
                if (strHead != "MS3D000000") {
                    throw new Exception($"{filename} is Not a MS3D file: magic string MS3D000000 is not found.");
                }

                Int32 version = stream.ReadInt32();
                if (version != 4) {
                    throw new Exception("MS3D: Unsupported file format version, 4 was expected");
                }

                UInt16 vertexCount = stream.ReadUInt16();
                var vertices = new TempVertex[vertexCount];
                for (var i = 0; i < vertexCount; ++i) {
                    var vertex = new TempVertex();// vertices[i];

                    {
                        //stream.IncPtr(1);
                        var tmp = stream.ReadByte();
                    }
                    vertex.position = ReadVector(stream);
                    var bone_id = (UInt32)stream.ReadByte();
                    if (bone_id == byte.MaxValue) { bone_id = UInt32.MaxValue; }
                    vertex.boneId[0] = (uint)bone_id;// stream.GetI1();
                    vertex.refCount = stream.ReadByte();// stream.GetI1();

                    vertex.boneId[1] = vertex.boneId[2] = vertex.boneId[3] = UInt32.MaxValue;// UINT_MAX;
                    vertex.weights[1] = vertex.weights[2] = vertex.weights[3] = 0.0f;
                    vertex.weights[0] = 1.0f;

                    vertices[i] = vertex;
                }

                UInt16 triangleCount = stream.ReadUInt16();
                var triangles = new TempTriangle[triangleCount];
                for (var i = 0; i < triangleCount; ++i) {
                    var triangle = new TempTriangle();

                    {
                        //stream.IncPtr(2);
                        var tmp = stream.ReadBytes(2);
                    }
                    for (var t = 0; t < 3; ++t) {
                        triangle.indices[t] = stream.ReadUInt16();// stream.GetI2();
                    }

                    for (var t = 0; t < 3; ++t) {
                        triangle.normals[t] = ReadVector(stream);
                    }

                    for (var t = 0; t < 3; ++t) {
                        //stream >> (float &)(t.uv[j].x); // see note in ReadColor()
                        triangle.uv[t].x = stream.ReadSingle();
                    }
                    for (var t = 0; t < 3; ++t) {
                        //stream >> (float &)(t.uv[j].y);
                        triangle.uv[t].y = stream.ReadSingle();
                    }

                    triangle.sg = stream.ReadByte();// stream.GetI1();
                    triangle.group = stream.ReadByte();// stream.GetI1();

                    triangles[i] = triangle;
                }

                bool need_default = false;
                UInt16 groupCount = stream.ReadUInt16();
                var groups = new TempGroup[groupCount];
                for (var i = 0; i < groupCount; ++i) {
                    var group = new TempGroup();

                    //stream.IncPtr(1);
                    var tmp = stream.ReadByte();
                    //stream.CopyAndAdvance(t.name, 32);
                    //t.name[32] = '\0';
                    byte[] name = stream.ReadBytes(32); var strName = Encoding.ASCII.GetString(name);
                    group.name = strName.Substring(0, strName.IndexOf('\0'));

                    UInt16 num = stream.ReadUInt16();
                    //stream >> num;
                    group.triangles = new uint[num];
                    for (var t = 0; t < num; ++t) {
                        group.triangles[t] = stream.ReadUInt16();// stream.GetI2();
                    }
                    group.materialId = stream.ReadByte();// stream.GetI1();
                    if (group.materialId == UInt32.MaxValue) {
                        need_default = true;
                    }

                    groups[i] = group;
                }

                UInt16 materialCount = stream.ReadUInt16();
                var materials = new TempMaterial[materialCount];
                for (var j = 0; j < materialCount; ++j) {
                    var material = new TempMaterial();
                    {
                        //stream.CopyAndAdvance(t.name, 32);
                        //t.name[32] = '\0';
                        var name = stream.ReadBytes(32); var strName = Encoding.ASCII.GetString(name);
                        material.name = strName.Substring(0, strName.IndexOf('\0'));
                    }
                    material.ambient = ReadColor(stream);
                    material.diffuse = ReadColor(stream);
                    material.specular = ReadColor(stream);
                    material.emissive = ReadColor(stream);
                    //stream >> t.shininess >> t.transparency;
                    material.shininess = stream.ReadSingle();
                    material.transparency = stream.ReadSingle();

                    {
                        //stream.IncPtr(1);
                        var tmp = stream.ReadByte();
                    }
                    {
                        //stream.CopyAndAdvance(t.texture, 128);
                        //t.texture[128] = '\0';
                        var name = stream.ReadBytes(128); var strName = Encoding.ASCII.GetString(name);
                        material.texture = strName.Substring(0, strName.IndexOf('\0'));
                    }
                    {
                        //stream.CopyAndAdvance(t.alphamap, 128);
                        //t.alphamap[128] = '\0';
                        var name = stream.ReadBytes(128); var strName = Encoding.ASCII.GetString(name);
                        material.alphamap = strName.Substring(0, strName.IndexOf('\0'));
                    }

                    materials[j] = material;
                }

                //stream >> animfps >> currenttime >> totalframes;
                float animfps = stream.ReadSingle();
                float currenttime = stream.ReadSingle();
                UInt32 totalframes = stream.ReadUInt32();

                UInt16 jointCount = stream.ReadUInt16();
                var joints = new TempJoint[jointCount];
                for (var i = 0; i < jointCount; ++i) {
                    var joint = new TempJoint();

                    {
                        //stream.IncPtr(1);
                        var tmp = stream.ReadByte();
                    }
                    {
                        //stream.CopyAndAdvance(j.name, 32);
                        //j.name[32] = '\0';
                        var name = stream.ReadBytes(128); var strName = Encoding.ASCII.GetString(name);
                        joint.name = strName.Substring(0, strName.IndexOf('\0'));
                    }
                    {
                        //stream.CopyAndAdvance(j.parentName, 32);
                        //j.parentName[32] = '\0';
                        var name = stream.ReadBytes(128); var strName = Encoding.ASCII.GetString(name);
                        joint.parentName = strName.Substring(0, strName.IndexOf('\0'));
                    }
                    joint.rotation = ReadVector(stream);
                    joint.position = ReadVector(stream);
                    {
                        var count = stream.ReadUInt16();
                        joint.rotFrames = new TempKeyFrame[count];
                    }
                    {
                        var count = stream.ReadUInt16();
                        joint.posFrames = new TempKeyFrame[count];
                    }

                    for (var a = 0; a < joint.rotFrames.Length; ++a) {
                        joint.rotFrames[a].time = stream.ReadSingle();
                        joint.rotFrames[a].value = ReadVector(stream);
                    }
                    for (uint a = 0; a < joint.posFrames.Length; ++a) {
                        joint.posFrames[a].time = stream.ReadSingle();
                        joint.posFrames[a].value = ReadVector(stream);
                    }

                    joints[i] = joint;
                }

                if (stream.BaseStream.Length - stream.BaseStream.Position > 4) {
                    UInt32 subversion = stream.ReadUInt32();
                    if (subversion == 1) {
                        ReadComments(stream, groups);
                        ReadComments(stream, materials);
                        ReadComments(stream, joints);

                        // model comment - print it for we have such a nice log.
                        var tmp = stream.ReadInt32();
                        if (tmp != 0) {
                            int length = stream.ReadInt32();
                            if (length > stream.BaseStream.Length - stream.BaseStream.Position) {
                                throw new Exception("MS3D: Model comment is too long");
                            }

                            {
                                //string s = string(reinterpret_cast<char*>(stream.GetPtr()), len);
                                //Log.WriteLine("MS3D: Model comment: ", s);
                                var bytes = stream.ReadBytes(length); var comment = Encoding.ASCII.GetString(bytes);
                                comment = comment.Substring(0, comment.IndexOf('\0'));
                                Log.WriteLine($"MS3D: Model comment: {comment}");
                            }
                        }

                        if (stream.BaseStream.Length - stream.BaseStream.Position > 4) {
                            subversion = stream.ReadUInt32();
                            if (1u < subversion && subversion <= 3u) {
                                for (var i = 0; i < vertexCount; ++i) {
                                    TempVertex vertex = vertices[i];
                                    vertex.weights[3] = 1.0f;
                                    for (uint t = 0; t < 3; vertex.weights[3] -= vertex.weights[t++]) {
                                        vertex.boneId[t + 1] = stream.ReadByte();//.GetI1();
                                        var weight = stream.ReadByte();
                                        vertex.weights[t] = weight / 255.0f;
                                    }
                                    //stream.IncPtr((subversion - 1) << 2u);
                                    var forward = (subversion - 1) << 2;
                                    stream.BaseStream.Position += forward;
                                }

                                // even further extra data is not of interest for us, at least now now.
                            }
                        }
                    }
                }

                // convert to proper aiXX data structures

                if (need_default && materials.Length == 0) {
                    Log.WriteLine("MS3D: Found group with no material assigned, spawning default material");
                    // if one of the groups has no material assigned, but there are other
                    // groups with materials, a default material needs to be added (
                    // scenepreprocessor adds a default material only if nummat==0).
                    var material = new TempMaterial(); material.name = "<MS3D_DefaultMat>";
                    materials = new TempMaterial[] { material };//.emplace_back();
                    material.diffuse = new vec4(0.6f, 0.6f, 0.6f, 1.0f);
                    material.transparency = 1.0f;
                    material.shininess = 0.0f;

                    // this is because these TempXXX struct's have no c'tors.
                    //m.texture[0] = m.alphamap[0] = '\0';
                    material.texture = ""; material.alphamap = "";

                    for (var i = 0; i < groups.Length; ++i) {
                        TempGroup group = groups[i];
                        if (group.materialId == UInt32.MaxValue) {
                            group.materialId = (uint)(materials.Length - 1);// static_cast<uint>(materials.size() - 1);
                        }
                        groups[i] = group;
                    }
                }

                // convert materials to our generic key-value dict-alike
                if (materials.Length > 0) {
                    scene.mMaterials = new aiMaterial[materials.Length];
                    for (int i = 0; i < materials.Length; ++i) {
                        var mo = new aiMaterial();
                        scene.mMaterials[scene.mNumMaterials++] = mo;

                        TempMaterial material = materials[i];

                        if (!string.IsNullOrEmpty(material.alphamap)) {
                            //tmp = aiString(mi.alphamap);
                            mo.AddProperty(material.alphamap, "$tex.file", aiTextureType.aiTextureType_OPACITY, 0);
                        }
                        if (!string.IsNullOrEmpty(material.texture)) {
                            //tmp = aiString(mi.texture);
                            mo.AddProperty(material.texture, "$tex.file", aiTextureType.aiTextureType_DIFFUSE, 0);
                        }
                        if (!string.IsNullOrEmpty(material.name)) {
                            //tmp = aiString(mi.name);
                            mo.AddProperty(material.name, "?mat.name", 0, 0);
                        }

                        mo.AddProperty(material.ambient, 1, "$clr.ambient", 0, 0);
                        mo.AddProperty(material.diffuse, 1, "$clr.diffuse", 0, 0);
                        mo.AddProperty(material.specular, 1, "$clr.specular", 0, 0);
                        mo.AddProperty(material.emissive, 1, "$clr.emissive", 0, 0);

                        mo.AddProperty(material.shininess, 1, "$mat.shininess", 0, 0);
                        mo.AddProperty(material.transparency, 1, "$mat.opacity", 0, 0);

                        var sm = material.shininess > 0.0f ? aiShadingMode.aiShadingMode_Phong : aiShadingMode.aiShadingMode_Gouraud;
                        mo.AddProperty((int)sm, 1, "$mat.shadingm", 0, 0);
                    }
                }

                // convert groups to meshes
                if (groups.Length == 0) {
                    throw new Exception("MS3D: Didn't get any group records, file is malformed");
                }

                scene.mNumMeshes = groups.Length;// static_cast<uint>(groups.size());
                scene.mMeshes = new aiMesh[groups.Length];
                for (var i = 0; i < scene.mNumMeshes; ++i) {
                    aiMesh mesh = new aiMesh(); scene.mMeshes[i] = mesh;
                    TempGroup group = groups[i];

                    if (scene.mNumMaterials != 0 && group.materialId > scene.mNumMaterials) {
                        throw new Exception("MS3D: Encountered invalid material index, file is malformed");
                    } // no error if no materials at all - scenepreprocessor adds one then

                    mesh.mMaterialIndex = group.materialId;
                    mesh.mPrimitiveTypes = aiPrimitiveType.aiPrimitiveType_TRIANGLE;

                    mesh.mNumFaces = group.triangles.Length;// static_cast<uint>(g.triangles.size());
                    mesh.mFaces = new aiFace[group.triangles.Length];
                    mesh.mNumVertices = mesh.mNumFaces * 3;

                    // storage for vertices - verbose format, as requested by the postprocessing pipeline
                    mesh.mVertices = new vec3[mesh.mNumVertices];
                    mesh.mNormals = new vec3[mesh.mNumVertices];
                    mesh.mTextureCoords[0] = new vec3[mesh.mNumVertices];
                    mesh.mNumUVComponents[0] = 2;

                    //typedef std::map<uint, uint> BoneSet;
                    var mybones = new Dictionary<uint, uint>();

                    for (int j = 0, index = 0; j < mesh.mNumFaces; ++j) {
                        aiFace face = new aiFace();// m.mFaces[j];
                        if (group.triangles[j] >= triangles.Length) {
                            throw new Exception("MS3D: Encountered invalid triangle index, file is malformed");
                        }

                        face.mNumIndices = 3;
                        face.mIndices = new uint[3];

                        TempTriangle triangle = triangles[(int)group.triangles[j]];
                        for (uint k = 0; k < 3; ++k, ++index) {
                            if (triangle.indices[k] >= vertices.Length) {
                                throw new Exception("MS3D: Encountered invalid vertex index, file is malformed");
                            }

                            TempVertex vertex = vertices[(int)triangle.indices[k]];
                            for (uint t = 0; t < 4; ++t) {
                                if (vertex.boneId[t] != UInt32.MaxValue) {
                                    if (vertex.boneId[t] >= joints.Length) {
                                        throw new Exception("MS3D: Encountered invalid bone index, file is malformed");
                                    }
                                    if (mybones.TryGetValue(vertex.boneId[t], out var value)) {
                                        ++mybones[vertex.boneId[t]];
                                    }
                                    else {
                                        mybones[vertex.boneId[t]] = 1;
                                    }
                                }
                            }

                            // collect vertex components
                            mesh.mVertices[index] = vertex.position;

                            mesh.mNormals[index] = triangle.normals[k];
                            mesh.mTextureCoords[0][index] = new vec3(triangle.uv[k].x, 1.0f - triangle.uv[k].y, 0.0f);
                            face.mIndices[k] = (uint)index;
                        }
                        mesh.mFaces[j] = face;
                    }

                    // allocate storage for bones
                    if (mybones.Count > 0) {
                        var bmap = new uint[joints.Length];
                        mesh.mBones = new aiBone[mybones.Count];
                        foreach (var pair in mybones) {
                            aiBone bone = mesh.mBones[mesh.mNumBones] = new aiBone();
                            TempJoint joint = joints[(int)pair.Key];

                            bone.mName = joint.name;//.Set(jnt.name);
                            bone.mWeights = new aiVertexWeight[(int)pair.Value];

                            bmap[(int)pair.Key] = mesh.mNumBones++;
                        }

                        // .. and collect bone weights
                        for (int j = 0, id = 0; j < mesh.mNumFaces; ++j) {
                            TempTriangle triangle = triangles[(int)group.triangles[j]];

                            for (uint k = 0; k < 3; ++k, ++id) {
                                TempVertex vertex = vertices[(int)triangle.indices[k]];
                                for (var t = 0; t < 4; ++t) {
                                    uint bone = vertex.boneId[t];
                                    if (bone == UInt32.MaxValue) { continue; }

                                    aiBone outBone = mesh.mBones[bmap[(int)bone]];
                                    aiVertexWeight outWeight = outBone.mWeights[outBone.mNumWeights++];

                                    outWeight.mVertexId = (uint)id;
                                    outWeight.mWeight = vertex.weights[t];
                                }
                            }
                        }
                    }
                }

                // ... add dummy nodes under a single root, each holding a reference to one
                // mesh. If we didn't do this, we'd lose the group name.
                aiNode rootNode = scene.mRootNode = new aiNode("<MS3DRoot>");

#if ASSIMP_BUILD_MS3D_ONE_NODE_PER_MESH
                rootNode.mChildren = new aiNode[rootNode.mNumChildren = scene.mNumMeshes + (joints.Length != 0 ? 1 : 0)];

                for (var i = 0; i < scene.mNumMeshes; ++i) {
                    TempGroup g = groups[i];

                    // we need to generate an unique name for all mesh nodes.
                    // since we want to keep the group name, a prefix is
                    // prepended.
                    aiNode nd = rootNode.mChildren[i] = new aiNode($"<MS3DMesh>_{g.name}");
                    nd.mParent = rootNode;

                    nd.mMeshes = new uint[nd.mNumMeshes = 1];
                    nd.mMeshes[0] = (uint)i;
                }
#else
                rootNode.mMeshes = new uint[scene.mNumMeshes];
                for (uint i = 0; i < scene.mNumMeshes; ++i) {
                    rootNode.mMeshes[rootNode.mNumMeshes++] = i;
                }
#endif

                // convert animations as well
                if (joints.Length > 0) {
#if ! ASSIMP_BUILD_MS3D_ONE_NODE_PER_MESH
                    rootNode.mChildren = new aiNode[1];
                    rootNode.mNumChildren = 1;

                    aiNode jointRoot = rootNode.mChildren[0] = new aiNode("<MS3DJointRoot>");
#else
                    aiNode jointRoot = rootNode.mChildren[scene.mNumMeshes] = new aiNode("<MS3DJointRoot>");
#endif
                    jointRoot.mParent = rootNode;
                    CollectChildJoints(joints, jointRoot);
                    //jt.mName.Set("<MS3DJointRoot>");

                    scene.mAnimations = new aiAnimation[scene.mNumAnimations = 1];
                    var anim = new aiAnimation(); anim.mName = ("<MS3DMasterAnim>");
                    scene.mAnimations[0] = anim;

                    // carry the fps info to the user by scaling all times with it
                    anim.mTicksPerSecond = animfps;

                    // leave duration at its default, so ScenePreprocessor will fill an appropriate
                    // value (the values taken from some MS3D files seem to be too unreliable
                    // to pass the validation)
                    // anim.mDuration = totalframes/animfps;

                    anim.mChannels = new aiNodeAnim[joints.Length];
                    foreach (var joint in joints) {
                        //if ((*it).rotFrames.empty() && (*it).posFrames.empty()) { continue; }
                        if ((joint.rotFrames == null || joint.rotFrames.Length == 0)
                            && (joint.posFrames == null || joint.posFrames.Length == 0)) { continue; }

                        aiNodeAnim nodeAnim = new aiNodeAnim(); nodeAnim.mNodeName = joint.name;// ((*it).name);
                        anim.mChannels[anim.mNumChannels++] = nodeAnim;
                        if (joint.rotFrames != null && joint.rotFrames.Length > 0) {
                            nodeAnim.mRotationKeys = new aiQuatKey[joint.rotFrames.Length];
                            foreach (var rot in joint.rotFrames) {
                                aiQuatKey q = nodeAnim.mRotationKeys[nodeAnim.mNumRotationKeys++];

                                q.mTime = rot.time * animfps;
                                var _mat4 = mat4.FromEulerAnglesXYZ(joint.rotation)
                                        * mat4.FromEulerAnglesXYZ(rot.value);
                                q.mValue = new aiQuaternion(new mat3(_mat4));
                            }
                        }
                        if (joint.posFrames != null && joint.posFrames.Length > 0) {
                            nodeAnim.mPositionKeys = new aiVectorKey[joint.posFrames.Length];

                            var qu = 0;// = nd.mRotationKeys;
                            foreach (var pos in joint.posFrames) {
                                aiVectorKey v = nodeAnim.mPositionKeys[nodeAnim.mNumPositionKeys++];

                                v.mTime = pos.time * animfps;
                                v.mValue = joint.position + pos.value;

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
        public vec3 position;
        public fixed uint boneId[4];
        public uint refCount;
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
        public vec2[] uv = new vec2[3];

        public uint sg, group;

        public TempTriangle() { }
    };

    public unsafe struct TempGroup : IHasComment {
        public string name;// = new byte[33]; // +0
        public uint[] triangles;///= new List<uint>();
        public uint materialId; // 0xff is no material
        public string comment;
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
