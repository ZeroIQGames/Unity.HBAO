#ifndef HBAO_PLUS_INPUT_HLSL
#define HBAO_PLUS_INPUT_HLSL

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

//region constants

/**
 * @brief Number of calculation steps
 */
static const int HBAO_STEPS
#ifdef _HBAO_PLUS_FOUR_STEPS
= 4;
#elif defined _HBAO_PLUS_EIGHT_STEPS
= 8;
#else
= 8;
#endif

/**
 * @brief Number of directions
 */
static const int HBAO_DIRECTIONS
#ifdef _HBAO_PLUS_FOUR_DIRECTIONS
= 4;
#elif defined _HBAO_PLUS_EIGHT_DIRECTIONS
= 8;
#else
= 8;
#endif



/**
 * @brief Radius of the blur kernel in pixels
 */
static const int BLUR_KERNEL_RADIUS
#if defined _BLUR_KERNEL_RADIUS_1
= 1;
#define _BLUR_KERNEL_RADIUS 1
#elif defined _BLUR_KERNEL_RADIUS_2
= 2;
#define _BLUR_KERNEL_RADIUS 2
#elif defined _BLUR_KERNEL_RADIUS_3
= 3;
#define _BLUR_KERNEL_RADIUS 3
#elif defined _BLUR_KERNEL_RADIUS_4
= 4;
#define _BLUR_KERNEL_RADIUS 4
#else
= 2;
#define _BLUR_KERNEL_RADIUS 2
#endif

static const float BLUR_SIGMA = (BLUR_KERNEL_RADIUS + 1) * 0.5f;

static const float BLUR_FALLOFF = 1.0f / (2.0f * BLUR_SIGMA * BLUR_SIGMA);

// ReSharper disable once CppInconsistentNaming
/**
 * @brief Sharpness value if variable sharpness is disabled. Aliases \c _ForegroundSharpness
 */
#define _UniformSharpness _ForegroundBlurSharpness

/**
 * @brief Angle difference between two consecutive rays of HBAO before jitter application
 */
static const float HBAO_RAY_ANGLE_DIFF = 2.0 * PI / HBAO_DIRECTIONS;

//end region

CBUFFER_START(HBAO_Per_Material_Params)

/**
 * @brief Texture layer that should be sampled from \c _DeinterleavedDepthTexture that corresponds to the current slice being processed.
 */
float _DeinterleavedDepthTextureIndices[16];

CBUFFER_END

CBUFFER_START(HBAO_Per_Render_Params)

//region HBAO+ visual parameters

/**
 * @brief Radius of the AO effect to power of 2
 */
float _AORadius2;

/**
 * @brief Negative 1 over radius of AO effect to power of 2 (-1 / _AORadius2 ^ 2)
 */
float _AONegativeInvR2;

/**
 * @brief The number of screen pixels on a vertical line with length \c AORadius  would take on the screen if it was placed exactly 1 meters away from the camera.
 * @remarks By dividing this by linear Z the number of screen pixels can be derived at any arbitrary fistance from the camera.
 */
float _AOVerticalRadiusToScreenPixels;

/**
 * @brief Number of minimum AO pixels for radius for objects in the background. Will be -1 if background AO is disabled.
 */
int _BackgroundAORadiusPixels;

/**
 * @brief Number of maximum AO pixels for radius for objects in the foreground. Will be -1 if background AO is disabled.
 */
int _ForegroundAORadiusPixels;

/**
 * @brief Small scale AO values. The higher the value, the darker the AO. Valid range is [0, 2]
 */
float _SmallScaleAOAmount;

/**
 * @brief Large scale AO values. The higher the value, the darker the AO. Valid range is [0, 2]
 */
float _LargeScaleAOAmount;

/**
 * @brief Bias for the minimum height difference to result in non-zero accumulated AO. The higher the value, the biffer the required height diff for AO to appear. Valid range is [0.0, 0.5].
 */
float _TessellationBias;

/**
 * @brief Sharpness value applied for objects closer than \c _ForegroundSharpnessDepth to the camera.
 * @remarks Also used as the uniform sharpness when variable sharpness is disabled.
 */
float _ForegroundBlurSharpness;

/**
 * @brief Linearized depth where objects closer than this will receive \c _ForegroundSharpness value.
 */
