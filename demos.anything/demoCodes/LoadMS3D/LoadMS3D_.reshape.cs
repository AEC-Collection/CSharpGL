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
    internal unsafe partial class LoadMS3D_ : demoCode {

        public override void reshape(GL gl, int width, int height) {
            float aspectRatio = (float)width / (float)height, fieldOfView = 45.0f * (float)Math.PI / 180.0f;

            gl.glMatrixMode(GL.GL_PROJECTION);
            gl.glLoadIdentity();

            CSharpGL.mat4 projection = glm.perspective(fieldOfView, aspectRatio, 0.1f, 1000.0f);
            var array = (projection).ToArray();
            fixed (GLfloat* p = array) {
                gl.glMultMatrixf(p);
            }

            gl.glViewport(0, 0, width, height);
        }
    }
}
