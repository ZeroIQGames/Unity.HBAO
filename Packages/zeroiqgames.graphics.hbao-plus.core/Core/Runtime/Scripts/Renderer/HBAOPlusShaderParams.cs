using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

// ReSharper disable MemberCanBePrivate.Global
namespace ZeroIQGames.Graphics.HBAOPlus.Core.Renderer
{
    /// <summary>
    /// Class that holds keyword names and parameter indices shader between compute and raster shaders
    /// </summary>
    internal static class HBAOPlusShaderParams
    {
        #region Keywords

        /// <summary>
        /// Global keyword signaling shaders that screen space AO is present and should be sampled.
        /// </summary>
        public static GlobalKeyword ScreenSpaceAmbientOcclusionKeyword = new("_SCREEN_SPACE_OCCLUSION");
        
        /// <summary>
        /// Keyword that enables double depth texture mode.
        /// </summary>
        public const string DOUBLE_DEPTH_TEXTURE_KEYWORD_NAME = "_HBAO_PLUS_DOUBLE_DEPTH_TEXTURES";
        
        /// <summary>
        /// Keyword that sets coarse AO to use 8 rays per pixel.
        /// </summary>
        /// <remarks>Active state mutually exclusive with <see cref="FOUR_DIRECTION_COARSE_AO_KEYWORD_NAME"/></remarks>
        public const string EIGHT_DIRECTION_COARSE_AO_KEYWORD_NAME = "_HBAO_PLUS_EIGHT_DIRECTIONS";
        
        /// <summary>
        /// Keyword that sets coarse AO to use 4 rays per pixel.
        /// </summary>
        /// <remarks>Active state mutually exclusive with <see cref="EIGHT_DIRECTION_COARSE_AO_KEYWORD_NAME"/></remarks>
        public const string FOUR_DIRECTION_COARSE_AO_KEYWORD_NAME = "_HBAO_PLUS_FOUR_DIRECTIONS";
        
        /// <summary>
        /// Keyword that sets coarse AO to sample 8 points along each ray.
        /// </summary>
        /// <remarks>Active state mutually exclusive with <see cref="FOUR_STEP_COARSE_AO_KEYWORD_NAME"/></remarks>
        public const string EIGHT_STEP_COARSE_AO_KEYWORD_NAME = "_HBAO_PLUS_EIGHT_STEPS";
        
        /// <summary>
        /// Keyword that sets coarse AO to sample 8 points along each ray.
        /// </summary>
        /// <remarks>Active state mutually exclusive with <see cref="EIGHT_STEP_COARSE_AO_KEYWORD_NAME"/></remarks>
        public const string FOUR_STEP_COARSE_AO_KEYWORD_NAME = "_HBAO_PLUS_FOUR_STEPS";
        
        /// <summary>
        /// Keyword that signals to AO reinterleave pass that a blur pass will be performed. This prevents the pass from applying AO power which should instead be applied at the end of blur pass.
        /// </summary>
        public const string BLUR_POST_PROCESS_KEYWORD_NAME = "_HBAO_PLUS_BLUR_ENABLED";
        
        /// <summary>
        /// Keyword that enables depth-dependent blur sharpness calculation pass.
        /// </summary>
        public const string DEPTH_DEPENDENT_BLUR_SHARPNESS_KEYWORD_NAME = "_HBAO_PLUS_DEPTH_DEPENDENT_BLUR_SHARPNESS";
        
        /// <summary>
        /// Keyword that shows whether if normals are in world space (enabled) or view space (disabled).
        /// </summary>
        public const string NORMALS_IN_WORLD_SPACE_KEYWORD_NAME = "_HBAO_PLUS_NORMALS_IN_WORLD_SPACE";
        
        /// <summary>
        /// Keyword that shows whether if R16_SFloat texture format is used for deinterleaved depth (enabled) or R32_SFloat (disabled).
        /// </summary>
        public const string DEINTERLEAVED_DEPTH_TEX_FORMAT_R16_KEYWORD_NAME = "_HBAO_PLUS_NORMALS_IN_WORLD_SPACE";
        
        /// <summary>
        /// Keyword that shows whether if R8_UNorm texture format is used for deinterleaved AO (enabled) or R16_UNorm (disabled).
        /// </summary>
        public const string DEINTERLEAVED_AO_TEX_FORMAT_R8_KEYWORD_NAME = "_HBAO_PLUS_NORMALS_IN_WORLD_SPACE";
        
        /// <summary>
        /// Keyword setting the blur kernel radius to 1 pixel.
        /// </summary>
        /// <remarks>Active state mutually exclusive with other blur kernel radius keywords</remarks>
        public const string BLUR_KERNEL_RADIUS_ONE_KEYWORD_NAME = "_BLUR_KERNEL_RADIUS_1";
        
