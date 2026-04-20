#ifndef HBAO_PLUS_BLUR_COMPUTE_HLSL
#define HBAO_PLUS_BLUR_COMPUTE_HLSL

#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusFiltersCompute.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusBilateralFilterCompute.hlsl"

// We use 16x16 thread groups (256 threads per group)
// This includes edge pixels that are used in the calculations but are not written to
// Each thread group calculates a block size of 
// at least 8x8 pixels (when kernel radius is 4 pixels) and up to 15x15 pixels (when kernel radius is 1 pixel).
[numthreads(BLUR_NUM_THREADS_X, BLUR_NUM_THREADS_Y, BLUR_NUM_THREADS_Z)]
void IntermediateBlur(uint3 DispatchThreadID : SV_DispatchThreadID, uint3 GroupThreadID : SV_GroupThreadID, uint3 GroupID : SV_GroupID)
{
#ifdef _HBAO_PLUS_BLUR_FILTER_BILATERAL
    uint2  ThreadPixelID;
    float2 NormalizedScreenSpaceUV = GetThreadNormalizedScreenSpaceUV(GroupID, GroupThreadID, DispatchThreadID, ThreadPixelID);
    uint   GroupThreadIndex        = GetGroupThreadIndex(GroupThreadID);
	
    // Initialize group memory
    KernelData Data;
    float4     RawAODepthNormal;
    HBAOPlusBilateralFilter::PopulateThreadGroupMemory(GroupThreadIndex, NormalizedScreenSpaceUV, Data, RawAODepthNormal);
	
    // Memory barrier. Note that this can only be called in converging code paths, so we call it here before early out of halo threads
    GroupMemoryBarrierWithGroupSync();
    if (IsHaloThread(GroupThreadID))
    {
        // This is a thread that is read from by other threads but does not write into blur render target
        return;
    }
    
    // Initialize thread static data
    HBAOPlusBilateralFilter::InitializeThread(GroupThreadID);
    
#ifdef BLUR_TRANSPOSE_WRITE
    // Do a transpose write so we end up with vertical writes but horizontal reads in both passes
    ThreadPixelID = ThreadPixelID.yx;
#endif
    
#pragma warning( disable : 3206 ) // Implicit truncation of vector type
    // Write the blurred AO to the render target texture
    // Do a transpose write so we end up with vertical writes but horizontal reads in both passes
    _BlurRenderTargetTextureRW[ThreadPixelID] = half4(HBAOPlusBilateralFilter::CalculateBlurredAO(Data), RawAODepthNormal.yzw);
#pragma warning( default : 3206 )
    
#elif _HBAO_PLUS_BLUR_FILTER_GAUSSIAN
#endif
}


// We use 16x16 thread groups (256 threads per group)
// This includes edge pixels that are used in the calculations but are not written to
// Each thread group calculates a block size of 
// at least 8x8 pixels (when kernel radius is 4 pixels) and up to 15x15 pixels (when kernel radius is 1 pixel).
[numthreads(BLUR_NUM_THREADS_X, BLUR_NUM_THREADS_Y, BLUR_NUM_THREADS_Z)]
void FinalBlur(uint3 DispatchThreadID : SV_DispatchThreadID, uint3 GroupThreadID : SV_GroupThreadID, uint3 GroupID : SV_GroupID)
{
#ifdef _HBAO_PLUS_BLUR_FILTER_BILATERAL	
    uint2  ThreadPixelID;
    float2 NormalizedScreenSpaceUV = GetThreadNormalizedScreenSpaceUV(GroupID, GroupThreadID, DispatchThreadID, ThreadPixelID);
    uint   GroupThreadIndex        = GetGroupThreadIndex(GroupThreadID);
	
    // Initialize group memory
    KernelData Data;
    float4     RawAODepthNormal;
    HBAOPlusBilateralFilter::PopulateThreadGroupMemory(GroupThreadIndex, NormalizedScreenSpaceUV, Data, RawAODepthNormal);
	
    // Memory barrier. Note that this can only be called in converging code paths, so we call it here before early out of halo threads
    GroupMemoryBarrierWithGroupSync();
    if (IsHaloThread(GroupThreadID))
    {
        // This is a thread that is read from by other threads but does not write into blur render target
        return;
    }
    
    // Initialize thread static data
    HBAOPlusBilateralFilter::InitializeThread(GroupThreadID);
    
#ifdef BLUR_TRANSPOSE_WRITE
    // Do a transpose write so we end up with vertical writes but horizontal reads in both passes
    ThreadPixelID = ThreadPixelID.yx;
#endif
    
    // Write the blurred AO to the render target texture
    _BlurRenderTargetTextureRW[ThreadPixelID] = pow(saturate(HBAOPlusBilateralFilter::CalculateBlurredAO(Data)), _IntensityExponent);
#elif _HBAO_PLUS_BLUR_FILTER_GAUSSIAN
#elif _HBAO_PLUS_BLUR_FILTER_KAWASE
#endif
}

// We use 32x24 thread groups (768 threads per group)
// This includes edge pixels that are used in the calculations but are not written to
// Each thread group calculates a block size of 
// at least 8x8 pixels (when kernel radius is 4 pixels) and up to 15x15 pixels (when kernel radius is 1 pixel).
[numthreads(BLUR_NUM_THREADS_X, BLUR_NUM_THREADS_Y, BLUR_NUM_THREADS_Z)]
void CompressedDoublePassBlur(uint3 DispatchThreadID : SV_DispatchThreadID, uint3 GroupThreadID : SV_GroupThreadID, uint3 GroupID : SV_GroupID)
{
#ifdef _HBAO_PLUS_BLUR_FILTER_BILATERAL	
    uint2  ThreadPixelID;
    float2 NormalizedScreenSpaceUV = GetThreadNormalizedScreenSpaceUV(GroupID, GroupThreadID, DispatchThreadID, ThreadPixelID);
    uint   GroupThreadIndex        = GetGroupThreadIndex(GroupThreadID);
	
    // Initialize group memory
    KernelData Data;
    float4     RawAODepthNormal;
    HBAOPlusBilateralFilter::PopulateThreadGroupMemory(GroupThreadIndex, NormalizedScreenSpaceUV, Data, RawAODepthNormal);
	
    // Memory barrier. Note that this can only be called in converging code paths, so we call it here before early out of halo threads
    GroupMemoryBarrierWithGroupSync();
    if (!IsVerticalHaloThread(GroupThreadID))
    {
        // Initialize thread static data
        HBAOPlusBilateralFilter::InitializeThread(GroupThreadID);
        HBAOPlusBilateralFilter::SetIntermediateAO(Data, HBAOPlusBilateralFilter::CalculateBlurredAOVertical(Data), GroupThreadIndex);
    }
    
    // Wait for all threads to finish vertical blur calculation
    GroupMemoryBarrierWithGroupSync();
    
    if (!IsHaloThread(GroupThreadID))
    {
        HBAOPlusBilateralFilter::StartFinalCompressedPass();
        _BlurRenderTargetTextureRW[ThreadPixelID] = pow(saturate(HBAOPlusBilateralFilter::CalculateBlurredAOHorizontal(Data)), _IntensityExponent);
    }
    
#elif _HBAO_PLUS_BLUR_FILTER_GAUSSIAN
#elif _HBAO_PLUS_BLUR_FILTER_KAWASE
#endif
}


#endif