float _ForegroundBlurViewDepth;

/**
 * @brief Sharpness value applied for objects further than \c _BackgroundSharpnessDepth from the camera.
 */
float _BackgroundBlurSharpness;

/**
 * @brief Linearized depth where objects further than this will receive \c _BackgroundSharpness value.
 */
float _BackgroundBlurViewDepth;

/**
 * @brief Exponent that the raw calculated AO is raised to the power of. Higher values result in stronger AO. Expected range in [1, 4]
 */
float _IntensityExponent;

//endregion

//region Screen params

/**
 * @brief Dimensions of full res textures in the pass
 * @details x: Texture width, y: Texture height, z: 1 / Texture width, w: 1 / Texture height
 */
float4 _FullResDimensions;

/**
 * @brief Dimensions of full res textures in the pass
 * @details x: Texture width, y: Texture height, z: 1 / Texture width, w: 1 / Texture height
 */
float4 _QuarterResDimensions;

/**
 * @brief Uniform jitter values generated per slice
 */
float4 _AOJitterArray[16];

/**
 * @brief Scale and bias for finding x and y tangents of a ray going from the camera to any screen space UV.
 * @details Component values -> x: Horizontal scale based on UV, y: Vertical scale based on UV, z: Horizontal bias, w: Vertical bias \n
 * Usage example: 
 * @code 
 * // We first calculate the tangent of the horizontal component of the ray going from 
 * // the camera to the specified UV against the ray going to the screen center.
 * // After that, the X component of view space position would be the linear depth from the camera 
 * // multiplied by the calculated tangent.
 * float HorizontalTangentFromCenter = mad(NormalizedScreenSpaceUV.x, _UVToViewParams.x, _UVToViewParams.z);
 * float ViewSpaceX                  = HorizontalTangentFromCenter * LinearDepth;
 * // Similarly for the Y component
 * float VerticalTangentFromCenter = mad(NormalizedScreenSpaceUV.y, _UVToViewParams.y, _UVToViewParams.w);
 * float ViewSpaceY                = VerticalTangentFromCenter * LinearDepth;
 * // The view space position finally would be:
 * float3 PositionVS = float3(HorizontalTangentFromCenter, VerticalTangentFromCenter, LinearDepth);
 * // This can be shortened to:
 * float3 PositionVS = float3(mad(NormalizedScreenSpaceUV, _UVToViewParams.xy, _UVToViewParams.zw) * LinearDepth, LinearDepth);
 * @endcode 
 */
float4 _UVToViewParams;

//endregion

CBUFFER_END

CBUFFER_START(HBAO_PER_SLICE_PARAMS)

/**
 * @brief Index of the slice being processed
 */
uint _SliceIndex;

CBUFFER_END

CBUFFER_START(HBAO_Per_Pass_Params)

/**
 * @brief Texel size data for \c _DepthTextureOne
 */
float4 _DepthTextureOne_TexelSize;

/**
 * @brief Texel size data for \c _DepthTextureTwo
 */
float4 _DepthTextureTwo_TexelSize;

/**
 * @brief Texel size data for \c _LinearizedDepth
 */
float4 _LinearizedDepth_TexelSize;

/**
 * @brief Texel size data for \c _DeinterleavedDepthTexture
 */
float4 _DeinterleavedDepthTexture_TexelSize;

/**
 * @brief Texture layer that should be sampled from \c _DeinterleavedDepthTexture that corresponds to the current slice being processed.
 */
float _InstanceIdToRenderTargetIndexId[4];

/**
 * @brief Texel size data for \c _ViewSpaceNormalsTexture
 */
float4 _ViewSpaceNormalsTexture_TexelSize;

/**
 * @brief Texel size data for \c _DeinterleavedDepthTextureRW
 */
float4 _DeinterleavedDepthTextureRW_TexelSize;

/**
 * @brief Texel size data for \c _AODepthTexture
 */
float4 _IntermediateBlurTexture_TexelSize;

CBUFFER_END

/**
 * @brief Static slice index variable accessible from everywhere in the shader. Should be set-up first.
 */
static uint DeinterleavedDepthSliceIndex;

/**
 * @brief First depth texture of the screen.
 */