        /// <summary>
        /// Keyword setting the blur kernel radius to 2 pixels.
        /// </summary>
        /// <remarks>Active state mutually exclusive with other blur kernel radius keywords</remarks>
        public const string BLUR_KERNEL_RADIUS_TWO_KEYWORD_NAME = "_BLUR_KERNEL_RADIUS_2";
        
        /// <summary>
        /// Keyword setting the blur kernel radius to 3 pixels.
        /// </summary>
        /// <remarks>Active state mutually exclusive with other blur kernel radius keywords</remarks>
        public const string BLUR_KERNEL_RADIUS_THREE_KEYWORD_NAME = "_BLUR_KERNEL_RADIUS_3";
        
        /// <summary>
        /// Keyword setting the blur kernel radius to 4 pixels.
        /// </summary>
        /// <remarks>Active state mutually exclusive with other blur kernel radius keywords</remarks>
        public const string BLUR_KERNEL_RADIUS_FOUR_KEYWORD_NAME = "_BLUR_KERNEL_RADIUS_4";
        
        /// <summary>
        /// Keyword that enables the optimized path for shader target 45 and above. Should only be activated if the device supports shader target 45.
        /// </summary>
        /// <seealso cref="SystemInfo.graphicsShaderLevel"/>
        public const string SUPPORTS_SHADER_TARGET_45_KEYWORD_NAME = "_HBAO_PLUS_SUPPORTS_SHADER_TARGET_45";
        
        /// <summary>
        /// Keyword that signals the shader that setting render layer from any stage (vertex/geometry) is supported by the graphics API of the device.
        /// </summary>
        /// <seealso cref="SystemInfo.supportsRenderTargetArrayIndexFromVertexShader"/>
        public const string SUPPORTS_SET_ARRAY_INDEX_FROM_ANY_STAGE_KEYWORD_NAME = "_HBAO_PLUS_SUPPORTS_SET_ARRAY_INDEX_FROM_ANY_STAGE";
        
        /// <summary>
        /// Keyword that signals the shader that geometry stage is supported by the graphics API of the device.
        /// </summary>
        public const string SUPPORTS_GEOMETRY_STAGE_KEYWORD_NAME = "_HBAO_PLUS_SUPPORTS_GEOMETRY_STAGE";
        
        /// <summary>
        /// Keyword that signals the shader that instancing is supported by the graphics API of the device.
        /// </summary>
        public const string SUPPORTS_INSTANCING_KEYWORD_NAME = "_HBAO_PLUS_SUPPORTS_INSTANCING";
        
        /// <summary>
        /// Keyword that signals the shader that integer operations are supported by the graphics API of the device.
        /// </summary>
        public const string SUPPORTS_INTEGERS_KEYWORD_NAME = "_HBAO_PLUS_SUPPORTS_INTEGERS";
        
        /// <summary>
        /// Keyword that signals the shader that the render path is set to raster only and compute shaders are not used.
        /// </summary>
        public const string RASTER_ONLY_RENDER_PATH_KEYWORD_NAME = "_HBAO_PLUS_RASTER_ONLY_RENDER_PATH";
        
        /// <summary>
        /// Keyword that sets the blur algorithm used to bilateral filter.
        /// </summary>
        /// <remarks>Active state mutually exclusive with other blur algorithm keywords</remarks>
        public const string BLUR_ALGORITHM_BILATERAL_KEYWORD_NAME = "_HBAO_PLUS_BLUR_FILTER_BILATERAL";
        
        /// <summary>
        /// Keyword that sets the blur algorithm used to gaussian filter.
        /// </summary>
        /// <remarks>Active state mutually exclusive with other blur algorithm keywords</remarks>
        public const string BLUR_ALGORITHM_GAUSSIAN_KEYWORD_NAME = "_HBAO_PLUS_BLUR_FILTER_GAUSSIAN";
        
        /// <summary>
        /// Keyword that sets the blur algorithm used to kawase filter.
        /// </summary>
        /// <remarks>Active state mutually exclusive with other blur algorithm keywords</remarks>
        public const string BLUR_ALGORITHM_KAWASE_KEYWORD_NAME = "_HBAO_PLUS_BLUR_FILTER_KAWASE";
        
        /// <summary>
        /// Keyword that sets the blur algorithm used to kawase filter.
        /// </summary>
        /// <remarks>Active state mutually exclusive with other blur algorithm keywords</remarks>
        public const string BLUR_SHARPNESS_FROM_DEPTH_KEYWORD_NAME = "_HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH";
        
        /// <summary>
        /// Keyword that sets the blur algorithm used to kawase filter.
        /// </summary>
        /// <remarks>Active state mutually exclusive with other blur algorithm keywords</remarks>
        public const string BLUR_SHARPNESS_FROM_NORMAL_KEYWORD_NAME = "_HBAO_PLUS_BLUR_SHARPNESS_FROM_NORMAL";
        
