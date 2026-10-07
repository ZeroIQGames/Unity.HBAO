using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine.Rendering.Universal;
using ZeroIQGames.Graphics.HBAOPlus.Core.Renderer;

namespace ZeroIQGames.Graphics.HBAOPlus.URP.Renderer
{
    /// <summary>
    /// Renderer feature 
    /// </summary>
    [DisallowMultipleRendererFeature("Horizon-Based Ambient Occlusion")]
    [Tooltip("The Ambient Occlusion effect darkens creases, holes, intersections and surfaces that are close to each other. HBAO+ can not be used with other AO techniques at the same time.")]
    public partial class HBAOPlusRendererFeature : ScriptableRendererFeature
    {
        public override void Create()
        {
            _RenderPass ??= new RenderPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPrePasses + 1,
            };
            _Settings ??= new HBAOPlusSettings();
#if UNITY_EDITOR
            if (_RasterShader == null)
            {
                // Load the default shader using AssetDatabase
                _RasterShader  = AssetDatabase.LoadAssetAtPath<Shader>(_RASTER_SHADER_PATH);  
            }

            if (_ComputeShader == null)
            {
                // Load the default shader using AssetDatabase
                _ComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(_COMPUTE_SHADER_PATH); 
            }
#endif
            if (_RasterShader != null)
            {
                _Settings.RasterPassMaterial = CoreUtils.CreateEngineMaterial(_RasterShader);
            }
            
            _Settings.ComputePassShader   = _ComputeShader; 
            _Settings.AORenderPath        = HBAOPlusUtility.GetAORenderPath(AORenderPathPreferenceOrder);
            _Settings.BlurRenderPath      = HBAOPlusUtility.GetBlurRenderPath(_BlurPreferredRenderPath, BlurQuality, SharpnessSource, (int)BlurRadius);
            _Settings.bUseDepthNormal     = GeometrySource == EGeometrySource.DepthNormal;
            _Settings.RasterShaderParams  = new HBAOPlusRasterShaderParams(_RasterShader);
            _Settings.ComputeShaderParams = new HBAOPlusComputeShaderParams(_ComputeShader);
            _Settings.AOQuality           = AOQuality;
            _Settings.BlurQuality         = BlurQuality;
            _Settings.BlurSharpnessSource = SharpnessSource;
            _Settings.BlurKernelRadius    = BlurRadius;
            
            
            // Per-frame callback
            RenderPipelineManager.beginContextRendering -= _OnBeginContextRendering; // Remove the callback if it already exists
            if (UsePerFrameRandomJitter)
            {
                RenderPipelineManager.beginContextRendering += _OnBeginContextRendering; // Add the callback
            }
        }

        public override void AddRenderPasses(ScriptableRenderer Renderer, ref RenderingData RenderingData)
        {
            using (new ProfilingScope(_AddRenderPassesMarker))
            {
                var VolumeSettingsComponent = VolumeManager.instance.stack?.GetComponent<HBAOPlusVolumeComponent>();
                if (VolumeSettingsComponent is null)
                {
                    Debug.LogWarning("You need at least one HBAOPlusVolumeComponent in your VolumeStack to use HBAO+ this feature.");

                    return;
                }

                // Volume is not active or does not exist
            
                if (!VolumeSettingsComponent.active)
                {
                    return;
                }
            
                // Volume is active but the effect is disabled
                if (!VolumeSettingsComponent.bEnabled.value)
                {
                    return;
                }

                if (_RasterShader == null)
                {
                    Debug.LogError("HBAO+ raster shader is null");
                
                    return;
                }
            
                if (_ComputeShader == null)
                {
                    Debug.LogError("HBAO+ compute shader is null");
                
                    return;
                }

#if UNITY_EDITOR && !HBAO_PLUS_DISABLE_IN_EDITOR_SHADER_KEYWORDS_UPDATE
                // Update every frame in the editor to prevent errors on shader recompilation
                _Settings.RasterShaderParams  = new HBAOPlusRasterShaderParams(_RasterShader);
                _Settings.ComputeShaderParams = new HBAOPlusComputeShaderParams(_ComputeShader);
#endif
                // Pass skipped due to settings/hardware
                HBAOPlusUtility.UpdateSettingsFromVolume(VolumeSettingsComponent, _Settings);
                if (!_RenderPass.Setup(_Settings))
                {
                    return;
                }
            
                Renderer.EnqueuePass(_RenderPass);
            }
        }

