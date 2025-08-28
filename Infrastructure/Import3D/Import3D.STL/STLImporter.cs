using System;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;

namespace Import3D.STL {
    public unsafe partial class STLImporter {

        // Imports the given file into the given scene structure.
        public static void InternReadFile(string pFile, aiScene scene) {

            var color = new vec4(1.0f);

            if (IsBinarySTL(pFile)) {
                var file = new FileStream(pFile, FileMode.Open, FileAccess.Read);
                var context = new BinContext(file, scene);
                var bMatClr = LoadBinaryFile(context);
                if (bMatClr) { color = context.mClrColorDefault; }
                file.Dispose();
            }
            else if (IsAsciiSTL(pFile)) {

                //var fullText = File.ReadAllText(pFile);
                //var separator = new char[] { ' ', '\t', '\r', '\n', '\f' };
                //var tokens = fullText.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                var context = new AsciiContext(pFile, scene);

                LoadASCIIFile(context);
            }
            else {
                throw new Exception($"Failed to determine STL storage representation for {pFile}");
            }

            // create a single default material, using a white diffuse color for consistency with
            // other geometric types (e.g., PLY).
            var pcMat = new aiMaterial();
            string s =/*AI_DEFAULT_MATERIAL_NAME*/aiMaterial.DEFAULT_MATERIAL;
            pcMat.AddProperty(s, /*AI_MATKEY_NAME*/"?mat.name", 0, 0);

            pcMat.AddProperty(color, 1, /*AI_MATKEY_COLOR_DIFFUSE*/"$clr.diffuse", 0, 0);
            pcMat.AddProperty(color, 1, /*AI_MATKEY_COLOR_SPECULAR*/"$clr.specular", 0, 0);
            color = new vec4(0.05f, 0.05f, 0.05f, 1.0f);
            pcMat.AddProperty(color, 1, /*AI_MATKEY_COLOR_AMBIENT*/"$clr.ambient", 0, 0);

            scene.mNumMaterials = 1;
            scene.mMaterials = new aiMaterial[1] { pcMat };

            //mBuffer = null;
        }
        static readonly char[] spaceSeparator = new char[] { ' ', '\t' };
        // Read an ASCII STL file
        private static void LoadASCIIFile(AsciiContext context) {
            var meshes = new List<aiMesh>();
            var nodes = new List<aiNode>();

            // try to guess how many vertices we could have
            // assume all tokens are like : vertex x y z
            var sizeEstimate = context.lines.Count / 2;
            var positionBuffer = new List<vec3>(sizeEstimate);
            var normalBuffer = new List<vec3>(sizeEstimate);

            {
                var meshIndices = new List<uint>();
                var pMesh = new aiMesh();
                pMesh.mMaterialIndex = 0;
                meshIndices.Add((uint)meshes.Count);//.push_back((unsigned int)meshes.size());
                meshes.Add(pMesh);
                var node = new aiNode("");
                node.mParent = context.scene.mRootNode;
                nodes.Add(node);
                {
                    string name = "<STL_ASCII>";
                    var line = context.NextLine(); if (line != null) {
                        var parts = line.Split(spaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1) { name = parts[1]; }
                    }
                    pMesh.mName = name;
                }

                int faceVertexCounter = 3;
                for (var line = context.NextLine(); line != null; line = context.NextLine()) {
                    var parts = line.Split(spaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                    // facet normal -0.13 -0.13 -0.98
                    if (line.StartsWith("facet")) {
                        if (faceVertexCounter != 3) {
                            Log.WriteLine("STL: A new facet begins but the old is not yet complete");
                        }
                        faceVertexCounter = 0;
                        if (parts[1] != "normal") {
                            Log.WriteLine("STL: a facet normal vector was expected but not found");
                        }
                        else {
                            var normal = new vec3(
                                float.Parse(parts[2]),
                                float.Parse(parts[3]),
                                float.Parse(parts[4])
                                );
                            normalBuffer.Add(normal);
                            normalBuffer.Add(normal);
                            normalBuffer.Add(normal);
                        }
                    }
                    else if (line.StartsWith("vertex")) { // vertex 1.50000 1.50000 0.00000
                        if (faceVertexCounter >= 3) {
                            Log.WriteLine("STL: a facet with more than 3 vertices has been found");
                        }
                        else {
                            var position = new vec3(
                                float.Parse(parts[1]),
                                float.Parse(parts[2]),
                                float.Parse(parts[3])
                                );
                            positionBuffer.Add(position);
                            faceVertexCounter++;
                        }
                    }
                    else if (line.StartsWith("endsolid")) {
                        // finished!
                        break;
                    }
                    else { // else skip the whole line
                    }
                }

                if (positionBuffer.Count == 0) {
                    pMesh.mNumFaces = 0;
                    Log.WriteLine("STL: mesh is empty or invalid; no data loaded");
                }
                if (positionBuffer.Count % 3 != 0) {
                    pMesh.mNumFaces = 0;
                    throw new Exception("STL: Invalid number of vertices");
                }
                if (normalBuffer.Count != positionBuffer.Count) {
                    pMesh.mNumFaces = 0;
                    throw new Exception("Normal buffer size does not match position buffer size");
                }

                // only process position buffer when filled, else exception when accessing with index operator
                // see line 353: only warning is triggered
                // see line 373(now): access to empty position buffer with index operator forced exception
                if (positionBuffer.Count > 0) {
                    pMesh.mNumFaces = positionBuffer.Count / 3;
                    pMesh.mNumVertices = positionBuffer.Count;
                    pMesh.mVertices = positionBuffer.ToArray();
                    //positionBuffer.Clear();
                }
                // also only process normalBuffer when filled, else exception when accessing with index operator
                if (normalBuffer.Count > 0) {
                    pMesh.mNormals = normalBuffer.ToArray();
                    //normalBuffer.Clear();
                }

                // now copy faces
                addFacesToMesh(pMesh);

                // assign the meshes to the current node
                pushMeshesToNode(meshIndices, node);
            }

            // now add the loaded meshes
            context.scene.mNumMeshes = meshes.Count;
            context.scene.mMeshes = meshes.ToArray();

            context.scene.mRootNode.mNumChildren = nodes.Count;
            context.scene.mRootNode.mChildren = nodes.ToArray();
        }

        private static void pushMeshesToNode(List<uint> meshIndices, aiNode node) {
            Debug.Assert(null != node);
            if (meshIndices.Count == 0) { return; }

            node.mNumMeshes = meshIndices.Count;
            node.mMeshes = meshIndices.ToArray();
            //meshIndices.Clear();
        }

        const byte UnicodeBoundary = 127;

        // An ascii STL buffer will begin with "solid NAME", where NAME is optional.
        // Note: The "solid NAME" check is necessary, but not sufficient, to determine
        // if the buffer is ASCII; a binary header could also begin with "solid NAME".
        private static bool IsAsciiSTL(string filename) {
            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read)) {
                var bytes = stackalloc byte[5];
                // skip spaces
                {
                    int b = 0;
                    while (file.Position < file.Length) {
                        b = file.ReadByte();
                        if (b != ' ' && b != '\t' && b != '\r' && b != '\n' && b != '\f') { break; }
                        if (b < 0) { break; }
                    }

                    if (file.Position + 4 >= file.Length) {
                        return false;
                    }

                    bytes[0] = (byte)b;
                }

                file.ReadExactly(new Span<byte>(bytes + 1, 4));
                bool isASCII = Import3D.Utility.strncmp(bytes, "solid", 5) == 0;
                if (isASCII) {
                    // A lot of importers are write solid even if the file is binary. So we have to check for ASCII-characters.
                    var pos = file.Position;
                    while (file.Position < file.Length) {
                        var b = file.ReadByte();
                        if (b > UnicodeBoundary) {
                            isASCII = false;
                            break;
                        }
                    }
                    file.Position = pos;
                }
                return isASCII;
            }
        }