        /// <summary>
        /// Keyword that sets the blur algorithm used to kawase filter.
        /// </summary>
        /// <remarks>Active state mutually exclusive with other blur algorithm keywords</remarks>
        public const string BLUR_SHARPNESS_FROM_DEPTH_NORMAL_KEYWORD_NAME = "_HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH_NORMAL";

        #endregion

        #region Textures

        /// <summary>
        /// Scene depth texture one and the only depth texture used if not using double depth texture mode
        /// </summary>
        public static readonly int DepthTextureOnePropertyId = Shader.PropertyToID("_DepthTextureOne");
                    
        /// <summary>
        /// Scene depth texture two only used if double depth texture is enabled
        /// </summary>
        public static readonly int DepthTextureTwoPropertyId = Shader.PropertyToID("_DepthTextureTwo");
            
        /// <summary>
        /// Scene normals texture parameter used for AO calculation.
        /// </summary>
        public static readonly int SceneNormalsTexturePropertyId = Shader.PropertyToID("_ViewSpaceNormalsTexture");
        
        /// <summary>
        /// Parameter that holds the deinterleaved depth texture array
        /// </summary>
        public static readonly int DeinterleavedDepthTextureParamId = Shader.PropertyToID("_DeinterleavedDepthTexture");
            
        /// <summary>
        /// Parameter that holds the deinterleaved depth texture array
        /// </summary>
        public static readonly int DeinterleavedAOTextureParamId = Shader.PropertyToID("_DeinterleavedAOTexture");
        
        /// <summary>
        /// Parameter that holds the intermediate blur texture
        /// </summary>
        public static readonly int IntermediateBlurTextureParamId = Shader.PropertyToID("_IntermediateBlurTexture");
        
        /// <summary>
        ///Parameter holding view space linearized depth texture
        /// </summary>
        public static readonly int LinearizedDepthTexturePropertyId = Shader.PropertyToID("_LinearizedDepth");
        
        /// <summary>
        /// Global SSAO texture parameter used by lit shaders.
        /// </summary>
        public static readonly int SSAOTexturePropertyId = Shader.PropertyToID("_ScreenSpaceOcclusionTexture");

        #endregion

        #region Cbuffer parameters

        /// <summary>
        /// Parameter that holds the deinterleaved depth texture array
        /// </summary>
        public static readonly int SliceAOJitterArrayParamId = Shader.PropertyToID("_AOJitterArray");
        
        /// <summary>
        /// Parameter that holds the deinterleaved depth texture array
        /// </summary>
        public static readonly int SliceUVOffsetPropertyId = Shader.PropertyToID("_SliceUVOffset");
        
        /// <summary>
        /// Parameters used for converting from screen space UV to view space position
        /// </summary>
        public static readonly int UVToViewParamsPropertyId = Shader.PropertyToID("_UVToViewParams");
        
        public static readonly int FullResDimensionsPropertyId = Shader.PropertyToID("_FullResDimensions");
        
        public static readonly int QuarterResDimensionsPropertyId = Shader.PropertyToID("_QuarterResDimensions");
        
        public static readonly int AORadius2PropertyId = Shader.PropertyToID("_AORadius2");
        
        public static readonly int AONegativeInverseRadius2PropertyId = Shader.PropertyToID("_AONegativeInvR2");
        
        public static readonly int AOVerticalRadiusToScreenPixelsPropertyId = Shader.PropertyToID("_AOVerticalRadiusToScreenPixels");
        
        public static readonly int ForegroundAORadiusPixelsPropertyId = Shader.PropertyToID("_ForegroundAORadiusPixels");
        
        public static readonly int BackgroundAORadiusPixelsPropertyId = Shader.PropertyToID("_BackgroundAORadiusPixels");
        
        public static readonly int LargeScaleAOAmountPropertyId = Shader.PropertyToID("_LargeScaleAOAmount");
        
        public static readonly int SmallScaleAOAmountPropertyId = Shader.PropertyToID("_SmallScaleAOAmount");
        
        public static readonly int IntensityExponentPropertyId = Shader.PropertyToID("_IntensityExponent");
        
        public static readonly int TessellationBiasPropertyId = Shader.PropertyToID("_TessellationBias");
        
        public static readonly int BlurUniformSharpnessPropertyId = Shader.PropertyToID("_ForegroundBlurSharpness"); // Uniform blur uses foreground sharpness variable
        
        public static readonly int BlurForegroundSharpnessPropertyId = Shader.PropertyToID("_ForegroundBlurSharpness");
        
        public static readonly int BlurBackgroundSharpnessPropertyId = Shader.PropertyToID("_BackgroundBlurSharpness");
        
