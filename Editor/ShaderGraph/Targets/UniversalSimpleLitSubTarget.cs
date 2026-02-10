#if UNITY_2022_2_OR_NEWER && !(UNITY_2022_2_0 || UNITY_2022_2_1 || UNITY_2022_2_2 || UNITY_2022_2_3 || UNITY_2022_2_4 || UNITY_2022_2_5 || UNITY_2022_2_6 || UNITY_2022_2_7 || UNITY_2022_2_8 || UNITY_2022_2_9 || UNITY_2022_2_10 || UNITY_2022_2_11 || UNITY_2022_2_12 || UNITY_2022_2_13 || UNITY_2022_2_14)
#define UNITY_2022_2_15_OR_NEWER
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Legacy;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using static Unity.Rendering.Universal.ShaderUtils;
using static UnityEditor.Rendering.Universal.ShaderGraph.SubShaderUtils;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    sealed class UniversalSimpleLitSubTarget : UniversalSubTarget
    {
        static readonly GUID kSourceCodeGuid = new GUID("d6c78107b64145745805d963de80cc28");
        public const string kSimpleLitMaterialTypeTag = "\"UniversalMaterialType\" = \"SimpleLit\"";

#if UNITY_2022_2_OR_NEWER
        public override int latestVersion => 2;
#elif UNITY_2022_1_OR_NEWER
        public override int latestVersion => 1;
#endif

        static WorkflowMode m_WorkflowMode = WorkflowMode.Specular;

        [SerializeField]
        bool m_SpecularHighlights = false;

        [SerializeField]
        NormalDropOffSpace m_NormalDropOffSpace = NormalDropOffSpace.Tangent;

#if UNITY_2022_1_OR_NEWER
        [SerializeField]
        bool m_BlendModePreserveSpecular = true;
#endif

        // --- FIX: Variable für Fog ---
        [SerializeField]
        bool m_ReceiveFog = true;
        // -----------------------------

        public UniversalSimpleLitSubTarget()
        {
            displayName = "Simple Lit";
        }

        protected override ShaderID shaderID => ShaderID.Unknown;

        public static WorkflowMode workflowMode
        {
            get => m_WorkflowMode;
        }

        public bool specularHighlights
        {
            get => m_SpecularHighlights;
            set => m_SpecularHighlights = value;
        }

        public NormalDropOffSpace normalDropOffSpace
        {
            get => m_NormalDropOffSpace;
            set => m_NormalDropOffSpace = value;
        }

#if UNITY_2022_1_OR_NEWER
        public bool blendModePreserveSpecular
        {
            get => m_BlendModePreserveSpecular;
            set => m_BlendModePreserveSpecular = value;
        }
#endif

        // --- FIX: Property Accessor ---
        public bool receiveFog
        {
            get => m_ReceiveFog;
            set => m_ReceiveFog = value;
        }
        // ------------------------------

        public override bool IsActive() => true;

        public override void Setup(ref TargetSetupContext context)
        {
            context.AddAssetDependency(kSourceCodeGuid, AssetCollection.Flags.SourceDependency);
            base.Setup(ref context);

            var universalRPType = typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset);
            if (!context.HasCustomEditorForRenderPipeline(universalRPType))
            {
                var gui = typeof(ShaderGraphSimpleLitGUI);
#if HAS_VFX_GRAPH
                if (TargetsVFX())
                    gui = typeof(VFXShaderGraphSimpleLitGUI);
#endif
                context.AddCustomEditorForRenderPipeline(gui.FullName, universalRPType);
            }

            // Process SubShaders
#if UNITY_2022_2_15_OR_NEWER
            // Wir übergeben hier m_ReceiveFog noch nicht an die SubShader Funktion, da diese Signatur fix ist.
            // Das Keyword wird stattdessen über CollectShaderProperties und Defines gesteuert.
            context.AddSubShader(PostProcessSubShader(SubShaders.SimpleLitSubShader(target, target.renderType, target.renderQueue, blendModePreserveSpecular, specularHighlights)));
#elif UNITY_2022_1_OR_NEWER
            context.AddSubShader(PostProcessSubShader(SubShaders.SimpleLitComputeDotsSubShader(target, target.renderType, target.renderQueue, blendModePreserveSpecular, specularHighlights)));
            context.AddSubShader(PostProcessSubShader(SubShaders.SimpleLitGLESSubShader(target, target.renderType, target.renderQueue, blendModePreserveSpecular, specularHighlights)));
#else
            context.AddSubShader(PostProcessSubShader(SubShaders.SimpleLitComputeDotsSubShader(target, target.renderType, target.renderQueue, specularHighlights)));
            context.AddSubShader(PostProcessSubShader(SubShaders.SimpleLitGLESSubShader(target, target.renderType, target.renderQueue, specularHighlights)));
#endif
        }

        public override void ProcessPreviewMaterial(Material material)
        {
            if (target.allowMaterialOverride)
            {
                material.SetFloat(Property.SpecularWorkflowMode, (float)workflowMode);
                material.SetFloat(SimpleLitProperty.SpecularHighlights, specularHighlights ? 1.0f : 0.0f);
                material.SetFloat(Property.CastShadows, target.castShadows ? 1.0f : 0.0f);
                material.SetFloat(Property.ReceiveShadows, target.receiveShadows ? 1.0f : 0.0f);

                // --- FIX: Setze Property mit korrektem Namen "_ReceiveFog" ---
                material.SetFloat("_ReceiveFog", m_ReceiveFog ? 1.0f : 0.0f);
                if (m_ReceiveFog)
                {
                    material.EnableKeyword("_RECEIVE_FOG");
                }
                else
                {
                    material.DisableKeyword("_RECEIVE_FOG");
                }
                // -------------------------------------------------------------

                material.SetFloat(Property.SurfaceType, (float)target.surfaceType);
                material.SetFloat(Property.BlendMode, (float)target.alphaMode);
                material.SetFloat(Property.AlphaClip, target.alphaClip ? 1.0f : 0.0f);
                material.SetFloat(Property.CullMode, (int)target.renderFace);
                material.SetFloat(Property.ZWriteControl, (float)target.zWriteControl);
                material.SetFloat(Property.ZTest, (float)target.zTestMode);
            }

            material.SetFloat(Property.QueueOffset, 0.0f);
            material.SetFloat(Property.QueueControl, (float)BaseShaderGUI.QueueControl.Auto);

            ShaderGraphSimpleLitGUI.UpdateMaterial(material, MaterialUpdateType.CreatedNewMaterial);
        }

        public override void GetFields(ref TargetFieldContext context)
        {
            base.GetFields(ref context);
            var descs = context.blocks.Select(x => x.descriptor);

            context.AddField(UniversalFields.NormalDropOffOS, normalDropOffSpace == NormalDropOffSpace.Object);
            context.AddField(UniversalFields.NormalDropOffTS, normalDropOffSpace == NormalDropOffSpace.Tangent);
            context.AddField(UniversalFields.NormalDropOffWS, normalDropOffSpace == NormalDropOffSpace.World);
            context.AddField(UniversalFields.Normal, descs.Contains(BlockFields.SurfaceDescription.NormalOS) ||
                descs.Contains(BlockFields.SurfaceDescription.NormalTS) ||
                descs.Contains(BlockFields.SurfaceDescription.NormalWS));
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            context.AddBlock(BlockFields.SurfaceDescription.Smoothness);
            context.AddBlock(BlockFields.SurfaceDescription.NormalOS, normalDropOffSpace == NormalDropOffSpace.Object);
            context.AddBlock(BlockFields.SurfaceDescription.NormalTS, normalDropOffSpace == NormalDropOffSpace.Tangent);
            context.AddBlock(BlockFields.SurfaceDescription.NormalWS, normalDropOffSpace == NormalDropOffSpace.World);
            context.AddBlock(BlockFields.SurfaceDescription.Emission);

            context.AddBlock(BlockFields.SurfaceDescription.Specular, specularHighlights || target.allowMaterialOverride);
            context.AddBlock(BlockFields.SurfaceDescription.Alpha, (target.surfaceType == SurfaceType.Transparent || target.alphaClip) || target.allowMaterialOverride);
            context.AddBlock(BlockFields.SurfaceDescription.AlphaClipThreshold, (target.alphaClip) || target.allowMaterialOverride);
        }

        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            if (target.allowMaterialOverride)
            {
                collector.AddFloatProperty(Property.SpecularWorkflowMode, (float)workflowMode);
                collector.AddFloatProperty(SimpleLitProperty.SpecularHighlights, specularHighlights ? 1.0f : 0.0f);
                collector.AddFloatProperty(Property.CastShadows, target.castShadows ? 1.0f : 0.0f);
                collector.AddFloatProperty(Property.ReceiveShadows, target.receiveShadows ? 1.0f : 0.0f);

                // --- FIX: Property mit korrektem Namen "_ReceiveFog" hinzufügen ---
                // WICHTIG: Der String muss "_ReceiveFog" sein, damit URP Standard-Funktionen ihn finden.
                collector.AddToggleProperty("_ReceiveFog", m_ReceiveFog);
                // ------------------------------------------------------------------

                collector.AddFloatProperty(Property.SurfaceType, (float)target.surfaceType);
                collector.AddFloatProperty(Property.BlendMode, (float)target.alphaMode);
                collector.AddFloatProperty(Property.AlphaClip, target.alphaClip ? 1.0f : 0.0f);
#if UNITY_2022_1_OR_NEWER
                collector.AddFloatProperty(Property.BlendModePreserveSpecular, blendModePreserveSpecular ? 1.0f : 0.0f);
#endif
                collector.AddFloatProperty(Property.SrcBlend, 1.0f);
                collector.AddFloatProperty(Property.DstBlend, 0.0f);
                collector.AddFloatProperty(Property.SrcBlendAlpha, 1.0f);
                if (target.surfaceType == SurfaceType.Opaque)
                    collector.AddFloatProperty(Property.DstBlendAlpha, 0.0f);
                else
                    collector.AddFloatProperty(Property.DstBlendAlpha, 10.0f);
           
                collector.AddToggleProperty(Property.ZWrite, (target.surfaceType == SurfaceType.Opaque));
                collector.AddFloatProperty(Property.ZWriteControl, (float)target.zWriteControl);
                collector.AddFloatProperty(Property.ZTest, (float)target.zTestMode);
                collector.AddFloatProperty(Property.CullMode, (float)target.renderFace);

#if UNITY_2022_2_OR_NEWER
                bool enableAlphaToMask = (target.alphaClip && (target.surfaceType == SurfaceType.Opaque));
                collector.AddFloatProperty(Property.AlphaToMask, enableAlphaToMask ? 1.0f : 0.0f);
#endif
            }

            collector.AddFloatProperty(Property.QueueOffset, 0.0f);
            collector.AddFloatProperty(Property.QueueControl, -1.0f);
        }

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<String> registerUndo)
        {
            var universalTarget = (target as UniversalTarget);
            universalTarget.AddDefaultMaterialOverrideGUI(ref context, onChange, registerUndo);

            context.AddProperty("Specular Highlights", new Toggle() { value = specularHighlights }, (evt) =>
            {
                if (Equals(specularHighlights, evt.newValue)) return;
                registerUndo("Change Specular Highlights");
                specularHighlights = evt.newValue;
                onChange();
            });

            // --- FIX: GUI Toggle ---
            context.AddProperty("Receive Fog", new Toggle() { value = m_ReceiveFog }, (evt) =>
            {
                if (Equals(m_ReceiveFog, evt.newValue)) return;
                registerUndo("Change Receive Fog");
                m_ReceiveFog = evt.newValue;
                onChange();
            });
            // -----------------------

            universalTarget.AddDefaultSurfacePropertiesGUI(ref context, onChange, registerUndo, showReceiveShadows: true);

            context.AddProperty("Fragment Normal Space", new EnumField(NormalDropOffSpace.Tangent) { value = normalDropOffSpace }, (evt) =>
            {
                if (Equals(normalDropOffSpace, evt.newValue)) return;
                registerUndo("Change Fragment Normal Space");
                normalDropOffSpace = (NormalDropOffSpace)evt.newValue;
                onChange();
            });

#if UNITY_2022_1_OR_NEWER
            if (target.surfaceType == SurfaceType.Transparent)
            {
                if (target.alphaMode == AlphaMode.Alpha || target.alphaMode == AlphaMode.Additive)
                    context.AddProperty("Preserve Specular Lighting", new Toggle() { value = blendModePreserveSpecular }, (evt) =>
                    {
                        if (Equals(blendModePreserveSpecular, evt.newValue)) return;
                        registerUndo("Change Preserve Specular");
                        blendModePreserveSpecular = evt.newValue;
                        onChange();
                    });
            }
#endif
        }

        protected override int ComputeMaterialNeedsUpdateHash()
        {
            int hash = base.ComputeMaterialNeedsUpdateHash();
            hash = hash * 23 + target.allowMaterialOverride.GetHashCode();
            // --- FIX: Hash Update ---
            hash = hash * 23 + m_ReceiveFog.GetHashCode();
            // ------------------------
            return hash;
        }

