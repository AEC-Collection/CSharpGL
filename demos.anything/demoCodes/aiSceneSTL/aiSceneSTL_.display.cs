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

namespace aiSceneSTL {
    internal unsafe partial class aiSceneSTL_ : demoCode {
        /* the global Assimp scene object */
        aiScene scene;
        uint /*GLuint*/ scene_list = 0;
        CSharpGL.vec3 scene_min, scene_max, scene_center;

        /* current rotation angle */
        static float angle = 0.0f;


        public aiSceneSTL_(FormInstance mainForm, WindowsGLCanvas canvas) : base(mainForm, canvas) { }

        public override void display(GL gl) {
            float tmp;

            gl.glClear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);

            gl.glMatrixMode(GL.GL_MODELVIEW);
            gl.glLoadIdentity();

            //gl.gluLookAt(0.f, 0.f, 3.f, 0.f, 0.f, -5.f, 0.f, 1.f, 0.f);
            var view = CSharpGL.glm.lookAt(new CSharpGL.vec3(0, 1, 0), new CSharpGL.vec3(0, 0, 0), new CSharpGL.vec3(1, 0, 0));
            var array = view.ToArray();
            fixed (float* p = array) { gl.glMultMatrixf(p); }

            //gl.glTranslatef(0.0f, 0.0f, 40.0f); // Move 40 Units And Into The Screen
            gl.glRotatef(90, 0.0f, 0.0f, 1.0f);
            gl.glRotatef(angle, 0.0f, 1.0f, 0.0f);
            gl.glRotatef(90, 1.0f, 0.0f, 0.0f);
            angle += 1.0f;


            /* scale the whole asset to fit into our view frustum */
            tmp = scene_max.x - scene_min.x;
            tmp = Math.Max(scene_max.y - scene_min.y, tmp);
            tmp = Math.Max(scene_max.z - scene_min.z, tmp);
            tmp = 1.0f / tmp;
            gl.glScalef(tmp, tmp, tmp);

            /* center the model */
            gl.glTranslatef(-scene_center.x, -scene_center.y, -scene_center.z);

            /* if the display list has not been made yet, create a new one and
               fill it with scene contents */
            if (scene_list == 0) {
                scene_list = gl.glGenLists(1);
                gl.glNewList(scene_list, GL.GL_COMPILE);
                /* now begin at the root node of the imported data and traverse
                   the scenegraph by multiplying subsequent local transforms
                   together on GL's matrix stack. */
                recursive_render(gl, scene, scene.mRootNode);
                gl.glEndList();
            }

            gl.glCallList(scene_list);

            //gl.glutSwapBuffers();

            //do_motion();

        }


