using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using System.Xml.Linq;

namespace Import3D.FBX {
    /** Dummy class to encapsulate the conversion process */
    public unsafe class FBXConverter {
        public FBXConverter(aiScene outValue, FBXDocument doc, bool removeEmptyBones) {
            this.mSceneOut = outValue; this.doc = doc; this.mRemoveEmptyBones = removeEmptyBones;

            // animations need to be converted first since this will
            // populate the node_anim_chain_bits map, which is needed
            // to determine which nodes need to be generated.
            ConvertAnimations();
            // Embedded textures in FBX could be connected to nothing but to itself,
            // for instance Texture . Video connection only but not to the main graph,
            // The idea here is to traverse all objects to find these Textures and convert them,
            // so later during material conversion it will find converted texture in the textures_converted array.
            if (doc.Settings().readTextures) {
                ConvertOrphanedEmbeddedTextures();
            }
            ConvertRootNode();

            if (doc.Settings().readAllMaterials) {
                // unfortunately this means we have to evaluate all objects
                for (Dictionary < UInt64, FBXLazyObject >::value_type & v : doc.Objects()) {

                    FBXObject* ob = v.second.Get();
                    if (!ob) {
                        continue;
                    }

                    FBXMaterial* mat = dynamic_cast<FBXMaterial*>(ob);
                    if (mat) {

                        if (materials_converted.find(mat) == materials_converted.end()) {
                            ConvertMaterial(*mat, null);
                        }
                    }
                }
            }

            ConvertGlobalSettings();
            TransferDataToScene();

            // if we didn't read any meshes set the AI_SCENE_FLAGS_INCOMPLETE
            // to make sure the scene passes assimp's validation. FBX files
            // need not contain geometry (i.e. camera animations, raw armatures).
            if (outValue.mNumMeshes == 0) {
                outValue.mFlags |= AI_SCENE_FLAGS_INCOMPLETE;
            }
            else {
                // Apply the FBX axis metadata unless requested not to
                if (!doc.Settings().ignoreUpDirection)
                    correctRootTransform(mSceneOut);
            }

        }

        // ------------------------------------------------------------------------------------------------
        // find scene root and trigger recursive scene conversion
        void ConvertRootNode() {
            mSceneOut.mRootNode = new aiNode();
            string unique_name;
            GetUniqueName("RootNode", unique_name);
            mSceneOut.mRootNode.mName.Set(unique_name);

            // root has ID 0
            ConvertNodes(0L, mSceneOut.mRootNode, mSceneOut.mRootNode);

        }

        // ------------------------------------------------------------------------------------------------
        // collect and assign child nodes
        void ConvertNodes(UInt64 id, aiNode parent, aiNode root_node, mat4 parent_transform = new mat4()) {
            List<FBXConnection> & conns = doc.GetConnectionsByDestinationSequenced(id, "Model");

            List<PotentialNode> nodes;
            nodes.reserve(conns.Count);

            List<PotentialNode> nodes_chain;
            List<PotentialNode> post_nodes_chain;

            for (FBXConnection con : conns) {
                // ignore object-property links
                if (con.PropertyName().length()) {
                    // really important we document why this is ignored.
                    FBXImporter::LogInfo("ignoring property link - no docs on why this is ignored");
                    continue; //?
                }

                // convert connection source object into Object base class
                FBXObject * object = con.SourceObject();
                if (null == object) {
                    FBXImporter::LogError("failed to convert source object for Model link");
                    continue;
                }

                // FBX Model::Cube, Model::Bone001, etc elements
                // This detects if we can cast the object into this model structure.
                FBXModel* model = dynamic_cast<FBXModel*>(object);

                if (null != model) {
                    nodes_chain.clear();
                    post_nodes_chain.clear();
                    mat4 new_abs_transform = parent_transform;
                    string node_name = FixNodeName(model.Name());
                    // even though there is only a single input node, the design of
                    // assimp (or rather: the complicated transformation chain that
                    // is employed by fbx) means that we may need multiple aiNode's
                    // to represent a fbx node's transformation.

                    // generate node transforms - this includes pivot data
                    // if need_additional_node is true then you t
                    bool need_additional_node = GenerateTransformationNodeChain(*model, node_name, nodes_chain, post_nodes_chain);

                    // assert that for the current node we must have at least a single transform
                    System.Diagnostics.Debug.Assert(nodes_chain.Count);

                    if (need_additional_node) {
                        nodes_chain.emplace_back(node_name);
                    }

                    // setup metadata on newest node
                    SetupNodeMetadata(*model, *nodes_chain.back().mNode);

                    // link all nodes in a row
                    aiNode last_parent = parent;
                    for (PotentialNode & child : nodes_chain) {
                        System.Diagnostics.Debug.Assert(child.mNode);

                        if (last_parent != parent) {
                            last_parent.mNumChildren = 1;
                            last_parent.mChildren = new aiNode[1];
                            last_parent.mChildren[0] = child.mOwnership.release();
                        }

                        child.mParent = last_parent;
                        last_parent = child.mNode;

                        new_abs_transform *= child.mTransformation;
                    }

                    // attach geometry
                    ConvertModel(*model, nodes_chain.back().mNode, root_node, new_abs_transform);

                    // check if there will be any child nodes
                    List<FBXConnection> & child_conns = doc.GetConnectionsByDestinationSequenced(model.ID(), "Model");

                    // if so, link the geometric transform inverse nodes
                    // before we attach any child nodes
                    if (child_conns.Count) {
                        for (PotentialNode & postnode : post_nodes_chain) {
                            System.Diagnostics.Debug.Assert(postnode.mNode);

                            if (last_parent != parent) {
                                last_parent.mNumChildren = 1;
                                last_parent.mChildren = new aiNode[1];
                                last_parent.mChildren[0] = postnode.mOwnership.release();
                            }

                            postnode.mParent = last_parent;
                            last_parent = postnode.mNode;

                            new_abs_transform *= postnode.mTransformation;
                        }
                    }
                    else {
                        // free the nodes we allocated as we don't need them
                        post_nodes_chain.clear();
                    }

                    // recursion call - child nodes
                    ConvertNodes(model.ID(), last_parent, root_node, new_abs_transform);

                    if (doc.Settings().readLights) {
                        ConvertLights(*model, node_name);
                    }

                    if (doc.Settings().readCameras) {
                        ConvertCameras(*model, node_name);
                    }

                    nodes.push_back(std::move(nodes_chain.front()));
                    nodes_chain.clear();
                }
            }

            if (nodes.empty()) {
                parent.mNumChildren = 0;
                parent.mChildren = null;
            }
            else {
                parent.mChildren = new aiNode[nodes.Count]();
                parent.mNumChildren = static_cast<uint>(nodes.Count);
                for (uint i = 0; i < nodes.Count; ++i) {
                    parent.mChildren[i] = nodes[i].mOwnership.release();
                }
            }

        }

        // ------------------------------------------------------------------------------------------------
        void ConvertLights(FBXModel &model, string &orig_name) {
            List<FBXNodeAttribute*> & node_attrs = model.GetAttributes();
            for (FBXNodeAttribute* attr : node_attrs) {
                FBXLight* light = dynamic_cast<FBXLight*>(attr);
                if (light) {
                    ConvertLight(*light, orig_name);
                }
            }

        }

        // ------------------------------------------------------------------------------------------------
        void ConvertCameras(FBXModel &model, string &orig_name) {
            List<FBXNodeAttribute*> & node_attrs = model.GetAttributes();
            for (FBXNodeAttribute* attr : node_attrs) {
                FBXCamera* cam = dynamic_cast<FBXCamera*>(attr);
                if (cam) {
                    ConvertCamera(*cam, orig_name);
                }
            }

        }

        // ------------------------------------------------------------------------------------------------
        void ConvertLight(FBXLight &light, string &orig_name) {
            lights.push_back(new aiLight());
            aiLight* out_light = lights.back();

            out_light.mName.Set(orig_name);

            float intensity = light.Intensity() / 100.0f;
            vec3 & col = light.Color();

            out_light.mColorDiffuse = vec3(col.x, col.y, col.z);
            out_light.mColorDiffuse.r *= intensity;
            out_light.mColorDiffuse.g *= intensity;
            out_light.mColorDiffuse.b *= intensity;

            out_light.mColorSpecular = out_light.mColorDiffuse;

            // lights are defined along negative y direction
            out_light.mPosition = vec3(0.0f);
            out_light.mDirection = vec3(0.0f, -1.0f, 0.0f);
            out_light.mUp = vec3(0.0f, 0.0f, -1.0f);

            switch (light.LightType()) {
            case FBXLight::Type_Point:
            out_light.mType = aiLightSource_POINT;
            break;

            case FBXLight::Type_Directional:
            out_light.mType = aiLightSource_DIRECTIONAL;
            break;

            case FBXLight::Type_Spot:
            out_light.mType = aiLightSource_SPOT;
            out_light.mAngleOuterCone = AI_DEG_TO_RAD(light.OuterAngle());
            out_light.mAngleInnerCone = AI_DEG_TO_RAD(light.InnerAngle());
            break;

            case FBXLight::Type_Area:
            FBXImporter::LogWarn("cannot represent area light, set to UNDEFINED");
            out_light.mType = aiLightSource_UNDEFINED;
            break;

            case FBXLight::Type_Volume:
            FBXImporter::LogWarn("cannot represent volume light, set to UNDEFINED");
            out_light.mType = aiLightSource_UNDEFINED;
            break;
            default:
            FBXImporter::LogError("Not handled light type: ", light.LightType());
            break;
            }

            float decay = light.DecayStart();
            switch (light.DecayType()) {
            case FBXLight::Decay_None:
            out_light.mAttenuationConstant = decay;
            out_light.mAttenuationLinear = 0.0f;
            out_light.mAttenuationQuadratic = 0.0f;
            break;
            case FBXLight::Decay_Linear:
            out_light.mAttenuationConstant = 0.0f;
            out_light.mAttenuationLinear = 2.0f / decay;
            out_light.mAttenuationQuadratic = 0.0f;
            break;
            case FBXLight::Decay_Quadratic:
            out_light.mAttenuationConstant = 0.0f;
            out_light.mAttenuationLinear = 0.0f;
            out_light.mAttenuationQuadratic = 2.0f / (decay * decay);
            break;
            case FBXLight::Decay_Cubic:
            FBXImporter::LogWarn("cannot represent cubic attenuation, set to Quadratic");
            out_light.mAttenuationQuadratic = 1.0f;
            break;
            default:
            FBXImporter::LogError("Not handled light decay type: ", light.DecayType());
            break;
            }

        }

        // ------------------------------------------------------------------------------------------------
        void ConvertCamera(FBXCamera &cam, string &orig_name) {
            cameras.push_back(new aiCamera());
            aiCamera* out_camera = cameras.back();

            out_camera.mName.Set(orig_name);

            out_camera.mAspect = cam.AspectWidth() / cam.AspectHeight();

            // NOTE: Camera mPosition, mLookAt and mUp must be set to default here.
            // All transformations to the camera will be handled by its node in the scenegraph.
            out_camera.mPosition = vec3(0.0f);
            out_camera.mLookAt = vec3(1.0f, 0.0f, 0.0f);
            out_camera.mUp = vec3(0.0f, 1.0f, 0.0f);

            // NOTE: Some software (maya) does not put FieldOfView in FBX, so we compute
            // mHorizontalFOV from FocalLength and FilmWidth with unit conversion.

            // TODO: This is not a complete solution for how FBX cameras can be stored.
            // TODO: Incorporate non-square pixel aspect ratio.
            // TODO: FBX aperture mode might be storing vertical FOV in need of conversion with aspect ratio.

            float fov_deg = cam.FieldOfView();
            // If FOV not specified in file, compute using FilmWidth and FocalLength.
            if (fov_deg == kFovUnknown) {
                float film_width_inches = cam.FilmWidth();
                float focal_length_mm = cam.FocalLength();
                ASSIMP_LOG_VERBOSE_DEBUG("FBX FOV unspecified. Computing from FilmWidth (", film_width_inches, "inches) and FocalLength (", focal_length_mm, "mm).");
                double half_fov_rad = std::atan2(film_width_inches * 25.4 * 0.5, focal_length_mm);
                out_camera.mHorizontalFOV = static_cast<float>(half_fov_rad);
            }
            else {
                // FBX fov is full-view degrees. We want half-view radians.
                out_camera.mHorizontalFOV = AI_DEG_TO_RAD(fov_deg) * 0.5f;
            }

            out_camera.mClipPlaneNear = cam.NearPlane();
            out_camera.mClipPlaneFar = cam.FarPlane();

        }

        // ------------------------------------------------------------------------------------------------
        void GetUniqueName(string &name, string &uniqueName) {
            uniqueName = name;
            auto it_pair = mNodeNames.insert({ name, 0 }); // duplicate node name instance count
            uint &i = it_pair.first.second;
            while (!it_pair.second) {
                ++i;
                std::ostringstream ext;
                ext << name << std::setfill('0') << std::setw(3) << i;
                uniqueName = ext.str();
                it_pair = mNodeNames.insert({ uniqueName, 0 });
            }

        }

        // ------------------------------------------------------------------------------------------------
        // this returns unified names usable within assimp identifiers (i.e. no space characters -
        // while these would be allowed, they are a potential trouble spot so better not use them).
        char* NameTransformationComp(TransformationComp comp) {
            switch (comp) {
            case TransformationComp_Translation:
            return "Translation";
            case TransformationComp_RotationOffset:
            return "RotationOffset";
            case TransformationComp_RotationPivot:
            return "RotationPivot";
            case TransformationComp_PreRotation:
            return "PreRotation";
            case TransformationComp_Rotation:
            return "Rotation";
            case TransformationComp_PostRotation:
            return "PostRotation";
            case TransformationComp_RotationPivotInverse:
            return "RotationPivotInverse";
            case TransformationComp_ScalingOffset:
            return "ScalingOffset";
            case TransformationComp_ScalingPivot:
            return "ScalingPivot";
            case TransformationComp_Scaling:
            return "Scaling";
            case TransformationComp_ScalingPivotInverse:
            return "ScalingPivotInverse";
            case TransformationComp_GeometricScaling:
            return "GeometricScaling";
            case TransformationComp_GeometricRotation:
            return "GeometricRotation";
            case TransformationComp_GeometricTranslation:
            return "GeometricTranslation";
            case TransformationComp_GeometricScalingInverse:
            return "GeometricScalingInverse";
            case TransformationComp_GeometricRotationInverse:
            return "GeometricRotationInverse";
            case TransformationComp_GeometricTranslationInverse:
            return "GeometricTranslationInverse";
            case TransformationComp_MAXIMUM: // this is to silence compiler warnings
            default:
            break;
            }

            System.Diagnostics.Debug.Assert(false);

            return null;

        }

        // ------------------------------------------------------------------------------------------------
        // Returns an unique name for a node or traverses up a hierarchy until a non-empty name is found and
        // then makes this name unique
        string MakeUniqueNodeName(FBXModel* model, aiNode &parent) {
            string original_name = FixNodeName(model.Name());
            if (original_name.empty()) {
                original_name = getAncestorBaseName(&parent);
            }
            string unique_name;
            GetUniqueName(original_name, unique_name);
            return unique_name;

        }
        /// This struct manages nodes which may or may not end up in the node hierarchy.
        /// When a node becomes a child of another node, that node becomes its owner and mOwnership should be released.
        public struct PotentialNode {
            public PotentialNode() {
                this.mOwnership = new aiNode("");
                this.mNode = this.mOwnership.get();
            }
            public PotentialNode(string name) {
                this.mOwnership = new aiNode(name);
                this.mNode = this.mOwnership.get();
            }
            //aiNode operator.() { return mNode; }
            aiNode mOwnership;
            aiNode mNode;
        }

        // ------------------------------------------------------------------------------------------------
        // note: this returns the REAL fbx property names
        char* NameTransformationCompProperty(TransformationComp comp) {
            switch (comp) {
            case TransformationComp_Translation:
            return "Lcl Translation";
            case TransformationComp_RotationOffset:
            return "RotationOffset";
            case TransformationComp_RotationPivot:
            return "RotationPivot";
            case TransformationComp_PreRotation:
            return "PreRotation";
            case TransformationComp_Rotation:
            return "Lcl Rotation";
            case TransformationComp_PostRotation:
            return "PostRotation";
            case TransformationComp_RotationPivotInverse:
            return "RotationPivotInverse";
            case TransformationComp_ScalingOffset:
            return "ScalingOffset";
            case TransformationComp_ScalingPivot:
            return "ScalingPivot";
            case TransformationComp_Scaling:
            return "Lcl Scaling";
            case TransformationComp_ScalingPivotInverse:
            return "ScalingPivotInverse";
            case TransformationComp_GeometricScaling:
            return "GeometricScaling";
            case TransformationComp_GeometricRotation:
            return "GeometricRotation";
            case TransformationComp_GeometricTranslation:
            return "GeometricTranslation";
            case TransformationComp_GeometricScalingInverse:
            return "GeometricScalingInverse";
            case TransformationComp_GeometricRotationInverse:
            return "GeometricRotationInverse";
            case TransformationComp_GeometricTranslationInverse:
            return "GeometricTranslationInverse";
            case TransformationComp_MAXIMUM:
            break;
            }

            System.Diagnostics.Debug.Assert(false);

            return null;

        }

        // ------------------------------------------------------------------------------------------------
        vec3 TransformationCompDefaultValue(TransformationComp comp) {
            // XXX a neat way to solve the never-ending special cases for scaling
            // would be to do everything in log space!
            return comp == TransformationComp_Scaling ? vec3(1.f, 1.f, 1.f) : vec3();

        }

