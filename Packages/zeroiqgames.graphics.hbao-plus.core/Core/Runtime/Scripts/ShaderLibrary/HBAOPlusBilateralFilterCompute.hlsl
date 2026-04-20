#ifndef HBAO_PLUS_BILATERAL_FILTER_COMPUTE_HLSL
#define HBAO_PLUS_BILATERAL_FILTER_COMPUTE_HLSL

#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusBilateralFilter.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusTransformationUtil.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusFiltersCompute.hlsl"

namespace HBAOPlusBilateralFilter
{
    
// Support for native 16 bit and smaller thread group shared memory
#ifdef _SUPPORTS_NATIVE_16BIT
    #define FLOAT16_T float16_t
    #define FLOAT16_T2 float16_t2
    #define FLOAT16_T3 float16_t3
    #define FLOAT16_T4 float16_t4
#else
    #define FLOAT16_T half
    #define FLOAT16_T2 half2
    #define FLOAT16_T3 half3
    #define FLOAT16_T4 half4
#endif
    
    
#ifdef _HBAO_PLUS_BLUR_SHARPNESS_FROM_NORMAL
    /**
     * @brief Thread group shared 16x16 AO-Normal values. Used when the sharpness source is only scene normals.
     */
    groupshared FLOAT16_T4 AONormalCache[BLUR_NUM_THREADS_X * BLUR_NUM_THREADS_Y];
#else
    /**
     * @brief Thread group shared 16x16 AO-Depth values. Used when the sharpness source is either only scene depth or both depth and normal. 
     */
    groupshared FLOAT16_T2 AODepthCache[BLUR_NUM_THREADS_X * BLUR_NUM_THREADS_Y];
#endif
#ifdef _HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH_NORMAL
    /**
     * @brief Thread group shared 16x16 AO-Depth-Normal values. Used when the sharpness source is both depth and normal. 
     */
    groupshared FLOAT16_T3 NormalCache[BLUR_NUM_THREADS_X * BLUR_NUM_THREADS_Y];
#endif
    
#ifdef COMPRESSED_DOUBLE_PASS_BLUR
    /**
     * @brief Intermediate cache to save AO values when doing a compressed double pass
     */
    groupshared FLOAT16_T IntermediateAOCache[BLUR_NUM_THREADS_X * BLUR_NUM_THREADS_Y];

    /**
     * @brief Whether if currently the final pass in a compressed two-pass blur iis being processed by this thread.
     */
    static bool IsFinalCompressedPass = false;
#endif

    /**
     * @brief ID of the current thread in its thread group
     */
    static uint3 CurrentThreadID;

    /**
     * @brief Initialize the thread static data accessed from multiple places
     * @param GroupThreadID ID of the thread in its thread group
     */
    void InitializeThread(uint3 GroupThreadID)
    {
        CurrentThreadID = GroupThreadID;
    }
    
    /**
     * @brief Populate the thread group shared memory values.
     */
    void PopulateThreadGroupMemory(uint GroupThreadIndex, float2 NormalizedScreenSpaceUV, out KernelData Data, out float4 RawAODepthNormal)
    {
        InitializeKernelData(NormalizedScreenSpaceUV, Data, RawAODepthNormal);
#ifdef _HBAO_PLUS_BLUR_SHARPNESS_FROM_NORMAL
	    AONormalCache[GroupThreadIndex] = half4(Data.CenterAO, Data.CenterNormal);
#else
	    AODepthCache[GroupThreadIndex] = half2(Data.CenterAO, Data.CenterPositionVS.z);
#endif
#ifdef _HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH_NORMAL
	    NormalCache[GroupThreadIndex] = Data.CenterNormal;
#endif
    }
    
