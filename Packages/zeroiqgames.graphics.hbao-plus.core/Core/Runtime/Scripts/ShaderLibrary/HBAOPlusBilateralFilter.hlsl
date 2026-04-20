#ifndef HBAO_PLUS_BILATERAL_FILTER_HLSL
#define HBAO_PLUS_BILATERAL_FILTER_HLSL

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusTransformationUtil.hlsl"

/**
 * @brief Whether if to calculate Scale and Bias of the kernel once at the start and use a mad operation to calculate sample weight (1), or use a minus and a multiple for every sample (0)
 * @todo Tests on rtx 3060 show absolutely no difference. Remove if unnecessary.
 */
#define FILTER_USE_MAD_OP 1

#if defined(_HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH_NORMAL) // Both are defined
    #define _HBAO_PLUS_BILATERAL_FILTER_DEPTH_WEIGHT_TERM
    #define _HBAO_PLUS_BILATERAL_FILTER_NORMAL_WEIGHT_TERM
#elif defined(_HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH)
    #define _HBAO_PLUS_BILATERAL_FILTER_DEPTH_WEIGHT_TERM
#elif defined(_HBAO_PLUS_BLUR_SHARPNESS_FROM_NORMAL)
    #define _HBAO_PLUS_BILATERAL_FILTER_NORMAL_WEIGHT_TERM
#endif


/**
 * @brief Data of the blur kernel center
 */
struct KernelData
{
    /**
     * @brief Normalized UV of the kernel center (pixel being calculated) in screen space
     */
    float2 CenterNormalizedScreenSpaceUV;

    /**
     * @brief Coarse AO(x component) and linearized depth(y component) value at the center of the blur kernel.
     */
    half CenterAO;
    
    /**
     * @brief Normal vector at the center of the kernel
     */
    half3 CenterNormal;
    
    /**
     * @brief Position of the kernel center in view space.
     */
    float3 CenterPositionVS;
    
    /**
     * @brief How sharp the applied blur should be.
     */
    float BlurSharpness;
    
#if FILTER_USE_MAD_OP // Pre-compute bias and scale and use mad intrinsic
    /**
     * @brief Minus kernel center depth multiplied by sharpness
     */
    float Bias;
    
    /**
     * @brief How sharp the applied blur should be. Same as \c BlurSharpness
     */
    float Scale;
#endif
};

/**
 * @brief Convert raw AO depth to absolute AO depth usable by HBAO+ by converting the normalized liear depth to absolute linear depth
 * @param RawAODepth Raw sampled value from the AODepth texture (x: Reinterleaved AO value for the pixel, y: Normalized linearized depth of the pixel)
 * @return AO Depth value with depth converted to absolute linear depth
 */
float2 GetAbsoluteAODepth(float2 RawAODepth)
{
    return float2(RawAODepth.x, TransformToLinearEyeDepth(RawAODepth.y, _ProjectionParams));
}

/**
 * @brief Sample the AODepth texture and get the pre-blur AO value and normalized linearized depth value at the specified \p NormalizedScreenSpaceUV
 * @param NormalizedScreenSpaceUV UV of the screen space pixel to sample ao and depth for
 * @return x: Reinterleaved AO value for the pixel, y: Normalized linearized depth of the pixel
 */
float2 SampleAODepthRaw(float2 NormalizedScreenSpaceUV)
{
    return SAMPLE_TEXTURE2D_LOD(_IntermediateBlurTexture, sampler_PointClamp, NormalizedScreenSpaceUV, 0.0).xy;
}

/**
 * @brief Sample the AODepth texture and get the pre-blur AO value and normalized linearized depth value at the specified \p NormalizedScreenSpaceUV
 * @param NormalizedScreenSpaceUV UV of the screen space pixel to sample ao and depth for
 * @return x: Reinterleaved AO value for the pixel, y: Normalized linearized depth of the pixel, zw: Packed and encoded normal
 */
float4 SampleAODepthNormalRaw(float2 NormalizedScreenSpaceUV)
{
    return SAMPLE_TEXTURE2D_LOD(_IntermediateBlurTexture, sampler_PointClamp, NormalizedScreenSpaceUV, 0.0);
}