        // ------------------------------------------------------------------------------------------------
        void GetRotationMatrix(FBXModel.RotOrder mode, vec3 &rotation, mat4 & outValue) {
            if (mode == FBXModel::RotOrder_SphericXYZ) {
                FBXImporter::LogError("Unsupported RotationMode: SphericXYZ");
                outValue = mat4();
                return;
            }

            float angle_epsilon = Math::getEpsilon<float>();

            outValue = mat4();

            bool is_id[3] = { true, true, true };

            mat4 temp[3];
            auto rot = AI_DEG_TO_RAD(rotation);
            if (std::fabs(rot.z) > angle_epsilon) {
                mat4::RotationZ(rot.z, temp[2]);
                is_id[2] = false;
            }
            if (std::fabs(rot.y) > angle_epsilon) {
                mat4::RotationY(rot.y, temp[1]);
                is_id[1] = false;
            }
            if (std::fabs(rot.x) > angle_epsilon) {
                mat4::RotationX(rot.x, temp[0]);
                is_id[0] = false;
            }

            int order[3] = { -1, -1, -1 };

            // note: rotation order is inverted since we're left multiplying as is usual in assimp
            switch (mode) {
            case FBXModel::RotOrder_EulerXYZ:
            order[0] = 2;
            order[1] = 1;
            order[2] = 0;
            break;

            case FBXModel::RotOrder_EulerXZY:
            order[0] = 1;
            order[1] = 2;
            order[2] = 0;
            break;

            case FBXModel::RotOrder_EulerYZX:
            order[0] = 0;
            order[1] = 2;
            order[2] = 1;
            break;

            case FBXModel::RotOrder_EulerYXZ:
            order[0] = 2;
            order[1] = 0;
            order[2] = 1;
            break;

            case FBXModel::RotOrder_EulerZXY:
            order[0] = 1;
            order[1] = 0;
            order[2] = 2;
            break;

            case FBXModel::RotOrder_EulerZYX:
            order[0] = 0;
            order[1] = 1;
            order[2] = 2;
            break;

            default:
            System.Diagnostics.Debug.Assert(false);
            break;
            }

            System.Diagnostics.Debug.Assert(order[0] >= 0);
            System.Diagnostics.Debug.Assert(order[0] <= 2);
            System.Diagnostics.Debug.Assert(order[1] >= 0);
            System.Diagnostics.Debug.Assert(order[1] <= 2);
            System.Diagnostics.Debug.Assert(order[2] >= 0);
            System.Diagnostics.Debug.Assert(order[2] <= 2);

            if (!is_id[order[0]]) {
                outValue = temp[order[0]];
            }

            if (!is_id[order[1]]) {
                outValue = outValue * temp[order[1]];
            }

            if (!is_id[order[2]]) {
                outValue = outValue * temp[order[2]];
            }

        }
        // ------------------------------------------------------------------------------------------------
        /**
         *  checks if a node has more than just scaling, rotation and translation components
         */
        bool NeedsComplexTransformationChain(FBXModel &model) {
            PropertyTable & props = model.Props();

            auto zero_epsilon = Math::getEpsilon<ai_real>();
            vec3 all_ones(1.0f, 1.0f, 1.0f);
            for (int i = 0; i < TransformationComp_MAXIMUM; ++i) {
                TransformationComp comp = static_cast<TransformationComp>(i);

                if (comp == TransformationComp_Rotation || comp == TransformationComp_Scaling || comp == TransformationComp_Translation) {
                    continue;
                }

                bool scale_compare = (comp == TransformationComp_GeometricScaling || comp == TransformationComp_Scaling);

                bool ok = true;
                vec3 & v = PropertyGet<vec3>(props, NameTransformationCompProperty(comp), ok);
                if (ok && scale_compare) {
                    if ((v - all_ones).SquareLength() > zero_epsilon) {
                        return true;
                    }
                }
                else if (ok) {
                    if (v.SquareLength() > zero_epsilon) {
                        return true;
                    }
                }
            }

            return false;

        }

        // ------------------------------------------------------------------------------------------------
        // note: name must be a FixNodeName() result
        string NameTransformationChainNode(string &name, TransformationComp comp) {
            return name + string(MAGIC_NODE_TAG) + "_" + NameTransformationComp(comp);

        }

        // ------------------------------------------------------------------------------------------------
        /**
         *  note: memory for output_nodes is managed by the caller, via the PotentialNode struct.
         */
        struct PotentialNode;
        bool GenerateTransformationNodeChain(FBXModel &model, string &name, List<PotentialNode> &output_nodes, List<PotentialNode> &post_output_nodes) {
            PropertyTable & props = model.Props();
            FBXModel.RotOrder rot = model.RotationOrder();

            bool ok;

            mat4 chain[TransformationComp_MAXIMUM];

            System.Diagnostics.Debug.Assert(TransformationComp_MAXIMUM < 32);
            std::UInt32 chainBits = 0;
            // A node won't need a node chain if it only has these.
            std::UInt32 chainMaskSimple = (1 << TransformationComp_Translation) + (1 << TransformationComp_Scaling) + (1 << TransformationComp_Rotation);
            // A node will need a node chain if it has any of these.
            std::UInt32 chainMaskComplex = ((1 << (TransformationComp_MAXIMUM)) - 1) - chainMaskSimple;

            std::fill_n(chain, static_cast<uint>(TransformationComp_MAXIMUM), mat4());

            // generate transformation matrices for all the different transformation components
            float zero_epsilon = Math::getEpsilon<float>();
            vec3 all_ones(1.0f, 1.0f, 1.0f);

            vec3 & PreRotation = PropertyGet<vec3>(props, "PreRotation", ok);
            if (ok && PreRotation.SquareLength() > zero_epsilon) {
                chainBits = chainBits | (1 << TransformationComp_PreRotation);

                GetRotationMatrix(FBXModel.RotOrder::RotOrder_EulerXYZ, PreRotation, chain[TransformationComp_PreRotation]);
            }

            vec3 & PostRotation = PropertyGet<vec3>(props, "PostRotation", ok);
            if (ok && PostRotation.SquareLength() > zero_epsilon) {
                chainBits = chainBits | (1 << TransformationComp_PostRotation);

                GetRotationMatrix(FBXModel.RotOrder::RotOrder_EulerXYZ, PostRotation, chain[TransformationComp_PostRotation]);
            }

            vec3 & RotationPivot = PropertyGet<vec3>(props, "RotationPivot", ok);
            if (ok && RotationPivot.SquareLength() > zero_epsilon) {
                chainBits = chainBits | (1 << TransformationComp_RotationPivot) | (1 << TransformationComp_RotationPivotInverse);

                mat4::Translation(RotationPivot, chain[TransformationComp_RotationPivot]);
                mat4::Translation(-RotationPivot, chain[TransformationComp_RotationPivotInverse]);
            }

            vec3 & RotationOffset = PropertyGet<vec3>(props, "RotationOffset", ok);
            if (ok && RotationOffset.SquareLength() > zero_epsilon) {
                chainBits = chainBits | (1 << TransformationComp_RotationOffset);

                mat4::Translation(RotationOffset, chain[TransformationComp_RotationOffset]);
            }

            vec3 & ScalingOffset = PropertyGet<vec3>(props, "ScalingOffset", ok);
            if (ok && ScalingOffset.SquareLength() > zero_epsilon) {
                chainBits = chainBits | (1 << TransformationComp_ScalingOffset);

                mat4::Translation(ScalingOffset, chain[TransformationComp_ScalingOffset]);
            }

            vec3 & ScalingPivot = PropertyGet<vec3>(props, "ScalingPivot", ok);
            if (ok && ScalingPivot.SquareLength() > zero_epsilon) {
                chainBits = chainBits | (1 << TransformationComp_ScalingPivot) | (1 << TransformationComp_ScalingPivotInverse);

                mat4::Translation(ScalingPivot, chain[TransformationComp_ScalingPivot]);
                mat4::Translation(-ScalingPivot, chain[TransformationComp_ScalingPivotInverse]);
            }

            vec3 & Translation = PropertyGet<vec3>(props, "Lcl Translation", ok);
            if (ok && Translation.SquareLength() > zero_epsilon) {
                chainBits = chainBits | (1 << TransformationComp_Translation);

                mat4::Translation(Translation, chain[TransformationComp_Translation]);
            }

            vec3 & Scaling = PropertyGet<vec3>(props, "Lcl Scaling", ok);
            if (ok && (Scaling - all_ones).SquareLength() > zero_epsilon) {
                chainBits = chainBits | (1 << TransformationComp_Scaling);

                mat4::Scaling(Scaling, chain[TransformationComp_Scaling]);
            }

            vec3 & Rotation = PropertyGet<vec3>(props, "Lcl Rotation", ok);
            if (ok && Rotation.SquareLength() > zero_epsilon) {
                chainBits = chainBits | (1 << TransformationComp_Rotation);

                GetRotationMatrix(rot, Rotation, chain[TransformationComp_Rotation]);
            }

            vec3 & GeometricScaling = PropertyGet<vec3>(props, "GeometricScaling", ok);
            if (ok && (GeometricScaling - all_ones).SquareLength() > zero_epsilon) {
                chainBits = chainBits | (1 << TransformationComp_GeometricScaling);
                mat4::Scaling(GeometricScaling, chain[TransformationComp_GeometricScaling]);
                vec3 GeometricScalingInverse = GeometricScaling;
                bool canscale = true;
                for (uint i = 0; i < 3; ++i) {
                    if (std::fabs(GeometricScalingInverse[i]) > zero_epsilon) {
                        GeometricScalingInverse[i] = 1.0f / GeometricScaling[i];
                    }
                    else {
                        FBXImporter::LogError("cannot invert geometric scaling matrix with a 0.0 scale component");
                        canscale = false;
                        break;
                    }
                }
                if (canscale) {
                    chainBits = chainBits | (1 << TransformationComp_GeometricScalingInverse);
                    mat4::Scaling(GeometricScalingInverse, chain[TransformationComp_GeometricScalingInverse]);
                }
            }

            vec3 & GeometricRotation = PropertyGet<vec3>(props, "GeometricRotation", ok);
            if (ok && GeometricRotation.SquareLength() > zero_epsilon) {
                chainBits = chainBits | (1 << TransformationComp_GeometricRotation) | (1 << TransformationComp_GeometricRotationInverse);
                GetRotationMatrix(rot, GeometricRotation, chain[TransformationComp_GeometricRotation]);
                GetRotationMatrix(rot, GeometricRotation, chain[TransformationComp_GeometricRotationInverse]);
                chain[TransformationComp_GeometricRotationInverse].Inverse();
            }

            vec3 & GeometricTranslation = PropertyGet<vec3>(props, "GeometricTranslation", ok);
            if (ok && GeometricTranslation.SquareLength() > zero_epsilon) {
                chainBits = chainBits | (1 << TransformationComp_GeometricTranslation) | (1 << TransformationComp_GeometricTranslationInverse);
                mat4::Translation(GeometricTranslation, chain[TransformationComp_GeometricTranslation]);
                mat4::Translation(-GeometricTranslation, chain[TransformationComp_GeometricTranslationInverse]);
            }

            // now, if we have more than just Translation, Scaling and Rotation,
            // we need to generate a full node chain to accommodate for assimp's
            // lack to express pivots and offsets.
            if ((chainBits & chainMaskComplex) && doc.Settings().preservePivots) {
                FBXImporter::LogInfo("generating full transformation chain for node: ", name);

                // query the anim_chain_bits dictionary to find out which chain elements
                // have associated node animation channels. These can not be dropped
                // even if they have identity transform in bind pose.
                Dictionary<string, uint>::const_iterator it = node_anim_chain_bits.find(name);
                uint anim_chain_bitmask = (it == node_anim_chain_bits.end() ? 0 : (*it).second);

                uint bit = 0x1;
                for (int i = 0; i < TransformationComp_MAXIMUM; ++i, bit <<= 1) {
                    TransformationComp comp = static_cast<TransformationComp>(i);

                    if ((chainBits & bit) == 0 && (anim_chain_bitmask & bit) == 0) {
                        continue;
                    }

                    if (comp == TransformationComp_PostRotation) {
                        chain[i] = chain[i].Inverse();
                    }

                    PotentialNode nd;
                    nd.mName.Set(NameTransformationChainNode(name, comp));
                    nd.mTransformation = chain[i];

                    // geometric inverses go in a post-node chain
                    if (comp == TransformationComp_GeometricScalingInverse ||
                            comp == TransformationComp_GeometricRotationInverse ||
                            comp == TransformationComp_GeometricTranslationInverse) {
                        post_output_nodes.emplace_back(std::move(nd));
                    }
                    else {
                        output_nodes.emplace_back(std::move(nd));
                    }
                }

                System.Diagnostics.Debug.Assert(output_nodes.Count);
                return true;
            }

            // else, we can just multiply the matrices together
            PotentialNode nd;

            // name passed to the method is already unique
            nd.mName.Set(name);
            // for ( auto &transform : chain) {
            // skip inverse chain for no preservePivots
            for (uint i = TransformationComp_Translation; i < TransformationComp_MAXIMUM; i++) {
                nd.mTransformation = nd.mTransformation * chain[i];
            }
            output_nodes.push_back(std::move(nd));
            return false;

        }

        // ------------------------------------------------------------------------------------------------
        void SetupNodeMetadata(FBXModel model, aiNode nd) {
            PropertyTable props = model.Props();
            Dictionary<string, FBXProperty> unparsedProperties = props.GetUnparsedProperties();

            // create metadata on node
            int numStaticMetaData = 2;
            aiMetadata data = aiMetadata.Alloc((uint)(unparsedProperties.Count + numStaticMetaData));
            nd.mMetaData = data;
            int index = 0;

            // find user defined properties (3ds Max)
            data.Set(index++, "UserProperties", string(PropertyGet<string>(props, "UDP3DSMAX", "")));
            // preserve the info that a node was marked as Null node in the original file.
            data.Set(index++, "IsNull", model.IsNull() ? true : false);

            // add unparsed properties to the node's metadata
            foreach (var prop in unparsedProperties) {
                // Interpret the property as a concrete type
                if (TypedProperty<bool> interpretedBool = prop.second.As<TypedProperty<bool>>()) {
                    data.Set(index++, prop.first, interpretedBool.Value());
                } else if (TypedProperty<int> interpretedInt = prop.second.As<TypedProperty<int>>()) {
                    data.Set(index++, prop.first, interpretedInt.Value());
                } else if (TypedProperty < UInt32 > interpretedUInt = prop.second.As<TypedProperty<UInt32>>()) {
                    data.Set(index++, prop.first, interpretedUInt.Value());
                }
                else if (TypedProperty < UInt64 > interpretedUint64 = prop.second.As<TypedProperty<UInt64>>()) {
                    data.Set(index++, prop.first, interpretedUint64.Value());
                }
                else if (TypedProperty < Int64 > interpretedint64 = prop.second.As<TypedProperty<Int64>>()) {
                    data.Set(index++, prop.first, interpretedint64.Value());
                }
                else if (TypedProperty<float> interpretedFloat = prop.second.As<TypedProperty<float>>()) {
                    data.Set(index++, prop.first, interpretedFloat.Value());
                } else if (TypedProperty<string> interpretedString = prop.second.As<TypedProperty<string>>()) {
                    data.Set(index++, prop.first, string(interpretedString.Value()));
                } else if (TypedProperty < vec3 > interpretedVec3 = prop.second.As<TypedProperty<vec3>>()) {
                    data.Set(index++, prop.first, interpretedVec3.Value());
                }
                else {
                    System.Diagnostics.Debug.Assert(false);
                }
            }


        }

        // ------------------------------------------------------------------------------------------------
        void ConvertModel(FBXModel &model, aiNode parent, aiNode root_node, mat4 &absolute_transform) {
            List<Geometry*> & geos = model.GetGeometry();

            List<uint> meshes;
            meshes.reserve(geos.Count);

            for (Geometry* geo : geos) {
                FBXMeshGeometry* mesh = dynamic_cast<FBXMeshGeometry*>(geo);
                LineGeometry* line = dynamic_cast<LineGeometry*>(geo);
                if (mesh) {
                    List<uint> & indices = ConvertMesh(*mesh, model, parent, root_node, absolute_transform);
                    std::copy(indices.begin(), indices.end(), std::back_inserter(meshes));
                }
                else if (line) {
                    List<uint> & indices = ConvertLine(*line, root_node);
                    std::copy(indices.begin(), indices.end(), std::back_inserter(meshes));
                }
                else if (geo) {
                    FBXImporter::LogWarn("ignoring unrecognized geometry: ", geo.Name());
                }
                else {
                    FBXImporter::LogWarn("skipping null geometry");
                }
            }

            if (meshes.Count) {
                parent.mMeshes = new uint[meshes.Count]();
                parent.mNumMeshes = static_cast<uint>(meshes.Count);

                std::swap_ranges(meshes.begin(), meshes.end(), parent.mMeshes);
            }

        }

        // ------------------------------------------------------------------------------------------------
        // FBXMeshGeometry . aiMesh, return mesh index + 1 or 0 if the conversion failed
        List<uint>
        ConvertMesh(FBXMeshGeometry &mesh, FBXModel &model, aiNode parent, aiNode root_node, mat4 &absolute_transform) {
            List<uint> temp;

            Dictionary<Geometry, List<uint>>.const_iterator it = meshes_converted.find(&mesh);
            if (it != meshes_converted.end()) {
                std.copy((*it).second.begin(), (*it).second.end(), std.back_inserter(temp));
                return temp;
            }

            List<vec3> & vertices = mesh.GetVertices();
            List<uint> & faces = mesh.GetFaceIndexCounts();
            if (vertices.empty() || faces.empty()) {
                FBXImporter.LogWarn("ignoring empty geometry: ", mesh.Name());
                return temp;
            }

            // one material per mesh maps easily to aiMesh. Multiple material
            // meshes need to be split.
            List<int> & mindices = mesh.GetMaterialIndices();
            if (doc.Settings().readMaterials && !mindices.empty()) {
                List<int>.value_type base = mindices[0];
                for (List<int>.value_type index : mindices) {
                    if (index != base) {
                        return ConvertMeshMultiMaterial(mesh, model, absolute_transform, parent, root_node);
                    }
                }
            }

            // faster code-path, just copy the data
            temp.push_back(ConvertMeshSingleMaterial(mesh, model, absolute_transform, parent, root_node));
            return temp;

        }

        // ------------------------------------------------------------------------------------------------
        List<uint> ConvertLine(LineGeometry &line, aiNode root_node) {
            List<uint> temp;

            List<vec3> & vertices = line.GetVertices();
            List<int> & indices = line.GetIndices();
            if (vertices.empty() || indices.empty()) {
                FBXImporter.LogWarn("ignoring empty line: ", line.Name());
                return temp;
            }

            aiMesh* out_mesh = SetupEmptyMesh(line, root_node);
            out_mesh.mPrimitiveTypes |= aiPrimitiveType_LINE;

            // copy vertices
            out_mesh.mNumVertices = static_cast<uint>(vertices.Count);
            out_mesh.mVertices = new vec3[out_mesh.mNumVertices];
            std.copy(vertices.begin(), vertices.end(), out_mesh.mVertices);

            // Number of line segments (faces) is "Number of Points - Number of Endpoints"
            // N.B.: Endpoints in FbxLine are denoted by negative indices.
            // If such an Index is encountered, add 1 and multiply by -1 to get the real index.
            uint epcount = 0;
            for (unsigned i = 0; i < indices.Count; i++) {
                if (indices[i] < 0) {
                    epcount++;
                }
            }
            uint pcount = static_cast<uint>(indices.Count);
            uint scount = out_mesh.mNumFaces = pcount - epcount;

            aiFace* fac = out_mesh.mFaces = new aiFace[scount]();
            for (uint i = 0; i < pcount; ++i) {
                if (indices[i] < 0) continue;
                aiFace & f = *fac++;
                f.mNumIndices = 2; // 2 == aiPrimitiveType_LINE
                f.mIndices = new uint[2];
                f.mIndices[0] = indices[i];
                int segid = indices[(i + 1 == pcount ? 0 : i + 1)]; // If we have reached he last point, wrap around
                f.mIndices[1] = (segid < 0 ? (segid + 1) * -1 : segid); // Convert EndPoint Index to normal Index
            }
            temp.push_back(static_cast<uint>(mMeshes.Count - 1));
            return temp;

        }