    /**
     * @brief Get AO, depth, and normal of the neighbor thread pixel at the specified delta
     * @param PixelDelta Delta of the target thread/pixel relative to the current thread/pixel
     * @param LinearDepth Absolute linearized depth value read from group memory
     * @param AONormal AO (x component) and normal value (yzw components) read from group memory
     */
    void GetAODepthNormalFromGroupMemory(int2 PixelDelta, out float LinearDepth, out half4 AONormal)
    {
        uint TargetThreadGroupIndex = GetGroupThreadIndex(CurrentThreadID + int3(PixelDelta, 0));
#if defined(_HBAO_PLUS_BLUR_SHARPNESS_FROM_NORMAL)
	    LinearDepth = 0.0f; // Don't have depth data when sharpness is from normal
        AONormal    = AONormalCache[TargetThreadGroupIndex];
#elif defined(_HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH)
        LinearDepth = AODepthCache[TargetThreadGroupIndex].y;
        AONormal    = half4(AODepthCache[TargetThreadGroupIndex].x, HALF3_ZERO);
#elif defined(_HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH_NORMAL)
	    AONormal    = half4(AODepthCache[TargetThreadGroupIndex].x, NormalCache[TargetThreadGroupIndex]);
        LinearDepth = AODepthCache[TargetThreadGroupIndex].y;
#else
        LinearDepth = HALF_ZERO;
        AONormal    = HALF4_ZERO;
#endif
#ifdef COMPRESSED_DOUBLE_PASS_BLUR
        if (IsFinalCompressedPass)
        {
            AONormal.x = IntermediateAOCache[TargetThreadGroupIndex];
        }
#endif
    }

