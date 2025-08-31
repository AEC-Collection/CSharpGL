using Assimp.Unmanaged;
using Assimp;
using CSharpGL;
using demos.anything;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Import3D;
using System.Windows.Forms.Design;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.IO;
using System.Diagnostics;
using static System.Net.WebRequestMethods;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ConstrainedExecution;
using System.Drawing.Design;
using System.Numerics;

namespace aiSceneTextureLegacy {
    unsafe partial class aiSceneTextureLegacy_ {

        public override void init(GL gl) {

            Import3DFromFile(modelpath);

            LoadGLTextures(gl, g_scene);

            gl.glEnable(GL.GL_TEXTURE_2D);
            gl.glShadeModel(GL.GL_SMOOTH);         // Enables Smooth Shading
            //gl.glClearColor(1.0f, 1.0f, 1.0f, 0.0f);
            gl.glClearDepth(1.0f);             // Depth Buffer Setup
            gl.glEnable(GL.GL_DEPTH_TEST);        // Enables Depth Testing
            gl.glDepthFunc(GL.GL_LEQUAL);         // The Type Of Depth Test To Do
            gl.glHint(GL.GL_PERSPECTIVE_CORRECTION_HINT, GL.GL_NICEST);  // Really Nice Perspective Calculation


            gl.glEnable(GL.GL_LIGHTING);
            gl.glEnable(GL.GL_LIGHT0);    // Uses default lighting parameters
            gl.glLightModeli(GL.GL_LIGHT_MODEL_TWO_SIDE, (int)GL.GL_TRUE);
            gl.glEnable(GL.GL_NORMALIZE);

            gl.glLightfv(GL.GL_LIGHT1, GL.GL_AMBIENT, LightAmbient);
            gl.glLightfv(GL.GL_LIGHT1, GL.GL_DIFFUSE, LightDiffuse);
            gl.glLightfv(GL.GL_LIGHT1, GL.GL_POSITION, LightPosition);
            gl.glEnable(GL.GL_LIGHT1);

            this.canvas.GLKeyDown += Canvas_GLKeyDown;

            MessageBox.Show("P: polygon mode");
        }

        private int polygonModeIndex = 2;
        private static GLenum[] polygonModes = [GL.GL_POINT, GL.GL_LINE, GL.GL_FILL];
        private void Canvas_GLKeyDown(object sender, GLKeyEventArgs e) {
            if (e.KeyData == GLKeys.P) {
                polygonModeIndex++;
                if (polygonModeIndex >= polygonModes.Length) { polygonModeIndex = 0; }
            }
        }

        private bool Import3DFromFile(string filename) {
            //g_scene = importer.ReadFile(filename, aiProcessPreset_TargetRealtime_Quality);
            //var model = Import3D.Obj.ObjFileParser.Parse(filename, modelName: filename);
            var scene = new Import3D.aiScene(name: filename);
            //Import3D.Obj.ObjSceneBuilder.BuildScene(model, scene);
            var importer = new Import3D.MS3D.MS3DImporter();
            importer.InternReadFile(filename, scene);
            g_scene = scene;

            CSharpGL.vec3 min = new CSharpGL.vec3(), max = new CSharpGL.vec3();
            get_bounding_box(scene, ref min, ref max);
            scene_min = *(Import3D.vec3*)(&min); scene_max = *(Import3D.vec3*)(&max);
            scene_center.x = (scene_min.x + scene_max.x) / 2.0f;
            scene_center.y = (scene_min.y + scene_max.y) / 2.0f;
            scene_center.z = (scene_min.z + scene_max.z) / 2.0f;

            // We're done. Everything will be cleaned up by the importer destructor
            return true;
        }
        /* ---------------------------------------------------------------------------- */
        void get_bounding_box(aiScene scene, ref CSharpGL.vec3 min, ref CSharpGL.vec3 max) {
            CSharpGL.mat4 trafo = CSharpGL.mat4.identity();

            get_bounding_box_for_node(scene, scene.mRootNode, ref min, ref max, trafo);
        }