        // ------------------------------------------------------------------------------------------------
        aiMesh* SetupEmptyMesh(Geometry &mesh, aiNode parent) {
            aiMesh* out_mesh = new aiMesh();
            mMeshes.push_back(out_mesh);
            meshes_converted[&mesh].push_back(static_cast<uint>(mMeshes.Count - 1));

            // set name
            string name = mesh.Name();
            if (name.substr(0, 10) == "Geometry.") {
                name = name.substr(10);
            }

            if (name.length()) {
                out_mesh.mName.Set(name);
            }
            else {
                out_mesh.mName = parent.mName;
            }

            return out_mesh;

        }

        // ------------------------------------------------------------------------------------------------
        uint ConvertMeshSingleMaterial(FBXMeshGeometry &mesh, FBXModel &model, mat4 &absolute_transform,
                aiNode parent, aiNode root_node) {
            List<int> & mindices = mesh.GetMaterialIndices();
            aiMesh* out_mesh = SetupEmptyMesh(mesh, parent);

            List<vec3> & vertices = mesh.GetVertices();
            List<uint> & faces = mesh.GetFaceIndexCounts();

            // copy vertices
            out_mesh.mNumVertices = static_cast<uint>(vertices.Count);
            out_mesh.mVertices = new vec3[vertices.Count];

            std.copy(vertices.begin(), vertices.end(), out_mesh.mVertices);

            // generate dummy faces
            out_mesh.mNumFaces = static_cast<uint>(faces.Count);
            aiFace* fac = out_mesh.mFaces = new aiFace[faces.Count]();

            uint cursor = 0;
            for (uint pcount : faces) {
                aiFace & f = *fac++;
                f.mNumIndices = pcount;
                f.mIndices = new uint[pcount];
                switch (pcount) {
                case 1:
                out_mesh.mPrimitiveTypes |= aiPrimitiveType_POINT;
                break;
                case 2:
                out_mesh.mPrimitiveTypes |= aiPrimitiveType_LINE;
                break;
                case 3:
                out_mesh.mPrimitiveTypes |= aiPrimitiveType_TRIANGLE;
                break;
                default:
                out_mesh.mPrimitiveTypes |= aiPrimitiveType_POLYGON;
                break;
                }
                for (uint i = 0; i < pcount; ++i) {
                    f.mIndices[i] = cursor++;
                }
            }

            // copy normals
            List<vec3> & normals = mesh.GetNormals();
            if (normals.Count) {
                System.Diagnostics.Debug.Assert(normals.Count == vertices.Count);

                out_mesh.mNormals = new vec3[vertices.Count];
                std.copy(normals.begin(), normals.end(), out_mesh.mNormals);
            }

            // copy tangents - assimp requires both tangents and bitangents (binormals)
            // to be present, or neither of them. Compute binormals from normals
            // and tangents if needed.
            List<vec3> & tangents = mesh.GetTangents();
            List<vec3>* binormals = &mesh.GetBinormals();

            if (tangents.Count) {
                List<vec3> tempBinormals;
                if (!binormals.Count) {
                    if (normals.Count) {
                        tempBinormals.resize(normals.Count);
                        for (uint i = 0; i < tangents.Count; ++i) {
                            tempBinormals[i] = normals[i] ^ tangents[i];
                        }

                        binormals = &tempBinormals;
                    }
                    else {
                        binormals = null;
                    }
                }

                if (binormals) {
                    System.Diagnostics.Debug.Assert(tangents.Count == vertices.Count);
                    System.Diagnostics.Debug.Assert(binormals.Count == vertices.Count);

                    out_mesh.mTangents = new vec3[vertices.Count];
                    std.copy(tangents.begin(), tangents.end(), out_mesh.mTangents);

                    out_mesh.mBitangents = new vec3[vertices.Count];
                    std.copy(binormals.begin(), binormals.end(), out_mesh.mBitangents);
                }
            }

            // copy texture coords
            for (uint i = 0; i < AI_MAX_NUMBER_OF_TEXTURECOORDS; ++i) {
                List<vec2> & uvs = mesh.GetTextureCoords(i);
                if (uvs.empty()) {
                    break;
                }

                vec3* out_uv = out_mesh.mTextureCoords[i] = new vec3[vertices.Count];
                for (vec2 & v : uvs) {
                    *out_uv++ = vec3(v.x, v.y, 0.0f);
                }

                out_mesh.SetTextureCoordsName(i, string(mesh.GetTextureCoordChannelName(i)));

                out_mesh.mNumUVComponents[i] = 2;
            }

            // copy vertex colors
            for (uint i = 0; i < AI_MAX_NUMBER_OF_COLOR_SETS; ++i) {
                List<vec4> & colors = mesh.GetVertexColors(i);
                if (colors.empty()) {
                    break;
                }

                out_mesh.mColors[i] = new vec4[vertices.Count];
                std.copy(colors.begin(), colors.end(), out_mesh.mColors[i]);
            }

            if (!doc.Settings().readMaterials || mindices.empty()) {
                FBXImporter.LogError("no material assigned to mesh, setting default material");
                out_mesh.mMaterialIndex = GetDefaultMaterial();
            }
            else {
                ConvertMaterialForMesh(out_mesh, model, mesh, mindices[0]);
            }

            if (doc.Settings().readWeights && mesh.DeformerSkin() != null && !doc.Settings().useSkeleton) {
                ConvertWeights(out_mesh, mesh, absolute_transform, parent, NO_MATERIAL_SEPARATION, null);
            }
            else if (doc.Settings().readWeights && mesh.DeformerSkin() != null && doc.Settings().useSkeleton) {
                SkeletonBoneContainer sbc;
                ConvertWeightsToSkeleton(out_mesh, mesh, absolute_transform, parent, NO_MATERIAL_SEPARATION, null, sbc);
                aiSkeleton skeleton = createAiSkeleton(sbc);
                if (skeleton != null) {
                    mSkeletons.emplace_back(skeleton);
                }
            }

            List<aiAnimMesh*> animMeshes;
            for (FBXBlendShape* blendShape : mesh.GetBlendShapes()) {
                for (FBXBlendShapeChannel* blendShapeChannel : blendShape.BlendShapeChannels()) {
                    auto & shapeGeometries = blendShapeChannel.GetShapeGeometries();
                    for (ShapeGeometry* shapeGeometry : shapeGeometries) {
                        auto & curNormals = shapeGeometry.GetNormals();
                        aiAnimMesh* animMesh = aiCreateAnimMesh(out_mesh, true, !curNormals.empty());
                        auto & curVertices = shapeGeometry.GetVertices();
                        auto & curIndices = shapeGeometry.GetIndices();
                        // losing channel name if using shapeGeometry.Name()
                        //  if blendShapeChannel Name is empty or doesn't have a ".", add geoMetryName;
                        auto aniName = FixAnimMeshName(blendShapeChannel.Name());
                        auto geoMetryName = FixAnimMeshName(shapeGeometry.Name());
                        if (aniName.empty()) {
                            aniName = geoMetryName;
                        }
                        else if (aniName.find('.') == aniName.npos) {
                            aniName += "." + geoMetryName;
                        }
                        animMesh.mName.Set(aniName);
                        for (int j = 0; j < curIndices.Count; j++) {
                            uint curIndex = curIndices.at(j);
                            vec3 vertex = curVertices.at(j);
                            vec3 normal = curNormals.empty() ? vec3() : curNormals.at(j);
                            uint count = 0;
                            uint* outIndices = mesh.ToOutputVertexIndex(curIndex, count);
                            for (uint k = 0; k < count; k++) {
                                uint index = outIndices[k];
                                animMesh.mVertices[index] += vertex;
                                if (animMesh.mNormals != null) {
                                    animMesh.mNormals[index] += normal;
                                    animMesh.mNormals[index].NormalizeSafe();
                                }
                            }
                        }
                        animMesh.mWeight = shapeGeometries.Count > 1 ? blendShapeChannel.DeformPercent() / 100.0f : 1.0f;
                        animMeshes.push_back(animMesh);
                    }
                }
            }
            int numAnimMeshes = animMeshes.Count;
            if (numAnimMeshes > 0) {
                out_mesh.mNumAnimMeshes = static_cast<uint>(numAnimMeshes);
                out_mesh.mAnimMeshes = new aiAnimMesh*[numAnimMeshes];
                for (int i = 0; i < numAnimMeshes; i++) {
                    out_mesh.mAnimMeshes[i] = animMeshes.at(i);
                }
            }
            return static_cast<uint>(mMeshes.Count - 1);

        }

        // ------------------------------------------------------------------------------------------------
        List<uint>
        ConvertMeshMultiMaterial(FBXMeshGeometry &mesh, FBXModel &model, mat4 &absolute_transform, aiNode parent, aiNode root_node) {
            List<int> & mindices = mesh.GetMaterialIndices();
            System.Diagnostics.Debug.Assert(mindices.Count);

            std.set<List<int>.value_type> had;
            List<uint> indices;

            for (List<int>.value_type index : mindices) {
                if (had.find(index) == had.end()) {

                    indices.push_back(ConvertMeshMultiMaterial(mesh, model, absolute_transform, index, parent, root_node));
                    had.insert(index);
                }
            }

            return indices;

        }

        // ------------------------------------------------------------------------------------------------
        uint ConvertMeshMultiMaterial(FBXMeshGeometry &mesh, FBXModel &model, mat4 &absolute_transform, MatIndexArray::value_type index,
                aiNode parent, aiNode root_node) {
            aiMesh* out_mesh = SetupEmptyMesh(mesh, parent);

            List<int> & mindices = mesh.GetMaterialIndices();
            List<vec3> & vertices = mesh.GetVertices();
            List<uint> & faces = mesh.GetFaceIndexCounts();

            bool process_weights = doc.Settings().readWeights && mesh.DeformerSkin() != null;

            uint count_faces = 0;
            uint count_vertices = 0;

            // count faces
            List<uint>.const_iterator itf = faces.begin();
            for (List<int>.const_iterator it = mindices.begin(),
                                           end = mindices.end();
                    it != end; ++it, ++itf) {
                if ((*it) != index) {
                    continue;
                }
                ++count_faces;
                count_vertices += *itf;
            }

            System.Diagnostics.Debug.Assert(count_faces);
            System.Diagnostics.Debug.Assert(count_vertices);

            // mapping from output indices to DOM indexing, needed to resolve weights or blendshapes
            List<uint> reverseMapping;
            SortedDictionary<uint, uint> translateIndexMap;
            if (process_weights || mesh.GetBlendShapes().Count > 0) {
                reverseMapping.resize(count_vertices);
            }

            // allocate output data arrays, but don't fill them yet
            out_mesh.mNumVertices = count_vertices;
            out_mesh.mVertices = new vec3[count_vertices];

            out_mesh.mNumFaces = count_faces;
            aiFace* fac = out_mesh.mFaces = new aiFace[count_faces]();

            // allocate normals
            List<vec3> & normals = mesh.GetNormals();
            if (normals.Count) {
                System.Diagnostics.Debug.Assert(normals.Count == vertices.Count);
                out_mesh.mNormals = new vec3[count_vertices];
            }

            // allocate tangents, binormals.
            List<vec3> & tangents = mesh.GetTangents();
            List<vec3>* binormals = &mesh.GetBinormals();
            List<vec3> tempBinormals;

            if (tangents.Count) {
                if (!binormals.Count) {
                    if (normals.Count) {
                        // XXX this computes the binormals for the entire mesh, not only
                        // the part for which we need them.
                        tempBinormals.resize(normals.Count);
                        for (uint i = 0; i < tangents.Count; ++i) {
                            tempBinormals[i] = normals[i] ^ tangents[i];
                        }

                        binormals = &tempBinormals;
                    }
                    else {
                        binormals = null;
                    }
                }

                if (binormals) {
                    System.Diagnostics.Debug.Assert(tangents.Count == vertices.Count);
                    System.Diagnostics.Debug.Assert(binormals.Count == vertices.Count);

                    out_mesh.mTangents = new vec3[count_vertices];
                    out_mesh.mBitangents = new vec3[count_vertices];
                }
            }

            // allocate texture coords
            uint num_uvs = 0;
            for (uint i = 0; i < AI_MAX_NUMBER_OF_TEXTURECOORDS; ++i, ++num_uvs) {
                List<vec2> & uvs = mesh.GetTextureCoords(i);
                if (uvs.empty()) {
                    break;
                }

                out_mesh.mTextureCoords[i] = new vec3[count_vertices];
                out_mesh.mNumUVComponents[i] = 2;
            }

            // allocate vertex colors
            uint num_vcs = 0;
            for (uint i = 0; i < AI_MAX_NUMBER_OF_COLOR_SETS; ++i, ++num_vcs) {
                List<vec4> & colors = mesh.GetVertexColors(i);
                if (colors.empty()) {
                    break;
                }

                out_mesh.mColors[i] = new vec4[count_vertices];
            }

            uint cursor = 0, in_cursor = 0;

            itf = faces.begin();
            for (List<int>.const_iterator it = mindices.begin(), end = mindices.end(); it != end; ++it, ++itf) {
                uint pcount = *itf;
                if ((*it) != index) {
                    in_cursor += pcount;
                    continue;
                }

                aiFace & f = *fac++;

                f.mNumIndices = pcount;
                f.mIndices = new uint[pcount];
                switch (pcount) {
                case 1:
                out_mesh.mPrimitiveTypes |= aiPrimitiveType_POINT;
                break;
                case 2:
                out_mesh.mPrimitiveTypes |= aiPrimitiveType_LINE;
                break;
                case 3:
                out_mesh.mPrimitiveTypes |= aiPrimitiveType_TRIANGLE;
                break;
                default:
                out_mesh.mPrimitiveTypes |= aiPrimitiveType_POLYGON;
                break;
                }
                for (uint i = 0; i < pcount; ++i, ++cursor, ++in_cursor) {
                    f.mIndices[i] = cursor;

                    if (reverseMapping.Count) {
                        reverseMapping[cursor] = in_cursor;
                        translateIndexMap[in_cursor] = cursor;
                    }

                    out_mesh.mVertices[cursor] = vertices[in_cursor];

                    if (out_mesh.mNormals) {
                        out_mesh.mNormals[cursor] = normals[in_cursor];
                    }

                    if (out_mesh.mTangents) {
                        out_mesh.mTangents[cursor] = tangents[in_cursor];
                        out_mesh.mBitangents[cursor] = (*binormals)[in_cursor];
                    }

                    for (uint j = 0; j < num_uvs; ++j) {
                        List<vec2> & uvs = mesh.GetTextureCoords(j);
                        out_mesh.mTextureCoords[j][cursor] = vec3(uvs[in_cursor].x, uvs[in_cursor].y, 0.0f);
                    }

                    for (uint j = 0; j < num_vcs; ++j) {
                        List<vec4> & cols = mesh.GetVertexColors(j);
                        out_mesh.mColors[j][cursor] = cols[in_cursor];
                    }
                }
            }

            ConvertMaterialForMesh(out_mesh, model, mesh, index);

            if (process_weights) {
                ConvertWeights(out_mesh, mesh, absolute_transform, parent, index, &reverseMapping);
            }

            List<aiAnimMesh*> animMeshes;
            for (FBXBlendShape* blendShape : mesh.GetBlendShapes()) {
                for (FBXBlendShapeChannel* blendShapeChannel : blendShape.BlendShapeChannels()) {
                    auto & shapeGeometries = blendShapeChannel.GetShapeGeometries();
                    for (ShapeGeometry* shapeGeometry : shapeGeometries) {
                        auto & curNormals = shapeGeometry.GetNormals();
                        aiAnimMesh* animMesh = aiCreateAnimMesh(out_mesh, true, !curNormals.empty());
                        auto & curVertices = shapeGeometry.GetVertices();
                        auto & curIndices = shapeGeometry.GetIndices();
                        animMesh.mName.Set(FixAnimMeshName(shapeGeometry.Name()));
                        for (int j = 0; j < curIndices.Count; j++) {
                            uint curIndex = curIndices.at(j);
                            vec3 vertex = curVertices.at(j);
                            vec3 normal = curNormals.empty() ? vec3() : curNormals.at(j);
                            uint count = 0;
                            uint* outIndices = mesh.ToOutputVertexIndex(curIndex, count);
                            for (uint k = 0; k < count; k++) {
                                uint outIndex = outIndices[k];
                                if (translateIndexMap.find(outIndex) == translateIndexMap.end())
                                    continue;
                                uint transIndex = translateIndexMap[outIndex];
                                animMesh.mVertices[transIndex] += vertex;
                                if (animMesh.mNormals != null) {
                                    animMesh.mNormals[transIndex] += normal;
                                    animMesh.mNormals[transIndex].NormalizeSafe();
                                }
                            }
                        }
                        animMesh.mWeight = shapeGeometries.Count > 1 ? blendShapeChannel.DeformPercent() / 100.0f : 1.0f;
                        animMeshes.push_back(animMesh);
                    }
                }
            }

            int numAnimMeshes = animMeshes.Count;
            if (numAnimMeshes > 0) {
                out_mesh.mNumAnimMeshes = static_cast<uint>(numAnimMeshes);
                out_mesh.mAnimMeshes = new aiAnimMesh*[numAnimMeshes];
                for (int i = 0; i < numAnimMeshes; i++) {
                    out_mesh.mAnimMeshes[i] = animMeshes.at(i);
                }
            }

            return static_cast<uint>(mMeshes.Count - 1);

        }

        // ------------------------------------------------------------------------------------------------
        static uint NO_MATERIAL_SEPARATION = /* std::numeric_limits<uint>::max() */
                static_cast<uint>(-1);

