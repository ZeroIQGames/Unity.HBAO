using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;


// ReSharper disable MemberCanBePrivate.Global
namespace ZeroIQGames.Graphics.HBAOPlus.Core.Renderer
{
    internal static partial class HBAOPlusUtility
    {
        /// <summary>
        /// Get the format of the blur intermediate texture based on the blur settings and the current platform.
        /// </summary>
        /// <param name="Settings">HBAO+ settings object</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">Throws if an unexpected <see cref="HBAOPlusSettings.BlurQuality"/> or <see cref="HBAOPlusSettings.BlurSharpnessSource"/> is encountered.</exception>
        public static GraphicsFormat GetBlurTextureFormat(HBAOPlusSettings Settings)
        {
            GraphicsFormat BlurTexFormat;
            if (!Settings.bUseBlur)
            {
                BlurTexFormat = GraphicsFormat.None;
            }
            else
            {
                BlurTexFormat = Settings.BlurQuality switch
                {
                    EQuality.Low    => GraphicsFormat.R8_UNorm,
                    EQuality.Medium => GraphicsFormat.R16_UNorm,
                    EQuality.High   => Settings.BlurSharpnessSource switch
                    {
                        EBlurSharpnessSource.Depth       => GraphicsFormat.R16G16_UNorm,
                        EBlurSharpnessSource.Normal      => GraphicsFormat.R8G8B8A8_UNorm,
                        EBlurSharpnessSource.DepthNormal => GraphicsFormat.R16G16B16A16_UNorm,
                        _                                => throw new ArgumentOutOfRangeException(nameof(Settings.BlurSharpnessSource), Settings.BlurSharpnessSource, "Invalid blur sharpness source."),
                    },
                    _ => throw new ArgumentOutOfRangeException(nameof(Settings.BlurQuality), Settings.BlurQuality, "Invalid blur quality."),
                };
            }

            return SystemInfo.GetCompatibleFormat(BlurTexFormat, GraphicsFormatUsage.Sample | GraphicsFormatUsage.Render);
        }
        
        /// <summary>
        /// Get the value of the given <see cref="VolumeParameter{T}"/> if it is overridden or the passed default value otherwise.
        /// </summary>
        /// <param name="VolumeParam">Parameter to get the value from</param>
        /// <param name="defaultValue">Default value to return if the parameter is not overridden</param>
        /// <typeparam name="T">Type of the parameter value</typeparam>
        /// <returns>Overridden value of the volume parameter or default value if not overridden</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetValueOrDefault<T>(this VolumeParameter<T> VolumeParam, T defaultValue)
        {
            return VolumeParam.overrideState ? VolumeParam.value : defaultValue;
        }
        
        /// <summary>
        /// Set the ambient occlusion global shader parameters.
        /// </summary>
        /// <param name="Cmd">Command buffer to set the parameter with</param>
        /// <param name="Settings">HBAO+ settings</param>
        public static void SetAmbientOcclusionParamsShaderProperty(IBaseCommandBuffer Cmd, HBAOPlusSettings Settings)
        {
            Cmd.SetGlobalVector(HBAOPlusShaderParams.AmbientOcclusionParamsPropertyId, new Vector4(1f, 0f, 0f, Settings.DirectLightingStrength));
        }
        
        /// <summary>
        /// Set the ambient occlusion global keyword that signals shaders that Ambient Occlusion data is available and should be used.
        /// </summary>
        /// <param name="Cmd">Command buffer to set the parameter with</param>
        /// <param name="bEnabled">Keyword enabled value</param>
        public static void SetAmbientOcclusionGlobalKeyword(IBaseCommandBuffer Cmd, bool bEnabled)
        {
            Cmd.SetKeyword(HBAOPlusShaderParams.ScreenSpaceAmbientOcclusionKeyword, bEnabled);
        }
        
        /// <summary>
        /// Update settings parameters using the <paramref name="VolumeSettingsComponent"/> argument
        /// </summary>
        /// <param name="VolumeSettingsComponent">Volume settings component to use for settings value</param>
        /// <param name="Settings">Settings object to update</param>
        public static void UpdateSettingsFromVolume(HBAOPlusVolumeComponent VolumeSettingsComponent, HBAOPlusSettings Settings)
        {
            if (VolumeSettingsComponent.bForegroundAO.GetValueOrDefault(HBAOPlusDefaultSettings.ENABLE_FOREGROUND_AO))
            {
                Settings.ForegroundAOViewDepth = VolumeSettingsComponent.ForegroundAOViewDepth.GetValueOrDefault(HBAOPlusDefaultSettings.FOREGROUND_AO_VIEW_DEPTH);
            }
            else
            {
                Settings.ForegroundAOViewDepth = -1.0f;
            }
            if (VolumeSettingsComponent.bBackgroundAO.GetValueOrDefault(HBAOPlusDefaultSettings.ENABLE_BACKGROUND_AO))
            {
                Settings.BackgroundAOViewDepth = Mathf.Max(VolumeSettingsComponent.BackgroundAOViewDepth.GetValueOrDefault(HBAOPlusDefaultSettings.BACKGROUND_VIEW_DEPTH), Settings.ForegroundAOViewDepth);
            }
            else
            {
                Settings.BackgroundAOViewDepth = -1.0f;
            }
            Settings.Bias              = VolumeSettingsComponent.Bias.GetValueOrDefault(HBAOPlusDefaultSettings.TESSELATION_BIAS);
            Settings.LargeScaleAO      = VolumeSettingsComponent.LargeScaleAO.GetValueOrDefault(HBAOPlusDefaultSettings.LARGE_SCALE_AO_FACTOR);
            Settings.SmallScaleAO      = VolumeSettingsComponent.SmallScaleAO.GetValueOrDefault(HBAOPlusDefaultSettings.SMALL_SCALE_AO_FACTOR);
            Settings.AORadius          = VolumeSettingsComponent.AORadius.GetValueOrDefault(HBAOPlusDefaultSettings.AO_RADIUS);
            Settings.IntensityExponent = VolumeSettingsComponent.IntensityExponent.GetValueOrDefault(HBAOPlusDefaultSettings.INTENSITY_EXPONENT);
            
            // Blur
            Settings.bUseBlur                     = VolumeSettingsComponent.bBlurEnabled.GetValueOrDefault(HBAOPlusDefaultSettings.BLUR_ENABLED);
            Settings.bDepthDependentBlurSharpness = VolumeSettingsComponent.bDepthDependentBlurSharpness.GetValueOrDefault(HBAOPlusDefaultSettings.DEPTH_DEPENDENT_BLUR_SHARPNESS);
            Settings.BlurForegroundSharpness      = VolumeSettingsComponent.ForegroundBlurSharpness.GetValueOrDefault(HBAOPlusDefaultSettings.BLUR_SHARPNESS);
            Settings.BlurBackgroundSharpness      = VolumeSettingsComponent.BackgroundBlurSharpness.GetValueOrDefault(HBAOPlusDefaultSettings.BLUR_SHARPNESS);
            Settings.BlurBackgroundViewDepth      = VolumeSettingsComponent.BackgroundBlurViewDepth.GetValueOrDefault(HBAOPlusDefaultSettings.BACKGROUND_BLUR_VIEW_DEPTH);
            Settings.BlurForegroundViewDepth      = VolumeSettingsComponent.ForegroundBlurViewDepth.GetValueOrDefault(HBAOPlusDefaultSettings.FOREGROUND_BLUR_VIEW_DEPTH);
            Settings.BlurUniformSharpness         = VolumeSettingsComponent.UniformBlurSharpness.GetValueOrDefault(HBAOPlusDefaultSettings.BLUR_SHARPNESS);
            Settings.DirectLightingStrength       = VolumeSettingsComponent.DirectLightingStrength.GetValueOrDefault(HBAOPlusDefaultSettings.DIRECT_LIGHTING_STRENGTH);
        }
        