        public static readonly int BlurForegroundViewDepthPropertyId = Shader.PropertyToID("_ForegroundBlurViewDepth");
        
        public static readonly int BlurBackgroundViewDepthPropertyId = Shader.PropertyToID("_BackgroundBlurViewDepth");
        
        public static readonly int AmbientOcclusionParamsPropertyId = Shader.PropertyToID("_AmbientOcclusionParam");

        #endregion
    }
    
    internal struct HBAOPlusRasterShaderParams
    {
        #region Cbuffer params and textures

        /// <summary>
        /// AO slice that will be sampled during coarse AO pass.
        /// </summary>
        public static readonly int InstanceIdToRenderTargetIndexIdPropertyId = Shader.PropertyToID("_InstanceIdToRenderTargetIndexId");
        
        /// <summary>
        /// Scale parameter data used for fullscreen blit
        /// </summary>
        public static readonly int ScaleBiasPropertyId = Shader.PropertyToID("_BlitScaleBias");
            
        /// <summary>
        /// Slice index that the pass renders to
        /// </summary>
        public static readonly int RenderSliceIndexPropertyId = Shader.PropertyToID("_SliceIndex");
            
        /// <summary>
        /// Depth texture slice that will be sampled during coarse AO pass.
        /// </summary>
        public static readonly int DepthTextureSliceIndicesPropertyId = Shader.PropertyToID("_DeinterleavedDepthTextureIndices");

        #endregion
        
        #region Keywords

        public LocalKeyword DoubleDepthTextureKeyword { get; private set; }
        
        public LocalKeyword EightStepCoarseAOKeyword { get; private set; }
        
        public LocalKeyword FourStepCoarseAOKeyword { get; private set; }
        
        public LocalKeyword EightDirectionCoarseAOKeyword { get; private set; }
        
        public LocalKeyword FourDirectionCoarseAOKeyword { get; private set; }
        
        public LocalKeyword BlurPostProcessKeyword { get; private set; }
        
        public LocalKeyword RasterDepthDependentBlurSharpnessKeyword { get; private set; }
        
        public LocalKeyword SupportsShaderTarget45Keyword { get; private set; }
        
        public LocalKeyword SupportsSetArrayIndexFromAnyStageKeyword { get; private set; }
        
        public LocalKeyword SupportsInstancingKeyword { get; private set; }
        
        public LocalKeyword SupportsGeometryShaderKeyword { get; private set; }
        
        public LocalKeyword SupportsIntegerOperationsKeyword { get; private set; }
        
        public LocalKeyword RasterOnlyRenderPathKeyword { get; private set; }
        
        public LocalKeyword NormalsInWorldSpaceKeyword { get; private set; }
        
        public LocalKeyword BlurKernelRadiusOneKeyword { get; private set; }
        
        public LocalKeyword BlurKernelRadiusTwoKeyword { get; private set; }
        
        public LocalKeyword BlurKernelRadiusThreeKeyword { get; private set; }
        
        public LocalKeyword BlurKernelRadiusFourKeyword { get; private set; }
        
        public LocalKeyword BlurAlgorithmBilateralKeyword { get; private set; }
        
        public LocalKeyword BlurAlgorithmGaussianKeyword { get; private set; }
        
        public LocalKeyword BlurAlgorithmKawaseKeyword { get; private set; }
        
        public LocalKeyword BlurSharpnessFromDepthKeyword { get; private set; }
        
        public LocalKeyword BlurSharpnessFromNormalKeyword { get; private set; }

        public LocalKeyword BlurSharpnessFromDepthNormalKeyword { get; private set; }
        
        #endregion
        


