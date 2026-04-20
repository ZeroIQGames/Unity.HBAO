#ifndef HBAO_PLUS_BILATERAL_FILTER_RASTER_HLSL
#define HBAO_PLUS_BILATERAL_FILTER_RASTER_HLSL

#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusBilateralFilter.hlsl"

/**
 * @brief Accumulate AO from the specified \p StartingSampleDistance to the constant \c BLUR_KERNEL_RADIUS
 * @param StartingSampleDistance Starting Distance from the kernel to accumulate AO from
 * @param DeltaUV Delta of the UV between the center Screen Space UV and adjacent (either horizontal or vertical) pixel. Should be either x or y component of _AODepthTexture_TexelSize and 0 for the other component.
 * @param DepthSlope Slope of the depth from the center to the radis direction
 * @param Data Kernel data including center values and sharpness
 * @param AccumulatedAO Current total accumulated AO
 * @param WeightSum Current total weight of the accumulated AO
 */
void AccumulateAO(
    float       StartingSampleDistance,
    float2      DeltaUV,
    float       DepthSlope,
    KernelData  Data,
    inout float AccumulatedAO,
    inout float WeightSum
)
{
    UNITY_UNROLL
    for (float SampleDistance = StartingSampleDistance; SampleDistance <= BLUR_KERNEL_RADIUS; SampleDistance += 1)
    {
        float2 SampledAODepth;
        half3  SampleNormal;
        float2 SampleUV = SampleDistance * DeltaUV + Data.CenterNormalizedScreenSpaceUV;
        SampleAODepthNormal(SampleUV,  SampledAODepth, SampleNormal);
        AccumulateSampledAO(SampledAODepth.y, half4(SampledAODepth.x, SampleNormal), SampleUV, SampleDistance, DepthSlope, Data, AccumulatedAO, WeightSum);
    }
}

/**
 * @brief Accumulate AO in the direction specified by \p DeltaUV, calculating the depth slope automatically based on the immediate neighbor of the kernel center.
 * @param DeltaUV Delta of the UV between the center Screen Space UV and adjacent (either horizontal or vertical) pixel. Should be either x or y component of _AODepthTexture_TexelSize and 0 for the other component.
 * @param Data Kernel data including center values and sharpness
 * @param AccumulatedAO Current total accumulated AO
 * @param WeightSum Current total weight of the accumulated AO
 * @remarks Applies depth slope only if the corresponding keyword is set
 */
void AccumulateAO(
    float2      DeltaUV,
    KernelData  Data,
    inout float AccumulatedAO,
    inout float WeightSum
)
{
#ifdef _BILATERAL_FILTER_DEPTH_FROM_SLOPE
    // We need the immediate neighbor sample to calculate depth slope
    float2 SampleAODepth;
    half3  SampleNormal;
    float2 SampleUV = DeltaUV + Data.CenterNormalizedScreenSpaceUV;
    SampleAODepthNormal(SampleUV,  SampleAODepth, SampleNormal);
    float  DepthSlope = (SampleAODepth.y - Data.CenterPositionVS.z) / 1.0f; // DeltaDepth / SampleDistance

    // Since we already sampled the first neighbor, we accumulate the sampled AO for that one here to prevent unnecessary sampling of the same pixel again.
    AccumulateSampledAO(SampleAODepth.y, half4(SampleAODepth.x, SampleNormal), SampleUV, 1, DepthSlope, Data, AccumulatedAO, WeightSum);
    
    // We accumulated AO for distance 1 above, so we start from distance 2
    AccumulateAO(2, DeltaUV, DepthSlope, Data, AccumulatedAO, WeightSum);
#else
    // We accumulated AO for distance 1 above, so we start from distance 2
    AccumulateAO(1, DeltaUV, 0.0f, Data, AccumulatedAO, WeightSum);
#endif
}

/**
 * @brief Calculate blurred AO at the specified screen space UV based on coarse AO and effect settings
 * @param NormalizedScreenSpaceUV UV of the pixel to calculate blurred AO for
 * @param Axis Axis of the applied blur. 1,0 for horizontal and 0,1 for vertical. Accumulates the AO in both positive and negative directions.
 * @return x: Blurred AO of the pixel. y: Normalized linear depth of the pixel.
 */
half4 CalculateBilateralBlurredAO(float2 NormalizedScreenSpaceUV, int2 Axis)
{
    // Initialize kernel data
    KernelData Data;
    float4 RawAODepthNormal;
    InitializeKernelData(NormalizedScreenSpaceUV, Data, RawAODepthNormal);
    
#if FILTER_USE_MAD_OP
    Data.Scale = Data.BlurSharpness;
    Data.Bias = -Data.CenterPositionVS.z * Data.BlurSharpness;
#endif
    
    float  AccumulatedAo = Data.CenterAO; // Total blurred AO value starting with the center AO
    float  WeightSum     = 1.0f;          // Sum of the blurred AO weights
    float2 DeltaUV       = Axis * _IntermediateBlurTexture_TexelSize.xy;
    
    // Accumulate neighbor AO values
    AccumulateAO(+DeltaUV, Data, AccumulatedAo, WeightSum);
    AccumulateAO(-DeltaUV, Data, AccumulatedAo, WeightSum);

    return half4(AccumulatedAo / WeightSum, RawAODepthNormal.yzw);
}

/**
 * @brief Calculate vertically blurred AO at the specified screen space UV based on coarse AO and effect settings
 * @param NormalizedScreenSpaceUV UV of the pixel to calculate blurred AO for
 * @return x: Blurred AO of the pixel. y: Linearized depth of the pixel.
 */
half4 CalculateBilateralBlurredAOVertical(float2 NormalizedScreenSpaceUV)
{
    return CalculateBilateralBlurredAO(NormalizedScreenSpaceUV, int2(0, 1));
}

/**
 * @brief Calculate horizontally blurred AO at the specified screen space UV based on coarse AO and effect settings
 * @param NormalizedScreenSpaceUV UV of the pixel to calculate blurred AO for
 * @return x: Blurred AO of the pixel. y: Linearized depth of the pixel.
 */
half4 CalculateBilateralBlurredAOHorizontal(float2 NormalizedScreenSpaceUV)
{
    return CalculateBilateralBlurredAO(NormalizedScreenSpaceUV, int2(1, 0));
}

#endif