        /// <summary>
        /// Set up shader parameters that are uniform in the same frame through all the passes for the given camera.
        /// </summary>
        /// <param name="RenderCamera">Camera that will render the frame</param>
        /// <param name="Settings">HBAO+ settings</param>
        /// <param name="Resources">Resources for the HBAO+</param>
        public static void SetupPerViewShaderParameters<TTextureHandle>(Camera RenderCamera, HBAOPlusSettings Settings, in HBAOPlusResources<TTextureHandle> Resources)
        {
            // AO parameters
            float CameraHeight        = RenderCamera.pixelHeight;
            float TanHalfVerticalFOV  = Resources.TanHalfVerticalFOV;
            float AORadius2           = Settings.AORadius * Settings.AORadius;
            float NegativeInvR2       = -1.0f / AORadius2;
            float AOVerticalRToScreen = Settings.AORadius * 0.5f * CameraHeight / TanHalfVerticalFOV;

            // Material CBuffer
            Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.AORadius2PropertyId, AORadius2);
            Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.AONegativeInverseRadius2PropertyId, NegativeInvR2);
            Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.AOVerticalRadiusToScreenPixelsPropertyId, AOVerticalRToScreen);
            Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.ForegroundAORadiusPixelsPropertyId, Settings.bForegroundAOEnabled ? AOVerticalRToScreen / Settings.ForegroundAOViewDepth : -1.0f);
            Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.BackgroundAORadiusPixelsPropertyId, Settings.bBackgroundAOEnabled ? AOVerticalRToScreen / Settings.BackgroundAOViewDepth : -1.0f);
            Settings.RasterPassMaterial.SetVector(HBAOPlusShaderParams.UVToViewParamsPropertyId, Resources.UVToViewParams);
            Settings.RasterPassMaterial.SetVector(HBAOPlusShaderParams.FullResDimensionsPropertyId, Resources.FullResDimensions);
            Settings.RasterPassMaterial.SetVector(HBAOPlusShaderParams.QuarterResDimensionsPropertyId, Resources.QuarterResDimensions);
            Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.TessellationBiasPropertyId, Settings.Bias);
            Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.LargeScaleAOAmountPropertyId, Settings.LargeScaleAO);
            Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.SmallScaleAOAmountPropertyId, Settings.SmallScaleAO);
            Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.IntensityExponentPropertyId, Settings.IntensityExponent);
            Settings.RasterPassMaterial.SetFloatArray(HBAOPlusRasterShaderParams.DepthTextureSliceIndicesPropertyId, _RasterDepthTextureArrayIndices);
            
            Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.AORadius2PropertyId, AORadius2);
            Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.AONegativeInverseRadius2PropertyId, NegativeInvR2);
            Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.AOVerticalRadiusToScreenPixelsPropertyId, AOVerticalRToScreen);
            Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.ForegroundAORadiusPixelsPropertyId, Settings.bForegroundAOEnabled ? AOVerticalRToScreen / Settings.ForegroundAOViewDepth : -1.0f);
            Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.BackgroundAORadiusPixelsPropertyId, Settings.bBackgroundAOEnabled ? AOVerticalRToScreen / Settings.BackgroundAOViewDepth : -1.0f);
            Settings.ComputePassShader.SetVector(HBAOPlusShaderParams.UVToViewParamsPropertyId, Resources.UVToViewParams);
            Settings.ComputePassShader.SetVector(HBAOPlusShaderParams.FullResDimensionsPropertyId, Resources.FullResDimensions);
            Settings.ComputePassShader.SetVector(HBAOPlusShaderParams.QuarterResDimensionsPropertyId, Resources.QuarterResDimensions);
            Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.TessellationBiasPropertyId, Settings.Bias);
            Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.LargeScaleAOAmountPropertyId, Settings.LargeScaleAO);
            Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.SmallScaleAOAmountPropertyId, Settings.SmallScaleAO);
            Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.IntensityExponentPropertyId, Settings.IntensityExponent);

            if (!Settings.bUseBlur)
            {
                return;
            } 
            
            // Blur
            if (Settings.bDepthDependentBlurSharpness)
            {
                Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.BlurForegroundSharpnessPropertyId, Settings.BlurForegroundSharpness);
                Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.BlurBackgroundSharpnessPropertyId, Settings.BlurBackgroundSharpness);
                Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.BlurForegroundViewDepthPropertyId, Settings.BlurForegroundViewDepth);
                Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.BlurBackgroundViewDepthPropertyId, Settings.BlurBackgroundViewDepth);
                
                Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.BlurForegroundSharpnessPropertyId, Settings.BlurForegroundSharpness);
                Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.BlurBackgroundSharpnessPropertyId, Settings.BlurBackgroundSharpness);
                Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.BlurForegroundViewDepthPropertyId, Settings.BlurForegroundViewDepth);
                Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.BlurBackgroundViewDepthPropertyId, Settings.BlurBackgroundViewDepth);
            }
            else // Uniform blur
            {
                Settings.RasterPassMaterial.SetFloat(HBAOPlusShaderParams.BlurUniformSharpnessPropertyId, Settings.BlurUniformSharpness);
                Settings.ComputePassShader.SetFloat(HBAOPlusShaderParams.BlurUniformSharpnessPropertyId, Settings.BlurUniformSharpness);
            }
        }
        
        /// <summary>
        /// Set up shader keywords that are constant throughout the frame for all cameras
        /// </summary>
        /// <param name="Settings">HBAO+ settings</param>
        public static void SetupShaderKeywords(HBAOPlusSettings Settings)
        {
            if (Settings.ComputePassShader == null)
            {
                Debug.LogError("Compute shader is not set up for HBAO+.");
                
                return;
            }

            if (Settings.RasterPassMaterial == null)
            {
                Debug.LogError("Material is not set up for HBAO+.");
                
                return;
            }
            
            // AO quality keywords
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.EightStepCoarseAOKeyword, Settings.AOQuality is EQuality.High);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.EightDirectionCoarseAOKeyword, Settings.AOQuality is not EQuality.Low);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.FourStepCoarseAOKeyword, Settings.AOQuality is not EQuality.High);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.FourDirectionCoarseAOKeyword, Settings.AOQuality is EQuality.Low);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.EightStepCoarseAOKeyword, Settings.AOQuality is EQuality.High);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.EightDirectionCoarseAOKeyword, Settings.AOQuality is not EQuality.Low);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.FourStepCoarseAOKeyword, Settings.AOQuality is not EQuality.High);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.FourDirectionCoarseAOKeyword, Settings.AOQuality is EQuality.Low);
            
            // Whether if normals in the camera normals texture, are in world space or view space
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.NormalsInWorldSpaceKeyword, Settings.bUseDepthNormal);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.NormalsInWorldSpaceKeyword, Settings.bUseDepthNormal);

            // Whether if blur is enabled
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.BlurPostProcessKeyword, Settings.bUseBlur);
            
            // Blur kernel radius size
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.BlurKernelRadiusOneKeyword, Settings.BlurKernelRadius is EBlurRadius.One);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.BlurKernelRadiusTwoKeyword, Settings.BlurKernelRadius is EBlurRadius.Two);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.BlurKernelRadiusThreeKeyword, Settings.BlurKernelRadius is EBlurRadius.Three);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.BlurKernelRadiusFourKeyword, Settings.BlurKernelRadius is EBlurRadius.Four);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.BlurKernelRadiusOneKeyword, Settings.BlurKernelRadius is EBlurRadius.One);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.BlurKernelRadiusTwoKeyword, Settings.BlurKernelRadius is EBlurRadius.Two);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.BlurKernelRadiusThreeKeyword, Settings.BlurKernelRadius is EBlurRadius.Three);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.BlurKernelRadiusFourKeyword, Settings.BlurKernelRadius is EBlurRadius.Four);

            // Blur Algorithm
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.BlurAlgorithmBilateralKeyword, Settings.BlurQuality is EQuality.High);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.BlurAlgorithmGaussianKeyword, Settings.BlurQuality is EQuality.Medium);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.BlurAlgorithmKawaseKeyword, Settings.BlurQuality is EQuality.Low);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.BlurAlgorithmBilateralKeyword, Settings.BlurQuality is EQuality.High);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.BlurAlgorithmGaussianKeyword, Settings.BlurQuality is EQuality.Medium);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.BlurAlgorithmKawaseKeyword, Settings.BlurQuality is EQuality.Low);
            
            // Blur Sharpness
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.RasterDepthDependentBlurSharpnessKeyword, Settings.bDepthDependentBlurSharpness);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.BlurSharpnessFromDepthKeyword, Settings.BlurSharpnessSource is EBlurSharpnessSource.Depth);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.BlurSharpnessFromNormalKeyword, Settings.BlurSharpnessSource is EBlurSharpnessSource.Normal);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.BlurSharpnessFromDepthNormalKeyword, Settings.BlurSharpnessSource is EBlurSharpnessSource.DepthNormal);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.BlurSharpnessFromDepthKeyword, Settings.BlurSharpnessSource is EBlurSharpnessSource.Depth);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.BlurSharpnessFromNormalKeyword, Settings.BlurSharpnessSource is EBlurSharpnessSource.Normal);
            Settings.ComputePassShader.SetKeyword(Settings.ComputeShaderParams.BlurSharpnessFromDepthNormalKeyword, Settings.BlurSharpnessSource is EBlurSharpnessSource.DepthNormal);
            
            // Shader model keywords
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.SupportsShaderTarget45Keyword, SystemInfo.graphicsShaderLevel >= 45);
            //Settings.RasterPassMaterial.SetKeyword(Settings.ShaderKeywords.SupportsSetArrayIndexFromAnyStageKeyword, SystemInfo.supportsRenderTargetArrayIndexFromVertexShader);
            //Settings.RasterPassMaterial.SetKeyword(Settings.ShaderKeywords.SupportsGeometryShaderKeyword, SystemInfo.supportsGeometryShaders);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.SupportsInstancingKeyword, SystemInfo.supportsInstancing);
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.SupportsIntegerOperationsKeyword, SystemInfo.graphicsShaderLevel >= 40); // Supported on shader level 4.0 and newer
            Settings.RasterPassMaterial.SetKeyword(Settings.RasterShaderParams.RasterOnlyRenderPathKeyword, Settings.AORenderPath is not EAORenderPath.Compute);
        }
        
        #region Depth Linearization Pass
        
        
            
        /// <summary>
        /// Initialize depth linearization pass data object based on the render resources and pass settings.
        /// </summary>
        /// <param name="PassData">Pass data object to initialize</param>
        /// <param name="Settings">Pass settings object</param>
        /// <param name="Resources">Resources for the pass</param>
        public static void InitializeDepthLinearizationPassData<TTextureHandle>(DepthLinearizationPassData<TTextureHandle> PassData, HBAOPlusSettings Settings, in HBAOPlusResources<TTextureHandle> Resources)
        {
            PassData.Settings               = Settings;
            PassData.HardwareDepthTexture   = Resources.HardwareDepthTexture;
            PassData.LinearizedDepthTexture = Resources.LinearizedDepthTexture;
            PassData.PassPropertyBlock      = Resources.PassPropertyBlock;
        }

        public static void ApplyDepthLinearizationPassParams(RasterCommandBuffer Cmd, DepthLinearizationPassData<TextureHandle> PassData, bool bClearMatPropBlock = true)
        {
            if (bClearMatPropBlock)
            {
                PassData.PassPropertyBlock.Clear();
            }
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DepthTextureOnePropertyId, PassData.HardwareDepthTexture);
            PassData.PassPropertyBlock.SetVector(HBAOPlusRasterShaderParams.ScaleBiasPropertyId, Vector2.one);
        }

        public static void ApplyDepthLinearizationPassParams(CommandBuffer Cmd, DepthLinearizationPassData<RTHandle> PassData, bool bClearMatPropBlock = true)
        {
            if (bClearMatPropBlock)
            {
                PassData.PassPropertyBlock.Clear();
            }
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DepthTextureOnePropertyId, PassData.HardwareDepthTexture);
            PassData.PassPropertyBlock.SetVector(HBAOPlusRasterShaderParams.ScaleBiasPropertyId, Vector2.one);
        }
        
        #endregion
                    
        #region Depth Deinterleave Pass

        /// <summary>
        /// 
        /// </summary>
        /// <param name="PassData">Pass data object to initialize</param>
        /// <param name="Settings">Pass settings object</param>
        /// <param name="Resources">Resources for the pass</param>
        public static void InitializeDepthDeinterleavePassData<TTextureHandle>(DepthDeinterleaveComputePassData<TTextureHandle> PassData, HBAOPlusSettings Settings, in HBAOPlusResources<TTextureHandle> Resources)
        {
            PassData.DeinterleavedDepthTexture           = Resources.ComputeDeinterleavedDepthTexture;
            PassData.HardwareDepthTexture                = Resources.HardwareDepthTexture;
            PassData.Settings                            = Settings;
            PassData.DepthTextureDimensions              = Resources.FullResDimensions;
            PassData.DeinterleavedDepthTextureDimensions = Resources.QuarterResDimensions;
        }
        
        /// <summary>
        /// Initialize the depth deinterleave pass data object for raster pass based on the render resources and pass settings.
        /// </summary>
        /// <param name="PassData">Pass data object to initialize</param>
        /// <param name="Settings">Pass settings object</param>
        /// <param name="Resources">Resources for the pass</param>
        public static void InitializeDepthDeinterleavePassData<TTextureHandle>(DepthDeinterleaveRasterPassData<TTextureHandle> PassData, HBAOPlusSettings Settings, in HBAOPlusResources<TTextureHandle> Resources)
        {
            PassData.DeinterleavedDepthTextureOne        = Resources.RasterDeinterleavedDepthTextureOne;
            PassData.DeinterleavedDepthTextureTwo        = Resources.RasterDeinterleavedDepthTextureTwo;
            PassData.DeinterleavedDepthTextureThree      = Resources.RasterDeinterleavedDepthTextureThree;
            PassData.DeinterleavedDepthTextureFour       = Resources.RasterDeinterleavedDepthTextureFour;
            PassData.HardwareDepthTexture                = Resources.HardwareDepthTexture;
            PassData.Settings                            = Settings;
            PassData.FullResDimensions                   = Resources.FullResDimensions;
            PassData.PassPropertyBlock                   = Resources.PassPropertyBlock;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Cmd"></param>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeDepthDeinterleavePassData{TTextureHandle}(DepthDeinterleaveComputePassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="bClearMatPropBlock">Whether if the material property block should be cleared first.</param>
        public static void ApplyDepthDeinterleavePassParams(IComputeCommandBuffer Cmd, DepthDeinterleaveComputePassData<TextureHandle> PassData, bool bClearMatPropBlock = true)
        {
            Cmd.SetComputeVectorParam(
                PassData.Settings.ComputePassShader, 
                HBAOPlusShaderParams.FullResDimensionsPropertyId, 
                PassData.DepthTextureDimensions
            );
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader, 
                PassData.Settings.ComputeShaderParams.DeinterleaveDepthKernelId,
                HBAOPlusShaderParams.DepthTextureOnePropertyId, 
                PassData.HardwareDepthTexture
            );
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader, 
                PassData.Settings.ComputeShaderParams.DeinterleaveDepthKernelId,
                HBAOPlusComputeShaderParams.DeinterleavedDepthTextureRWPropertyId, 
                PassData.DeinterleavedDepthTexture
            );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Cmd"></param>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeDepthDeinterleavePassData{TTextureHandle}(DepthDeinterleaveComputePassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="bClearMatPropBlock">Whether if the material property block should be cleared first.</param>
        public static void ApplyDepthDeinterleavePassParams(CommandBuffer Cmd, DepthDeinterleaveComputePassData<RTHandle> PassData, bool bClearMatPropBlock = true)
        {
            Cmd.SetComputeVectorParam(
                PassData.Settings.ComputePassShader, 
                HBAOPlusShaderParams.FullResDimensionsPropertyId, 
                PassData.DepthTextureDimensions
            );
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader, 
                PassData.Settings.ComputeShaderParams.DeinterleaveDepthKernelId,
                HBAOPlusShaderParams.DepthTextureOnePropertyId, 
                PassData.HardwareDepthTexture
            );
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader, 
                PassData.Settings.ComputeShaderParams.DeinterleaveDepthKernelId,
                HBAOPlusComputeShaderParams.DeinterleavedDepthTextureRWPropertyId, 
                PassData.DeinterleavedDepthTexture
            );
        }

        /// <summary>
        /// Apply depth deinterleave material parameters for raster deinterleaving.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeDepthDeinterleavePassData{TTextureHandle}(DepthDeinterleaveRasterPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="bClearMatPropBlock">Whether if the material property block should be cleared first.</param>
        public static void ApplyDepthDeinterleavePassParams(DepthDeinterleaveRasterPassData<TextureHandle> PassData, bool bClearMatPropBlock = true)
        {
            if (bClearMatPropBlock)
            {
                PassData.PassPropertyBlock.Clear();
            }
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DepthTextureOnePropertyId, PassData.HardwareDepthTexture);
            PassData.PassPropertyBlock.SetVector(HBAOPlusShaderParams.FullResDimensionsPropertyId, PassData.FullResDimensions);
            PassData.PassPropertyBlock.SetVector(HBAOPlusRasterShaderParams.ScaleBiasPropertyId, Vector2.one);
        }
        
        /// <summary>
        /// Apply depth deinterleave material parameters for raster deinterleaving.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeDepthDeinterleavePassData{TTextureHandle}(DepthDeinterleaveRasterPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="bClearMatPropBlock">Whether if the material property block should be cleared first.</param>
        public static void ApplyDepthDeinterleavePassParams(DepthDeinterleaveRasterPassData<RTHandle> PassData, bool bClearMatPropBlock = true)
        {
            if (bClearMatPropBlock)
            {
                PassData.PassPropertyBlock.Clear();
            }
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DepthTextureOnePropertyId, PassData.HardwareDepthTexture);
            PassData.PassPropertyBlock.SetVector(HBAOPlusShaderParams.FullResDimensionsPropertyId, PassData.FullResDimensions);
            PassData.PassPropertyBlock.SetVector(HBAOPlusRasterShaderParams.ScaleBiasPropertyId, Vector2.one);
        }
        
        /// <summary>
        /// Apply depth deinterleave material parameters for raster deinterleaving.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeDepthDeinterleavePassData{TTextureHandle}(DepthDeinterleaveRasterPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="SliceIndex">Index of the slice being processed</param>
        public static void ApplyDepthDeinterleavePerSliceParams<TTextureHandle>(DepthDeinterleaveRasterPassData<TTextureHandle> PassData, int SliceIndex)
        {
            PassData.PassPropertyBlock.SetInteger(HBAOPlusRasterShaderParams.RenderSliceIndexPropertyId, SliceIndex);
        }

        #endregion
        
        #region Normal Reconstruction Pass

        /// <summary>
        /// Initialize Normal reconstruction pass data object based on the render resources and pass settings.
        /// </summary>
        /// <param name="PassData">Pass data object to initialize</param>
        /// <param name="Settings">Pass settings object</param>
        /// <param name="Resources">Resources for the pass</param>
        public static void InitializeNormalReconstructionPassData<TTextureHandle>(NormalReconstructionPassData<TTextureHandle> PassData, HBAOPlusSettings Settings, in HBAOPlusResources<TTextureHandle> Resources)
        {
            PassData.LinearizedDepthTexture = Resources.LinearizedDepthTexture;
            PassData.PassPropertyBlock      = Resources.PassPropertyBlock;
            PassData.Settings               = Settings;
        }

        /// <summary>
        /// Apply normal reconstruction material parameters.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeNormalReconstructionPassData"/></param>
        /// <param name="bClearMatPropBlock">Whether if the material property block should be cleared first.</param>
        public static void ApplyNormalReconstructionPassParams(NormalReconstructionPassData<TextureHandle> PassData, bool bClearMatPropBlock = true)
        {
            if (bClearMatPropBlock)
            {
                PassData.PassPropertyBlock.Clear();
            }
            
            PassData.PassPropertyBlock.Clear();
            PassData.PassPropertyBlock.SetVector(HBAOPlusRasterShaderParams.ScaleBiasPropertyId, Vector2.one);
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.LinearizedDepthTexturePropertyId, PassData.LinearizedDepthTexture);
        }
        
        /// <summary>
        /// Apply normal reconstruction material parameters.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeNormalReconstructionPassData"/></param>
        /// <param name="bClearMatPropBlock">Whether if the material property block should be cleared first.</param>
        public static void ApplyNormalReconstructionPassParams(NormalReconstructionPassData<RTHandle> PassData, bool bClearMatPropBlock = true)
        {
            if (bClearMatPropBlock)
            {
                PassData.PassPropertyBlock.Clear();
            }
            
            PassData.PassPropertyBlock.Clear();
            PassData.PassPropertyBlock.SetVector(HBAOPlusRasterShaderParams.ScaleBiasPropertyId, Vector2.one);
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.LinearizedDepthTexturePropertyId, PassData.LinearizedDepthTexture);
        }

        #endregion

        #region Coarse AO Pass

        /// <summary>
        /// Get the coarse AO raster pass index based on the render path and system capabilities.
        /// </summary>
        /// <param name="Settings">HBAO plus settings</param>
        /// <returns>Index of the coarse AO pass</returns>
        // ReSharper disable once MemberCanBePrivate.Global
        public static int GetCoarseAOPassIndex(HBAOPlusSettings Settings)
        {
            if (SystemInfo.supportsRenderTargetArrayIndexFromVertexShader)
            {
                return (int)ERasterShaderPass.CoarseAOArrayTargetVS;
            }
            
            if (Settings.AORenderPath is EAORenderPath.Raster) // Fallback to geometry if RenderTargetArrayIndex semantic is not supported in vertex shader
            {
                return (int)ERasterShaderPass.CoarseAOWithGS;
            }
            
            return (int)ERasterShaderPass.CoarseAOSimpleRaster; // Fallback to simple raster if geometry is not supported
        }
        
        /// <summary>
        /// Initialize coarse AO pass data object based on the render resources and pass settings.
        /// </summary>
        /// <param name="PassData">Pass data object to initialize</param>
        /// <param name="Settings">Pass settings object</param>
        /// <param name="Resources">Resources for the pass</param>
        public static void InitializeCoarseAOPassData<TTextureHandle>(CoarseAOComputePassData<TTextureHandle> PassData, HBAOPlusSettings Settings, in HBAOPlusResources<TTextureHandle> Resources)
        {
            PassData.SceneNormalsTexture          = Resources.SceneNormalsTexture;
            PassData.Settings                     = Settings;
            PassData.DeinterleavedDepthTexture    = Resources.ComputeDeinterleavedDepthTexture;
            PassData.DeinterleavedCoarseAOTexture = Resources.DeinterleavedAOTexture;
            PassData.ThreadGroupsCount            = new int3(
                Mathf.CeilToInt(Resources.QuarterResDimensions.x / HBAOPlusComputeShaderParams.CoarseAOKernalThreadGroupDimensions.x),
                Mathf.CeilToInt(Resources.QuarterResDimensions.y / HBAOPlusComputeShaderParams.CoarseAOKernalThreadGroupDimensions.y),
                16 // 16 layers of coarse AO
            );
        }
        
        /// <summary>
        /// Initialize coarse AO pass data object based on the render resources and pass settings.
        /// </summary>
        /// <param name="PassData">Pass data object to initialize</param>
        /// <param name="Settings">Pass settings object</param>
        /// <param name="Resources">Resources for the pass</param>
        public static void InitializeCoarseAOPassData<TTextureHandle>(CoarseAORasterPassData<TTextureHandle> PassData, HBAOPlusSettings Settings, in HBAOPlusResources<TTextureHandle> Resources)
        {
            PassData.SceneNormalsTexture     = Resources.SceneNormalsTexture;
            PassData.PassPropertyBlock       = Resources.PassPropertyBlock;
            PassData.Settings                = Settings;
            PassData.PassIndex               = GetCoarseAOPassIndex(Settings);
            
            // Deinterleaved depth textures
            PassData.DeinterleavedDepthTexture      = Resources.ComputeDeinterleavedDepthTexture;
            PassData.DeinterleavedDepthTextureOne   = Resources.RasterDeinterleavedDepthTextureOne;
            PassData.DeinterleavedDepthTextureTwo   = Resources.RasterDeinterleavedDepthTextureTwo;
            PassData.DeinterleavedDepthTextureThree = Resources.RasterDeinterleavedDepthTextureThree;
            PassData.DeinterleavedDepthTextureFour  = Resources.RasterDeinterleavedDepthTextureFour;
        }

        /// <summary>
        /// Apply coarse AO compute kernel parameters
        /// </summary>
        /// <param name="Cmd">Compute command buffer for the pass</param>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeCoarseAOPassData{TTextureHandle}(CoarseAOComputePassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        public static void ApplyCoarseAOComputePassParams(IComputeCommandBuffer Cmd, CoarseAOComputePassData<TextureHandle> PassData)
        {
            // Scene normals texture
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader, 
                PassData.Settings.ComputeShaderParams.CoarseAOKernelId, 
                HBAOPlusShaderParams.SceneNormalsTexturePropertyId, 
                PassData.SceneNormalsTexture
            );
            
            // Deinterleaved depth texture
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader, 
                PassData.Settings.ComputeShaderParams.CoarseAOKernelId, 
                HBAOPlusShaderParams.DeinterleavedDepthTextureParamId, 
                PassData.DeinterleavedDepthTexture
            );
            
            // RW AO texture
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader, 
                PassData.Settings.ComputeShaderParams.CoarseAOKernelId,
                HBAOPlusComputeShaderParams.DeinterleavedAOTextureRWPropertyId, 
                PassData.DeinterleavedCoarseAOTexture
            );
            
            // Jitter values for all 16 slices
            Cmd.SetComputeVectorArrayParam(
                PassData.Settings.ComputePassShader, 
                HBAOPlusShaderParams.SliceAOJitterArrayParamId, 
                PassData.Settings.Jitter.GetJitterViewNoAlloc(_JitterValuesPreAlloc)
            );
        }

        /// <summary>
        /// Apply coarse AO compute kernel parameters
        /// </summary>
        /// <param name="Cmd">Native command buffer for the pass</param>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeCoarseAOPassData{TTextureHandle}(CoarseAOComputePassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        public static void ApplyCoarseAOComputePassParams(CommandBuffer Cmd, CoarseAOComputePassData<RTHandle> PassData)
        {
            // Scene normals texture
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader, 
                PassData.Settings.ComputeShaderParams.CoarseAOKernelId, 
                HBAOPlusShaderParams.SceneNormalsTexturePropertyId, 
                PassData.SceneNormalsTexture
            );
            
            // Deinterleaved depth texture
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader, 
                PassData.Settings.ComputeShaderParams.CoarseAOKernelId, 
                HBAOPlusShaderParams.DeinterleavedDepthTextureParamId, 
                PassData.DeinterleavedDepthTexture
            );
            
            // RW AO texture
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader, 
                PassData.Settings.ComputeShaderParams.CoarseAOKernelId,
                HBAOPlusComputeShaderParams.DeinterleavedAOTextureRWPropertyId, 
                PassData.DeinterleavedCoarseAOTexture
            );
            
            // Jitter values for all 16 slices
            Cmd.SetComputeVectorArrayParam(
                PassData.Settings.ComputePassShader, 
                HBAOPlusShaderParams.SliceAOJitterArrayParamId, 
                PassData.Settings.Jitter.GetJitterViewNoAlloc(_JitterValuesPreAlloc)
            );
        }
        
        /// <summary>
        /// Apply slice index invariant material parameters for the coarse AO pass.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeCoarseAOPassData{TTextureHandle}(CoarseAORasterPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="bClearMatPropBlock">Whether if the material property block should be cleared first.</param>
        public static void ApplySharedCoarseAOPassParams(CoarseAORasterPassData<TextureHandle> PassData, bool bClearMatPropBlock = true)
        {
            if (bClearMatPropBlock)
            {
                PassData.PassPropertyBlock.Clear();
            }
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.SceneNormalsTexturePropertyId, PassData.SceneNormalsTexture);
            PassData.PassPropertyBlock.SetFloatArray(HBAOPlusRasterShaderParams.DepthTextureSliceIndicesPropertyId, _RasterDepthTextureArrayIndices);
            PassData.PassPropertyBlock.SetVectorArray(HBAOPlusShaderParams.SliceAOJitterArrayParamId, PassData.Settings.Jitter.GetJitterViewNoAlloc(_JitterValuesPreAlloc));
            PassData.PassPropertyBlock.SetVector(HBAOPlusRasterShaderParams.ScaleBiasPropertyId, Vector2.one);
        }
        
        /// <summary>
        /// Apply slice index invariant material parameters for the coarse AO pass.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeCoarseAOPassData{TTextureHandle}(CoarseAORasterPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="bClearMatPropBlock">Whether if the material property block should be cleared first.</param>
        public static void ApplySharedCoarseAOPassParams(CoarseAORasterPassData<RTHandle> PassData, bool bClearMatPropBlock = true)
        {
            if (bClearMatPropBlock)
            {
                PassData.PassPropertyBlock.Clear();
            }
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.SceneNormalsTexturePropertyId, PassData.SceneNormalsTexture);
            PassData.PassPropertyBlock.SetFloatArray(HBAOPlusRasterShaderParams.DepthTextureSliceIndicesPropertyId, _RasterDepthTextureArrayIndices);
            PassData.PassPropertyBlock.SetVectorArray(HBAOPlusShaderParams.SliceAOJitterArrayParamId, PassData.Settings.Jitter.GetJitterViewNoAlloc(_JitterValuesPreAlloc));
            PassData.PassPropertyBlock.SetVector(HBAOPlusRasterShaderParams.ScaleBiasPropertyId, Vector2.one);
        }
        
        /// <summary>
        /// Apply per-slice index material parameters for the coarse AO pass.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeCoarseAOPassData{TTextureHandle}(CoarseAORasterPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="SliceIndex">Current pass slice index</param>
        public static void ApplyPerSliceCoarseAOPassParams(CoarseAORasterPassData<TextureHandle> PassData, int SliceIndex)
        {
            if (PassData.Settings.AORenderPath is EAORenderPath.Compute)
            {
                PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DeinterleavedDepthTextureParamId, PassData.DeinterleavedDepthTexture);
                PassData.PassPropertyBlock.SetInteger(HBAOPlusRasterShaderParams.DepthTextureSliceIndicesPropertyId, SliceIndex);
            }
            else
            {
                PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DeinterleavedDepthTextureParamId, _GetAODepthTextureFromSliceIndex(PassData, SliceIndex));
            }
            PassData.PassPropertyBlock.SetInteger(HBAOPlusRasterShaderParams.RenderSliceIndexPropertyId, SliceIndex);
        }
        
        /// <summary>
        /// Apply per-slice index material parameters for the coarse AO pass.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeCoarseAOPassData{TTextureHandle}(CoarseAORasterPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="SliceIndex">Current pass slice index</param>
        public static void ApplyPerSliceCoarseAOPassParams(CoarseAORasterPassData<RTHandle> PassData, int SliceIndex)
        {
            if (PassData.Settings.AORenderPath is EAORenderPath.Compute)
            {
                PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DeinterleavedDepthTextureParamId, PassData.DeinterleavedDepthTexture);
                PassData.PassPropertyBlock.SetInteger(HBAOPlusRasterShaderParams.DepthTextureSliceIndicesPropertyId, SliceIndex);
            }
            else
            {
                PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DeinterleavedDepthTextureParamId, _GetAODepthTextureFromSliceIndex(PassData, SliceIndex));
            }
            PassData.PassPropertyBlock.SetInteger(HBAOPlusRasterShaderParams.RenderSliceIndexPropertyId, SliceIndex);
        }
        
        /// <summary>
        /// Apply per instanced draw call material parameters for the coarse AO pass.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeCoarseAOPassData{TTextureHandle}(CoarseAORasterPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="DepthTextureIndex">Index of the current depth texture being processed. Valid values are [0, 4)</param>
        public static void ApplyInstancedCoarseAOPassParams(CoarseAORasterPassData<TextureHandle> PassData, int DepthTextureIndex)
        {
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DeinterleavedDepthTextureParamId, _GetAODepthTextureFromTextureIndex(PassData, DepthTextureIndex));
            PassData.PassPropertyBlock.SetFloatArray(HBAOPlusRasterShaderParams.InstanceIdToRenderTargetIndexIdPropertyId, _CoarseAOInstanceIdToRenderTargetArrayIndices[DepthTextureIndex]);
        }
        
        /// <summary>
        /// Apply per instanced draw call material parameters for the coarse AO pass.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeCoarseAOPassData{TTextureHandle}(CoarseAORasterPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="DepthTextureIndex">Index of the current depth texture being processed. Valid values are [0, 4)</param>
        public static void ApplyInstancedCoarseAOPassParams(CoarseAORasterPassData<RTHandle> PassData, int DepthTextureIndex)
        {
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DeinterleavedDepthTextureParamId, _GetAODepthTextureFromTextureIndex(PassData, DepthTextureIndex));
            PassData.PassPropertyBlock.SetFloatArray(HBAOPlusRasterShaderParams.InstanceIdToRenderTargetIndexIdPropertyId, _CoarseAOInstanceIdToRenderTargetArrayIndices[DepthTextureIndex]);
        }
        
        /// <summary>
        /// Apply per instanced draw call material parameters for the coarse AO pass.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeCoarseAOPassData{TTextureHandle}(CoarseAORasterPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        public static void ApplyInstancedCoarseAOPassParams(CoarseAORasterPassData<TextureHandle> PassData)
        {
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DeinterleavedDepthTextureParamId, PassData.DeinterleavedDepthTexture);
        }
        
        /// <summary>
        /// Apply per instanced draw call material parameters for the coarse AO pass.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeCoarseAOPassData{TTextureHandle}(CoarseAORasterPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        public static void ApplyInstancedCoarseAOPassParams(CoarseAORasterPassData<RTHandle> PassData)
        {
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DeinterleavedDepthTextureParamId, PassData.DeinterleavedDepthTexture);
        }

        private static TTextureHandle _GetAODepthTextureFromSliceIndex<TTextureHandle>(CoarseAORasterPassData<TTextureHandle> PassData, int AOSliceIndex)
        {
            // Set the current texture array and the correct index from the array
            // See Resources struct for more info
            return AOSliceIndex switch
            {
                0 or 2 or 8 or 10  => PassData.DeinterleavedDepthTextureOne,
                1 or 3 or 9 or 11  => PassData.DeinterleavedDepthTextureTwo,
                4 or 6 or 12 or 14 => PassData.DeinterleavedDepthTextureThree,
                5 or 7 or 13 or 15 => PassData.DeinterleavedDepthTextureFour,
                _                  => throw new ArgumentOutOfRangeException(nameof(AOSliceIndex), AOSliceIndex, null)
            };
        }
        
        private static TTextureHandle _GetAODepthTextureFromTextureIndex<TTextureHandle>(CoarseAORasterPassData<TTextureHandle> PassData, int DepthTextureIndex)
        {
            // Set the current texture array and the correct index from the array
            // See Resources struct for more info
            return DepthTextureIndex switch
            {
                0 => PassData.DeinterleavedDepthTextureOne,
                1 => PassData.DeinterleavedDepthTextureTwo,
                2 => PassData.DeinterleavedDepthTextureThree,
                3 => PassData.DeinterleavedDepthTextureFour,
                _ => throw new ArgumentOutOfRangeException(nameof(DepthTextureIndex), DepthTextureIndex, null)
            };
        }


        /// <summary>
        /// Pre-allocated static array of jitter values for coarse AO pass.
        /// </summary>
        private static readonly Vector4[] _JitterValuesPreAlloc = new Vector4[16];
        
        /// <summary>
        /// Deinterleave depth texture slice index for deinterleave AO textures.
        /// Depth textures are divided into 4 texture arrays when using raster render path and are mapped according to the diagram below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c> <br />
        /// First texture array is used for slices 0, 2, 8, 10 <br />
        /// Second texture array is used for slices 1, 3, 9, 11 <br />
        /// Third texture array is used for slices 4, 6, 12, 14 <br />
        /// Fourth texture array is used for slices 5, 7, 13, 15 <br />
        /// </summary>
        /// <remarks>Dynamically indexed so we use a cbuffer instead of a hlsl static const array for reduced register pressure</remarks>
        private static readonly float[] _RasterDepthTextureArrayIndices = {
            0, // First texture slice 0
            0, // Second texture slice 0
            1, // First texture slice 1
            1, // Second texture slice 1
            0, // Third texture slice 0
            0, // Fourth texture slice 0
            1, // Third texture slice 1
            1, // Fourth texture slice 1
            2, // First texture slice 2
            2, // Second texture slice 2
            3, // First texture slice 3
            3, // Second texture slice 3
            2, // Third texture slice 2
            2, // Fourth texture slice 2
            3, // Third texture slice 3
            3, // Fourth texture slice 3
        };

        /// <summary>
        /// Coarse AO depth slices per Depth texture
        /// </summary>
        private static readonly float[][] _CoarseAOInstanceIdToRenderTargetArrayIndices = {
            new float[] { 0, 2, 8, 10,  }, // Coarse AO slices that use the first depth texture of the raster render path
            new float[] { 1, 3, 9, 11,  }, // Coarse AO slices that use the second depth texture of the raster render path
            new float[] { 4, 6, 12, 14, }, // Coarse AO slices that use the third depth texture of the raster render path
            new float[] { 5, 7, 13, 15, }, // // Coarse AO slices that use the fourth depth texture of the raster render path
        };

        #endregion

        #region AO Reinterleave Pass

        /// <summary>
        /// Initialize AO reinterleave pass data object based on the render resources and pass settings.
        /// </summary>
        /// <param name="PassData">Pass data object to initialize</param>
        /// <param name="Settings">Pass settings object</param>
        /// <param name="Resources">Resources for the pass</param>
        public static void InitializeAOReinterleavePassData<TTextureHandle>(AOReinterleavePassData<TTextureHandle> PassData, HBAOPlusSettings Settings, in HBAOPlusResources<TTextureHandle> Resources)
        {
            PassData.LinearizedDepthTexture  = Resources.LinearizedDepthTexture;
            PassData.DeinterleavedAOTexture  = Resources.DeinterleavedAOTexture;
            PassData.ViewSpaceNormalsTexture = Resources.SceneNormalsTexture;
            PassData.PassPropertyBlock       = Resources.PassPropertyBlock;
            PassData.Settings                = Settings;
        }

        /// <summary>
        /// Apply AO reinterleave pass material parameters.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeAOReinterleavePassData"/></param>
        /// <param name="bClearMatPropBlock">Whether if the material property block should be cleared first.</param>
        public static void ApplyAOReinterleavePassParams(AOReinterleavePassData<TextureHandle> PassData, bool bClearMatPropBlock = true)
        {
            if (bClearMatPropBlock)
            {
                PassData.PassPropertyBlock.Clear();
            }
            
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.LinearizedDepthTexturePropertyId, PassData.LinearizedDepthTexture);
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DeinterleavedAOTextureParamId, PassData.DeinterleavedAOTexture);
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.SceneNormalsTexturePropertyId, PassData.ViewSpaceNormalsTexture);
            PassData.PassPropertyBlock.SetVector(HBAOPlusRasterShaderParams.ScaleBiasPropertyId, Vector2.one);
        }
        
        /// <summary>
        /// Apply AO reinterleave pass material parameters.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeAOReinterleavePassData"/></param>
        /// <param name="bClearMatPropBlock">Whether if the material property block should be cleared first.</param>
        public static void ApplyAOReinterleavePassParams(AOReinterleavePassData<RTHandle> PassData, bool bClearMatPropBlock = true)
        {
            if (bClearMatPropBlock)
            {
                PassData.PassPropertyBlock.Clear();
            }
            
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.LinearizedDepthTexturePropertyId, PassData.LinearizedDepthTexture);
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.DeinterleavedAOTextureParamId, PassData.DeinterleavedAOTexture);
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.SceneNormalsTexturePropertyId, PassData.ViewSpaceNormalsTexture);
            PassData.PassPropertyBlock.SetVector(HBAOPlusRasterShaderParams.ScaleBiasPropertyId, Vector2.one);
        }

        #endregion

        #region Blur Pass

        /// <summary>
        /// Initialize blur pass data object based on the render resources and pass settings.
        /// </summary>
        /// <param name="PassData">Pass data object to initialize</param>
        /// <param name="Settings">Pass settings object</param>
        /// <param name="Resources">Resources for the pass</param>
        public static void InitializeBlurPassData<TTextureHandle>(RasterBlurPassData<TTextureHandle> PassData, HBAOPlusSettings Settings, in HBAOPlusResources<TTextureHandle> Resources)
        {
            PassData.IntermediateBlurTextureOne = Resources.BlurTextureOne;
            PassData.IntermediateBlurTextureTwo = Resources.BlurTextureTwo;
            PassData.SceneNormalsTexture        = Resources.SceneNormalsTexture;
            PassData.PassPropertyBlock          = Resources.PassPropertyBlock;
            PassData.Settings                   = Settings;
        }
        
        /// <summary>
        /// Initialize blur pass data object based on the render resources and pass settings for the compressed compute kernel.
        /// </summary>
        /// <param name="PassData">Pass data object to initialize</param>
        /// <param name="Settings">Pass settings object</param>
        /// <param name="Resources">Resources for the pass</param>
        /// <param name="KernelInnerDimensions">Dimensions of the kernel based on the blur type not excluding halo pixels not written to</param>
        public static void InitializeCompressedBlurPassData<TTextureHandle>(ComputeBlurPassData<TTextureHandle> PassData, HBAOPlusSettings Settings, in HBAOPlusResources<TTextureHandle> Resources, uint3 KernelInnerDimensions)
        {
            PassData.IntermediateBlurTexture = Resources.BlurTextureOne;
            PassData.BlurRenderTargetTexture = Resources.FinalAOTexture;
            PassData.SceneNormalsTexture     = Resources.SceneNormalsTexture;
            PassData.Settings                = Settings;
            PassData.KernelIndex             = Settings.ComputeShaderParams.CompressedTwoPassBlurKernelId;
            PassData.ThreadGroupsCount       = new int3(
                Mathf.CeilToInt(Resources.FullResDimensions.x / KernelInnerDimensions.x),
                Mathf.CeilToInt(Resources.FullResDimensions.y / KernelInnerDimensions.y),
                1 // Full screen 1-layer
            );
        }

        /// <summary>
        /// Initialize blur pass data object based on the render resources and pass settings.
        /// </summary>
        /// <param name="PassData">Pass data object to initialize</param>
        /// <param name="Settings">Pass settings object</param>
        /// <param name="Resources">Resources for the pass</param>
        /// <param name="KernelInnerDimensions">Dimensions of the kernel based on the blur type not excluding halo pixels not written to</param>
        /// <param name="bIsIntermediatePass">Whether if this is an intermediate blur pass data</param>
        public static void InitializeBlurPassData<TTextureHandle>(ComputeBlurPassData<TTextureHandle> PassData, HBAOPlusSettings Settings, in HBAOPlusResources<TTextureHandle> Resources, uint3 KernelInnerDimensions, bool bIsIntermediatePass)
        {
            PassData.IntermediateBlurTexture = bIsIntermediatePass ? Resources.BlurTextureOne : Resources.BlurTextureTwo;
            PassData.BlurRenderTargetTexture = bIsIntermediatePass ? Resources.BlurTextureTwo : Resources.FinalAOTexture;
            PassData.SceneNormalsTexture     = Resources.SceneNormalsTexture;
            PassData.Settings                = Settings;
            PassData.KernelIndex             = bIsIntermediatePass ? Settings.ComputeShaderParams.IntermediateBlurKernelId : Settings.ComputeShaderParams.FinalBlurKernelId;
            PassData.ThreadGroupsCount       = new int3(
                Mathf.CeilToInt(Resources.FullResDimensions.x / KernelInnerDimensions.x),
                Mathf.CeilToInt(Resources.FullResDimensions.y / KernelInnerDimensions.y),
                1 // Full screen 1-layer
            );
        }

        /// <summary>
        /// Apply blur pass material parameters.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeBlurPassData{TTextureHandle}(RasterBlurPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="bSourceIsFirstTempTexture">Whether if the source AO texture is <see cref="RasterBlurPassData{TextureHandle}.IntermediateBlurTextureOne"/> (true) or <see cref="RasterBlurPassData{TextureHandle}.IntermediateBlurTextureTwo"/> (false)</param>
        /// <param name="bClearMatPropBlock">Whether if the material property block should be cleared first.</param>
        public static void ApplyBlurPassParams(RasterBlurPassData<TextureHandle> PassData, bool bSourceIsFirstTempTexture, bool bClearMatPropBlock = true)
        {
            if (bClearMatPropBlock)
            {
                PassData.PassPropertyBlock.Clear();
            }
            
            PassData.PassPropertyBlock.SetVector(HBAOPlusRasterShaderParams.ScaleBiasPropertyId, Vector2.one);
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.SceneNormalsTexturePropertyId, PassData.SceneNormalsTexture);
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.IntermediateBlurTextureParamId, bSourceIsFirstTempTexture ? PassData.IntermediateBlurTextureOne : PassData.IntermediateBlurTextureTwo);
        }
        
        /// <summary>
        /// Apply blur pass material parameters.
        /// </summary>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeBlurPassData{TTextureHandle}(RasterBlurPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle})"/></param>
        /// <param name="bSourceIsFirstTempTexture">Whether if the source AO texture is <see cref="RasterBlurPassData{TextureHandle}.IntermediateBlurTextureOne"/> (true) or <see cref="RasterBlurPassData{TextureHandle}.IntermediateBlurTextureTwo"/> (false)</param>
        /// <param name="bClearMatPropBlock">Whether if the material property block should be cleared first.</param>
        public static void ApplyBlurPassParams(RasterBlurPassData<RTHandle> PassData, bool bSourceIsFirstTempTexture, bool bClearMatPropBlock = true)
        {
            if (bClearMatPropBlock)
            {
                PassData.PassPropertyBlock.Clear();
            }
            
            PassData.PassPropertyBlock.SetVector(HBAOPlusRasterShaderParams.ScaleBiasPropertyId, Vector2.one);
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.SceneNormalsTexturePropertyId, PassData.SceneNormalsTexture);
            PassData.PassPropertyBlock.SetTexture(HBAOPlusShaderParams.IntermediateBlurTextureParamId, bSourceIsFirstTempTexture ? PassData.IntermediateBlurTextureOne : PassData.IntermediateBlurTextureTwo);
        }


        /// <summary>
        /// Apply blur pass material parameters.
        /// </summary>
        /// <param name="Cmd"></param>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeBlurPassData{TTextureHandle}(ComputeBlurPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle}, uint3, bool)"/></param>
        public static void ApplyBlurPassParams(IComputeCommandBuffer Cmd, ComputeBlurPassData<TextureHandle> PassData)
        {
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader,
                PassData.KernelIndex, 
                HBAOPlusShaderParams.SceneNormalsTexturePropertyId, 
                PassData.SceneNormalsTexture
            );

            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader,
                PassData.KernelIndex, 
                HBAOPlusShaderParams.IntermediateBlurTextureParamId, 
                PassData.IntermediateBlurTexture
            );
            
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader,
                PassData.KernelIndex, 
                HBAOPlusComputeShaderParams.BlurRenderTargetTextureRWPropertyId, 
                PassData.BlurRenderTargetTexture
            );
        }

        /// <summary>
        /// Apply blur pass material parameters.
        /// </summary>
        /// <param name="Cmd"></param>
        /// <param name="PassData">Pass data initialized using <see cref="InitializeBlurPassData{TTextureHandle}(ComputeBlurPassData{TTextureHandle}, HBAOPlusSettings, in HBAOPlusResources{TTextureHandle}, uint3, bool)"/></param>
        public static void ApplyBlurPassParams(CommandBuffer Cmd, ComputeBlurPassData<RTHandle> PassData)
        {
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader,
                PassData.KernelIndex, 
                HBAOPlusShaderParams.SceneNormalsTexturePropertyId, 
                PassData.SceneNormalsTexture
            );

            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader,
                PassData.KernelIndex, 
                HBAOPlusShaderParams.IntermediateBlurTextureParamId, 
                PassData.IntermediateBlurTexture
            );
            
            Cmd.SetComputeTextureParam(
                PassData.Settings.ComputePassShader,
                PassData.KernelIndex, 
                HBAOPlusComputeShaderParams.BlurRenderTargetTextureRWPropertyId, 
                PassData.BlurRenderTargetTexture
            );
        }

        /// <summary>
        /// Checks whether if the specified blur render path is a compute path.
        /// </summary>
        /// <param name="RenderPath">Render path to check</param>
        /// <returns>True if either multi-pass or single-pass compute, false otherwise.</returns>
        public static bool IsBlurRenderPathCompute(EBlurRenderPath RenderPath)
        {
            return RenderPath is EBlurRenderPath.ComputeSinglePass or EBlurRenderPath.ComputeMultiPass;
        }

        #endregion

    }
}