        public HBAOPlusRasterShaderParams(Shader RasterShader)
        {
            if (RasterShader != null)
            {
                DoubleDepthTextureKeyword                = new LocalKeyword(RasterShader, HBAOPlusShaderParams.DOUBLE_DEPTH_TEXTURE_KEYWORD_NAME);
                EightStepCoarseAOKeyword                 = new LocalKeyword(RasterShader, HBAOPlusShaderParams.EIGHT_STEP_COARSE_AO_KEYWORD_NAME);
                FourStepCoarseAOKeyword                  = new LocalKeyword(RasterShader, HBAOPlusShaderParams.FOUR_STEP_COARSE_AO_KEYWORD_NAME);
                EightDirectionCoarseAOKeyword            = new LocalKeyword(RasterShader, HBAOPlusShaderParams.EIGHT_DIRECTION_COARSE_AO_KEYWORD_NAME);
                FourDirectionCoarseAOKeyword             = new LocalKeyword(RasterShader, HBAOPlusShaderParams.FOUR_DIRECTION_COARSE_AO_KEYWORD_NAME);
                BlurPostProcessKeyword                   = new LocalKeyword(RasterShader, HBAOPlusShaderParams.BLUR_POST_PROCESS_KEYWORD_NAME);
                NormalsInWorldSpaceKeyword               = new LocalKeyword(RasterShader, HBAOPlusShaderParams.NORMALS_IN_WORLD_SPACE_KEYWORD_NAME);
                BlurKernelRadiusOneKeyword               = new LocalKeyword(RasterShader, HBAOPlusShaderParams.BLUR_KERNEL_RADIUS_ONE_KEYWORD_NAME);
                BlurKernelRadiusTwoKeyword               = new LocalKeyword(RasterShader, HBAOPlusShaderParams.BLUR_KERNEL_RADIUS_TWO_KEYWORD_NAME);
                BlurKernelRadiusThreeKeyword             = new LocalKeyword(RasterShader, HBAOPlusShaderParams.BLUR_KERNEL_RADIUS_THREE_KEYWORD_NAME);
                BlurKernelRadiusFourKeyword              = new LocalKeyword(RasterShader, HBAOPlusShaderParams.BLUR_KERNEL_RADIUS_FOUR_KEYWORD_NAME);
                RasterDepthDependentBlurSharpnessKeyword = new LocalKeyword(RasterShader, HBAOPlusShaderParams.DEPTH_DEPENDENT_BLUR_SHARPNESS_KEYWORD_NAME);
                SupportsShaderTarget45Keyword            = new LocalKeyword(RasterShader, HBAOPlusShaderParams.SUPPORTS_SHADER_TARGET_45_KEYWORD_NAME);
                SupportsSetArrayIndexFromAnyStageKeyword = default; //new LocalKeyword(RasterShader, HBAOPlusUtility.SUPPORTS_SET_ARRAY_INDEX_FROM_ANY_STAGE_KEYWORD);
                SupportsGeometryShaderKeyword            = default; //new LocalKeyword(RasterShader, HBAOPlusUtility.SUPPORTS_GEOMETRY_STAGE_KEYWORD);
                SupportsInstancingKeyword                = new LocalKeyword(RasterShader, HBAOPlusShaderParams.SUPPORTS_INSTANCING_KEYWORD_NAME);
                SupportsIntegerOperationsKeyword         = new LocalKeyword(RasterShader, HBAOPlusShaderParams.SUPPORTS_INTEGERS_KEYWORD_NAME);
                RasterOnlyRenderPathKeyword              = new LocalKeyword(RasterShader, HBAOPlusShaderParams.RASTER_ONLY_RENDER_PATH_KEYWORD_NAME);
                BlurAlgorithmBilateralKeyword            = new LocalKeyword(RasterShader, HBAOPlusShaderParams.BLUR_ALGORITHM_BILATERAL_KEYWORD_NAME);
                BlurAlgorithmGaussianKeyword             = new LocalKeyword(RasterShader, HBAOPlusShaderParams.BLUR_ALGORITHM_GAUSSIAN_KEYWORD_NAME);
                BlurAlgorithmKawaseKeyword               = new LocalKeyword(RasterShader, HBAOPlusShaderParams.BLUR_ALGORITHM_KAWASE_KEYWORD_NAME);
                BlurSharpnessFromDepthKeyword            = new LocalKeyword(RasterShader, HBAOPlusShaderParams.BLUR_SHARPNESS_FROM_DEPTH_KEYWORD_NAME);
                BlurSharpnessFromNormalKeyword           = new LocalKeyword(RasterShader, HBAOPlusShaderParams.BLUR_SHARPNESS_FROM_NORMAL_KEYWORD_NAME);
                BlurSharpnessFromDepthNormalKeyword      = new LocalKeyword(RasterShader, HBAOPlusShaderParams.BLUR_SHARPNESS_FROM_DEPTH_NORMAL_KEYWORD_NAME);
            }
            else
            {
                DoubleDepthTextureKeyword                 = default;
                EightStepCoarseAOKeyword                  = default;
                FourStepCoarseAOKeyword                   = default;
                EightDirectionCoarseAOKeyword             = default;
                FourDirectionCoarseAOKeyword              = default;
                BlurPostProcessKeyword                    = default;
                NormalsInWorldSpaceKeyword                = default;
                BlurKernelRadiusOneKeyword                = default;
                BlurKernelRadiusTwoKeyword                = default;
                BlurKernelRadiusThreeKeyword              = default;
                BlurKernelRadiusFourKeyword               = default;
                RasterDepthDependentBlurSharpnessKeyword  = default;
                SupportsShaderTarget45Keyword             = default;
                SupportsSetArrayIndexFromAnyStageKeyword  = default;
                SupportsGeometryShaderKeyword             = default;
                SupportsInstancingKeyword                 = default;
                SupportsIntegerOperationsKeyword          = default;
                RasterOnlyRenderPathKeyword               = default;
                BlurAlgorithmBilateralKeyword             = default;
                BlurAlgorithmGaussianKeyword              = default;
                BlurAlgorithmKawaseKeyword                = default;
                BlurSharpnessFromDepthKeyword             = default;
                BlurSharpnessFromNormalKeyword            = default;
                BlurSharpnessFromDepthNormalKeyword       = default;
            }
        }
    }
    