        // ------------------------------------------------------------------------------------------------
        /**
         *  - if materialIndex == NO_MATERIAL_SEPARATION, materials are not taken into
         *    account when determining which weights to include.
         *  - outputVertStartIndices is only used when a material index is specified, it gives for
         *    each output vertex the DOM index it maps to.
         */
        void ConvertWeights(aiMesh* outValue, FBXMeshGeometry &geo, mat4 &absolute_transform, aiNode parent = null,
                uint materialIndex = NO_MATERIAL_SEPARATION,
                List<uint>* outputVertStartIndices = null) {
            System.Diagnostics.Debug.Assert(geo.DeformerSkin());

            List<int> out_indices, index_out_indices, count_out_indices;

            FBXSkin & sk = *geo.DeformerSkin();

            List<aiBone> bones;
            bool no_mat_check = materialIndex == NO_MATERIAL_SEPARATION;
            System.Diagnostics.Debug.Assert(no_mat_check || outputVertStartIndices);

            try {
                // iterate over the sub deformers
                for (FBXCluster* cluster : sk.Clusters()) {
                    System.Diagnostics.Debug.Assert(cluster);

                    WeightIndexArray & indices = cluster.GetIndices();

                    List<int> & mats = geo.GetMaterialIndices();

                    int no_index_sentinel = std.numeric_limits<int>.max();

                    count_out_indices.clear();
                    index_out_indices.clear();
                    out_indices.clear();

                    // now check if *any* of these weights is contained in the output mesh,
                    // taking notes so we don't need to do it twice.
                    for (WeightIndexArray.value_type index : indices) {

                        uint count = 0;
                        uint* out_idx = geo.ToOutputVertexIndex(index, count);
                        // ToOutputVertexIndex only returns null if index is out of bounds
                        // which should never happen
                        System.Diagnostics.Debug.Assert(out_idx != null);

                        index_out_indices.push_back(no_index_sentinel);
                        count_out_indices.push_back(0);

                        for (uint i = 0; i < count; ++i) {
                            if (no_mat_check || static_cast<int>(mats[geo.FaceForVertexIndex(out_idx[i])]) == materialIndex) {

                                if (index_out_indices.back() == no_index_sentinel) {
                                    index_out_indices.back() = out_indices.Count;
                                }

                                if (no_mat_check) {
                                    out_indices.push_back(out_idx[i]);
                                }
                                else {
                                    // this extra lookup is in O(logn), so the entire algorithm becomes O(nlogn)
                                    List<uint>.iterator it = std.lower_bound(
                                            outputVertStartIndices.begin(),
                                            outputVertStartIndices.end(),
                                            out_idx[i]);

                                    out_indices.push_back(std.distance(outputVertStartIndices.begin(), it));
                                }

                                ++count_out_indices.back();
                            }
                        }
                    }

                    // if we found at least one, generate the output bones
                    // XXX this could be heavily simplified by collecting the bone
                    // data in a single step.
                    ConvertCluster(bones, cluster, out_indices, index_out_indices,
                            count_out_indices, absolute_transform, parent);
                }

                bone_map.clear();
            }
            catch (std.exception &) {
                std.for_each(bones.begin(), bones.end(), Util.delete_fun<aiBone>());
                throw;
            }

            if (bones.empty()) {
                outValue.mBones = null;
                outValue.mNumBones = 0;
                return;
            }

            outValue.mBones = new aiBone[bones.Count]();
            outValue.mNumBones = static_cast<uint>(bones.Count);
            std.swap_ranges(bones.begin(), bones.end(), outValue.mBones);

            }

            // ------------------------------------------------------------------------------------------------
            void ConvertWeightsToSkeleton(aiMesh* outValue, FBXMeshGeometry &geo, mat4 & absolute_transform,
                aiNode parent, uint materialIndex, List< uint > *outputVertStartIndices,
                SkeletonBoneContainer & skeletonContainer) {

                if (skeletonContainer.SkeletonBoneToMeshLookup.find(outValue) != skeletonContainer.SkeletonBoneToMeshLookup.end()) {
                    return;
                }

                ConvertWeights(outValue, geo, absolute_transform, parent, materialIndex, outputVertStartIndices);
                skeletonContainer.MeshArray.emplace_back(outValue);
                List<aiSkeletonBone>* ba = new List<aiSkeletonBone>;
                for (int i = 0; i < outValue.mNumBones; ++i) {
                    aiBone bone = outValue.mBones[i];
                    if (bone == null) {
                        continue;
                    }
                    aiSkeletonBone* skeletonBone = new aiSkeletonBone;
                    copyBoneToSkeletonBone(outValue, bone, skeletonBone);
                    ba.emplace_back(skeletonBone);
                }
                skeletonContainer.SkeletonBoneToMeshLookup[outValue] = ba;

            }

            // ------------------------------------------------------------------------------------------------
            void ConvertCluster(List<aiBone> &local_mesh_bones, FBXCluster* cl,
                    List< int > &out_indices, List<int> & index_out_indices,
                List<int> & count_out_indices, mat4 & absolute_transform, aiNode parent){
                System.Diagnostics.Debug.Assert(cluster != null); // make sure cluster valid

                string deformer_name = cluster.TargetNode().Name();
                string bone_name = string(FixNodeName(deformer_name));

                aiBone bone = null;

                if (bone_map.count(deformer_name)) {
                    ASSIMP_LOG_VERBOSE_DEBUG("retrieved bone from lookup ", bone_name.C_Str(), ". Deformer:", deformer_name);
                    bone = bone_map[deformer_name];
                }
                else {
                    ASSIMP_LOG_VERBOSE_DEBUG("created new bone ", bone_name.C_Str(), ". Deformer: ", deformer_name);
                    bone = new aiBone();
                    bone.mName = bone_name;

                    // bone.mOffsetMatrix = cluster.Transform();
                    //  store local transform link for post processing

                    bone.mOffsetMatrix = cluster.TransformLink();
                    bone.mOffsetMatrix.Inverse();

                    mat4 matrix = (mat4)absolute_transform;

                    bone.mOffsetMatrix = bone.mOffsetMatrix * matrix; // * mesh_offset

                    //
                    // Now calculate the aiVertexWeights
                    //

                    aiVertexWeight* cursor = null;

                    bone.mNumWeights = static_cast<uint>(out_indices.Count);
                    cursor = bone.mWeights = new aiVertexWeight[out_indices.Count];

                    int no_index_sentinel = std.numeric_limits<int>.max();
                    WeightArray & weights = cluster.GetWeights();

                    int c = index_out_indices.Count;
                    for (int i = 0; i < c; ++i) {
                        int index_index = index_out_indices[i];

                        if (index_index == no_index_sentinel) {
                            continue;
                        }

                        int cc = count_out_indices[i];
                        for (int j = 0; j < cc; ++j) {
                            // cursor runs from first element relative to the start
                            // or relative to the start of the next indexes.
                            aiVertexWeight & out_weight = *cursor++;

                            out_weight.mVertexId = static_cast<uint>(out_indices[index_index + j]);
                            out_weight.mWeight = weights[i];
                        }
                    }

                    bone_map.insert(std.pair<string, aiBone>(deformer_name, bone));
                }

                Log.WriteLine("bone research: Indices size: ", out_indices.Count);

                // lookup must be populated in case something goes wrong
                // this also allocates bones to mesh instance outside
                local_mesh_bones.push_back(bone);

            }

            // ------------------------------------------------------------------------------------------------
            void ConvertMaterialForMesh(aiMesh* outValue, FBXModel &model, FBXMeshGeometry & geo,
                MatIndexArray::value_type materialIndex){
                // locate source materials for this mesh
                List<FBXMaterial*> & mats = model.GetMaterials();
                if (static_cast<uint>(materialIndex) >= mats.Count || materialIndex < 0) {
                    FBXImporter.LogError("material index out of bounds, setting default material");
                    outValue.mMaterialIndex = GetDefaultMaterial();
                    return;
                }

                FBXMaterial* mat = mats[materialIndex];
                Dictionary<FBXMaterial, uint>.const_iterator it = materials_converted.find(mat);
                if (it != materials_converted.end()) {
                    outValue.mMaterialIndex = (*it).second;
                    return;
                }

                outValue.mMaterialIndex = ConvertMaterial(*mat, &geo);
                materials_converted[mat] = outValue.mMaterialIndex;

            }

            // ------------------------------------------------------------------------------------------------
            uint GetDefaultMaterial() {
                if (defaultMaterialIndex) {
                    return defaultMaterialIndex - 1;
                }

                aiMaterial* out_mat = new aiMaterial();
                materials.push_back(out_mat);

                vec3 diffuse = vec3(0.8f, 0.8f, 0.8f);
                out_mat.AddProperty(&diffuse, 1, AI_MATKEY_COLOR_DIFFUSE);

                string s;
                s.Set(AI_DEFAULT_MATERIAL_NAME);

                out_mat.AddProperty(&s, AI_MATKEY_NAME);

                defaultMaterialIndex = static_cast<uint>(materials.Count);
                return defaultMaterialIndex - 1;

            }

            // ------------------------------------------------------------------------------------------------
            // Material . aiMaterial
            uint ConvertMaterial(FBXMaterial &material, FBXMeshGeometry* mesh){
                PropertyTable & props = material.Props();

                // generate empty output material
                aiMaterial* out_mat = new aiMaterial();
                materials_converted[&material] = static_cast<uint>(materials.Count);

                materials.push_back(out_mat);

                string str;

                // strip Material. prefix
                string name = material.Name();
                if (name.substr(0, 10) == "Material.") {
                    name = name.substr(10);
                }

                // set material name if not empty - this could happen
                // and there should be no key for it in this case.
                if (name.length()) {
                    str.Set(name);
                    out_mat.AddProperty(&str, AI_MATKEY_NAME);
                }

                // Set the shading mode as best we can: The FBX specification only mentions Lambert and Phong, and only Phong is mentioned in Assimp's aiShadingMode enum.
                if (material.GetShadingModel() == "phong") {
                    aiShadingMode shadingMode = aiShadingMode_Phong;
                    out_mat.AddProperty<aiShadingMode>(&shadingMode, 1, AI_MATKEY_SHADING_MODEL);
                }

                // shading stuff and colors
                SetShadingPropertiesCommon(out_mat, props);
                SetShadingPropertiesRaw(out_mat, props, material.Textures(), mesh);

                // texture assignments
                SetTextureProperties(out_mat, material.Textures(), mesh);
                SetTextureProperties(out_mat, material.LayeredTextures(), mesh);

                return static_cast<uint>(materials.Count - 1);

            }

            // ------------------------------------------------------------------------------------------------
            // Video . aiTexture
            uint ConvertVideo(FBXVideo &video){
                // generate empty output texture
                aiTexture* out_tex = new aiTexture();
                textures.push_back(out_tex);

                // assuming the texture is compressed
                out_tex.mWidth = static_cast<uint>(video.ContentLength()); // total data size
                out_tex.mHeight = 0; // fixed to 0

                // steal the data from the Video to avoid an additional copy
                out_tex.pcData = reinterpret_cast<aiTexel*>(const_cast < FBXVideo &> (video).RelinquishContent());

                // try to extract a hint from the file extension
                string &filename = video.RelativeFilename().empty() ? video.FileName() : video.RelativeFilename();
                string ext = BaseImporter.GetExtension(filename);

                if (ext == "jpeg") {
                    ext = "jpg";
                }

                if (ext.Count <= 3) {
                    memcpy(out_tex.achFormatHint, ext.c_str(), ext.Count);
                }

                out_tex.mFilename.Set(filename.c_str());

                return static_cast<uint>(textures.Count - 1);

            }

            // ------------------------------------------------------------------------------------------------
            // convert embedded texture if necessary and return actual texture path
            string GetTexturePath(FBXTexture* tex) {
                string path;
                path.Set(tex.RelativeFilename());

                FBXVideo* media = tex.Media();
                if (media != null) {
                    bool textureReady = false; // tells if our texture is ready (if it was loaded or if it was found)
                    uint index = 0;

                    Dictionary<FBXVideo, uint>.const_iterator it = textures_converted.find(media);
                    if (it != textures_converted.end()) {
                        index = (*it).second;
                        textureReady = true;
                    }
                    else {
                        if (media.ContentLength() > 0) {
                            index = ConvertVideo(*media);
                            textures_converted[media] = index;
                            textureReady = true;
                        }
                    }

                    // setup texture reference string (copied from ColladaLoader.FindFilenameForEffectTexture), if the texture is ready
                    if (doc.Settings().useLegacyEmbeddedTextureNaming) {
                        if (textureReady) {
                            // TODO: check the possibility of using the flag "AI_CONFIG_IMPORT_FBX_EMBEDDED_TEXTURES_LEGACY_NAMING"
                            // In FBX files textures are now stored internally by Assimp with their filename included
                            // Now Assimp can lookup through the loaded textures after all data is processed
                            // We need to load all textures before referencing them, as FBX file format order may reference a texture before loading it
                            // This may occur on this case too, it has to be studied
                            path.data[0] = '*';
                            path.length = 1 + ASSIMP_itoa10(path.data + 1, AI_MAXLEN - 1, index);
                        }
                    }
                }

                return path;

            }

            // ------------------------------------------------------------------------------------------------
            void TrySetTextureProperties(aiMaterial* out_mat, TextureMap &textures,
                string &propName,
                aiTextureType target, FBXMeshGeometry*mesh){
					TextureMap.const_iterator it = _textures.find(propName);
if (it == _textures.end()) {
    return;
}

FBXTexture *tex = (*it).second;
if (tex != null) {
    string path = GetTexturePath(tex);
    out_mat.AddProperty(&path, _AI_MATKEY_TEXTURE_BASE, target, 0);

    aiUVTransform uvTrafo;
    // XXX handle all kinds of UV transformations
    uvTrafo.mScaling = tex.UVScaling();
    uvTrafo.mTranslation = tex.UVTranslation();
    uvTrafo.mRotation = tex.UVRotation();
    out_mat.AddProperty(&uvTrafo, 1, _AI_MATKEY_UVTRANSFORM_BASE, target, 0);

    PropertyTable &props = tex.Props();

    int uvIndex = 0;

    bool ok;
    string &uvSet = PropertyGet<string>(props, "UVSet", ok);
    if (ok) {
        // "default" is the name which usually appears in the FbxFileTexture template
        if (uvSet != "default" && uvSet.length()) {
            // this is a bit awkward - we need to find a mesh that uses this
            // material and scan its UV channels for the given UV name because
            // assimp references UV channels by index, not by name.

            // XXX: the case that UV channels may appear in different orders
            // in meshes is unhandled. A possible solution would be to sort
            // the UV channels alphabetically, but this would have the side
            // effect that the primary (first) UV channel would sometimes
            // be moved, causing trouble when users read only the first
            // UV channel and ignore UV channel assignments altogether.

            uint matIndex = static_cast<uint>(std.distance(materials.begin(),
                    std.find(materials.begin(), materials.end(), out_mat)));

            uvIndex = -1;
            if (!mesh) {
                for (Dictionary<Geometry, List<uint>>.value_type &v : meshes_converted) {
                    FBXMeshGeometry *meshGeom = dynamic_cast<FBXMeshGeometry *>(v.first);
                    if (!meshGeom) {
                        continue;
                    }

                    List<int> &mats = meshGeom.GetMaterialIndices();
                    List<int>.const_iterator curIt = std.find(mats.begin(), mats.end(), (int)matIndex);
                    if (curIt == mats.end()) {
                        continue;
                    }

                    int index = -1;
                    for (uint i = 0; i < AI_MAX_NUMBER_OF_TEXTURECOORDS; ++i) {
                        if (meshGeom.GetTextureCoords(i).empty()) {
                            break;
                        }
                        string name = meshGeom.GetTextureCoordChannelName(i);
                        if (name == uvSet) {
                            index = static_cast<int>(i);
                            break;
                        }
                    }
                    if (index == -1) {
                        FBXImporter.LogWarn("did not find UV channel named ", uvSet, " in a mesh using this material");
                        continue;
                    }

                    if (uvIndex == -1) {
                        uvIndex = index;
                    } else {
                        FBXImporter.LogWarn("the UV channel named ", uvSet,
                                " appears at different positions in meshes, results will be wrong");
                    }
                }
            } else {
                int index = -1;
                for (uint i = 0; i < AI_MAX_NUMBER_OF_TEXTURECOORDS; ++i) {
                    if (mesh.GetTextureCoords(i).empty()) {
                        break;
                    }
                    string name = mesh.GetTextureCoordChannelName(i);
                    if (name == uvSet) {
                        index = static_cast<int>(i);
                        break;
                    }
                }
                if (index == -1) {
                    FBXImporter.LogWarn("did not find UV channel named ", uvSet, " in a mesh using this material");
                }

                if (uvIndex == -1) {
                    uvIndex = index;
                }
            }

            if (uvIndex == -1) {
                FBXImporter.LogWarn("failed to resolve UV channel ", uvSet, ", using first UV channel");
                uvIndex = 0;
            }
        }
    }

    out_mat.AddProperty(&uvIndex, 1, _AI_MATKEY_UVWSRC_BASE, target, 0);
}

			}

            // ------------------------------------------------------------------------------------------------
            void TrySetTextureProperties(aiMaterial* out_mat, LayeredTextureMap &layeredTextures,
                string &propName,
                aiTextureType target, FBXMeshGeometry*mesh){
					LayeredTextureMap.const_iterator it = layeredTextures.find(propName);
if (it == layeredTextures.end()) {
    return;
}

int texCount = (*it).second.textureCount();

// Set the blend mode for layered textures
int blendmode = (*it).second.GetBlendMode();
out_mat.AddProperty(&blendmode, 1, _AI_MATKEY_TEXOP_BASE, target, 0);

for (int texIndex = 0; texIndex < texCount; texIndex++) {

    FBXTexture *tex = (*it).second.getTexture(texIndex);

    string path = GetTexturePath(tex);
    out_mat.AddProperty(&path, _AI_MATKEY_TEXTURE_BASE, target, texIndex);

    aiUVTransform uvTrafo;
    // XXX handle all kinds of UV transformations
    uvTrafo.mScaling = tex.UVScaling();
    uvTrafo.mTranslation = tex.UVTranslation();
    uvTrafo.mRotation = tex.UVRotation();
    out_mat.AddProperty(&uvTrafo, 1, _AI_MATKEY_UVTRANSFORM_BASE, target, texIndex);

    PropertyTable &props = tex.Props();

    int uvIndex = 0;

    bool ok;
    string &uvSet = PropertyGet<string>(props, "UVSet", ok);
    if (ok) {
        // "default" is the name which usually appears in the FbxFileTexture template
        if (uvSet != "default" && uvSet.length()) {
            // this is a bit awkward - we need to find a mesh that uses this
            // material and scan its UV channels for the given UV name because
            // assimp references UV channels by index, not by name.

            // XXX: the case that UV channels may appear in different orders
            // in meshes is unhandled. A possible solution would be to sort
            // the UV channels alphabetically, but this would have the side
            // effect that the primary (first) UV channel would sometimes
            // be moved, causing trouble when users read only the first
            // UV channel and ignore UV channel assignments altogether.

            uint matIndex = static_cast<uint>(std.distance(materials.begin(),
                    std.find(materials.begin(), materials.end(), out_mat)));

            uvIndex = -1;
            if (!mesh) {
                for (Dictionary<Geometry, List<uint>>.value_type &v : meshes_converted) {
                    FBXMeshGeometry *meshGeom = dynamic_cast<FBXMeshGeometry *>(v.first);
                    if (!meshGeom) {
                        continue;
                    }

                    List<int> &mats = meshGeom.GetMaterialIndices();
                    List<int>.const_iterator curIt = std.find(mats.begin(), mats.end(), (int)matIndex);
                    if (curIt == mats.end()) {
                        continue;
                    }

                    int index = -1;
                    for (uint i = 0; i < AI_MAX_NUMBER_OF_TEXTURECOORDS; ++i) {
                        if (meshGeom.GetTextureCoords(i).empty()) {
                            break;
                        }
                        string name = meshGeom.GetTextureCoordChannelName(i);
                        if (name == uvSet) {
                            index = static_cast<int>(i);
                            break;
                        }
                    }
                    if (index == -1) {
                        FBXImporter.LogWarn("did not find UV channel named ", uvSet, " in a mesh using this material");
                        continue;
                    }

                    if (uvIndex == -1) {
                        uvIndex = index;
                    } else {
                        FBXImporter.LogWarn("the UV channel named ", uvSet,
                                " appears at different positions in meshes, results will be wrong");
                    }
                }
            } else {
                int index = -1;
                for (uint i = 0; i < AI_MAX_NUMBER_OF_TEXTURECOORDS; ++i) {
                    if (mesh.GetTextureCoords(i).empty()) {
                        break;
                    }
                    string name = mesh.GetTextureCoordChannelName(i);
                    if (name == uvSet) {
                        index = static_cast<int>(i);
                        break;
                    }
                }
                if (index == -1) {
                    FBXImporter.LogWarn("did not find UV channel named ", uvSet, " in a mesh using this material");
                }

                if (uvIndex == -1) {
                    uvIndex = index;
                }
            }

            if (uvIndex == -1) {
                FBXImporter.LogWarn("failed to resolve UV channel ", uvSet, ", using first UV channel");
                uvIndex = 0;
            }
        }
    }

    out_mat.AddProperty(&uvIndex, 1, _AI_MATKEY_UVWSRC_BASE, target, texIndex);
}

				}