/**
 * @brief Sample the intermediate blur texture and get the pre-blur AO value at the specified \p NormalizedScreenSpaceUV
 * @param NormalizedScreenSpaceUV UV of the screen space pixel to sample AO for
 * @return Reinterleaved AO value for the pixel
 */
float SampleAO(float2 NormalizedScreenSpaceUV)
{
    return SAMPLE_TEXTURE2D(_IntermediateBlurTexture, sampler_PointClamp, NormalizedScreenSpaceUV).x;
}

/**
 * @brief Sample the AODepth texture and get the pre-blur AO value and absolute linearized depth value at the specified \p NormalizedScreenSpaceUV
 * @param NormalizedScreenSpaceUV UV of the screen space pixel to sample ao and depth for
 * @return x: Reinterleaved AO value for the pixel, y: Absolute linearized depth of the pixel
 */
float2 SampleAODepth(float2 NormalizedScreenSpaceUV)
{
    return GetAbsoluteAODepth(SampleAODepthRaw(NormalizedScreenSpaceUV));
}

/**
 * @brief Sample the AODepth texture and get the pre-blur AO value and absolute linearized depth value at the specified \p NormalizedScreenSpaceUV
 * @param NormalizedScreenSpaceUV UV of the screen space pixel to sample ao and depth for
 * @param AODepth x: Reinterleaved AO value for the pixel, y: Absolute linearized depth of the pixel
 * @param Normal Unpacked and uncoded unit length normal vector of the scene at the specified uv
 * 
 */
void SampleAODepthNormal(float2 NormalizedScreenSpaceUV, out float2 AODepth, out half3 Normal)
{
    float4 RawAODepthNormal = SampleAODepthNormalRaw(NormalizedScreenSpaceUV);
    
    // Unpacking
    AODepth = GetAbsoluteAODepth(RawAODepthNormal.xy);
    Normal  = UnpackAndDecodeNormal(RawAODepthNormal.zw);
}

/**
 * @brief Get the blur sharpness based on the pass settings for the specified linearized view space depth.
 * @param LinearizedDepth Linearized depth of the blur kernel center
 * @return Blur sharpness based on the effect settings
 * @remarks Lerps between foreground and background blur based on \p LinearizedDepth if variable sharpness is enabled. Constant sharpness otherwise.
 */
float GetBlurSharpness(float LinearizedDepth)
{
#ifdef _HBAO_PLUS_DEPTH_DEPENDENT_BLUR_SHARPNESS
    
    float LerpAlpha = (LinearizedDepth - _ForegroundBlurViewDepth) / (_BackgroundBlurViewDepth - _ForegroundBlurViewDepth);
    
    return lerp(_ForegroundBlurSharpness, _BackgroundBlurSharpness, saturate(LerpAlpha));

#else
    
    return _UniformSharpness;
    
#endif
}