    internal struct HBAOPlusComputeShaderParams
    {
        #region Cbuffer params and textures

        /// <summary>
        /// Parameter that holds the deinterleaved depth texture array UAV
        /// </summary>
        public static readonly int DeinterleavedDepthTextureRWPropertyId = Shader.PropertyToID("_DeinterleavedDepthTextureRW");
            
        /// <summary>
        /// Parameter that holds the deinterleaved AO texture array UAV
        /// </summary>
        public static readonly int DeinterleavedAOTextureRWPropertyId = Shader.PropertyToID("_DeinterleavedAOTextureRW");
        
        /// <summary>
        /// Parameter that holds the intermediate blur texture
        /// </summary>
        public static readonly int BlurRenderTargetTextureRWPropertyId = Shader.PropertyToID("_BlurRenderTargetTextureRW");

        #endregion

        #region Kernels
        
        /// <summary>
        /// Thread group dimensions for the depth deinterleave kernel.
        /// </summary>
        public static readonly uint3 DeinterleaveDepthKernalThreadGroupDimensions = new(8, 8, 1);
        
        /// <summary>
        /// Thread group dimensions for the coarse AO kernel.
        /// </summary>
        public static readonly uint3 CoarseAOKernalThreadGroupDimensions = new(8, 8, 1);

        /// <summary>
        /// Kernel name for the kernel that deinterleaves the depth texture array
        /// </summary>
        public const string DEINTERLEAVE_DEPTH_KERNEL_NAME = "DeinterleaveDepth";
        
        /// <summary>
        /// Kernel name for the kernel that computes the coarse AO and writes it to deinterleaved AO texture array
        /// </summary>
        public const string COARSE_AO_KERNEL_NAME = "DrawCoarseAO" ;
        
        /// <summary>
        /// Kernel name for the initial blur kernel.
        /// </summary>
        public const string INTERMEDIATE_BLUR_KERNEL_NAME = "IntermediateBlur" ;
        
        /// <summary>
        /// Kernel name for the final blur kernel.
        /// </summary>
        public const string FINAL_BLUR_KERNEL_NAME = "FinalBlur" ;
        
        /// <summary>
        /// Kernel name for the compressed two-pass blur kernel.
        /// </summary>
        public const string COMPRESSED_TWO_PASS_BLUR_KERNEL_NAME = "CompressedDoublePassBlur" ;
        
        /// <summary>
        /// Kernel int ID for the depth deinterleave kernel.
        /// </summary>
        public int DeinterleaveDepthKernelId { get; private set; }
        
        /// <summary>
        /// Kernel int ID for the coarse AO kernel.
        /// </summary>
        public int CoarseAOKernelId { get; private set; }
        
        /// <summary>
        /// Kernel int ID for the intermediate blur kernel.
        /// </summary>
        public int IntermediateBlurKernelId { get; private set; }
        
        /// <summary>
        /// Kernel int ID for the final blur kernel.
        /// </summary>
        public int FinalBlurKernelId { get; private set; }
        
        /// <summary>
        /// Single pass blur kernel that runs both vertical and horizontal blur passes in a single kernel dispatch.
        /// </summary>
        public int CompressedTwoPassBlurKernelId { get; private set; }

        #endregion
        
        #region Keywords

        public LocalKeyword DoubleDepthTextureKeyword { get; private set; }
        
        public LocalKeyword EightStepCoarseAOKeyword { get; private set; }
        
        public LocalKeyword FourStepCoarseAOKeyword { get; private set; }
        
        public LocalKeyword EightDirectionCoarseAOKeyword { get; private set; }
        
        public LocalKeyword FourDirectionCoarseAOKeyword { get; private set; }
        
        public LocalKeyword NormalsInWorldSpaceKeyword { get; private set; }
        
        public LocalKeyword DeinterleavedDepthTexFormatR16Keyword { get; private set; }
        
        public LocalKeyword DeinterleavedAOTexFormatR8Keyword { get; private set; }
        
        public LocalKeyword BlurAlgorithmBilateralKeyword { get; private set; }
        
        public LocalKeyword BlurAlgorithmGaussianKeyword { get; private set; }
        
        public LocalKeyword BlurAlgorithmKawaseKeyword { get; private set; }
        
        public LocalKeyword BlurSharpnessFromDepthKeyword { get; private set; }
        
        public LocalKeyword BlurSharpnessFromNormalKeyword { get; private set; }