            // ------------------------------------------------------------------------------------------------
            void SetTextureProperties(aiMaterial* out_mat, TextureMap &textures, FBXMeshGeometry* mesh){
				TrySetTextureProperties(out_mat, _textures, "DiffuseColor", aiTextureType_DIFFUSE, mesh);
TrySetTextureProperties(out_mat, _textures, "AmbientColor", aiTextureType_AMBIENT, mesh);
TrySetTextureProperties(out_mat, _textures, "EmissiveColor", aiTextureType_EMISSIVE, mesh);
TrySetTextureProperties(out_mat, _textures, "SpecularColor", aiTextureType_SPECULAR, mesh);
TrySetTextureProperties(out_mat, _textures, "SpecularFactor", aiTextureType_SPECULAR, mesh);
TrySetTextureProperties(out_mat, _textures, "TransparentColor", aiTextureType_OPACITY, mesh);
TrySetTextureProperties(out_mat, _textures, "ReflectionColor", aiTextureType_REFLECTION, mesh);
TrySetTextureProperties(out_mat, _textures, "DisplacementColor", aiTextureType_DISPLACEMENT, mesh);
TrySetTextureProperties(out_mat, _textures, "NormalMap", aiTextureType_NORMALS, mesh);
TrySetTextureProperties(out_mat, _textures, "Bump", aiTextureType_HEIGHT, mesh);
TrySetTextureProperties(out_mat, _textures, "ShininessExponent", aiTextureType_SHININESS, mesh);
TrySetTextureProperties(out_mat, _textures, "TransparencyFactor", aiTextureType_OPACITY, mesh);
TrySetTextureProperties(out_mat, _textures, "EmissiveFactor", aiTextureType_EMISSIVE, mesh);
TrySetTextureProperties(out_mat, _textures, "ReflectionFactor", aiTextureType_METALNESS, mesh);
// Maya counterparts
TrySetTextureProperties(out_mat, _textures, "Maya|DiffuseTexture", aiTextureType_DIFFUSE, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|NormalTexture", aiTextureType_NORMALS, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|SpecularTexture", aiTextureType_SPECULAR, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|FalloffTexture", aiTextureType_OPACITY, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|ReflectionMapTexture", aiTextureType_REFLECTION, mesh);

// Maya PBR
TrySetTextureProperties(out_mat, _textures, "Maya|baseColor", aiTextureType_BASE_COLOR, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|normalCamera", aiTextureType_NORMAL_CAMERA, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|emissionColor", aiTextureType_EMISSION_COLOR, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|metalness", aiTextureType_METALNESS, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|diffuseRoughness", aiTextureType_DIFFUSE_ROUGHNESS, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|base", aiTextureType_MAYA_BASE, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|specular", aiTextureType_MAYA_SPECULAR, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|specularColor", aiTextureType_MAYA_SPECULAR_COLOR, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|specularRoughness", aiTextureType_MAYA_SPECULAR_ROUGHNESS, mesh);

// Maya stingray
TrySetTextureProperties(out_mat, _textures, "Maya|TEX_color_map", aiTextureType_BASE_COLOR, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|TEX_normal_map", aiTextureType_NORMAL_CAMERA, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|TEX_emissive_map", aiTextureType_EMISSION_COLOR, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|TEX_metallic_map", aiTextureType_METALNESS, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|TEX_roughness_map", aiTextureType_DIFFUSE_ROUGHNESS, mesh);
TrySetTextureProperties(out_mat, _textures, "Maya|TEX_ao_map", aiTextureType_AMBIENT_OCCLUSION, mesh);

// 3DSMax Physical material
TrySetTextureProperties(out_mat, _textures, "3dsMax|Parameters|base_color_map", aiTextureType_BASE_COLOR, mesh);
TrySetTextureProperties(out_mat, _textures, "3dsMax|Parameters|bump_map", aiTextureType_NORMAL_CAMERA, mesh);
TrySetTextureProperties(out_mat, _textures, "3dsMax|Parameters|emission_map", aiTextureType_EMISSION_COLOR, mesh);
TrySetTextureProperties(out_mat, _textures, "3dsMax|Parameters|metalness_map", aiTextureType_METALNESS, mesh);
TrySetTextureProperties(out_mat, _textures, "3dsMax|Parameters|roughness_map", aiTextureType_DIFFUSE_ROUGHNESS, mesh);

// 3DSMax PBR materials
TrySetTextureProperties(out_mat, _textures, "3dsMax|main|base_color_map", aiTextureType_BASE_COLOR, mesh);
TrySetTextureProperties(out_mat, _textures, "3dsMax|main|norm_map", aiTextureType_NORMAL_CAMERA, mesh);
TrySetTextureProperties(out_mat, _textures, "3dsMax|main|emit_color_map", aiTextureType_EMISSION_COLOR, mesh);
TrySetTextureProperties(out_mat, _textures, "3dsMax|main|ao_map", aiTextureType_AMBIENT_OCCLUSION, mesh);
TrySetTextureProperties(out_mat, _textures, "3dsMax|main|opacity_map", aiTextureType_OPACITY, mesh);
// Metalness/Roughness material type
TrySetTextureProperties(out_mat, _textures, "3dsMax|main|metalness_map", aiTextureType_METALNESS, mesh);
// Specular/Gloss material type
TrySetTextureProperties(out_mat, _textures, "3dsMax|main|specular_map", aiTextureType_SPECULAR, mesh);

// Glossiness vs roughness in 3ds Max Pbr Materials
int useGlossiness;
if (out_mat.Get("$raw.3dsMax|main|useGlossiness", aiTextureType_NONE, 0, useGlossiness) == aiReturn_SUCCESS) {
    // These textures swap meaning if ((useGlossiness == 1) != (material type is Specular/Gloss))
    if (useGlossiness == 1) {
        TrySetTextureProperties(out_mat, _textures, "3dsMax|main|roughness_map", aiTextureType_SHININESS, mesh);
        TrySetTextureProperties(out_mat, _textures, "3dsMax|main|glossiness_map", aiTextureType_SHININESS, mesh);
    } else if (useGlossiness == 2) {
        TrySetTextureProperties(out_mat, _textures, "3dsMax|main|roughness_map", aiTextureType_DIFFUSE_ROUGHNESS, mesh);
        TrySetTextureProperties(out_mat, _textures, "3dsMax|main|glossiness_map", aiTextureType_DIFFUSE_ROUGHNESS, mesh);
    } else {
        FBXImporter.LogWarn("A 3dsMax Pbr Material must have a useGlossiness value to correctly interpret roughness and glossiness textures.");
    }
}

			}

            // ------------------------------------------------------------------------------------------------
            void SetTextureProperties(aiMaterial* out_mat, LayeredTextureMap &layeredTextures, FBXMeshGeometry* mesh){
				    TrySetTextureProperties(out_mat, layeredTextures, "DiffuseColor", aiTextureType_DIFFUSE, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "AmbientColor", aiTextureType_AMBIENT, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "EmissiveColor", aiTextureType_EMISSIVE, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "SpecularColor", aiTextureType_SPECULAR, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "SpecularFactor", aiTextureType_SPECULAR, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "TransparentColor", aiTextureType_OPACITY, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "ReflectionColor", aiTextureType_REFLECTION, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "DisplacementColor", aiTextureType_DISPLACEMENT, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "NormalMap", aiTextureType_NORMALS, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "Bump", aiTextureType_HEIGHT, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "ShininessExponent", aiTextureType_SHININESS, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "EmissiveFactor", aiTextureType_EMISSIVE, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "TransparencyFactor", aiTextureType_OPACITY, mesh);
    TrySetTextureProperties(out_mat, layeredTextures, "ReflectionFactor", aiTextureType_METALNESS, mesh);
			}

            // ------------------------------------------------------------------------------------------------
            vec3 GetColorPropertyFromMaterial(PropertyTable &props, string &baseName,
                bool &result){
    return GetColorPropertyFactored(props, baseName + "Color", baseName + "Factor", result, true);
					
			}
            vec3 GetColorPropertyFactored(PropertyTable &props, string &colorName,
                string &factorName, bool &result, bool useTemplate = true){
					 result = true;

 bool ok;
 vec3 BaseColor = PropertyGet<vec3>(props, colorName, ok, useTemplate);
 if (!ok) {
     result = false;
     return vec3(0.0f, 0.0f, 0.0f);
 }

 // if no factor name, return the colour as is
 if (factorName.empty()) {
     return vec3(BaseColor.x, BaseColor.y, BaseColor.z);
 }

 // otherwise it should be multiplied by the factor, if found.
 float factor = PropertyGet<float>(props, factorName, ok, useTemplate);
 if (ok) {
     BaseColor *= factor;
 }
 return vec3(BaseColor.x, BaseColor.y, BaseColor.z);

				}
            vec3 GetColorProperty(PropertyTable &props, string &colorName,
                bool &result, bool useTemplate = true){
					result = true;
bool ok;
vec3 &ColorVec = PropertyGet<vec3>(props, colorName, ok, useTemplate);
if (!ok) {
    result = false;
    return vec3(0.0f, 0.0f, 0.0f);
}
return vec3(ColorVec.x, ColorVec.y, ColorVec.z);

				}

            // ------------------------------------------------------------------------------------------------
            void SetShadingPropertiesCommon(aiMaterial* out_mat, PropertyTable &props){
				// Set shading properties.
// Modern FBX Files have two separate systems for defining these,
// with only the more comprehensive one described in the property template.
// Likely the other values are a legacy system,
// which is still always exported by the official FBX SDK.
//
// Blender's FBX import and export mostly ignore this legacy system,
// and as we only support recent versions of FBX anyway, we can do the same.
bool ok;

vec3 &Diffuse = GetColorPropertyFromMaterial(props, "Diffuse", ok);
if (ok) {
    out_mat.AddProperty(&Diffuse, 1, AI_MATKEY_COLOR_DIFFUSE);
}

vec3 &Emissive = GetColorPropertyFromMaterial(props, "Emissive", ok);
if (ok) {
    out_mat.AddProperty(&Emissive, 1, AI_MATKEY_COLOR_EMISSIVE);
} else {
    vec3 &emissiveColor = GetColorProperty(props, "Maya|emissive", ok);
    if (ok) {
        out_mat.AddProperty(&emissiveColor, 1, AI_MATKEY_COLOR_EMISSIVE);
    }
}

vec3 &Ambient = GetColorPropertyFromMaterial(props, "Ambient", ok);
if (ok) {
    out_mat.AddProperty(&Ambient, 1, AI_MATKEY_COLOR_AMBIENT);
}

// we store specular factor as SHININESS_STRENGTH, so just get the color
vec3 &Specular = GetColorProperty(props, "SpecularColor", ok, true);
if (ok) {
    out_mat.AddProperty(&Specular, 1, AI_MATKEY_COLOR_SPECULAR);
}

// and also try to get SHININESS_STRENGTH
float SpecularFactor = PropertyGet<float>(props, "SpecularFactor", ok, true);
if (ok) {
    out_mat.AddProperty(&SpecularFactor, 1, AI_MATKEY_SHININESS_STRENGTH);
}

// and the specular exponent
float ShininessExponent = PropertyGet<float>(props, "ShininessExponent", ok);
if (ok) {
    out_mat.AddProperty(&ShininessExponent, 1, AI_MATKEY_SHININESS);
    // Match Blender behavior to extract roughness when only shininess is present
    float roughness = 1.0f - (sqrt(ShininessExponent) / 10.0f);
    out_mat.AddProperty(&roughness, 1, AI_MATKEY_ROUGHNESS_FACTOR);
}

// TransparentColor / TransparencyFactor... gee thanks FBX :rolleyes:
vec3 &Transparent = GetColorPropertyFactored(props, "TransparentColor", "TransparencyFactor", ok);
float CalculatedOpacity = 1.0f;
if (ok) {
    out_mat.AddProperty(&Transparent, 1, AI_MATKEY_COLOR_TRANSPARENT);
    // as calculated by FBX SDK 2017:
    CalculatedOpacity = 1.0f - ((Transparent.r + Transparent.g + Transparent.b) / 3.0f);
}

// try to get the transparency factor
float TransparencyFactor = PropertyGet<float>(props, "TransparencyFactor", ok);
if (ok) {
    out_mat.AddProperty(&TransparencyFactor, 1, AI_MATKEY_TRANSPARENCYFACTOR);
}

// use of TransparencyFactor is inconsistent.
// Maya always stores it as 1.0,
// so we can't use it to set AI_MATKEY_OPACITY.
// Blender is more sensible and stores it as the alpha value.
// However both the FBX SDK and Blender always write an additional
// legacy "Opacity" field, so we can try to use that.
//
// If we can't find it,
// we can fall back to the value which the FBX SDK calculates
// from transparency colour (RGB) and factor (F) as
// 1.0 - F*((R+G+B)/3).
//
// There's no consistent way to interpret this opacity value,
// so it's up to clients to do the correct thing.
float Opacity = PropertyGet<float>(props, "Opacity", ok);
if (ok) {
    out_mat.AddProperty(&Opacity, 1, AI_MATKEY_OPACITY);
} else if (CalculatedOpacity != 1.0) {
    out_mat.AddProperty(&CalculatedOpacity, 1, AI_MATKEY_OPACITY);
}

// reflection color and factor are stored separately
vec3 &Reflection = GetColorProperty(props, "ReflectionColor", ok, true);
if (ok) {
    out_mat.AddProperty(&Reflection, 1, AI_MATKEY_COLOR_REFLECTIVE);
}

float ReflectionFactor = PropertyGet<float>(props, "ReflectionFactor", ok, true);
if (ok) {
    out_mat.AddProperty(&ReflectionFactor, 1, AI_MATKEY_REFLECTIVITY);
}

float BumpFactor = PropertyGet<float>(props, "BumpFactor", ok);
if (ok) {
    out_mat.AddProperty(&BumpFactor, 1, AI_MATKEY_BUMPSCALING);
}

float DispFactor = PropertyGet<float>(props, "DisplacementFactor", ok);
if (ok) {
    out_mat.AddProperty(&DispFactor, 1, "$mat.displacementscaling", 0, 0);
}

// PBR material information
vec3 &baseColor = GetColorProperty(props, "Maya|base_color", ok);
if (ok) {
    out_mat.AddProperty(&baseColor, 1, AI_MATKEY_BASE_COLOR);
}

float useColorMap = PropertyGet<float>(props, "Maya|use_color_map", ok);
if (ok) {
    out_mat.AddProperty(&useColorMap, 1, AI_MATKEY_USE_COLOR_MAP);
}

float useMetallicMap = PropertyGet<float>(props, "Maya|use_metallic_map", ok);
if (ok) {
    out_mat.AddProperty(&useMetallicMap, 1, AI_MATKEY_USE_METALLIC_MAP);
}

float metallicFactor = PropertyGet<float>(props, "Maya|metallic", ok);
if (ok) {
    out_mat.AddProperty(&metallicFactor, 1, AI_MATKEY_METALLIC_FACTOR);
}

float useRoughnessMap = PropertyGet<float>(props, "Maya|use_roughness_map", ok);
if (ok) {
    out_mat.AddProperty(&useRoughnessMap, 1, AI_MATKEY_USE_ROUGHNESS_MAP);
}

float roughnessFactor = PropertyGet<float>(props, "Maya|roughness", ok);
if (ok) {
    out_mat.AddProperty(&roughnessFactor, 1, AI_MATKEY_ROUGHNESS_FACTOR);
}

float useEmissiveMap = PropertyGet<float>(props, "Maya|use_emissive_map", ok);
if (ok) {
    out_mat.AddProperty(&useEmissiveMap, 1, AI_MATKEY_USE_EMISSIVE_MAP);
}

float emissiveIntensity = PropertyGet<float>(props, "Maya|emissive_intensity", ok);
if (ok) {
    out_mat.AddProperty(&emissiveIntensity, 1, AI_MATKEY_EMISSIVE_INTENSITY);
}

float useAOMap = PropertyGet<float>(props, "Maya|use_ao_map", ok);
if (ok) {
    out_mat.AddProperty(&useAOMap, 1, AI_MATKEY_USE_AO_MAP);
}

			}
            void SetShadingPropertiesRaw(aiMaterial* out_mat, PropertyTable &props, TextureMap & textures, FBXMeshGeometry* mesh){
				 // Add all the unparsed properties with a "$raw." prefix

 string prefix = "$raw.";

 for (Dictionary<string, FBXProperty>.value_type &prop : props.GetUnparsedProperties()) {

     string name = prefix + prop.first;

     if (TypedProperty<vec3> *interpretedVec3 = prop.second.As<TypedProperty<vec3>>()) {
         out_mat.AddProperty(&interpretedVec3.Value(), 1, name.c_str(), 0, 0);
     } else if (TypedProperty<vec3> *interpretedCol3 = prop.second.As<TypedProperty<vec3>>()) {
         out_mat.AddProperty(&interpretedCol3.Value(), 1, name.c_str(), 0, 0);
     } else if (TypedProperty<vec4> *interpretedCol4 = prop.second.As<TypedProperty<vec4>>()) {
         out_mat.AddProperty(&interpretedCol4.Value(), 1, name.c_str(), 0, 0);
     } else if (TypedProperty<float> *interpretedFloat = prop.second.As<TypedProperty<float>>()) {
         out_mat.AddProperty(&interpretedFloat.Value(), 1, name.c_str(), 0, 0);
     } else if (TypedProperty<int> *interpretedInt = prop.second.As<TypedProperty<int>>()) {
         out_mat.AddProperty(&interpretedInt.Value(), 1, name.c_str(), 0, 0);
     } else if (TypedProperty<bool> *interpretedBool = prop.second.As<TypedProperty<bool>>()) {
         int value = interpretedBool.Value() ? 1 : 0;
         out_mat.AddProperty(&value, 1, name.c_str(), 0, 0);
     } else if (TypedProperty<string> *interpretedString = prop.second.As<TypedProperty<string>>()) {
         string value = string(interpretedString.Value());
         out_mat.AddProperty(&value, name.c_str(), 0, 0);
     }
 }

 // Add the textures' properties

 for (TextureMap.const_iterator it = _textures.begin(); it != _textures.end(); ++it) {

     string name = prefix + it.first;

     FBXTexture *tex = it.second;
     if (tex != null) {
         string path;
         path.Set(tex.RelativeFilename());

         FBXVideo *media = tex.Media();
         if (media != null && media.ContentLength() > 0) {
             uint index;

             Dictionary<FBXVideo, uint>.const_iterator videoIt = textures_converted.find(media);
             if (videoIt != textures_converted.end()) {
                 index = videoIt.second;
             } else {
                 index = ConvertVideo(*media);
                 textures_converted[media] = index;
             }

             // setup texture reference string (copied from ColladaLoader.FindFilenameForEffectTexture)
             path.data[0] = '*';
             path.length = 1 + ASSIMP_itoa10(path.data + 1, AI_MAXLEN - 1, index);
         }

         out_mat.AddProperty(&path, (name + "|file").c_str(), aiTextureType_UNKNOWN, 0);

         aiUVTransform uvTrafo;
         // XXX handle all kinds of UV transformations
         uvTrafo.mScaling = tex.UVScaling();
         uvTrafo.mTranslation = tex.UVTranslation();
         uvTrafo.mRotation = tex.UVRotation();
         out_mat.AddProperty(&uvTrafo, 1, (name + "|uvtrafo").c_str(), aiTextureType_UNKNOWN, 0);

         int uvIndex = 0;

         bool uvFound = false;
         string &uvSet = PropertyGet<string>(tex.Props(), "UVSet", uvFound);
         if (uvFound) {
             // "default" is the name which usually appears in the FbxFileTexture template
             if (uvSet != "default" && uvSet.length()) {
                 // this is a bit awkward - we need to find a mesh that uses this
                 // material and scan its UV channels for the given UV name because
                 // assimp references UV channels by index, not by name.

                 // XXX: the case that UV channels may appear in different orders
                 // in meshes is unhandled. A possible solution would be to sort
                 // the UV channels alphabetically, but this would have the side
                 // effect that the primary (first) UV channel would sometimes
                 // be moved, causing trouble when users read only the first
                 // UV channel and ignore UV channel assignments altogether.

                 List<aiMaterial *>.iterator materialIt = std.find(materials.begin(), materials.end(), out_mat);
                 uint matIndex = static_cast<uint>(std.distance(materials.begin(), materialIt));

                 uvIndex = -1;
                 if (!mesh) {
                     for (Dictionary<Geometry, List<uint>>.value_type &v : meshes_converted) {
                         FBXMeshGeometry *meshGeom = dynamic_cast<FBXMeshGeometry *>(v.first);
                         if (!meshGeom) {
                             continue;
                         }

                         List<int> &mats = meshGeom.GetMaterialIndices();
                         if (std.find(mats.begin(), mats.end(), (int)matIndex) == mats.end()) {
                             continue;
                         }

                         int index = -1;
                         for (uint i = 0; i < AI_MAX_NUMBER_OF_TEXTURECOORDS; ++i) {
                             if (meshGeom.GetTextureCoords(i).empty()) {
                                 break;
                             }
                             string &curName = meshGeom.GetTextureCoordChannelName(i);
                             if (curName == uvSet) {
                                 index = static_cast<int>(i);
                                 break;
                             }
                         }
                         if (index == -1) {
                             FBXImporter.LogWarn("did not find UV channel named ", uvSet, " in a mesh using this material");
                             continue;
                         }

                         if (uvIndex == -1) {
                             uvIndex = index;
                         } else {
                             FBXImporter.LogWarn("the UV channel named ", uvSet, " appears at different positions in meshes, results will be wrong");
                         }
                     }
                 } else {
                     int index = -1;
                     for (uint i = 0; i < AI_MAX_NUMBER_OF_TEXTURECOORDS; ++i) {
                         if (mesh.GetTextureCoords(i).empty()) {
                             break;
                         }
                         string &curName = mesh.GetTextureCoordChannelName(i);
                         if (curName == uvSet) {
                             index = static_cast<int>(i);
                             break;
                         }
                     }
                     if (index == -1) {
                         FBXImporter.LogWarn("did not find UV channel named ", uvSet, " in a mesh using this material");
                     }

                     if (uvIndex == -1) {
                         uvIndex = index;
                     }
                 }

                 if (uvIndex == -1) {
                     FBXImporter.LogWarn("failed to resolve UV channel ", uvSet, ", using first UV channel");
                     uvIndex = 0;
                 }
             }
         }

         out_mat.AddProperty(&uvIndex, 1, (name + "|uvwsrc").c_str(), aiTextureType_UNKNOWN, 0);
     }
 }

			}