        /* ---------------------------------------------------------------------------- */
        void apply_material(GL gl, aiMaterial material) {
            //float c[4];

            GLenum fill_mode;
            aiReturn ret1, ret2;
            Import3D.vec4 diffuse;
            Import3D.vec4 specular;
            Import3D.vec4 ambient;
            Import3D.vec4 emission;
            float shininess, strength;
            int two_sided;
            int wireframe;
            int max;

            //set_float4(c, 0.8f, 0.8f, 0.8f, 1.0f);
            if (aiReturn.aiReturn_SUCCESS != material.aiGetMaterialColor(/*AI_MATKEY_COLOR_DIFFUSE*/"$clr.diffuse", 0, 0, &diffuse)) {
                diffuse = new Import3D.vec4(0.8f, 0.8f, 0.8f, 1.0f);
            }
            gl.glMaterialfv(GL.GL_FRONT_AND_BACK, GL.GL_DIFFUSE, (float*)&diffuse);

            if (aiReturn.aiReturn_SUCCESS != material.aiGetMaterialColor(/*AI_MATKEY_COLOR_SPECULAR*/"$clr.specular", 0, 0, &specular)) {
                specular = new Import3D.vec4(0.0f, 0.0f, 0.0f, 1.0f);
            }
            gl.glMaterialfv(GL.GL_FRONT_AND_BACK, GL.GL_SPECULAR, (float*)&specular);

            //set_float4(c, 0.2f, 0.2f, 0.2f, 1.0f);
            if (aiReturn.aiReturn_SUCCESS != material.aiGetMaterialColor(/*AI_MATKEY_COLOR_AMBIENT*/"$clr.ambient", 0, 0, &ambient)) {
                ambient = new Import3D.vec4(0.2f, 0.2f, 0.2f, 1.0f);
            }
            //color4_to_float4(&ambient, c);
            gl.glMaterialfv(GL.GL_FRONT_AND_BACK, GL.GL_AMBIENT, (float*)&ambient);

            //set_float4(c, 0.0f, 0.0f, 0.0f, 1.0f);
            if (aiReturn.aiReturn_SUCCESS != material.aiGetMaterialColor(/*AI_MATKEY_COLOR_EMISSIVE*/"$clr.emissive", 0, 0, &emission)) {
                emission = new Import3D.vec4(0.0f, 0.0f, 0.0f, 1.0f);
            }
            //color4_to_float4(&emission, c);
            gl.glMaterialfv(GL.GL_FRONT_AND_BACK, GL.GL_EMISSION, (float*)&emission);

            max = 1;
            ret1 = material.aiGetMaterialFloatArray(/*AI_MATKEY_SHININESS*/"$mat.shininess", 0, 0, &shininess, &max);
            if (ret1 == aiReturn.aiReturn_SUCCESS) {
                max = 1;
                ret2 = material.aiGetMaterialFloatArray(/*AI_MATKEY_SHININESS_STRENGTH*/"$mat.shinpercent", 0, 0, &strength, &max);
                if (ret2 == aiReturn.aiReturn_SUCCESS)
                    gl.glMaterialf(GL.GL_FRONT_AND_BACK, GL.GL_SHININESS, shininess * strength);
                else
                    gl.glMaterialf(GL.GL_FRONT_AND_BACK, GL.GL_SHININESS, shininess);
            }
            else {
                gl.glMaterialf(GL.GL_FRONT_AND_BACK, GL.GL_SHININESS, 0.0f);
                //set_float4(c, 0.0f, 0.0f, 0.0f, 0.0f);
                var zero = new Import3D.vec4(0, 0, 0, 0);
                gl.glMaterialfv(GL.GL_FRONT_AND_BACK, GL.GL_SPECULAR, (float*)&zero);
            }

            max = 1;
            if (aiReturn.aiReturn_SUCCESS == material.aiGetMaterialIntegerArray(/*AI_MATKEY_ENABLE_WIREFRAME*/"$mat.wireframe", 0, 0, &wireframe, &max))
                fill_mode = wireframe != 0 ? GL.GL_LINE : GL.GL_FILL;
            else
                fill_mode = GL.GL_FILL;
            gl.glPolygonMode(GL.GL_FRONT_AND_BACK, fill_mode);

            max = 1;
            if ((aiReturn.aiReturn_SUCCESS == material.aiGetMaterialIntegerArray(/*AI_MATKEY_TWOSIDED*/"$mat.twosided", 0, 0, &two_sided, &max)) && two_sided != 0)
                gl.glDisable(GL.GL_CULL_FACE);
            else
                gl.glEnable(GL.GL_CULL_FACE);
        }

        /* ---------------------------------------------------------------------------- */
        void recursive_render(GL gl, aiScene scene, aiNode node) {
            var m = node.mTransformation;

            /* update transform */
            //aiTransposeMatrix4(&m);
            gl.glPushMatrix();
            gl.glMultMatrixf(m.values);

            /* draw all meshes assigned to this node */
            for (var n = 0; n < node.mNumMeshes; ++n) {
                var mesh = scene.mMeshes[node.mMeshes[n]];

                apply_material(gl, scene.mMaterials[mesh.mMaterialIndex]);

                if (mesh.mNormals == null) {
                    gl.glDisable(GL.GL_LIGHTING);
                }
                else {
                    gl.glEnable(GL.GL_LIGHTING);
                }

                for (var t = 0; t < mesh.mNumFaces; ++t) {
                    var face = mesh.mFaces[t];
                    GLenum face_mode;

                    switch (face.mNumIndices) {
                    case 1: face_mode = GL.GL_POINTS; break;
                    case 2: face_mode = GL.GL_LINES; break;
                    case 3: face_mode = GL.GL_TRIANGLES; break;
                    default: face_mode = GL.GL_POLYGON; break;
                    }

                    gl.glBegin(face_mode);

                    for (var i = 0; i < face.mNumIndices; i++) {
                        int index = face.mIndices[i];
                        if (mesh.mColors[0] != null) {
                            var color = mesh.mColors[0][index];
                            gl.glColor4fv((float*)&color);
                        }
                        if (mesh.mNormals != null) {
                            var normal = mesh.mNormals[index];
                            gl.glNormal3fv((float*)&normal);
                        }
                        {
                            var position = mesh.mVertices[index];
                            gl.glVertex3fv((float*)&position);
                        }
                    }

                    gl.glEnd();
                }
            }

            /* draw all children */
            for (var n = 0; n < node.mNumChildren; ++n) {
                recursive_render(gl, scene, node.mChildren[n]);
            }

            gl.glPopMatrix();
        }

    }
}