        public LocalKeyword BlurSharpnessFromDepthNormalKeyword { get; private set; }
        
        public LocalKeyword BlurKernelRadiusOneKeyword { get; private set; }
        
        public LocalKeyword BlurKernelRadiusTwoKeyword { get; private set; }
        
        public LocalKeyword BlurKernelRadiusThreeKeyword { get; private set; }
        
        public LocalKeyword BlurKernelRadiusFourKeyword { get; private set; }

        #endregion

        /// <summary>
        /// Get the blur kernel group thread dimensions for multi-pass compressed kernel and the specified settings.
        /// </summary>
        /// <param name="BlurRadius">Radius of the blur in pixels</param>
        /// <param name="bActiveThreadsOnly">Whether if </param>
        /// <returns></returns>
        public static uint3 GetCompressedBlurKernelThreadNum(EBlurRadius BlurRadius, bool bActiveThreadsOnly)
        {
            switch (BlurRadius)
            {
                case EBlurRadius.One:
                case EBlurRadius.Two:
                case EBlurRadius.Three:
                case EBlurRadius.Four:
                {
                    uint HaloCount    = 2 * (uint)BlurRadius;
                    var  HaloThreads  = new uint3(HaloCount, HaloCount, 0);
                    var  GroupThreads = new uint3(32, 24, 1);
                
                    // Subtract halo threads if needed
                    return bActiveThreadsOnly ? GroupThreads - HaloThreads : GroupThreads;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(BlurRadius), BlurRadius, "Invalid blur radius");
            }
        }

        /// <summary>
        /// Get the blur kernel group thread dimensions for the specified settings.
        /// </summary>
        /// <param name="BlurQuality">Quality of the blur kernel applied</param>
        /// <param name="BlurDirection">Direction of the blur pass</param>
        /// <param name="BlurRadius">Radius of the blur in pixels</param>
        /// <param name="GroupShape">Shape of the kernel group</param>
        /// <param name="bActiveThreadsOnly">Whether if </param>
        /// <returns></returns>
        public static uint3 GetBlurKernelThreadNum(EQuality BlurQuality, EBlurDirection BlurDirection, EBlurRadius BlurRadius,  EBlurKernelShape GroupShape, bool bActiveThreadsOnly)
        {
            if (BlurDirection is EBlurDirection.TwoDimensional && BlurQuality is not EQuality.Low)
            {
                throw new ArgumentException("Two dimensional blur direction is only used by low quality blur");
            }
            
            if (GroupShape is EBlurKernelShape.Line)
            {
                // Linear blur kernel always uses 4 halo threads on each end
                uint3 HaloThreads  = BlurDirection is EBlurDirection.Horizontal ? new uint3(8, 0, 0) : new uint3(0, 8, 0);
                uint3 GroupThreads = BlurDirection is EBlurDirection.Horizontal ? new uint3(128, 1, 1) : new uint3(1, 128, 1);
                
                // Subtract halo threads if needed
                return bActiveThreadsOnly ? GroupThreads - HaloThreads : GroupThreads;
            }
            
            switch (BlurRadius)
            {
                case EBlurRadius.One:
                {
                    uint3 HaloThreads  = BlurDirection is EBlurDirection.Horizontal ? new uint3(2, 0, 0) : new uint3(0, 2, 0);
                    uint3 GroupThreads = BlurDirection is EBlurDirection.Horizontal ? new uint3(32, 24, 1) : new uint3(24, 32, 1);
                
                    // Subtract halo threads if needed
                    return bActiveThreadsOnly ? GroupThreads - HaloThreads : GroupThreads;
                }
                case EBlurRadius.Two:
                case EBlurRadius.Three:
                {
                    uint3 HaloThreads  = BlurDirection is EBlurDirection.Horizontal ? new uint3(2 * (uint)BlurRadius, 0, 0) : new uint3(0, 2 * (uint)BlurRadius, 0);
                    uint3 GroupThreads = BlurDirection is EBlurDirection.Horizontal ? new uint3(64, 12, 1) : new uint3(12, 64, 1);
                
                    // Subtract halo threads if needed
                    return bActiveThreadsOnly ? GroupThreads - HaloThreads : GroupThreads;
                }
                case EBlurRadius.Four:
                {
                    uint3 HaloThreads  = BlurDirection is EBlurDirection.Horizontal ? new uint3(8, 0, 0) : new uint3(0, 8, 0);
                    uint3 GroupThreads = BlurDirection is EBlurDirection.Horizontal ? new uint3(128, 6, 1) : new uint3(6, 128, 1);
                
                    // Subtract halo threads if needed
                    return bActiveThreadsOnly ? GroupThreads - HaloThreads : GroupThreads;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(BlurRadius), BlurRadius, "Invalid blur radius");
            }
        }
        