            // ------------------------------------------------------------------------------------------------
            // get the number of fps for a FrameRate enumerated value
            static double FrameRateToDouble(FBXFileGlobalSettings::FrameRate fp, double customFPSVal = -1.0){
				switch (fp) {
case FBXFileGlobalSettings.FrameRate_DEFAULT:
    return 1.0;

case FBXFileGlobalSettings.FrameRate_120:
    return 120.0;

case FBXFileGlobalSettings.FrameRate_100:
    return 100.0;

case FBXFileGlobalSettings.FrameRate_60:
    return 60.0;

case FBXFileGlobalSettings.FrameRate_50:
    return 50.0;

case FBXFileGlobalSettings.FrameRate_48:
    return 48.0;

case FBXFileGlobalSettings.FrameRate_30:
case FBXFileGlobalSettings.FrameRate_30_DROP:
    return 30.0;

case FBXFileGlobalSettings.FrameRate_NTSC_DROP_FRAME:
case FBXFileGlobalSettings.FrameRate_NTSC_FULL_FRAME:
    return 29.9700262;

case FBXFileGlobalSettings.FrameRate_PAL:
    return 25.0;

case FBXFileGlobalSettings.FrameRate_CINEMA:
    return 24.0;

case FBXFileGlobalSettings.FrameRate_1000:
    return 1000.0;

case FBXFileGlobalSettings.FrameRate_CINEMA_ND:
    return 23.976;

case FBXFileGlobalSettings.FrameRate_CUSTOM:
    return customFPSVal;

case FBXFileGlobalSettings.FrameRate_MAX: // this is to silence compiler warnings
    break;
}

System.Diagnostics.Debug.Assert(false);

return -1.0f;

			}

            // ------------------------------------------------------------------------------------------------
            // convert animation data to aiAnimation et al
            void ConvertAnimations(){
				// first of all determine framerate
FBXFileGlobalSettings.FrameRate fps = doc.GlobalSettings().TimeMode();
float custom = doc.GlobalSettings().CustomFrameRate();
anim_fps = FrameRateToDouble(fps, custom);

List<FBXAnimationStack *> &curAnimations = doc.AnimationStacks();
for (FBXAnimationStack *stack : curAnimations) {
    ConvertAnimationStack(*stack);
}

			}

            // ------------------------------------------------------------------------------------------------
            // takes a fbx node name and returns the identifier to be used in the assimp output scene.
            // the function is guaranteed to provide consistent results over multiple invocations
            // UNLESS RenameNode() is called for a particular node name.
            string FixNodeName(string &name){
				// strip Model. prefix, avoiding ambiguities (i.e. don't strip if
// this causes ambiguities, well possible between empty identifiers,
// such as "Model." and ""). Make sure the behaviour is consistent
// across multiple calls to FixNodeName().
if (name.substr(0, 7) == "Model.") {
    string temp = name.substr(7);
    return temp;
}

return name;

			}
            string FixAnimMeshName(string &name){
				 if (name.length()) {
     int indexOf = name.find_first_of(".");
     if (indexOf != string.npos && indexOf < name.Count - 2) {
         return name.substr(indexOf + 2);
     }
 }
 return name.length() ? name : "AnimMesh";

			}

            typedef SortedDictionary<FBXAnimationCurveNode *, FBXAnimationLayer *> LayerMap;

            // XXX: better use multi_map ..
            typedef SortedDictionary<string, List<FBXAnimationCurveNode*>> NodeMap;

            // ------------------------------------------------------------------------------------------------
            void ConvertAnimationStack(FBXAnimationStack &st){
				 AnimationLayerList &layers = st.Layers();
 if (layers.empty()) {
     return;
 }

 aiAnimation *anim = new aiAnimation();
 animations.push_back(anim);

 // strip AnimationStack. prefix
 string name = st.Name();
 if (name.substr(0, 16) == "AnimationStack.") {
     name = name.substr(16);
 } else if (name.substr(0, 11) == "AnimStack.") {
     name = name.substr(11);
 }

 anim.mName.Set(name);

 // need to find all nodes for which we need to generate node animations -
 // it may happen that we need to merge multiple layers, though.
 NodeMap node_map;

 // reverse mapping from curves to layers, much faster than querying
 // the FBX DOM for it.
 LayerMap layer_map;

 char *prop_whitelist[] = {
     "Lcl Scaling",
     "Lcl Rotation",
     "Lcl Translation",
     "DeformPercent"
 };

 SortedDictionary<string, morphAnimData *> morphAnimDatas;

 for (FBXAnimationLayer *layer : layers) {
     System.Diagnostics.Debug.Assert(layer);
     List<FBXAnimationCurveNode> &nodes = layer.Nodes(prop_whitelist, 4);
     for (FBXAnimationCurveNode node : nodes) {
         System.Diagnostics.Debug.Assert(node);
         FBXModel *model = dynamic_cast<FBXModel *>(node.Target());
         if (model) {
             string &curName = FixNodeName(model.Name());
             node_map[curName].push_back(node);
             layer_map[node] = layer;
             continue;
         }
         FBXBlendShapeChannel *bsc = dynamic_cast<FBXBlendShapeChannel *>(node.Target());
         if (bsc) {
             ProcessMorphAnimDatas(&morphAnimDatas, bsc, node);
         }
     }
 }

 // generate node animations
 List<aiNodeAnim *> node_anims;

 double min_time = 1e10;
 double max_time = -1e10;

 Int64 start_time = st.LocalStart();
 Int64 stop_time = st.LocalStop();
 bool has_local_startstop = start_time != 0 || stop_time != 0;
 if (!has_local_startstop) {
     // no time range given, so accept every keyframe and use the actual min/max time
     // the numbers are INT64_MIN/MAX, the 20000 is for safety because GenerateNodeAnimations uses an epsilon of 10000
     start_time = -9223372036854775807ll + 20000;
     stop_time = 9223372036854775807ll - 20000;
 }

 try {
     for (NodeMap.value_type &kv : node_map) {
         GenerateNodeAnimations(node_anims,
                 kv.first,
                 kv.second,
                 layer_map,
                 start_time, stop_time,
                 max_time,
                 min_time);
     }
 } catch (std.exception &) {
     std.for_each(node_anims.begin(), node_anims.end(), Util.delete_fun<aiNodeAnim>());
     throw;
 }

 if (node_anims.Count || morphAnimDatas.Count) {
     if (node_anims.Count) {
         anim.mChannels = new aiNodeAnim *[node_anims.Count]();
         anim.mNumChannels = static_cast<uint>(node_anims.Count);
         std.swap_ranges(node_anims.begin(), node_anims.end(), anim.mChannels);
     }
     if (morphAnimDatas.Count) {
         uint numMorphMeshChannels = static_cast<uint>(morphAnimDatas.Count);
         anim.mMorphMeshChannels = new aiMeshMorphAnim *[numMorphMeshChannels];
         anim.mNumMorphMeshChannels = numMorphMeshChannels;
         uint i = 0;
         for (auto &morphAnimIt : morphAnimDatas) {
             morphAnimData *animData = morphAnimIt.second;
             uint numKeys = static_cast<uint>(animData.Count);
             aiMeshMorphAnim *meshMorphAnim = new aiMeshMorphAnim();
             meshMorphAnim.mName.Set(morphAnimIt.first);
             meshMorphAnim.mNumKeys = numKeys;
             meshMorphAnim.mKeys = new aiMeshMorphKey[numKeys];
             uint j = 0;
             for (auto &animIt : *animData) {
                 morphKeyData *keyData = animIt.second;
                 uint numValuesAndWeights = static_cast<uint>(keyData.values.Count);
                 meshMorphAnim.mKeys[j].mNumValuesAndWeights = numValuesAndWeights;
                 meshMorphAnim.mKeys[j].mValues = new uint[numValuesAndWeights];
                 meshMorphAnim.mKeys[j].mWeights = new double[numValuesAndWeights];
                 meshMorphAnim.mKeys[j].mTime = CONVERT_FBX_TIME(animIt.first) * anim_fps;
                 for (uint k = 0; k < numValuesAndWeights; k++) {
                     meshMorphAnim.mKeys[j].mValues[k] = keyData.values.at(k);
                     meshMorphAnim.mKeys[j].mWeights[k] = keyData.weights.at(k);
                 }
                 j++;
             }
             anim.mMorphMeshChannels[i++] = meshMorphAnim;
         }
     }
 } else {
     // empty animations would fail validation, so drop them
     delete anim;
     animations.pop_back();
     FBXImporter.LogInfo("ignoring empty AnimationStack (using IK?): ", name);
     return;
 }

 double start_time_fps = has_local_startstop ? (CONVERT_FBX_TIME(start_time) * anim_fps) : min_time;
 double stop_time_fps = has_local_startstop ? (CONVERT_FBX_TIME(stop_time) * anim_fps) : max_time;

 // adjust relative timing for animation
 for (uint c = 0; c < anim.mNumChannels; c++) {
     aiNodeAnim *channel = anim.mChannels[c];
     for (UInt32 i = 0; i < channel.mNumPositionKeys; i++) {
         channel.mPositionKeys[i].mTime -= start_time_fps;
     }
     for (UInt32 i = 0; i < channel.mNumRotationKeys; i++) {
         channel.mRotationKeys[i].mTime -= start_time_fps;
     }
     for (UInt32 i = 0; i < channel.mNumScalingKeys; i++) {
         channel.mScalingKeys[i].mTime -= start_time_fps;
     }
 }
 for (uint c = 0; c < anim.mNumMorphMeshChannels; c++) {
     aiMeshMorphAnim *channel = anim.mMorphMeshChannels[c];
     for (UInt32 i = 0; i < channel.mNumKeys; i++) {
         channel.mKeys[i].mTime -= start_time_fps;
     }
 }

 // for some mysterious reason, mDuration is simply the maximum key -- the
 // validator always assumes animations to start at zero.
 anim.mDuration = stop_time_fps - start_time_fps;
 anim.mTicksPerSecond = anim_fps;

			}