        private static bool SkipSpaces(FileStream file, long length) {
            int b = 0;
            while (file.Position < length) {
                b = file.ReadByte();
                if (b != ' ' && b != '\t') { break; }
                if (b < 0) { break; }
            }

            return !IsLineEnd(b);
        }
        private static bool IsLineEnd(int b) {
            return (b == '\r' || b == '\n' || b == '\0' || b == '\f');
        }

        // Read a binary STL file
        private static bool LoadBinaryFile(BinContext context) {
            var file = context.file;
            var mScene = context.scene;

            // allocate one mesh
            mScene.mNumMeshes = 1;
            var pMesh = new aiMesh();
            mScene.mMeshes = new aiMesh[1] { pMesh };
            pMesh.mMaterialIndex = 0;

            // skip the first 80 bytes
            if (file.Length < 84) {
                throw new Exception("STL: file is too small for the header");
            }
            bool bIsMaterialise = false;

            // search for an occurrence of "COLOR=" in the header
            var pos = file.Position;
            var sz2 = pos;// (const unsigned char*)mBuffer;
            var szEnd = sz2 + 80;
            var bytes6 = stackalloc byte[6]; var span6 = new Span<byte>(bytes6, 6);
            while (file.Position < szEnd) {
                file.ReadExactly(span6);
                if (Import3D.Utility.strncmp(bytes6, "COLOR=", 6) == 0) {
                    // read the default vertex color for facets
                    bIsMaterialise = true;
                    Log.WriteLine("STL: Taking code path for Materialise files");
                    const float invByte = 1.0f / 255.0f;
                    context.mClrColorDefault.x = file.ReadByte() * invByte;
                    context.mClrColorDefault.y = file.ReadByte() * invByte;
                    context.mClrColorDefault.z = file.ReadByte() * invByte;
                    context.mClrColorDefault.w = file.ReadByte() * invByte;
                    break;
                }
                else { file.Position = file.Position - 5; }
            }

            // now read the number of facets
            context.scene.mRootNode.mName = "<STL_BINARY>";

            //const unsigned char* sz = (const unsigned char*)mBuffer + 80;
            file.Position = pos + 80;

            //var bytes4 = stackalloc byte[4]; var span4 = new Span<byte>(bytes4, 4);
            //file.ReadExactly(span4);
            var binaryReader = new BinaryReader(file);
            pMesh.mNumFaces = binaryReader.ReadInt32(); // *((uint32_t*)sz);
                                                        //sz += 4;

            if (context.file.Length < 84 + pMesh.mNumFaces * 50) {
                throw new Exception("STL: file is too small to hold all facets");
            }

            if (pMesh.mNumFaces == 0) {
                throw new Exception("STL: file is empty. There are no facets defined");
            }

            pMesh.mNumVertices = pMesh.mNumFaces * 3;

            var vp = new vec3[pMesh.mNumVertices]; var vpIndex = 0;
            pMesh.mVertices = vp;
            var vn = new vec3[pMesh.mNumVertices]; var vnIndex = 0;
            pMesh.mNormals = vn;

            vec3 theVec;
            vec3 theVec3F;

            for (uint i = 0; i < pMesh.mNumFaces; ++i) {
                // NOTE: Blender sometimes writes empty normals ... this is not
                // our fault ... the RemoveInvalidData helper step should fix that

                // There's one normal for the face in the STL; use it three times
                // for vertex normals
                //theVec = (vec3*)sz;
                theVec.x = binaryReader.ReadSingle();
                theVec.y = binaryReader.ReadSingle();
                theVec.z = binaryReader.ReadSingle();
                ////todo: ::memcpy(&theVec3F, theVec, sizeof(aiVector3f));
                vn[vnIndex++] = theVec; vn[vnIndex++] = theVec; vn[vnIndex++] = theVec;

                // vertex 1 2 3
                for (int t = 0; t < 3; t++) {
                    theVec.x = binaryReader.ReadSingle();
                    theVec.y = binaryReader.ReadSingle();
                    theVec.z = binaryReader.ReadSingle();
                    ////todo: ::memcpy(&theVec3F, theVec, sizeof(aiVector3f));
                    vp[vnIndex++] = theVec;
                }
                //sz = (char*)theVec;

                //uint16_t color = *((uint16_t*)sz);
                //sz += 2;
                UInt16 color = binaryReader.ReadUInt16();

                if ((color & (1 << 15)) != 0) {
                    // seems we need to take the color
                    if (pMesh.mColors[0] == null) {
                        pMesh.mColors[0] = new vec4[pMesh.mNumVertices];
                        for (uint j = 0; j < pMesh.mNumVertices; ++j) {
                            pMesh.mColors[0][j] = context.mClrColorDefault;
                        }
                        Log.WriteLine("STL: Mesh has vertex colors");
                    }
                    var clr = pMesh.mColors[0][i * 3];
                    clr.w = 1.0f;
                    float invVal = (1.0f / 31.0f);
                    if (bIsMaterialise) // this is reversed
                    {
                        clr.x = (color & 0x1fu) * invVal;
                        clr.y = ((color & (0x1fu << 5)) >> 5) * invVal;
                        clr.z = ((color & (0x1fu << 10)) >> 10) * invVal;
                    }
                    else {
                        clr.x = (color & 0x1fu) * invVal;
                        clr.y = ((color & (0x1fu << 5)) >> 5) * invVal;
                        clr.z = ((color & (0x1fu << 10)) >> 10) * invVal;
                    }
                    // assign the color to all vertices of the face
                    for (int t = 0; t < 3; t++) {
                        pMesh.mColors[i][i * 3 + t] = clr;
                    }
                }
            }

            // now copy faces
            addFacesToMesh(pMesh);

            var root = mScene.mRootNode;

            // allocate one node
            var node = new aiNode("");
            node.mParent = root;

            root.mNumChildren = 1;
            root.mChildren = new aiNode[root.mNumChildren];
            root.mChildren[0] = node;

            // add all created meshes to the single node
            node.mNumMeshes = mScene.mNumMeshes;
            node.mMeshes = new uint[mScene.mNumMeshes];
            for (uint i = 0; i < mScene.mNumMeshes; ++i) {
                node.mMeshes[i] = i;
            }

            if (bIsMaterialise && pMesh.mColors[0] == null) {
                // use the color as diffuse material color
                return true;
            }
            return false;

        }

        private static void addFacesToMesh(aiMesh pMesh) {
            pMesh.mFaces = new aiFace[pMesh.mNumFaces];
            for (int i = 0, p = 0; i < pMesh.mNumFaces; ++i) {
                var face = pMesh.mFaces[i];
                face.mNumIndices = 3;
                face.mIndices = new int[3];
                for (int o = 0; o < 3; ++o, ++p) {
                    face.mIndices[o] = p;
                }
                pMesh.mFaces[i] = face;
            }

        }

        // A valid binary STL buffer should consist of the following elements, in order:
        // 1) 80 byte header
        // 2) 4 byte face count
        // 3) 50 bytes per face
        private static unsafe bool IsBinarySTL(string filename) {
            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read)) {
                if (file.Length < 84) {
                    return false;
                }

                var pos = file.Position;
                file.Position = 80;
                UInt32 faceCount = 0;
                var span = new Span<byte>(&faceCount, sizeof(UInt32));
                file.ReadExactly(span);
                var expectedBinaryFileSize = faceCount * 50 + 84;

                return expectedBinaryFileSize == file.Length;
            }
        }
    }
}