        public HBAOPlusComputeShaderParams(ComputeShader ComputeShader)
        {
            if (ComputeShader != null)
            {
                // Kernels
                DeinterleaveDepthKernelId     = ComputeShader.FindKernel(DEINTERLEAVE_DEPTH_KERNEL_NAME);
                CoarseAOKernelId              = ComputeShader.FindKernel(COARSE_AO_KERNEL_NAME);
                IntermediateBlurKernelId      = ComputeShader.FindKernel(INTERMEDIATE_BLUR_KERNEL_NAME);
                FinalBlurKernelId             = ComputeShader.FindKernel(FINAL_BLUR_KERNEL_NAME);
                CompressedTwoPassBlurKernelId = ComputeShader.FindKernel(COMPRESSED_TWO_PASS_BLUR_KERNEL_NAME);
                
                // Keywords
                DoubleDepthTextureKeyword             = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.DOUBLE_DEPTH_TEXTURE_KEYWORD_NAME);
                EightStepCoarseAOKeyword              = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.EIGHT_STEP_COARSE_AO_KEYWORD_NAME);
                FourStepCoarseAOKeyword               = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.FOUR_STEP_COARSE_AO_KEYWORD_NAME);
                EightDirectionCoarseAOKeyword         = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.EIGHT_DIRECTION_COARSE_AO_KEYWORD_NAME);
                FourDirectionCoarseAOKeyword          = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.FOUR_DIRECTION_COARSE_AO_KEYWORD_NAME);
                NormalsInWorldSpaceKeyword            = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.NORMALS_IN_WORLD_SPACE_KEYWORD_NAME);
                DeinterleavedDepthTexFormatR16Keyword = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.DEINTERLEAVED_DEPTH_TEX_FORMAT_R16_KEYWORD_NAME);
                DeinterleavedAOTexFormatR8Keyword     = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.DEINTERLEAVED_AO_TEX_FORMAT_R8_KEYWORD_NAME);
                BlurAlgorithmBilateralKeyword         = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.BLUR_ALGORITHM_BILATERAL_KEYWORD_NAME);
                BlurAlgorithmGaussianKeyword          = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.BLUR_ALGORITHM_GAUSSIAN_KEYWORD_NAME);
                BlurAlgorithmKawaseKeyword            = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.BLUR_ALGORITHM_KAWASE_KEYWORD_NAME);
                BlurSharpnessFromDepthKeyword         = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.BLUR_SHARPNESS_FROM_DEPTH_KEYWORD_NAME);
                BlurSharpnessFromNormalKeyword        = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.BLUR_SHARPNESS_FROM_NORMAL_KEYWORD_NAME);
                BlurSharpnessFromDepthNormalKeyword   = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.BLUR_SHARPNESS_FROM_DEPTH_NORMAL_KEYWORD_NAME);
                BlurKernelRadiusOneKeyword            = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.BLUR_KERNEL_RADIUS_ONE_KEYWORD_NAME);
                BlurKernelRadiusTwoKeyword            = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.BLUR_KERNEL_RADIUS_TWO_KEYWORD_NAME);
                BlurKernelRadiusThreeKeyword          = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.BLUR_KERNEL_RADIUS_THREE_KEYWORD_NAME);
                BlurKernelRadiusFourKeyword           = new LocalKeyword(ComputeShader, HBAOPlusShaderParams.BLUR_KERNEL_RADIUS_FOUR_KEYWORD_NAME);
            }
            else
            {
                // Kernels
                DeinterleaveDepthKernelId     = -1;
                CoarseAOKernelId              = -1;
                IntermediateBlurKernelId      = -1;
                FinalBlurKernelId             = -1;
                CompressedTwoPassBlurKernelId = -1;
                
                // Keywords
                DoubleDepthTextureKeyword             = default;
                EightStepCoarseAOKeyword              = default;
                FourStepCoarseAOKeyword               = default;
                EightDirectionCoarseAOKeyword         = default;
                FourDirectionCoarseAOKeyword          = default;
                NormalsInWorldSpaceKeyword            = default;
                DeinterleavedDepthTexFormatR16Keyword = default;
                DeinterleavedAOTexFormatR8Keyword     = default;
                BlurAlgorithmBilateralKeyword         = default;
                BlurAlgorithmGaussianKeyword          = default;
                BlurAlgorithmKawaseKeyword            = default;
                BlurSharpnessFromDepthKeyword         = default;
                BlurSharpnessFromNormalKeyword        = default;
                BlurSharpnessFromDepthNormalKeyword   = default;
                BlurKernelRadiusOneKeyword            = default;
                BlurKernelRadiusTwoKeyword            = default;
                BlurKernelRadiusThreeKeyword          = default;
                BlurKernelRadiusFourKeyword           = default;
            }
        }
    }
}