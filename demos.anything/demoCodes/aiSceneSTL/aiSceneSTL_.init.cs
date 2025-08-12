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
using System.IO;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ConstrainedExecution;
using System.Drawing.Design;
using System.Numerics;

namespace aiSceneSTL {
    unsafe partial class aiSceneSTL_ {

        private const string model_file = "media/STL-models/Spider_ascii.stl";
        public override void init(GL gl) {

            // Load the model file.
            if (!loadasset(model_file)) {
                MessageBox.Show("Failed to load model. Please ensure that the specified file exists.");
                //aiDetachAllLogStreams();
                //return;// EXIT_FAILURE;
            }

            //gl.glClearColor(0.1f, 0.1f, 0.1f, 1.0f);

            gl.glEnable(GL.GL_LIGHTING);
            gl.glEnable(GL.GL_LIGHT0); /* Uses default lighting parameters */

            gl.glEnable(GL.GL_DEPTH_TEST);

            gl.glLightModeli(GL.GL_LIGHT_MODEL_TWO_SIDE, (int)GL.GL_TRUE);
            gl.glEnable(GL.GL_NORMALIZE);

            /* XXX docs say all polygons are emitted CCW, but tests show that some aren't. */
            //if (getenv("MODEL_IS_BROKEN"))
            gl.glFrontFace(GL.GL_CW);

            gl.glColorMaterial(GL.GL_FRONT_AND_BACK, GL.GL_DIFFUSE);


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
        /* ---------------------------------------------------------------------------- */
        bool loadasset(string path) {
            /* we are taking one of the postprocessing presets to avoid
               spelling out 20+ single postprocessing flags here. */
            var scene = new aiScene(name: path);
            Import3D.STL.STLImporter.InternReadFile(path, scene);

            CSharpGL.vec3 min = new CSharpGL.vec3(), max = new CSharpGL.vec3();
            get_bounding_box(scene, ref min, ref max);
            scene_min = min; scene_max = max;
            scene_center.x = (scene_min.x + scene_max.x) / 2.0f;
            scene_center.y = (scene_min.y + scene_max.y) / 2.0f;
            scene_center.z = (scene_min.z + scene_max.z) / 2.0f;
            this.scene = scene;
            return true;
        }


        /* ---------------------------------------------------------------------------- */
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
        /* ---------------------------------------------------------------------------- */
        void get_bounding_box(aiScene scene, ref CSharpGL.vec3 min, ref CSharpGL.vec3 max) {
            CSharpGL.mat4 trafo = CSharpGL.mat4.identity();

            get_bounding_box_for_node(scene, scene.mRootNode, ref min, ref max, trafo);
        }

    }
}