        void get_bounding_box_for_node(aiScene scene, aiNode node,
                   ref CSharpGL.vec3 min, ref CSharpGL.vec3 max, CSharpGL.mat4 trafo,
                   bool firstTime = true) {
            //CSharpGL.mat4 prev;

            //prev = trafo;
            //aiMultiplyMatrix4(trafo, &node.mTransformation);
            var m = node.mTransformation; var m2 = (CSharpGL.mat4*)&m;
            trafo = trafo * (*m2);

            for (var n = 0; n < node.mNumMeshes; ++n) {
                var mesh = scene.mMeshes[node.mMeshes[n]];
                for (var t = 0; t < mesh.mNumVertices; ++t) {

                    var tmp = mesh.mVertices[t];
                    var tmp2 = (CSharpGL.vec3*)&tmp;
                    //aiTransformVecByMatrix4(&tmp, trafo);
                    var tmp3 = trafo * new CSharpGL.vec4(*tmp2, 1.0f);
                    //*tmp2 = new CSharpGL.vec3(tmp3.x, tmp3.y, tmp3.z);
                    Debug.Assert(tmp3.w == 1.0f);

                    if (firstTime) {
                        min = new CSharpGL.vec3(tmp3.x, tmp3.y, tmp3.z);
                        max = new CSharpGL.vec3(tmp3.x, tmp3.y, tmp3.z);
                        firstTime = false;
                    }
                    else {
                        min.x = Math.Min(min.x, tmp3.x);
                        min.y = Math.Min(min.y, tmp3.y);
                        min.z = Math.Min(min.z, tmp3.z);

                        max.x = Math.Max(max.x, tmp3.x);
                        max.y = Math.Max(max.y, tmp3.y);
                        max.z = Math.Max(max.z, tmp3.z);
                    }
                }
            }

            for (var n = 0; n < node.mNumChildren; ++n) {
                get_bounding_box_for_node(scene, node.mChildren[n], ref min, ref max, trafo, firstTime);
            }

            //trafo = prev;
        }

        bool LoadGLTextures(GL gl, aiScene scene) {
            if (scene.HasTextures()) { return true; }

            /* getTexture Filenames and Numb of Textures */
            for (var m = 0; m < scene.mNumMaterials; m++) {
                int texIndex = 0;
                aiReturn texFound = aiReturn.aiReturn_SUCCESS;

                string path = "";  // filename

                while (texFound == aiReturn.aiReturn_SUCCESS) {
                    texFound = scene.mMaterials[m].GetTexture(aiTextureType.aiTextureType_DIFFUSE, texIndex, out path);
                    if (texFound == aiReturn.aiReturn_SUCCESS) {
                        //textureIdMap[path.data] = null; //fill map with textures, pointers still NULL yet
                        textureIdMap.Add(path, 0);
                        texIndex++;
                    }
                }
            }

            var numTextures = textureIdMap.Count;

            /* create and fill array with GL texture ids */
            textureIds = new GLuint[numTextures];
            fixed (GLuint* p = textureIds) {
                gl.glGenTextures(numTextures, p); /* Texture name generation */
            }

            var fileInfo = new FileInfo(modelpath);
            var basepath = fileInfo.DirectoryName; Debug.Assert(basepath != null);
            /* get iterator */
            var index = 0;
            foreach (var pair in textureIdMap) {
                var filename = pair.Key;
                textureIdMap[filename] = textureIds[index];

                var fileloc = Path.Combine(basepath, filename); /* Loading of image */
                int x, y, n;
                //byte* data = stbi_load(fileloc.c_str(), &x, &y, &n, STBI_rgb_alpha);
                var bitmap = new Bitmap(fileloc);
                var winGLBitmap = new WinGLBitmap(bitmap, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                //if (null != data) {
                // Binding of texture name
                gl.glBindTexture(GL.GL_TEXTURE_2D, textureIds[index]);
                // redefine standard texture values
                // We will use linear interpolation for magnification filter
                gl.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, (int)GL.GL_LINEAR);
                // We will use linear interpolation for minifying filter
                gl.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, (int)GL.GL_LINEAR);
                // Texture specification
                gl.glTexImage2D(GL.GL_TEXTURE_2D, 0, (int)GL.GL_RGBA,
                    winGLBitmap.Width, winGLBitmap.Height,
                    0, GL.GL_BGRA, GL.GL_UNSIGNED_BYTE, winGLBitmap.Scan0);// Texture specification.

                // we also want to be able to deal with odd texture dimensions
                gl.glPixelStorei(GL.GL_UNPACK_ALIGNMENT, 1);
                gl.glPixelStorei(GL.GL_UNPACK_ROW_LENGTH, 0);
                gl.glPixelStorei(GL.GL_UNPACK_SKIP_PIXELS, 0);
                gl.glPixelStorei(GL.GL_UNPACK_SKIP_ROWS, 0);
                //stbi_image_free(data);
                winGLBitmap.Dispose();
                //}
                //else {
                //    /* Error occurred */
                //    //const string message = "Couldn't load Image: " + fileloc;
                //    //std::wstring targetMessage;
                //    //wchar_t* tmp = new wchar_t[message.size() + 1];
                //    //memset(tmp, L'\0', sizeof(wchar_t) * (message.size() + 1));
                //    //utf8::utf8to16(message.c_str(), message.c_str() + message.size(), tmp);
                //    //targetMessage = tmp;
                //    //delete[] tmp;
                //    //MessageBox(null, targetMessage.c_str(), TEXT("ERROR"), MB_OK | MB_ICONEXCLAMATION);
                //    MessageBox.Show("failed to load image.");
                //}
                index++;
            }

            return true;
        }
    }
}
