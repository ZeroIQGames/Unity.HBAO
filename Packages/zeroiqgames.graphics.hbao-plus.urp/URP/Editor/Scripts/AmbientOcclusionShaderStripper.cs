using System;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZeroIQGames.Graphics.HBAOPlus.URP
{
    /// <summary>
    /// Shader stripper to prevent stripping of SSAO variants. This agrees with the URP shader stripper on everything except SSAO variants which this preserves.
    /// </summary>
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)] // Instantiated by CoreRP shader preprocessor
    public class AmbientOcclusionShaderStripper : IShaderVariantStripper, IShaderVariantStripperScope
    {
        /// <summary>
        /// Always active when URP is active.
        /// </summary>
        public bool active => UniversalRenderPipeline.asset != null;
        
        public bool CanRemoveVariant([DisallowNull] Shader shader, ShaderSnippetData passData, ShaderCompilerData variantData)
        {
            ShaderScriptableStripper.IShaderScriptableStrippingData strippingData = new ShaderScriptableStripper.StrippingData
            {
                volumeFeatures                       = ShaderBuildPreprocessor.volumeFeatures,
                stripSoftShadowQualityLevels         = !ShaderBuildPreprocessor.s_UseSoftShadowQualityLevelKeywords,
                strip2DPasses                        = ShaderBuildPreprocessor.s_Strip2DPasses,
                stripDebugDisplayShaders             = ShaderBuildPreprocessor.s_StripDebugDisplayShaders,
                stripScreenCoordOverrideVariants     = ShaderBuildPreprocessor.s_StripScreenCoordOverrideVariants,
                stripBicubicLightmapSamplingVariants = ShaderBuildPreprocessor.s_StripBicubicLightmapSamplingVariants,
                stripReflectionProbeRotationVariants = ShaderBuildPreprocessor.s_StripReflectionProbeRotationVariants,
                stripUnusedVariants                  = ShaderBuildPreprocessor.s_StripUnusedVariants,
                stripUnusedPostProcessingVariants    = ShaderBuildPreprocessor.s_StripUnusedPostProcessingVariants,
                stripUnusedXRVariants                = ShaderBuildPreprocessor.s_StripXRVariants,
                IsHDRDisplaySupportEnabled           = PlayerSettings.allowHDRDisplaySupport,
                IsRenderCompatibilityMode            =
#if URP_COMPATIBILITY_MODE
                    GraphicsSettings.TryGetRenderPipelineSettings<RenderGraphSettings>(out var renderGraphSettings) && renderGraphSettings.enableRenderCompatibilityMode,
#else
                    false,
#endif
                shader = shader,
                passData = passData,
                variantData = variantData
            };

            if (variantData.shaderKeywordSet.IsEnabled(new GlobalKeyword("_SCREEN_SPACE_OCCLUSION")))
            {
                Debug.Log("ScreenSpaceOcclusion is enabled");
            }
            
            // All feature sets need to have this variant unused to be stripped out.
            bool removeInput = strippingData.stripUnusedVariants;
            if (removeInput)
            {
                for (var index = 0; index < ShaderBuildPreprocessor.supportedFeaturesList.Count; index++)
                {
                    strippingData.shaderFeatures = ShaderBuildPreprocessor.supportedFeaturesList[index] | ShaderFeatures.ScreenSpaceOcclusion;

                    if (_UrpStripper.StripUnusedShaders(ref strippingData))
                    {
                        continue;
                    }

                    if (_UrpStripper.StripUnusedPass(ref strippingData))
                    {
                        continue;
                    }

                    if (_UrpStripper.StripInvalidVariants(ref strippingData))
                    {
                        continue;
                    }

                    if (_UrpStripper.StripUnsupportedVariants(ref strippingData))
                    {
                        continue;
                    }

                    if (_StripUnusedFeatures(ref strippingData))
                    {
                        continue;
                    }

                    removeInput = false;
                    break;
                }
            }

            // Check PostProcessing variants...
            if (!removeInput && strippingData.stripUnusedPostProcessingVariants)
            {
                if (_UrpStripper.StripVolumeFeatures(ShaderBuildPreprocessor.volumeFeatures, ref strippingData))
                {
                    removeInput = true;
                }
            }

            return removeInput;
        }

        /// <summary>
        /// Instance of the UrpShaderVariantStripper.
        /// </summary>
        private ShaderScriptableStripper _UrpStripper = (ShaderScriptableStripper)Activator.CreateInstance(typeof(ShaderScriptableStripper));

        public void BeforeShaderStripping(Shader ShaderVariant)
        {
            _UrpStripper.BeforeShaderStripping(ShaderVariant);
        }

        public void AfterShaderStripping(Shader ShaderVariant)
        {
            _UrpStripper.AfterShaderStripping(ShaderVariant);
        }

        private bool _StripUnusedFeatures(ref ShaderScriptableStripper.IShaderScriptableStrippingData strippingData)
        {
            if (_UrpStripper.StripUnusedFeatures_DebugDisplay(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_ScreenCoordOverride(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_ScreenSpaceIrradiance(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_BicubicLightmapSampling(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_ReflectionProbeRotation(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_MixedLighting(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_PunctualLightShadows(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_FoveatedRendering(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_DeferredRendering(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_DataDrivenLensFlare(ref strippingData))
            {
                return true;
            }

            // Even though, it's a post process and a volume override, we put that here since it depend on a URP asset property.
            if (_UrpStripper.StripUnusedFeatures_ScreenSpaceLensFlare(ref strippingData))
            {
                return true;
            }

            var stripTool = new ShaderStripTool<ShaderFeatures>(strippingData.shaderFeatures, ref strippingData);

            if (_UrpStripper.StripUnusedFeatures_MainLightShadows(ref strippingData, ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_AdditionalLightShadows(ref strippingData, ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_SoftShadows(ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_SoftShadowsQualityLevels(ref strippingData, ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_HDRGrading(ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_UseFastSRGBLinearConversion(ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_LightLayers(ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_RenderPassEnabled(ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_ReflectionProbes(ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_ClusterLightLoop(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_AdditionalLights(ref strippingData, ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_SHAuto(ref strippingData, ref stripTool))
            {
                return true;
            }

            // This removes SSAO variants which we actually need so we remove it.
            // TODO: We only need SSAO keyword on object materials so update this to only keep that instead of keeping every SSAO related variant.
            //if (_UrpStripper.StripUnusedFeatures_ScreenSpaceOcclusion(ref strippingData, ref stripTool))
            //{
            //    return true;
            //}

            if (_UrpStripper.StripUnusedFeatures_DecalsDbuffer(ref strippingData, ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_DecalsNormalBlend(ref strippingData, ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_DecalLayers(ref strippingData, ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_WriteRenderingLayers(ref strippingData, ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_AccurateGbufferNormals(ref strippingData, ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_CrossFadeLod(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_LightCookies(ref strippingData, ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_ProbesVolumes(ref stripTool))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_XRMirrorView(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_XROcclusionMesh(ref strippingData))
            {
                return true;
            }

            if (_UrpStripper.StripUnusedFeatures_XRMotionVector(ref strippingData))
            {
                return true;
            }

            return false;
        }
    }
}