#if UNITY_2022_1_OR_NEWER
        internal override void OnAfterParentTargetDeserialized()
        {
            Assert.IsNotNull(target);
            if (this.sgVersion < latestVersion)
            {
                if (this.sgVersion < 1)
                {
                    if (target.alphaMode == AlphaMode.Premultiply)
                    {
                        target.alphaMode = AlphaMode.Alpha;
                        blendModePreserveSpecular = true;
                    }
                    else
                        blendModePreserveSpecular = false;
                }
                ChangeVersion(latestVersion);
            }
        }
#endif

        #region SubShader
        static class SubShaders
        {
            // ... (Hier ändert sich nichts an den Signaturen, wir nutzen die ShaderKeywords unten) ...
            
#if UNITY_2022_2_15_OR_NEWER
            public static SubShaderDescriptor SimpleLitSubShader(UniversalTarget target, string renderType, string renderQueue, bool blendModePreserveSpecular, bool specularHighlights)
#elif UNITY_2022_1_OR_NEWER
            public static SubShaderDescriptor SimpleLitComputeDotsSubShader(UniversalTarget target, string renderType, string renderQueue, bool blendModePreserveSpecular, bool specularHighlights)
#else
            public static SubShaderDescriptor SimpleLitComputeDotsSubShader(UniversalTarget target, string renderType, string renderQueue, bool specularHighlights)
#endif
            {
                SubShaderDescriptor result = new SubShaderDescriptor()
                {
                    pipelineTag = UniversalTarget.kPipelineTag,
                    customTags = kSimpleLitMaterialTypeTag,
                    renderType = renderType,
                    renderQueue = renderQueue,
                    generatesPreview = true,
                    passes = new PassCollection()
                };

                // Forward Pass
#if UNITY_2022_2_15_OR_NEWER
                result.passes.Add(SimpleLitPasses.Forward(target, blendModePreserveSpecular, specularHighlights, CorePragmas.Forward, SimpleLitKeywords.Forward));
#elif UNITY_2022_2_OR_NEWER
                result.passes.Add(SimpleLitPasses.Forward(target, blendModePreserveSpecular, specularHighlights, CorePragmas.ForwardSM45, SimpleLitKeywords.DOTSForward));
#elif UNITY_2022_1_OR_NEWER
                result.passes.Add(SimpleLitPasses.Forward(target, blendModePreserveSpecular, specularHighlights, CorePragmas.DOTSForward));
#else
                result.passes.Add(SimpleLitPasses.Forward(target, specularHighlights, CorePragmas.DOTSForward));
#endif
                
                // GBuffer Pass
#if UNITY_2022_1_OR_NEWER
                result.passes.Add(SimpleLitPasses.GBuffer(target, blendModePreserveSpecular, specularHighlights));
#else
                result.passes.Add(SimpleLitPasses.GBuffer(target, specularHighlights));
#endif

                // ShadowCaster
                if (target.castShadows || target.allowMaterialOverride)
#if UNITY_2022_2_15_OR_NEWER
                    result.passes.Add(PassVariant(CorePasses.ShadowCaster(target), CorePragmas.Instanced));
#elif UNITY_2022_2_OR_NEWER
                    result.passes.Add(PassVariant(CorePasses.ShadowCaster(target), CorePragmas.InstancedSM45));
#else
                    result.passes.Add(PassVariant(CorePasses.ShadowCaster(target), CorePragmas.DOTSInstanced));
#endif

                // DepthOnly
                if (target.mayWriteDepth)
#if UNITY_2022_2_15_OR_NEWER
                    result.passes.Add(PassVariant(CorePasses.DepthOnly(target), CorePragmas.Instanced));
#elif UNITY_2022_2_OR_NEWER
                    result.passes.Add(PassVariant(CorePasses.DepthOnly(target), CorePragmas.InstancedSM45));
#else
                    result.passes.Add(PassVariant(CorePasses.DepthOnly(target), CorePragmas.DOTSInstanced));
#endif

                // DepthNormal
#if UNITY_2022_2_15_OR_NEWER
                result.passes.Add(PassVariant(SimpleLitPasses.DepthNormal(target), CorePragmas.Instanced));
#elif UNITY_2022_2_OR_NEWER
                result.passes.Add(PassVariant(SimpleLitPasses.DepthNormal(target), CorePragmas.InstancedSM45));
#else
                result.passes.Add(PassVariant(SimpleLitPasses.DepthNormal(target), CorePragmas.DOTSInstanced));
#endif

                // Meta
#if UNITY_2022_2_15_OR_NEWER
                result.passes.Add(PassVariant(SimpleLitPasses.Meta(target), CorePragmas.Default));
#elif UNITY_2022_2_OR_NEWER
                result.passes.Add(PassVariant(SimpleLitPasses.Meta(target), CorePragmas.DefaultSM45));
#else
                result.passes.Add(PassVariant(SimpleLitPasses.Meta(target), CorePragmas.DOTSDefault));
#endif

                // SceneSelection / Picking
#if UNITY_2022_2_15_OR_NEWER
                result.passes.Add(PassVariant(CorePasses.SceneSelection(target), CorePragmas.Default));
#elif UNITY_2022_2_OR_NEWER
                result.passes.Add(PassVariant(CorePasses.SceneSelection(target), CorePragmas.DefaultSM45));
#else
                result.passes.Add(PassVariant(CorePasses.SceneSelection(target), CorePragmas.DOTSDefault));
#endif

#if UNITY_2022_2_15_OR_NEWER
                result.passes.Add(PassVariant(CorePasses.ScenePicking(target), CorePragmas.Default));
#elif UNITY_2022_2_OR_NEWER
                result.passes.Add(PassVariant(CorePasses.ScenePicking(target), CorePragmas.DefaultSM45));
#else
                result.passes.Add(PassVariant(CorePasses.ScenePicking(target), CorePragmas.DOTSDefault));
#endif

                // 2D
#if UNITY_2022_2_15_OR_NEWER
                result.passes.Add(PassVariant(SimpleLitPasses._2D(target), CorePragmas.Default));
#elif UNITY_2022_2_OR_NEWER
                result.passes.Add(PassVariant(SimpleLitPasses._2D(target), CorePragmas.DefaultSM45));
#else
                result.passes.Add(PassVariant(SimpleLitPasses._2D(target), CorePragmas.DOTSDefault));
#endif
                return result;
            }

            // ... (Hier GLES SubShader falls benötigt, analoge Passes) ...
        }
        #endregion

        #region Passes
        static class SimpleLitPasses
        {
            static void AddWorkflowModeControlToPass(ref PassDescriptor pass, UniversalTarget target, WorkflowMode workflowMode)
            {
                pass.defines.Add(SimpleLitDefines.SpecularSetup, 1);
            }

            static void AddSpecularHighlightsControlToPass(ref PassDescriptor pass, UniversalTarget target, bool specularHighlights)
            {
                if (target.allowMaterialOverride)
                    pass.keywords.Add(SimpleLitDefines.SpecularColor);
                else if (specularHighlights)
                    pass.defines.Add(SimpleLitDefines.SpecularColor, 1);
            }

            static void AddReceiveShadowsControlToPass(ref PassDescriptor pass, UniversalTarget target, bool receiveShadows)
            {
                if (target.allowMaterialOverride)
                    pass.keywords.Add(SimpleLitKeywords.ReceiveShadowsOff);
                else if (!receiveShadows)
                    pass.defines.Add(SimpleLitKeywords.ReceiveShadowsOff, 1);
            }

            // --- FIX: Helper Funktion für Fog ---
            static void AddReceiveFogControlToPass(ref PassDescriptor pass, UniversalTarget target)
            {
                // In Unity 6 müssen wir sicherstellen, dass das Keyword definiert ist.
                // Wir checken hier, ob wir das target casten können, um an unseren bool zu kommen.
                // Da target vom Typ UniversalTarget ist, müssen wir tricksen oder einfach das Keyword immer hinzufügen
                // wenn wir AllowMaterialOverride haben.
                
                if (target.allowMaterialOverride)
                {
                    // Wenn Material Override an ist, nutzen wir ein Keyword, das per Material geschaltet wird.
                    // Das Standard-Keyword für Receive Fog ist _RECEIVE_FOG
                    pass.keywords.Add(new KeywordDescriptor()
                    {
                        displayName = "Receive Fog",
                        referenceName = "_RECEIVE_FOG",
                        type = KeywordType.Boolean,
                        definition = KeywordDefinition.ShaderFeature,
                        scope = KeywordScope.Local,
                    });
                }
                else
                {
                    // Wenn kein Override, dann backen wir es fest ein.
                    // ABER: Wir kommen hier nicht einfach an "m_ReceiveFog" ran, da "target" hier nur die Basisklasse ist.
                    // Trick: Wir fügen das Keyword hinzu, und ShaderGraph wird es basierend auf der Property (die wir oben in CollectShaderProperties gesetzt haben) steuern.
                     pass.keywords.Add(new KeywordDescriptor()
                    {
                        displayName = "Receive Fog",
                        referenceName = "_RECEIVE_FOG",
                        type = KeywordType.Boolean,
                        definition = KeywordDefinition.ShaderFeature,
                        scope = KeywordScope.Local,
                    });
                }
            }
            // ------------------------------------

#if UNITY_2022_2_OR_NEWER
            public static PassDescriptor Forward(
                UniversalTarget target,
                bool blendModePreserveSpecular,
                bool specularHighlights,
                PragmaCollection pragmas,
                KeywordCollection keywords)
#elif UNITY_2022_1_OR_NEWER
            public static PassDescriptor Forward(UniversalTarget target, bool blendModePreserveSpecular, bool specularHighlights, PragmaCollection pragmas = null)
#else
            public static PassDescriptor Forward(UniversalTarget target, bool specularHighlights, PragmaCollection pragmas = null)
#endif
            {
                var result = new PassDescriptor()
                {
                    displayName = "Universal Forward",
                    referenceName = "SHADERPASS_FORWARD",
                    lightMode = "UniversalForward",
                    useInPreview = true,
                    passTemplatePath = UniversalTarget.kUberTemplatePath,
                    sharedTemplateDirectories = UniversalTarget.kSharedTemplateDirectories,
                    validVertexBlocks = CoreBlockMasks.Vertex,
                    validPixelBlocks = SimpleLitBlockMasks.FragmentSimpleLit,
                    structs = CoreStructCollections.Default,
                    requiredFields = SimpleLitRequiredFields.Forward,
                    fieldDependencies = CoreFieldDependencies.Default,
#if UNITY_2022_1_OR_NEWER
                    renderStates = CoreRenderStates.UberSwitchedRenderState(target, blendModePreserveSpecular),
#else
                    renderStates = CoreRenderStates.UberSwitchedRenderState(target),
#endif
                    pragmas = pragmas ?? CorePragmas.Forward,
                    // --- FIX: Defines ---
                    // CoreDefines.UseFragmentFog definiert oft nur _FOG_FRAGMENT.
                    // Wir brauchen aber sicherheitshalber auch _RECEIVE_FOG Logic wenn der Shader das prüft.
                    defines = new DefineCollection() { CoreDefines.UseFragmentFog },
                    // --------------------
#if UNITY_2022_2_OR_NEWER
                    keywords = new KeywordCollection() { keywords },
#else
                    keywords = new KeywordCollection() { SimpleLitKeywords.Forward },
#endif
                    includes = SimpleLitIncludes.Forward,
                    customInterpolators = CoreCustomInterpDescriptors.Common
                };

#if UNITY_2022_1_OR_NEWER
                CorePasses.AddTargetSurfaceControlsToPass(ref result, target, blendModePreserveSpecular);
#else
                CorePasses.AddTargetSurfaceControlsToPass(ref result, target);
#endif
#if UNITY_2022_2_OR_NEWER
                CorePasses.AddAlphaToMaskControlToPass(ref result, target);
#endif
                AddWorkflowModeControlToPass(ref result, target, workflowMode);
                AddSpecularHighlightsControlToPass(ref result, target, specularHighlights);
                AddReceiveShadowsControlToPass(ref result, target, target.receiveShadows);
                
                // --- FIX: Fog Keyword registrieren ---
                AddReceiveFogControlToPass(ref result, target);
                // -------------------------------------

#if UNITY_2022_2_OR_NEWER
                CorePasses.AddLODCrossFadeControlToPass(ref result, target);
#endif
                return result;
            }

            // GBuffer Pass braucht den Fix auch
#if UNITY_2022_1_OR_NEWER
            public static PassDescriptor GBuffer(UniversalTarget target, bool blendModePreserveSpecular, bool specularHighlights)
#else
            public static PassDescriptor GBuffer(UniversalTarget target, bool specularHighlights)
#endif
            {
                var result = new PassDescriptor
                {
                    displayName = "GBuffer",
                    referenceName = "SHADERPASS_GBUFFER",
                    lightMode = "UniversalGBuffer",
#if UNITY_2022_2_OR_NEWER
                    useInPreview = true,
#endif
                    passTemplatePath = UniversalTarget.kUberTemplatePath,
                    sharedTemplateDirectories = UniversalTarget.kSharedTemplateDirectories,
                    validVertexBlocks = CoreBlockMasks.Vertex,
                    validPixelBlocks = SimpleLitBlockMasks.FragmentSimpleLit,
                    structs = CoreStructCollections.Default,
                    requiredFields = SimpleLitRequiredFields.GBuffer,
                    fieldDependencies = CoreFieldDependencies.Default,
#if UNITY_2022_1_OR_NEWER
                    renderStates = CoreRenderStates.UberSwitchedRenderState(target, blendModePreserveSpecular),
#else
                    renderStates = CoreRenderStates.UberSwitchedRenderState(target),
#endif
                    pragmas = CorePragmas.GBufferSM45, // oder je nach Version
                    defines = new DefineCollection() { CoreDefines.UseFragmentFog },
                    keywords = new KeywordCollection() { SimpleLitKeywords.GBuffer },
                    includes = SimpleLitIncludes.GBuffer,
                    customInterpolators = CoreCustomInterpDescriptors.Common
                };

#if UNITY_2022_1_OR_NEWER
                CorePasses.AddTargetSurfaceControlsToPass(ref result, target, blendModePreserveSpecular);
#else
                CorePasses.AddTargetSurfaceControlsToPass(ref result, target);
#endif
                AddWorkflowModeControlToPass(ref result, target, workflowMode);
                AddSpecularHighlightsControlToPass(ref result, target, specularHighlights);
                AddReceiveShadowsControlToPass(ref result, target, target.receiveShadows);

                // --- FIX: Fog Keyword registrieren ---
                AddReceiveFogControlToPass(ref result, target);
                // -------------------------------------

#if UNITY_2022_2_OR_NEWER
                CorePasses.AddLODCrossFadeControlToPass(ref result, target);
#endif
                return result;
            }

            // ... (Restliche Methoden wie Meta, _2D, DepthNormal bleiben unverändert, da Fog dort meist keine Rolle spielt) ...
            
            public static PassDescriptor Meta(UniversalTarget target)
            {
                var result = new PassDescriptor()
                {
                   displayName = "Meta",
                   referenceName = "SHADERPASS_META",
                   lightMode = "Meta",
                   passTemplatePath = UniversalTarget.kUberTemplatePath,
                   sharedTemplateDirectories = UniversalTarget.kSharedTemplateDirectories,
                   validVertexBlocks = CoreBlockMasks.Vertex,
                   validPixelBlocks = SimpleLitBlockMasks.FragmentMeta,
                   structs = CoreStructCollections.Default,
                   requiredFields = SimpleLitRequiredFields.Meta,
                   fieldDependencies = CoreFieldDependencies.Default,
                   renderStates = CoreRenderStates.Meta,
                   pragmas = CorePragmas.Default,
                   defines = new DefineCollection() { CoreDefines.UseFragmentFog },
                   keywords = new KeywordCollection() { CoreKeywordDescriptors.EditorVisualization },
                   includes = SimpleLitIncludes.Meta,
                   customInterpolators = CoreCustomInterpDescriptors.Common
                };
                CorePasses.AddAlphaClipControlToPass(ref result, target);
                return result;
            }

            public static PassDescriptor _2D(UniversalTarget target)
            {
                 var result = new PassDescriptor()
                {
                    referenceName = "SHADERPASS_2D",
                    lightMode = "Universal2D",
                    passTemplatePath = UniversalTarget.kUberTemplatePath,
                    sharedTemplateDirectories = UniversalTarget.kSharedTemplateDirectories,
                    validVertexBlocks = CoreBlockMasks.Vertex,
                    validPixelBlocks = CoreBlockMasks.FragmentColorAlpha,
                    structs = CoreStructCollections.Default,
                    fieldDependencies = CoreFieldDependencies.Default,
                    renderStates = CoreRenderStates.UberSwitchedRenderState(target),
                    pragmas = CorePragmas.Instanced,
                    defines = new DefineCollection(),
                    keywords = new KeywordCollection(),
                    includes = SimpleLitIncludes._2D,
                    customInterpolators = CoreCustomInterpDescriptors.Common
                };
                CorePasses.AddAlphaClipControlToPass(ref result, target);
                return result;
            }

            public static PassDescriptor DepthNormal(UniversalTarget target)
            {
                var result = new PassDescriptor()
                {
                    displayName = "DepthNormals",
                    referenceName = "SHADERPASS_DEPTHNORMALS",
                    lightMode = "DepthNormals",
                    useInPreview = false,
                    passTemplatePath = UniversalTarget.kUberTemplatePath,
                    sharedTemplateDirectories = UniversalTarget.kSharedTemplateDirectories,
                    validVertexBlocks = CoreBlockMasks.Vertex,
                    validPixelBlocks = CoreBlockMasks.FragmentDepthNormals,
                    structs = CoreStructCollections.Default,
                    requiredFields = CoreRequiredFields.DepthNormals,
                    fieldDependencies = CoreFieldDependencies.Default,
                    renderStates = CoreRenderStates.DepthNormalsOnly(target),
                    pragmas = CorePragmas.Instanced,
                    defines = new DefineCollection(),
#if UNITY_2022_2_15_OR_NEWER
                    keywords = new KeywordCollection(),
#elif UNITY_2022_2_OR_NEWER
                    keywords = new KeywordCollection() { CoreKeywords.DOTSDepthNormal },
#else
                    keywords = new KeywordCollection(),
#endif
                    includes = CoreIncludes.DepthNormalsOnly,
                    customInterpolators = CoreCustomInterpDescriptors.Common
                };

                CorePasses.AddAlphaClipControlToPass(ref result, target);
#if UNITY_2022_2_OR_NEWER
                CorePasses.AddLODCrossFadeControlToPass(ref result, target);
#endif
                return result;
            }
        }
        #endregion

        // ... (Restliche Regionen PortMasks, RequiredFields, Defines, Keywords, Includes wie gehabt) ...
        #region PortMasks
        static class SimpleLitBlockMasks
        {
            public static readonly BlockFieldDescriptor[] FragmentSimpleLit = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalOS,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Specular,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
            };

            public static readonly BlockFieldDescriptor[] FragmentMeta = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
            };
        }
        #endregion

        #region RequiredFields
        static class SimpleLitRequiredFields
        {
            public static readonly FieldCollection Forward = new FieldCollection()
            {
                StructFields.Attributes.uv1,
                StructFields.Attributes.uv2,
                StructFields.Varyings.positionWS,
                StructFields.Varyings.normalWS,
                StructFields.Varyings.tangentWS,                        
                UniversalStructFields.Varyings.staticLightmapUV,
                UniversalStructFields.Varyings.dynamicLightmapUV,
                UniversalStructFields.Varyings.sh,
                UniversalStructFields.Varyings.fogFactorAndVertexLight, 
                UniversalStructFields.Varyings.shadowCoord,             
            };

            public static readonly FieldCollection GBuffer = new FieldCollection()
            {
                StructFields.Attributes.uv1,
                StructFields.Attributes.uv2,
                StructFields.Varyings.positionWS,
                StructFields.Varyings.normalWS,
                StructFields.Varyings.tangentWS,                        
                UniversalStructFields.Varyings.staticLightmapUV,
                UniversalStructFields.Varyings.dynamicLightmapUV,
                UniversalStructFields.Varyings.sh,
                UniversalStructFields.Varyings.fogFactorAndVertexLight, 
                UniversalStructFields.Varyings.shadowCoord,             
            };

            public static readonly FieldCollection Meta = new FieldCollection()
            {
                StructFields.Attributes.positionOS,
                StructFields.Attributes.normalOS,
                StructFields.Attributes.uv0,                            
                StructFields.Attributes.uv1,                            
                StructFields.Attributes.uv2,                            
                StructFields.Attributes.instanceID,                     
                StructFields.Varyings.positionCS,
                StructFields.Varyings.texCoord0,                        
                StructFields.Varyings.texCoord1,                        
                StructFields.Varyings.texCoord2,                        
            };
        }
        #endregion

        #region Defines
        static class SimpleLitDefines
        {
            public static readonly KeywordDescriptor SpecularSetup = new KeywordDescriptor()
            {
                displayName = "Specular Setup",
                referenceName = "_SPECULAR_SETUP",
                type = KeywordType.Boolean,
                definition = KeywordDefinition.ShaderFeature,
                scope = KeywordScope.Local,
                stages = KeywordShaderStage.Fragment
            };

            public static readonly KeywordDescriptor SpecularColor = new KeywordDescriptor()
            {
                displayName = "Specular Color",
                referenceName = SimpleLitProperty.SpecularColorKeyword,
                type = KeywordType.Boolean,
                definition = KeywordDefinition.ShaderFeature,
                scope = KeywordScope.Local,
                stages = KeywordShaderStage.Fragment
            };
        }
        #endregion

        #region Keywords
        static class SimpleLitKeywords
        {
            public static readonly KeywordDescriptor ReceiveShadowsOff = new KeywordDescriptor()
            {
                displayName = "Receive Shadows Off",
                referenceName = ShaderKeywordStrings._RECEIVE_SHADOWS_OFF,
                type = KeywordType.Boolean,
                definition = KeywordDefinition.ShaderFeature,
                scope = KeywordScope.Local,
            };

#if UNITY_2022_2_OR_NEWER
#else
            public static readonly KeywordDescriptor ScreenSpaceAmbientOcclusion = new KeywordDescriptor()
            {
                displayName = "Screen Space Ambient Occlusion",
                referenceName = "_SCREEN_SPACE_OCCLUSION",
                type = KeywordType.Boolean,
                definition = KeywordDefinition.MultiCompile,
                scope = KeywordScope.Global,
                stages = KeywordShaderStage.Fragment,
            };
#endif

            public static readonly KeywordCollection Forward = new KeywordCollection
            {
#if UNITY_2022_2_OR_NEWER
                { CoreKeywordDescriptors.ScreenSpaceAmbientOcclusion },
#else
                { ScreenSpaceAmbientOcclusion },
#endif
                { CoreKeywordDescriptors.StaticLightmap },
                { CoreKeywordDescriptors.DynamicLightmap },
                { CoreKeywordDescriptors.DirectionalLightmapCombined },
                { CoreKeywordDescriptors.MainLightShadows },
                { CoreKeywordDescriptors.AdditionalLights },
                { CoreKeywordDescriptors.AdditionalLightShadows },
                { CoreKeywordDescriptors.ReflectionProbeBlending },
                { CoreKeywordDescriptors.ReflectionProbeBoxProjection },
                { CoreKeywordDescriptors.ShadowsSoft },
                { CoreKeywordDescriptors.LightmapShadowMixing },
                { CoreKeywordDescriptors.ShadowsShadowmask },
                { CoreKeywordDescriptors.DBuffer },
                { CoreKeywordDescriptors.LightLayers },
                { CoreKeywordDescriptors.DebugDisplay },
                { CoreKeywordDescriptors.LightCookies },
#if UNITY_6000_1_OR_NEWER
              { CoreKeywordDescriptors.ClusterLightLoop },
#elif UNITY_2022_2_OR_NEWER
              { CoreKeywordDescriptors.ForwardPlus },
#else
                { CoreKeywordDescriptors.ClusteredRendering },
#endif
            };

#if UNITY_2022_2_15_OR_NEWER
#elif UNITY_2022_2_OR_NEWER
            public static readonly KeywordCollection DOTSForward = new KeywordCollection
            {
                { Forward },
                { CoreKeywordDescriptors.WriteRenderingLayers },
            };
#endif

            public static readonly KeywordCollection GBuffer = new KeywordCollection
            {
                { CoreKeywordDescriptors.StaticLightmap },
                { CoreKeywordDescriptors.DynamicLightmap },
                { CoreKeywordDescriptors.DirectionalLightmapCombined },
                { CoreKeywordDescriptors.MainLightShadows },
                { CoreKeywordDescriptors.ReflectionProbeBlending },
                { CoreKeywordDescriptors.ReflectionProbeBoxProjection },
                { CoreKeywordDescriptors.ShadowsSoft },
                { CoreKeywordDescriptors.LightmapShadowMixing },
                { CoreKeywordDescriptors.MixedLightingSubtractive },
                { CoreKeywordDescriptors.DBuffer },
                { CoreKeywordDescriptors.GBufferNormalsOct },
#if UNITY_2022_2_15_OR_NEWER
                { CoreKeywordDescriptors.LightLayers },
#elif UNITY_2022_2_OR_NEWER
                { CoreKeywordDescriptors.WriteRenderingLayers },
#else
                { CoreKeywordDescriptors.LightLayers },
#endif
                { CoreKeywordDescriptors.RenderPassEnabled },
                { CoreKeywordDescriptors.DebugDisplay },
            };
        }
        #endregion

        #region Includes
        static class SimpleLitIncludes
        {
            const string kShadows = "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl";
            const string kMetaInput = "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl";
            const string kForwardPass = "Packages/com.zallist.universal-shadergraph-extensions/Editor/ShaderGraph/Includes/SimpleLitForwardPass.hlsl";
            const string kGBuffer = "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl";
            const string kSimpleLitGBufferPass = "Packages/com.zallist.universal-shadergraph-extensions/Editor/ShaderGraph/Includes/SimpleLitGBufferPass.hlsl";
            const string kLightingMetaPass = "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl";
            // TODO : Replace 2D for Simple one
            const string k2DPass = "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBR2DPass.hlsl";

            public static readonly IncludeCollection Forward = new IncludeCollection
            {
                // Pre-graph
#if UNITY_2022_2_15_OR_NEWER
                { CoreIncludes.DOTSPregraph },
                { CoreIncludes.WriteRenderLayersPregraph },
#endif
                { CoreIncludes.CorePregraph },
                { kShadows, IncludeLocation.Pregraph },
                { CoreIncludes.ShaderGraphPregraph },
                { CoreIncludes.DBufferPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kForwardPass, IncludeLocation.Postgraph },
            };

            public static readonly IncludeCollection GBuffer = new IncludeCollection
            {
                // Pre-graph
#if UNITY_2022_2_15_OR_NEWER
                { CoreIncludes.DOTSPregraph },
                { CoreIncludes.WriteRenderLayersPregraph },
#endif
                { CoreIncludes.CorePregraph },
                { kShadows, IncludeLocation.Pregraph },
                { CoreIncludes.ShaderGraphPregraph },
                { CoreIncludes.DBufferPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kGBuffer, IncludeLocation.Postgraph },
                { kSimpleLitGBufferPass, IncludeLocation.Postgraph },
            };

            public static readonly IncludeCollection Meta = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { CoreIncludes.ShaderGraphPregraph },
                { kMetaInput, IncludeLocation.Pregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kLightingMetaPass, IncludeLocation.Postgraph },
            };

            public static readonly IncludeCollection _2D = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { CoreIncludes.ShaderGraphPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { k2DPass, IncludeLocation.Postgraph },
            };
        }
        #endregion
    }

    public static class SimpleLitProperty
    {
        public static readonly string SpecularColorKeyword = "_SPECULAR_COLOR";
        public static readonly string SpecularHighlights = "_SpecularHighlights";
    }
}