        /// <summary>
        /// Cached pass instance
        /// </summary>
        private RenderPass _RenderPass;
        
        /// <summary>
        /// Settings used by the pass.
        /// </summary>
        private HBAOPlusSettings _Settings;
        
        /// <summary>
        /// Add render passes marker
        /// </summary>
        private static readonly ProfilingSampler _AddRenderPassesMarker = new("HBAO+ Add Render Passes");
        
        /// <summary>
        /// Default path to the raster shader for HBAO+
        /// </summary>
        private const string _RASTER_SHADER_PATH = "Packages/zeroiqgames.graphics.hbao-plus.urp/URP/Runtime/Scripts/Shaders/HBAOPlusURP.shader";
        
        /// <summary>
        /// Default path to the compute shader for HBAO+
        /// </summary>
        private const string _COMPUTE_SHADER_PATH = "Packages/zeroiqgames.graphics.hbao-plus.urp/URP/Runtime/Scripts/Shaders/HBAOPlusURP.compute";

        /// <summary>
        /// Build-time carrier for the keyword filter rule below. Keeps the <c>_SCREEN_SPACE_OCCLUSION</c> shader variants in the build, which URP otherwise removes while no stock SSAO feature is active.
        /// </summary>
        /// <remarks>Not a user setting. The rule only applies because this is a serialized field, part of the render pipeline asset's type tree.</remarks>
        [UsedImplicitly] // Used by shader prefilter
#if UNITY_EDITOR
        [UnityEditor.ShaderKeywordFilter.SelectIf(true, overridePriority: true, keywordNames: new [] { "", ShaderKeywordStrings.ScreenSpaceOcclusion })]
#endif
        [SerializeField]
        [HideInInspector] // Build-time only; not a user setting.
        private bool _bKeepScreenSpaceOcclusionVariants = true;

#if !UNITY_EDITOR // Use Create() for editor validation
        /// <summary>
        /// Validate renderer feature data
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Throws if any of the shaders cannot be assigned.</exception>
        private void OnValidate()
        {
            bool bValid = true;
            if (_ComputeShader == null)
            {
                bValid = false;
                
                Debug.LogError("HBAO+ compute shader is null. Check that the shader can be found at " + _COMPUTE_SHADER_PATH, this);
            }
            
            if (_RasterShader == null)
            {
                bValid = false;
                
                Debug.LogError("HBAO+ raster shader is null. Check that the shader can be found at " + _RASTER_SHADER_PATH, this);
            }

            
            if (!bValid) // Either raster or compute shader is missing.
            {
#if HBAO_PLUS_IGNORE_VALIDATION
                Debug.LogError("HBAO+ renderer feature validation failed. Check the console for more information. This can result in runtime errors and/or HBAO+ not working even if the player is successfully built.");
#else
                throw new System.InvalidOperationException("HBAO+ renderer feature validation failed. Check the console for more information. You can disable this error (at your own risk) by defining HBAO_PLUS_IGNORE_VALIDATION in your preprocessor definitions.");
#endif
            } 
        }
#endif

        private void OnDestroy()
        {
            // Shouldn't be necessary, but just in case
            RenderPipelineManager.beginContextRendering -= _OnBeginContextRendering;
        }

        /// <summary>
        /// Called every frame before rendering starts. Moves the random jitter view forward by one frame (16 vectors).
        /// </summary>
        private void _OnBeginContextRendering(ScriptableRenderContext Context, List<Camera> Cameras)
        {
            _Settings.Jitter.AdvanceRandomView();
        }
    }
}