    /**
     * @brief Accumulate AO from the specified \p StartingSampleDistance to the constant \c BLUR_KERNEL_RADIUS
     * @param StartingSampleDistance Starting Distance from the kernel to accumulate AO from
     * @param PixelDelta Delta of the kernel center pixel ID and the sample pixel ID
     * @param DeltaUV Delta of the UV between the center Screen Space UV and adjacent (either horizontal or vertical) pixel. Should be either x or y component of _AODepthTexture_TexelSize and 0 for the other component.
     * @param DepthSlope Slope of the depth from the center to the radis direction
     * @param Data Kernel data including center values and sharpness
     * @param AccumulatedAO Current total accumulated AO
     * @param WeightSum Current total weight of the accumulated AO
     */
    void AccumulateAO(
        float       StartingSampleDistance,
        int2        PixelDelta,
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
            float  SampleDepth;
            half4  SampleAONormal;
            float2 SampleUV = SampleDistance * DeltaUV + Data.CenterNormalizedScreenSpaceUV;
            GetAODepthNormalFromGroupMemory(PixelDelta * SampleDistance,  SampleDepth, SampleAONormal);
            AccumulateSampledAO(SampleDepth, SampleAONormal, SampleUV, SampleDistance, DepthSlope, Data, AccumulatedAO, WeightSum);
        }
    }

    /**
     * @brief Accumulate AO in the direction specified by \p DeltaUV, calculating the depth slope automatically based on the immediate neighbor of the kernel center.
     * @param PixelDelta Delta of the kernel center pixel ID and the sample pixel ID
     * @param DeltaUV Delta of the UV between the center Screen Space UV and adjacent (either horizontal or vertical) pixel. Should be either x or y component of _AODepthTexture_TexelSize and 0 for the other component.
     * @param Data Kernel data including center values and sharpness
     * @param AccumulatedAO Current total accumulated AO
     * @param WeightSum Current total weight of the accumulated AO
     * @remarks Applies depth slope only if the corresponding keyword is set
     */
    void AccumulateAO(
        int2        PixelDelta,
        float2      DeltaUV,
        KernelData  Data,
        inout float AccumulatedAO,
        inout float WeightSum
    )
    {
#ifdef _BILATERAL_FILTER_DEPTH_FROM_SLOPE
        // We need the immediate neighbor sample to calculate depth slope
        half4 SampleAONormal;
        float SampleDepth;
        GetAODepthNormalFromGroupMemory(PixelDelta,  SampleDepth, SampleAONormal);
        float  DepthSlope = (SampleDepth - Data.CenterPositionVS.z) / 1.0f; // DeltaDepth / SampleDistance
        float2 SampleUV   = DeltaUV + Data.CenterNormalizedScreenSpaceUV;

        // Since we already sampled the first neighbor, we accumulate the sampled AO for that one here to prevent unnecessary sampling of the same pixel again.
        AccumulateSampledAO(SampleDepth, SampleAONormal, SampleUV, 1, DepthSlope, Data, AccumulatedAO, WeightSum);
        
        // We accumulated AO for distance 1 above, so we start from distance 2
        AccumulateAO(2, PixelDelta, DeltaUV, DepthSlope, Data, AccumulatedAO, WeightSum);
#else
        // We accumulated AO for distance 1 above, so we start from distance 2
        AccumulateAO(1, PixelDelta, DeltaUV, 0.0f, Data, AccumulatedAO, WeightSum);
#endif
    }
    
    /**
     * @brief Calculate blurred AO at the specified screen space UV based on coarse AO and effect settings
     * @param Data Kernel data for the current pixel
     * @param Axis Axis of the applied blur. 1,0 for horizontal and 0,1 for vertical. Accumulates the AO in both positive and negative directions.
     * @return Blurred AO value
     */
    half CalculateBlurredAO(KernelData Data, int2 Axis)
    {
        float  AccumulatedAo = Data.CenterAO; // Total blurred AO value starting with the center AO
        float  WeightSum     = 1.0f;          // Sum of the blurred AO weights
        
    #if defined(BLUR_TRANSPOSE_WRITE) && defined(FINAL_BLUR_PASS)
        // Input texture is flipped by the previous pass
        float2 DeltaUV = Axis * _FullResDimensions.wz;
    #else
        float2 DeltaUV = Axis * _FullResDimensions.zw;
    #endif
        
    
        // Accumulate neighbor AO values
        AccumulateAO(+Axis, +DeltaUV, Data, AccumulatedAo, WeightSum);
        AccumulateAO(-Axis, -DeltaUV, Data, AccumulatedAo, WeightSum);

        return half(AccumulatedAo / WeightSum);
    }

    /**
     * @brief Calculate vertically blurred AO at the specified screen space UV based on coarse AO and effect settings
     * @param Data Kernel data for the current pixel
     * @return Blurred AO value
     */
    half CalculateBlurredAOVertical(KernelData Data)
    {
        return CalculateBlurredAO(Data, int2(0, 1));
    }

    /**
     * @brief Calculate horizontally blurred AO at the specified screen space UV based on coarse AO and effect settings
     * @param Data Kernel data for the current pixel
     * @return Blurred AO value
     */
    half CalculateBlurredAOHorizontal(KernelData Data)
    {
        return CalculateBlurredAO(Data, int2(1, 0));
    }
    
    /**
     * @brief Calculate horizontally blurred AO at the specified screen space UV based on coarse AO and effect settings
     * @param Data Kernel data for the current pixel
     * @return Blurred AO value
     */
    half CalculateBlurredAO(KernelData Data)
    {
    #ifdef BLUR_DIRECTION_HORIZONTAL
        return CalculateBlurredAOHorizontal(Data);
    #else 
        return CalculateBlurredAOVertical(Data);
    #endif
    }

    /**
     * @brief Save the intermediate AO value to group shared memory. Used for compressed double pass and is noop otherwise
     * @param Data Kernel data for the current thread
     * @param AO Calculated intermediate AO value
     * @param GroupThreadIndex Cache index of the thread in its thread group
     */
    void SetIntermediateAO(inout KernelData Data, half AO, uint GroupThreadIndex)
    {
#ifdef COMPRESSED_DOUBLE_PASS_BLUR
        Data.CenterAO                         = AO;
        IntermediateAOCache[GroupThreadIndex] = AO;
#endif
    }

    /**
     * @brief Initialize the required parameters for the final pass of a compressed two-pass blur. Should be called before calling blur functions.
     */
    void StartFinalCompressedPass()
    {
#ifdef COMPRESSED_DOUBLE_PASS_BLUR
        IsFinalCompressedPass = true;
#endif
    }
}



#endif