            // ------------------------------------------------------------------------------------------------
            void ProcessMorphAnimDatas(SortedDictionary<string, morphAnimData*>* morphAnimDatas,
                    FBXBlendShapeChannel* bsc, FBXAnimationCurveNode* node){
						List<FBXConnection> bscConnections = doc.GetConnectionsBySourceSequenced(bsc.ID(), "Deformer");
for (FBXConnection bscConnection : bscConnections) {
    auto bs = dynamic_cast<FBXBlendShape *>(bscConnection.DestinationObject());
    if (bs) {
        auto channelIt = std.find(bs.BlendShapeChannels().begin(), bs.BlendShapeChannels().end(), bsc);
        if (channelIt != bs.BlendShapeChannels().end()) {
            auto channelIndex = static_cast<uint>(std.distance(bs.BlendShapeChannels().begin(), channelIt));
            List<FBXConnection> bsConnections = doc.GetConnectionsBySourceSequenced(bs.ID(), "Geometry");
            for (FBXConnection bsConnection : bsConnections) {
                auto geo = dynamic_cast<Geometry *>(bsConnection.DestinationObject());
                if (geo) {
                    List<FBXConnection> geoConnections = doc.GetConnectionsBySourceSequenced(geo.ID(), "Model");
                    for (FBXConnection geoConnection : geoConnections) {
                        auto model = dynamic_cast<FBXModel *>(geoConnection.DestinationObject());
                        if (model) {
                            auto geoIt = std.find(model.GetGeometry().begin(), model.GetGeometry().end(), geo);
                            auto geoIndex = static_cast<uint>(std.distance(model.GetGeometry().begin(), geoIt));
                            auto name = string(FixNodeName(model.Name() + "*"));
                            name.length = 1 + ASSIMP_itoa10(name.data + name.length, AI_MAXLEN - 1, geoIndex);
                            morphAnimData *animData;
                            auto animIt = morphAnimDatas.find(name.C_Str());
                            if (animIt == morphAnimDatas.end()) {
                                animData = new morphAnimData();
                                morphAnimDatas.insert(std.make_pair(name.C_Str(), animData));
                            } else {
                                animData = animIt.second;
                            }
                            for (std.pair<string, FBXAnimationCurve *> curvesIt : node.Curves()) {
                                if (curvesIt.first == "d|DeformPercent") {
                                    FBXAnimationCurve *animationCurve = curvesIt.second;
                                    List<Int64> &keys = animationCurve.GetKeys();
                                    List<float> &values = animationCurve.GetValues();
                                    uint k = 0;
                                    for (auto key : keys) {
                                        morphKeyData *keyData;
                                        auto keyIt = animData.find(key);
                                        if (keyIt == animData.end()) {
                                            keyData = new morphKeyData();
                                            animData.insert(std.make_pair(key, keyData));
                                        } else {
                                            keyData = keyIt.second;
                                        }
                                        keyData.values.push_back(channelIndex);
                                        keyData.weights.push_back(values.at(k) / 100.0f);
                                        k++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

					}

            // ------------------------------------------------------------------------------------------------
            void GenerateNodeAnimations(List<aiNodeAnim*> &node_anims,
                string &fixed_name,
                List<FBXAnimationCurveNode*> & curves,
                LayerMap & layer_map,
                Int64 start, Int64 stop,
                double &max_time,
                double &min_time){
					
    NodeMap node_property_map;
    System.Diagnostics.Debug.Assert(curves.Count);

#ifdef ASSIMP_BUILD_DEBUG
    validateAnimCurveNodes(curves, doc.Settings().strictMode);
#endif
    FBXAnimationCurveNode curve_node = null;
    for (FBXAnimationCurveNode node : curves) {
        System.Diagnostics.Debug.Assert(node);

        if (node.TargetProperty().empty()) {
            FBXImporter.LogWarn("target property for animation curve not set: ", node.Name());
            continue;
        }

        curve_node = node;
        if (node.Curves().empty()) {
            FBXImporter.LogWarn("no animation curves assigned to AnimationCurveNode: ", node.Name());
            continue;
        }

        node_property_map[node.TargetProperty()].push_back(node);
    }

    System.Diagnostics.Debug.Assert(curve_node);
    System.Diagnostics.Debug.Assert(curve_node.TargetAsModel());

    FBXModel &target = *curve_node.TargetAsModel();

    // check for all possible transformation components
    NodeMap.const_iterator chain[TransformationComp_MAXIMUM];

    bool has_any = false;
    bool has_complex = false;

    for (int i = 0; i < TransformationComp_MAXIMUM; ++i) {
        TransformationComp comp = static_cast<TransformationComp>(i);

        // inverse pivots don't exist in the input, we just generate them
        if (comp == TransformationComp_RotationPivotInverse || comp == TransformationComp_ScalingPivotInverse) {
            chain[i] = node_property_map.end();
            continue;
        }

        chain[i] = node_property_map.find(NameTransformationCompProperty(comp));
        if (chain[i] != node_property_map.end()) {

            // check if this curves contains redundant information by looking
            // up the corresponding node's transformation chain.
            if (doc.Settings().optimizeEmptyAnimationCurves &&
                    IsRedundantAnimationData(target, comp, (chain[i].second))) {

                FBXImporter.LogVerboseDebug("dropping redundant animation channel for node ", target.Name());
                continue;
            }

            has_any = true;

            if (comp != TransformationComp_Rotation && comp != TransformationComp_Scaling && comp != TransformationComp_Translation) {
                has_complex = true;
            }
        }
    }

    if (!has_any) {
        FBXImporter.LogWarn("ignoring node animation, did not find any transformation key frames");
        return;
    }

    // this needs to play nicely with GenerateTransformationNodeChain() which will
    // be invoked _later_ (animations come first). If this node has only rotation,
    // scaling and translation _and_ there are no animated other components either,
    // we can use a single node and also a single node animation channel.
    if (!has_complex && !NeedsComplexTransformationChain(target)) {
        aiNodeAnim *nd = GenerateSimpleNodeAnim(fixed_name, target, chain,
                node_property_map.end(),
                start, stop,
                max_time,
                min_time);

        System.Diagnostics.Debug.Assert(nd);
        if (nd.mNumPositionKeys == 0 && nd.mNumRotationKeys == 0 && nd.mNumScalingKeys == 0) {
            delete nd;
        } else {
            node_anims.push_back(nd);
        }
        return;
    }

    // otherwise, things get gruesome and we need separate animation channels
    // for each part of the transformation chain. Remember which channels
    // we generated and pass this information to the node conversion
    // code to avoid nodes that have identity transform, but non-identity
    // animations, being dropped.
    uint flags = 0, bit = 0x1;
    for (int i = 0; i < TransformationComp_MAXIMUM; ++i, bit <<= 1) {
        TransformationComp comp = static_cast<TransformationComp>(i);

        if (chain[i] != node_property_map.end()) {
            flags |= bit;

            System.Diagnostics.Debug.Assert(comp != TransformationComp_RotationPivotInverse);
            System.Diagnostics.Debug.Assert(comp != TransformationComp_ScalingPivotInverse);

            string &chain_name = NameTransformationChainNode(fixed_name, comp);

            aiNodeAnim *na = null;
            switch (comp) {
            case TransformationComp_Rotation:
            case TransformationComp_PreRotation:
            case TransformationComp_PostRotation:
            case TransformationComp_GeometricRotation:
                na = GenerateRotationNodeAnim(chain_name,
                        target,
                        (*chain[i]).second,
                        layer_map,
                        start, stop,
                        max_time,
                        min_time);

                break;

            case TransformationComp_RotationOffset:
            case TransformationComp_RotationPivot:
            case TransformationComp_ScalingOffset:
            case TransformationComp_ScalingPivot:
            case TransformationComp_Translation:
            case TransformationComp_GeometricTranslation:
                na = GenerateTranslationNodeAnim(chain_name,
                        target,
                        (*chain[i]).second,
                        layer_map,
                        start, stop,
                        max_time,
                        min_time);

                // pivoting requires us to generate an implicit inverse channel to undo the pivot translation
                if (comp == TransformationComp_RotationPivot) {
                    string &invName = NameTransformationChainNode(fixed_name,
                            TransformationComp_RotationPivotInverse);

                    aiNodeAnim *inv = GenerateTranslationNodeAnim(invName,
                            target,
                            (*chain[i]).second,
                            layer_map,
                            start, stop,
                            max_time,
                            min_time,
                            true);

                    System.Diagnostics.Debug.Assert(inv);
                    if (inv.mNumPositionKeys == 0 && inv.mNumRotationKeys == 0 && inv.mNumScalingKeys == 0) {
                        delete inv;
                    } else {
                        node_anims.push_back(inv);
                    }

                    System.Diagnostics.Debug.Assert(TransformationComp_RotationPivotInverse > i);
                    flags |= bit << (TransformationComp_RotationPivotInverse - i);
                } else if (comp == TransformationComp_ScalingPivot) {
                    string &invName = NameTransformationChainNode(fixed_name,
                            TransformationComp_ScalingPivotInverse);

                    aiNodeAnim *inv = GenerateTranslationNodeAnim(invName,
                            target,
                            (*chain[i]).second,
                            layer_map,
                            start, stop,
                            max_time,
                            min_time,
                            true);

                    System.Diagnostics.Debug.Assert(inv);
                    if (inv.mNumPositionKeys == 0 && inv.mNumRotationKeys == 0 && inv.mNumScalingKeys == 0) {
                        delete inv;
                    } else {
                        node_anims.push_back(inv);
                    }

                    System.Diagnostics.Debug.Assert(TransformationComp_RotationPivotInverse > i);
                    flags |= bit << (TransformationComp_RotationPivotInverse - i);
                }

                break;

            case TransformationComp_Scaling:
            case TransformationComp_GeometricScaling:
                na = GenerateScalingNodeAnim(chain_name,
                        target,
                        (*chain[i]).second,
                        layer_map,
                        start, stop,
                        max_time,
                        min_time);

                break;

            default:
                System.Diagnostics.Debug.Assert(false);
            }

            System.Diagnostics.Debug.Assert(na);
            if (na.mNumPositionKeys == 0 && na.mNumRotationKeys == 0 && na.mNumScalingKeys == 0) {
                delete na;
            } else {
                node_anims.push_back(na);
            }
            continue;
        }
    }

    node_anim_chain_bits[fixed_name] = flags;

				}

            // ------------------------------------------------------------------------------------------------
            bool IsRedundantAnimationData(FBXModel &target,
                TransformationComp comp,
                List< FBXAnimationCurveNode *> &curves){
					System.Diagnostics.Debug.Assert(curves.Count);

// look for animation nodes with
//  * sub channels for all relevant components set
//  * one key/value pair per component
//  * combined values match up the corresponding value in the bind pose node transformation
// only such nodes are 'redundant' for this function.

if (curves.Count > 1) {
    return false;
}

FBXAnimationCurveNode &nd = *curves.front();
AnimationCurveMap &sub_curves = nd.Curves();

AnimationCurveMap.const_iterator dx = sub_curves.find("d|X");
AnimationCurveMap.const_iterator dy = sub_curves.find("d|Y");
AnimationCurveMap.const_iterator dz = sub_curves.find("d|Z");

if (dx == sub_curves.end() || dy == sub_curves.end() || dz == sub_curves.end()) {
    return false;
}

List<float> &vx = (*dx).second.GetValues();
List<float> &vy = (*dy).second.GetValues();
List<float> &vz = (*dz).second.GetValues();

if (vx.Count != 1 || vy.Count != 1 || vz.Count != 1) {
    return false;
}

vec3 dyn_val = vec3(vx[0], vy[0], vz[0]);
vec3 &static_val = PropertyGet<vec3>(target.Props(),
        NameTransformationCompProperty(comp),
        TransformationCompDefaultValue(comp));

float epsilon = Math.getEpsilon<float>();
return (dyn_val - static_val).SquareLength() < epsilon;

				}

            // ------------------------------------------------------------------------------------------------
            aiNodeAnim* GenerateRotationNodeAnim(string &name,
                FBXModel & target,
                List<FBXAnimationCurveNode*> & curves,
                LayerMap & layer_map,
                Int64 start, Int64 stop,
                double &max_time,
                double &min_time){
					std.unique_ptr<aiNodeAnim> na(new aiNodeAnim());
na.mNodeName.Set(name);

ConvertRotationKeys(na.get(), curves, layer_map, start, stop, max_time, min_time, target.RotationOrder());

// dummy scaling key
na.mScalingKeys = new aiVectorKey[1];
na.mNumScalingKeys = 1;

na.mScalingKeys[0].mTime = 0.;
na.mScalingKeys[0].mValue = vec3(1.0f, 1.0f, 1.0f);

// dummy position key
na.mPositionKeys = new aiVectorKey[1];
na.mNumPositionKeys = 1;

na.mPositionKeys[0].mTime = 0.;
na.mPositionKeys[0].mValue = vec3();

return na.release();

				}

            // ------------------------------------------------------------------------------------------------
            aiNodeAnim* GenerateScalingNodeAnim(string &name,
                FBXModel & /*target*/,
                List<FBXAnimationCurveNode*> & curves,
                LayerMap & layer_map,
                Int64 start, Int64 stop,
                double &max_time,
                double &min_time){
					std.unique_ptr<aiNodeAnim> na(new aiNodeAnim());
na.mNodeName.Set(name);

ConvertScaleKeys(na.get(), curves, layer_map, start, stop, max_time, min_time);

// dummy rotation key
na.mRotationKeys = new aiQuatKey[1];
na.mNumRotationKeys = 1;

na.mRotationKeys[0].mTime = 0.;
na.mRotationKeys[0].mValue = aiQuaternion();

// dummy position key
na.mPositionKeys = new aiVectorKey[1];
na.mNumPositionKeys = 1;

na.mPositionKeys[0].mTime = 0.;
na.mPositionKeys[0].mValue = vec3();

return na.release();

				}

            // ------------------------------------------------------------------------------------------------
            aiNodeAnim* GenerateTranslationNodeAnim(string &name,
                FBXModel & /*target*/,
                List<FBXAnimationCurveNode*> & curves,
                LayerMap & layer_map,
                Int64 start, Int64 stop,
                double &max_time,
                double &min_time,
                bool inverse = false){
				std.unique_ptr<aiNodeAnim> na(new aiNodeAnim());
na.mNodeName.Set(name);

ConvertTranslationKeys(na.get(), curves, layer_map, start, stop, max_time, min_time);

if (inverse) {
    for (uint i = 0; i < na.mNumPositionKeys; ++i) {
        na.mPositionKeys[i].mValue *= -1.0f;
    }
}

// dummy scaling key
na.mScalingKeys = new aiVectorKey[1];
na.mNumScalingKeys = 1;

na.mScalingKeys[0].mTime = 0.;
na.mScalingKeys[0].mValue = vec3(1.0f, 1.0f, 1.0f);

// dummy rotation key
na.mRotationKeys = new aiQuatKey[1];
na.mNumRotationKeys = 1;

na.mRotationKeys[0].mTime = 0.;
na.mRotationKeys[0].mValue = aiQuaternion();

return na.release();

			}

            // ------------------------------------------------------------------------------------------------
            // generate node anim, extracting only Rotation, Scaling and Translation from the given chain
            aiNodeAnim* GenerateSimpleNodeAnim(string &name,
                FBXModel & target,
                NodeMap::const_iterator chain[TransformationComp_MAXIMUM],
                NodeMap::const_iterator iterEnd,
                Int64 start, Int64 stop,
                double &maxTime,
                double &minTime){
					std.unique_ptr<aiNodeAnim> na(new aiNodeAnim());
na.mNodeName.Set(name);

PropertyTable &props = target.Props();

// collect unique times and keyframe lists
KeyFrameListList keyframeLists[TransformationComp_MAXIMUM];
List<Int64> keytimes;

for (int i = 0; i < TransformationComp_MAXIMUM; ++i) {
    if (chain[i] == iterEnd)
        continue;

    if (i == TransformationComp_Rotation || i == TransformationComp_PreRotation || i == TransformationComp_PostRotation || i == TransformationComp_GeometricRotation) {
        keyframeLists[i] = GetRotationKeyframeList((*chain[i]).second, start, stop);
    } else {
        keyframeLists[i] = GetKeyframeList((*chain[i]).second, start, stop);
    }

    for (KeyFrameListList.const_iterator it = keyframeLists[i].begin(); it != keyframeLists[i].end(); ++it) {
        List<Int64> &times = *std.get<0>(*it);
        keytimes.insert(keytimes.end(), times.begin(), times.end());
    }

    // remove duplicates
    std.sort(keytimes.begin(), keytimes.end());

    auto last = std.unique(keytimes.begin(), keytimes.end());
    keytimes.erase(last, keytimes.end());
}

FBXModel.RotOrder rotOrder = target.RotationOrder();
int keyCount = keytimes.Count;

vec3 defTranslate = PropertyGet(props, "Lcl Translation", vec3(0.f, 0.f, 0.f));
vec3 defRotation = PropertyGet(props, "Lcl Rotation", vec3(0.f, 0.f, 0.f));
vec3 defScale = PropertyGet(props, "Lcl Scaling", vec3(1.f, 1.f, 1.f));

aiVectorKey *outTranslations = new aiVectorKey[keyCount];
aiQuatKey *outRotations = new aiQuatKey[keyCount];
aiVectorKey *outScales = new aiVectorKey[keyCount];

if (keyframeLists[TransformationComp_Translation].Count > 0) {
    InterpolateKeys(outTranslations, keytimes, keyframeLists[TransformationComp_Translation], defTranslate, maxTime, minTime);
} else {
    for (int i = 0; i < keyCount; ++i) {
        outTranslations[i].mTime = CONVERT_FBX_TIME(keytimes[i]) * anim_fps;
        outTranslations[i].mValue = defTranslate;
    }
}

if (keyframeLists[TransformationComp_Rotation].Count > 0) {
    InterpolateKeys(outRotations, keytimes, keyframeLists[TransformationComp_Rotation], defRotation, maxTime, minTime, rotOrder);
} else {
    aiQuaternion defQuat = EulerToQuaternion(defRotation, rotOrder);
    for (int i = 0; i < keyCount; ++i) {
        outRotations[i].mTime = CONVERT_FBX_TIME(keytimes[i]) * anim_fps;
        outRotations[i].mValue = defQuat;
    }
}

if (keyframeLists[TransformationComp_Scaling].Count > 0) {
    InterpolateKeys(outScales, keytimes, keyframeLists[TransformationComp_Scaling], defScale, maxTime, minTime);
} else {
    for (int i = 0; i < keyCount; ++i) {
        outScales[i].mTime = CONVERT_FBX_TIME(keytimes[i]) * anim_fps;
        outScales[i].mValue = defScale;
    }
}

bool ok = false;

auto zero_epsilon = ai_epsilon;

vec3 &preRotation = PropertyGet<vec3>(props, "PreRotation", ok);
if (ok && preRotation.SquareLength() > zero_epsilon) {
    aiQuaternion preQuat = EulerToQuaternion(preRotation, FBXModel.RotOrder_EulerXYZ);
    for (int i = 0; i < keyCount; ++i) {
        outRotations[i].mValue = preQuat * outRotations[i].mValue;
    }
}

vec3 &postRotation = PropertyGet<vec3>(props, "PostRotation", ok);
if (ok && postRotation.SquareLength() > zero_epsilon) {
    aiQuaternion postQuat = EulerToQuaternion(postRotation, FBXModel.RotOrder_EulerXYZ);
    for (int i = 0; i < keyCount; ++i) {
        outRotations[i].mValue = outRotations[i].mValue * postQuat;
    }
}

// convert TRS to SRT
for (int i = 0; i < keyCount; ++i) {
    aiQuaternion &r = outRotations[i].mValue;
    vec3 &s = outScales[i].mValue;
    vec3 &t = outTranslations[i].mValue;

    mat4 mat, temp;
    mat4.Translation(t, mat);
    mat *= mat4(r.GetMatrix());
    mat *= mat4.Scaling(s, temp);

    mat.Decompose(s, r, t);
}

na.mNumScalingKeys = static_cast<uint>(keyCount);
na.mNumRotationKeys = na.mNumScalingKeys;
na.mNumPositionKeys = na.mNumScalingKeys;

na.mScalingKeys = outScales;
na.mRotationKeys = outRotations;
na.mPositionKeys = outTranslations;

return na.release();

				}

            // key (time), value, mapto (component index)
            typedef std::tuple<std::shared_ptr<KeyTimeList>, std::shared_ptr<KeyValueList>, uint> KeyFrameList;
            typedef List<KeyFrameList> KeyFrameListList;

            // ------------------------------------------------------------------------------------------------
            KeyFrameListList GetKeyframeList(List<FBXAnimationCurveNode*> &nodes, Int64 start, Int64 stop){
				KeyFrameListList inputs;
inputs.reserve(nodes.Count * 3);

// give some breathing room for rounding errors
Int64 adj_start = start - 10000;
Int64 adj_stop = stop + 10000;

for (FBXAnimationCurveNode node : nodes) {
    System.Diagnostics.Debug.Assert(node);

    AnimationCurveMap &curves = node.Curves();
    for (AnimationCurveMap.value_type &kv : curves) {

        uint mapto;
        if (kv.first == "d|X") {
            mapto = 0;
        } else if (kv.first == "d|Y") {
            mapto = 1;
        } else if (kv.first == "d|Z") {
            mapto = 2;
        } else {
            FBXImporter.LogWarn("ignoring scale animation curve, did not recognize target component");
            continue;
        }

        FBXAnimationCurve *curve = kv.second;
        System.Diagnostics.Debug.Assert(curve.GetKeys().Count == curve.GetValues().Count);
        System.Diagnostics.Debug.Assert(curve.GetKeys().Count);

        // get values within the start/stop time window
        std.shared_ptr<List<Int64>> Keys(new List<Int64>());
        std.shared_ptr<List<float>> Values(new List<float>());
        int count = curve.GetKeys().Count;
        Keys.reserve(count);
        Values.reserve(count);
        for (int n = 0; n < count; n++) {
            Int64 k = curve.GetKeys().at(n);
            if (k >= adj_start && k <= adj_stop) {
                Keys.push_back(k);
                Values.push_back(curve.GetValues().at(n));
            }
        }

        inputs.emplace_back(Keys, Values, mapto);
    }
}
return inputs; // pray for NRVO :-)

			}
            KeyFrameListList GetRotationKeyframeList(List<FBXAnimationCurveNode*> &nodes, Int64 start, Int64 stop){
				KeyFrameListList inputs;
inputs.reserve(nodes.Count * 3);

// give some breathing room for rounding errors
Int64 adj_start = start - 10000;
Int64 adj_stop = stop + 10000;

for (FBXAnimationCurveNode node : nodes) {
    System.Diagnostics.Debug.Assert(node);

    AnimationCurveMap &curves = node.Curves();
    for (AnimationCurveMap.value_type &kv : curves) {

        uint mapto;
        if (kv.first == "d|X") {
            mapto = 0;
        } else if (kv.first == "d|Y") {
            mapto = 1;
        } else if (kv.first == "d|Z") {
            mapto = 2;
        } else {
            FBXImporter.LogWarn("ignoring scale animation curve, did not recognize target component");
            continue;
        }

        FBXAnimationCurve *curve = kv.second;
        System.Diagnostics.Debug.Assert(curve.GetKeys().Count == curve.GetValues().Count);
        System.Diagnostics.Debug.Assert(curve.GetKeys().Count);

        // get values within the start/stop time window
        std.shared_ptr<List<Int64>> Keys(new List<Int64>());
        std.shared_ptr<List<float>> Values(new List<float>());
        int count = curve.GetKeys().Count;

        Int64 tp = curve.GetKeys().at(0);
        float vp = curve.GetValues().at(0);
        Keys.push_back(tp);
        Values.push_back(vp);
        if (count > 1) {
            Int64 tc = curve.GetKeys().at(1);
            float vc = curve.GetValues().at(1);
            for (int n = 1; n < count; n++) {
                while (std.abs(vc - vp) >= 180.0f) {
                    double step = std.floor(double(tc - tp) / std.abs(vc - vp) * 179.0f);
                    Int64 tnew = tp + Int64(step);
                    float vnew = vp + (vc - vp) * float(step / (tc - tp));
                    if (tnew >= adj_start && tnew <= adj_stop) {
                        Keys.push_back(tnew);
                        Values.push_back(vnew);
                    } else {
                        // Something broke
                        break;
                    }
                    tp = tnew;
                    vp = vnew;
                }
                if (tc >= adj_start && tc <= adj_stop) {
                    Keys.push_back(tc);
                    Values.push_back(vc);
                }
                if (n + 1 < count) {
                    tp = tc;
                    vp = vc;
                    tc = curve.GetKeys().at(n + 1);
                    vc = curve.GetValues().at(n + 1);
                }
            }
        }
        inputs.emplace_back(Keys, Values, mapto);
    }
}
return inputs;

			}

            // ------------------------------------------------------------------------------------------------
            KeyTimeList GetKeyTimeList(KeyFrameListList &inputs){
				System.Diagnostics.Debug.Assert(!inputs.empty());

// reserve some space upfront - it is likely that the key-frame lists
// have matching time values, so max(of all key-frame lists) should
// be a good estimate.
List<Int64> keys;

int estimate = 0;
for (KeyFrameList &kfl : inputs) {
    estimate = std.max(estimate, std.get<0>(kfl).Count);
}

keys.reserve(estimate);

List<uint> next_pos;
next_pos.resize(inputs.Count, 0);

int count = inputs.Count;
while (true) {

    Int64 min_tick = std.numeric_limits<Int64>.max();
    for (int i = 0; i < count; ++i) {
        KeyFrameList &kfl = inputs[i];

        if (std.get<0>(kfl).Count > next_pos[i] && std.get<0>(kfl).at(next_pos[i]) < min_tick) {
            min_tick = std.get<0>(kfl).at(next_pos[i]);
        }
    }

    if (min_tick == std.numeric_limits<Int64>.max()) {
        break;
    }
    keys.push_back(min_tick);

    for (int i = 0; i < count; ++i) {
        KeyFrameList &kfl = inputs[i];

        while (std.get<0>(kfl).Count > next_pos[i] && std.get<0>(kfl).at(next_pos[i]) == min_tick) {
            ++next_pos[i];
        }
    }
}

return keys;

			}

            // ------------------------------------------------------------------------------------------------
            void InterpolateKeys(aiVectorKey* valOut, KeyTimeList &keys, KeyFrameListList & inputs,
                vec3 & def_value,
                double &max_time,
                double &min_time){
					System.Diagnostics.Debug.Assert(!keys.empty());
System.Diagnostics.Debug.Assert(null != valOut);

List<uint> next_pos;
int count(inputs.Count);

next_pos.resize(inputs.Count, 0);

for (List<Int64>.value_type time : keys) {
    ai_real result[3] = { def_value.x, def_value.y, def_value.z };

    for (int i = 0; i < count; ++i) {
        KeyFrameList &kfl = inputs[i];

        int ksize = std.get<0>(kfl).Count;
        if (ksize == 0) {
            continue;
        }
        if (ksize > next_pos[i] && std.get<0>(kfl).at(next_pos[i]) == time) {
            ++next_pos[i];
        }

        int id0 = next_pos[i] > 0 ? next_pos[i] - 1 : 0;
        int id1 = next_pos[i] == ksize ? ksize - 1 : next_pos[i];

        // use lerp for interpolation
        List<float>.value_type valueA = std.get<1>(kfl).at(id0);
        List<float>.value_type valueB = std.get<1>(kfl).at(id1);

        List<Int64>.value_type timeA = std.get<0>(kfl).at(id0);
        List<Int64>.value_type timeB = std.get<0>(kfl).at(id1);

        ai_real factor = timeB == timeA ? ai_real(0.) : static_cast<ai_real>((time - timeA)) / (timeB - timeA);
        ai_real interpValue = static_cast<ai_real>(valueA + (valueB - valueA) * factor);

        result[std.get<2>(kfl)] = interpValue;
    }

    // magic value to convert fbx times to seconds
    valOut.mTime = CONVERT_FBX_TIME(time) * anim_fps;

    min_time = std.min(min_time, valOut.mTime);
    max_time = std.max(max_time, valOut.mTime);

    valOut.mValue.x = result[0];
    valOut.mValue.y = result[1];
    valOut.mValue.z = result[2];

    ++valOut;
}

				}
				

            // ------------------------------------------------------------------------------------------------
            void InterpolateKeys(aiQuatKey* valOut, KeyTimeList &keys, KeyFrameListList & inputs,
                vec3 & def_value,
                double &maxTime,
                double &minTime,
                FBXModel::RotOrder order){
					 System.Diagnostics.Debug.Assert(!keys.empty());
 System.Diagnostics.Debug.Assert(null != valOut);

 std.unique_ptr<aiVectorKey[]> temp(new aiVectorKey[keys.Count]);
 InterpolateKeys(temp.get(), keys, inputs, def_value, maxTime, minTime);

 mat4 m;

 aiQuaternion lastq;

 for (int i = 0, c = keys.Count; i < c; ++i) {

     valOut[i].mTime = temp[i].mTime;

     GetRotationMatrix(order, temp[i].mValue, m);
     aiQuaternion quat = aiQuaternion(aiMatrix3x3(m));

     // take shortest path by checking the inner product
     // http://www.3dkingdoms.com/weekly/weekly.php?a=36
     if (quat.x * lastq.x + quat.y * lastq.y + quat.z * lastq.z + quat.w * lastq.w < 0) {
         quat.Conjugate();
         quat.w = -quat.w;
     }
     lastq = quat;

     valOut[i].mValue = quat;
 }

				}

            // ------------------------------------------------------------------------------------------------
            // euler xyz . quat
            aiQuaternion EulerToQuaternion(vec3 &rot, FBXModel::RotOrder order){
				mat4 m;
GetRotationMatrix(order, rot, m);

return aiQuaternion(aiMatrix3x3(m));

			}

            // ------------------------------------------------------------------------------------------------
            void ConvertScaleKeys(aiNodeAnim* na, List<FBXAnimationCurveNode*> &nodes, LayerMap & /*layers*/,
                Int64 start, Int64 stop,
                double &maxTime,
                double &minTime){
					System.Diagnostics.Debug.Assert(nodes.Count);

// XXX for now, assume scale should be blended geometrically (i.e. two
// layers should be multiplied with each other). There is a FBX
// property in the layer to specify the behaviour, though.

KeyFrameListList &inputs = GetKeyframeList(nodes, start, stop);
List<Int64> &keys = GetKeyTimeList(inputs);

na.mNumScalingKeys = static_cast<uint>(keys.Count);
na.mScalingKeys = new aiVectorKey[keys.Count];
if (keys.Count > 0) {
    InterpolateKeys(na.mScalingKeys, keys, inputs, vec3(1.0f, 1.0f, 1.0f), maxTime, minTime);
}

				}

            // ------------------------------------------------------------------------------------------------
            void ConvertTranslationKeys(aiNodeAnim* na, List<FBXAnimationCurveNode*> &nodes,
                LayerMap & /*layers*/,
                Int64 start, Int64 stop,
                double &maxTime,
                double &minTime){
					System.Diagnostics.Debug.Assert(nodes.Count);

// XXX see notes in ConvertScaleKeys()
KeyFrameListList &inputs = GetKeyframeList(nodes, start, stop);
List<Int64> &keys = GetKeyTimeList(inputs);

na.mNumPositionKeys = static_cast<uint>(keys.Count);
na.mPositionKeys = new aiVectorKey[keys.Count];
if (keys.Count > 0)
    InterpolateKeys(na.mPositionKeys, keys, inputs, vec3(0.0f, 0.0f, 0.0f), maxTime, minTime);

				}

            // ------------------------------------------------------------------------------------------------
            void ConvertRotationKeys(aiNodeAnim* na, List<FBXAnimationCurveNode*> &nodes,
                LayerMap & /*layers*/,
                Int64 start, Int64 stop,
                double &maxTime,
                double &minTime,
                FBXModel::RotOrder order){
					System.Diagnostics.Debug.Assert(nodes.Count);

// XXX see notes in ConvertScaleKeys()
List<KeyFrameList> &inputs = GetRotationKeyframeList(nodes, start, stop);
List<Int64> &keys = GetKeyTimeList(inputs);

na.mNumRotationKeys = static_cast<uint>(keys.Count);
na.mRotationKeys = new aiQuatKey[keys.Count];
if (!keys.empty()) {
    InterpolateKeys(na.mRotationKeys, keys, inputs, vec3(0.0f, 0.0f, 0.0f), maxTime, minTime, order);
}

				}

            // ------------------------------------------------------------------------------------------------
            // Copy global geometric data and some information about the source asset into scene metadata.
            void ConvertGlobalSettings(){
				if (null == mSceneOut) {
    return;
}

bool hasGenerator = !doc.Creator().empty();

mSceneOut.mMetaData = aiMetadata.Alloc(16 + (hasGenerator ? 1 : 0));
mSceneOut.mMetaData.Set(0, "UpAxis", doc.GlobalSettings().UpAxis());
mSceneOut.mMetaData.Set(1, "UpAxisSign", doc.GlobalSettings().UpAxisSign());
mSceneOut.mMetaData.Set(2, "FrontAxis", doc.GlobalSettings().FrontAxis());
mSceneOut.mMetaData.Set(3, "FrontAxisSign", doc.GlobalSettings().FrontAxisSign());
mSceneOut.mMetaData.Set(4, "CoordAxis", doc.GlobalSettings().CoordAxis());
mSceneOut.mMetaData.Set(5, "CoordAxisSign", doc.GlobalSettings().CoordAxisSign());
mSceneOut.mMetaData.Set(6, "OriginalUpAxis", doc.GlobalSettings().OriginalUpAxis());
mSceneOut.mMetaData.Set(7, "OriginalUpAxisSign", doc.GlobalSettings().OriginalUpAxisSign());
// double unitScaleFactor = (double)doc.GlobalSettings().UnitScaleFactor();
mSceneOut.mMetaData.Set(8, "UnitScaleFactor", doc.GlobalSettings().UnitScaleFactor());
mSceneOut.mMetaData.Set(9, "OriginalUnitScaleFactor", doc.GlobalSettings().OriginalUnitScaleFactor());
mSceneOut.mMetaData.Set(10, "AmbientColor", doc.GlobalSettings().AmbientColor());
mSceneOut.mMetaData.Set(11, "FrameRate", (int)doc.GlobalSettings().TimeMode());
mSceneOut.mMetaData.Set(12, "TimeSpanStart", doc.GlobalSettings().TimeSpanStart());
mSceneOut.mMetaData.Set(13, "TimeSpanStop", doc.GlobalSettings().TimeSpanStop());
mSceneOut.mMetaData.Set(14, "CustomFrameRate", doc.GlobalSettings().CustomFrameRate());
mSceneOut.mMetaData.Set(15, AI_METADATA_SOURCE_FORMAT_VERSION, string(ai_to_string(doc.FBXVersion())));
if (hasGenerator) {
    mSceneOut.mMetaData.Set(16, AI_METADATA_SOURCE_GENERATOR, string(doc.Creator()));
}

			}

            // ------------------------------------------------------------------------------------------------
            // copy generated meshes, animations, lights, cameras and textures to the output scene
            void TransferDataToScene(){
				System.Diagnostics.Debug.Assert(!mSceneOut.mMeshes);
System.Diagnostics.Debug.Assert(!mSceneOut.mNumMeshes);

// note: the trailing () ensures initialization with null - not
// many C++ users seem to know this, so pointing it out to avoid
// confusion why this code works.

if (!mMeshes.empty()) {
    mSceneOut.mMeshes = new aiMesh *[mMeshes.Count]();
    mSceneOut.mNumMeshes = static_cast<uint>(mMeshes.Count);

    std.swap_ranges(mMeshes.begin(), mMeshes.end(), mSceneOut.mMeshes);
}

if (!materials.empty()) {
    mSceneOut.mMaterials = new aiMaterial *[materials.Count]();
    mSceneOut.mNumMaterials = static_cast<uint>(materials.Count);

    std.swap_ranges(materials.begin(), materials.end(), mSceneOut.mMaterials);
}

if (!animations.empty()) {
    mSceneOut.mAnimations = new aiAnimation *[animations.Count]();
    mSceneOut.mNumAnimations = static_cast<uint>(animations.Count);

    std.swap_ranges(animations.begin(), animations.end(), mSceneOut.mAnimations);
}

if (!lights.empty()) {
    mSceneOut.mLights = new aiLight *[lights.Count]();
    mSceneOut.mNumLights = static_cast<uint>(lights.Count);

    std.swap_ranges(lights.begin(), lights.end(), mSceneOut.mLights);
}

if (!cameras.empty()) {
    mSceneOut.mCameras = new aiCamera *[cameras.Count]();
    mSceneOut.mNumCameras = static_cast<uint>(cameras.Count);

    std.swap_ranges(cameras.begin(), cameras.end(), mSceneOut.mCameras);
}

if (!textures.empty()) {
    mSceneOut.mTextures = new aiTexture *[textures.Count]();
    mSceneOut.mNumTextures = static_cast<uint>(textures.Count);

    std.swap_ranges(textures.begin(), textures.end(), mSceneOut.mTextures);
}

if (!mSkeletons.empty()) {
    mSceneOut.mSkeletons = new aiSkeleton[mSkeletons.Count];
    mSceneOut.mNumSkeletons = static_cast<uint>(mSkeletons.Count);
    std.swap_ranges(mSkeletons.begin(), mSkeletons.end(), mSceneOut.mSkeletons);
}

			}

            // ------------------------------------------------------------------------------------------------
            // FBX file could have embedded textures not connected to anything
            void ConvertOrphanedEmbeddedTextures(){
				// in C++14 it could be:
// for (auto&& [id, object] : objects)
for (auto &&id_and_object : doc.Objects()) {
    auto &&id = std.get<0>(id_and_object);
    auto &&object = std.get<1>(id_and_object);
    // If an object doesn't have parent
    if (doc.ConnectionsBySource().count(id) == 0) {
        FBXTexture *realTexture = null;
        try {
            auto &element = object.GetElement();
            Token &key = element.KeyToken();
            char *obtype = key.begin();
            int length = static_cast<int>(key.end() - key.begin());
            if (strncmp(obtype, "Texture", length) == 0) {
                if (FBXTexture *texture = static_cast<FBXTexture *>(object.Get())) {
                    if (texture.Media() && texture.Media().ContentLength() > 0) {
                        realTexture = texture;
                    }
                }
            }
        } catch (...) {
            // do nothing
        }
        if (realTexture) {
            FBXVideo *media = realTexture.Media();
            uint index = ConvertVideo(*media);
            textures_converted[media] = index;
        }
    }
}

			}
            // 0: not assigned yet, others: index is value - 1
            uint defaultMaterialIndex;

            List<aiMesh> mMeshes;
            List<aiMaterial*> materials;
            List<aiAnimation*> animations;
            List<aiLight*> lights;
            List<aiCamera*> cameras;
            List<aiTexture*> textures;

            Dictionary<FBXMaterial, uint> materials_converted;

            Dictionary<FBXVideo, uint> textures_converted;

            Dictionary<Geometry, List<uint>> meshes_converted;

            // fixed node name . which trafo chain components have animations?
            Dictionary<string, uint> node_anim_chain_bits;

            // number of nodes with the same name
            using Dictionary<string, uint> = Dictionary<string, uint>;
            Dictionary<string, uint> mNodeNames;

            // Deformer name is not the same as a bone name - it does contain the bone name though :)
            // Deformer names in FBX are always unique in an FBX file.
            SortedDictionary<string, aiBone> bone_map;

            double anim_fps;

            List<aiSkeleton> mSkeletons;
            aiScene mSceneOut;
            FBXDocument doc;
            bool mRemoveEmptyBones;
            static void BuildBoneList(aiNode current_node, aiNode root_node, aiScene scene,
                    List<aiBone> bones);

            void BuildBoneStack(aiNode current_node, aiNode root_node, aiScene scene,
                    List<aiBone> bones,
                    SortedDictionary<aiBone, aiNode> bone_stack,
                    List<aiNode> node_stack);

            static void BuildNodeList(aiNode current_node, List<aiNode> nodes);

            static aiNode GetNodeFromStack(string node_name, List<aiNode> nodes);

            static aiNode GetArmatureRoot(aiNode bone_node, List<aiBone> bone_list);

            static bool IsBoneNode(string bone_name, List<aiBone> bones);

        }

        /**
         *  The different parts that make up the final local transformation of a fbx-node
         */
        public enum TransformationComp {
            TransformationComp_GeometricScalingInverse = 0,
            TransformationComp_GeometricRotationInverse,
            TransformationComp_GeometricTranslationInverse,
            TransformationComp_Translation,
            TransformationComp_RotationOffset,
            TransformationComp_RotationPivot,
            TransformationComp_PreRotation,
            TransformationComp_Rotation,
            TransformationComp_PostRotation,
            TransformationComp_RotationPivotInverse,
            TransformationComp_ScalingOffset,
            TransformationComp_ScalingPivot,
            TransformationComp_Scaling,
            TransformationComp_ScalingPivotInverse,
            TransformationComp_GeometricTranslation,
            TransformationComp_GeometricRotation,
            TransformationComp_GeometricScaling,

            TransformationComp_MAXIMUM
        }

    }
}