TEXTURE2D(_DepthTextureOne);

/**
 * @brief Optional second depth texture of the screen.
 */
TEXTURE2D(_DepthTextureTwo);

/**
 * @brief Full screen texture with linearized view space depth values
 */
TEXTURE2D(_LinearizedDepth);

/**
 * @brief 16 or 4 layer deinterleaved texture array of linearized depth
 * @remarks Layer count depends on the render path (compute vs raster), but the correct layer/texture will be bound in any shader that needs this, so this should always be sampled at index 0.
 */
TEXTURE2D_ARRAY(_DeinterleavedDepthTexture);

/**
 * @brief 16 layer deinterleaved texture array of AO values
 */
TEXTURE2D_ARRAY(_DeinterleavedAOTexture);

/**
 * @brief Texture holding normal in view space for each pixel on the screen
 */
TEXTURE2D(_ViewSpaceNormalsTexture);

/**
 * @brief SSAO texture used in blue passes. Has AO value in the r channel and linearized depth in the g channel.
 */
TEXTURE2D(_IntermediateBlurTexture);

#ifdef _HBAO_PLUS_DEINTERLEAVED_DEPTH_TEX_FORMAT_R16
/**
 * @brief Output: 1/4 resolution 16-layer Texture2DArray (Requires Random Write)
 * @remarks R16 version usually used when scene normals are constructed in the depth-normal prepass
 */
RW_TEXTURE2D_ARRAY(half, _DeinterleavedDepthTextureRW);
#else
/**
 * @brief Output: 1/4 resolution 16-layer Texture2DArray (Requires Random Write)
 * @remarks R32 version usually used when normals are reconstructed from depth texture
 */
RW_TEXTURE2D_ARRAY(float, _DeinterleavedDepthTextureRW);
#endif


#ifdef _HBAO_PLUS_DEINTERLEAVED_AO_TEX_FORMAT_R8
/**
 * @brief Output: 1/4 resolution 16-layer Texture2DArray (Requires Random Write)
 * @remarks Used when R8_UNorm format is used for deinterleaved AO texture.
 */
RW_TEXTURE2D_ARRAY(unorm float, _DeinterleavedAOTextureRW);
#else
/**
 * @brief Output: 1/4 resolution 16-layer Texture2DArray (Requires Random Write)
 * @remarks Used when R16_UNorm format is used for deinterleaved AO texture.
 */
RW_TEXTURE2D_ARRAY(unorm half, _DeinterleavedAOTextureRW);
#endif


#if defined(FINAL_BLUR_PASS) || defined(_HBAO_PLUS_BLUR_FILTER_KAWASE)
/**
 * @brief Blur texture to write to in the final blur pass. Should be of type R8_UNorm.
 */
RW_TEXTURE2D(unorm float, _BlurRenderTargetTextureRW);
#elif defined(_HBAO_PLUS_BLUR_FILTER_GAUSSIAN)// We are in the first pass of gaussian filter 
/**
 * @brief Blur texture to write to in the first gaussian blur low-precision pass. Should be of type R8_UNorm.
 */
RW_TEXTURE2D(unorm float, _BlurRenderTargetTextureRW);
#elif defined(_HBAO_PLUS_BLUR_FILTER_BILATERAL) //Bilateral-filter first pass
#if defined(_HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH_NORMAL) // Sharpness source is both depth and normal
/**
 * @brief Blur texture to write to in the first bilateral blur pass with the sharpness source set to depth-normal. Should be of type R16G16B16A16_UNorm.
 */
RW_TEXTURE2D(unorm half4, _BlurRenderTargetTextureRW);
#elif defined(_HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH)
/**
 * @brief Blur texture to write to in the first bilateral blur pass with the sharpness source set to depth only. Should be of type R16G16_UNorm.
 */
RW_TEXTURE2D(unorm half2, _BlurRenderTargetTextureRW);
#elif defined(_HBAO_PLUS_BLUR_SHARPNESS_FROM_NORMAL)
/**
 * @brief Blur texture to write to in the first bilateral blur pass with the sharpness source set to normal only. Should be of type R8G8B8A8_UNorm.
 */
RW_TEXTURE2D(unorm float4, _BlurRenderTargetTextureRW);
#endif
#endif

#endif