void InitializeKernelData(float2 NormalizedScreenSpaceUV, out KernelData Data, out float4 RawAODepthNormal)
{
    // Sample the raw value
    RawAODepthNormal = SampleAODepthNormalRaw(NormalizedScreenSpaceUV);
    
    // Unpack raw values
    float2 AbsAODepth = GetAbsoluteAODepth(RawAODepthNormal.xy);
    float3 Normal     = UnpackAndDecodeNormal(RawAODepthNormal.zw);
    
    // Initialize kernel data
    Data.CenterAO                      = AbsAODepth.x;
    Data.CenterPositionVS              = ReconstructPositionVS(NormalizedScreenSpaceUV, AbsAODepth.y);
    Data.CenterNormal                  = Normal;
    Data.BlurSharpness                 = GetBlurSharpness(Data.CenterPositionVS.z);
    Data.CenterNormalizedScreenSpaceUV = NormalizedScreenSpaceUV;
    
    #if FILTER_USE_MAD_OP
    Data.Scale = Data.BlurSharpness;
    Data.Bias = -Data.CenterPositionVS.z * Data.BlurSharpness;
    #endif
}

    /**
     * @brief Calculate the sample weight to be used in the blur kernel based on its distance from the center and depth slope.
     * @param SampleNormal Normal of the sampled screen point
     * @param Data Kernel data including sharpness and center AODepth
     * @return Sample weight in the blue kernel
     */
    float CalculateBilateralSampleNormalWeight(half3 SampleNormal, KernelData Data)
    {
#ifdef _HBAO_PLUS_BILATERAL_FILTER_NORMAL_WEIGHT_TERM
        return smoothstep(half(0.8), half(1.0), dot(SampleNormal, Data.CenterNormal));
#else
        return 1.0f;
#endif
    }

    /**
     * @brief Calculate the sample weight to be used in the blur kernel based on depth difference.
     * @param SamplePositionVS Position of the sampled pixel in view space
     * @param SampleDistance Distance of the sample from the kernel center in pixels (e.g., 4 pixels)
     * @param DepthSlope Slope of the depth between the center and the sample
     * @param Data Kernel data including sharpness and center AODepth
     * @return Sample weight in the blue kernel
     */
    float CalculateBilateralSampleDepthWeight(float3 SamplePositionVS, float SampleDistance, float DepthSlope, KernelData Data)
    {
#ifdef _HBAO_PLUS_BILATERAL_FILTER_DEPTH_WEIGHT_TERM
        float SampleDepth;
#ifndef _BILATERAL_FILTER_DEPTH_FROM_SLOPE
        SampleDepth = SamplePositionVS.z;
#else
        SampleDepth -= DepthSlope * SampleDistance;
#endif

#if FILTER_USE_MAD_OP
        float DeltaZ = mad(SampleDepth, Data.Scale, Data.Bias);
#else
        float DeltaZ = (SampleDepth - Data.CenterPositionVS.z) * Data.BlurSharpness;
#endif
    
        return exp2(-SampleDistance * SampleDistance * BLUR_FALLOFF - DeltaZ * DeltaZ);
    
#else
        return 1.0f;
#endif
    }

    /**
     * @brief Calculate the sample weight to be used in the blur kernel based on its distance from the center and depth slope.
     * @param SamplePositionVS Position of the sampled pixel in view space
     * @param SampleNormal Normal of the sampled pixel
     * @param SampleDistance Distance of the sample from the kernel center in pixels (e.g., 4 pixels)
     * @param DepthSlope Slope of the depth between the center and the sample
     * @param Data Kernel data including sharpness and center AODepth
     * @return Sample weight in the blue kernel
     */
    float CalculateBilateralSampleWeight(float3 SamplePositionVS, half3 SampleNormal, float SampleDistance, float DepthSlope, KernelData Data)
    {
        float DepthWeightTerm  = CalculateBilateralSampleDepthWeight(SamplePositionVS, SampleDistance, DepthSlope, Data);
        float NormalWeightTerm = CalculateBilateralSampleNormalWeight(SampleNormal, Data);

        return min(DepthWeightTerm, NormalWeightTerm);
    }

    /**
     * @brief Add weighted AO of the sample to the current accumulated AO value
     * @param SampleDepth Depth texture value for this sample
     * @param SampleAONormal AO and normal of the sample screen point
     * @param SampleUV Normalized screen space UV of the sampled point
     * @param SampleDistance Distance of this sample from the kernel center in pixels
     * @param DepthSlope Depth slope of this sample from the kernel center depth
     * @param CenterData Data for the blur kernel including sample data and sharpness settings
     * @param AccumulatedAO Total accumulated AO for the kernel so far
     * @param WeightSum Total accumulated weight sum so far
     */
    void AccumulateSampledAO(
        float       SampleDepth,
        half4       SampleAONormal,
        float2      SampleUV,
        float       SampleDistance,
        float       DepthSlope,
        KernelData  CenterData,
        inout float AccumulatedAO,
        inout float WeightSum
    )
    {
        // Weight of the sample based on the sample distance and blur parameters
        float3 SamplePositionVS = ReconstructPositionVS(SampleUV, SampleDepth);
        float  SampleWeight     = CalculateBilateralSampleWeight(SamplePositionVS, SampleAONormal.yzw, SampleDistance, DepthSlope, CenterData);
        
        // Accumulate 
        AccumulatedAO += SampleWeight * SampleAONormal.x;
        WeightSum     += SampleWeight;
    }

#endif
