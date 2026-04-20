#ifndef HBAO_PLUS_DEPTH_DEINTERLEAVE_COMPUTE_HLSL
#define HBAO_PLUS_DEPTH_DEINTERLEAVE_COMPUTE_HLSL

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"


// We use 8x8 thread groups (64 threads per group)
[numthreads(8, 8, 1)]
void DeinterleaveDepth(uint3 GlobalThreadIndex : SV_DispatchThreadID)
{
    // Since thread groups are 64 in size, we can end up outside the screen on some edge groups.
    // We early out in that case.
    uint2 BaseScreenPosition = GlobalThreadIndex.xy * 4;
    if (BaseScreenPosition.x >= (uint)_FullResDimensions.x || BaseScreenPosition.y >= (uint)_FullResDimensions.y)
    {
        return;
    }
    
    // Normalized UV of the center of L0, L1, L4, L5 in the original depth texture
    float2 BaseNormalizedBlockUV  = (BaseScreenPosition + 1.0f) * _FullResDimensions.zw;
    
    // Each thread assigns values to 16 layers
    // Each layer has the linearized depth values of a 4x4 block
    // Layers are ordered row major in the block
    // | L00 | L01 | L02 | L03 |
    // | L04 | L05 | L06 | L07 |
    // | L08 | L09 | L10 | L11 |
    // | L12 | L13 | L14 | L15 |
    // We assign the original depth of block pixel to layers,
    // To speed up depth sampling, we use GatherRed in the center of each 4 adjacent pixels
    // Gather red has values in ccw order x: lower left, y: lower right, z: upper right, w: upper left
    // Note that coordinate origin goes from upper left (0,0) to lower right (1,1)
    // BaseNormalizedScreenUV calculated above is the uv in the center of L0, L1, L4, L5
    float4 HardwareDepthsOne   = _DepthTextureOne.GatherRed(sampler_PointClamp, BaseNormalizedBlockUV); // in order L4, L5, L1, L0
    float4 HardwareDepthsTwo   = _DepthTextureOne.GatherRed(sampler_PointClamp, BaseNormalizedBlockUV + float2(_FullResDimensions.z * 2, 0.0f)); // in order L6, L7, L3, L2
    float4 HardwareDepthsThree = _DepthTextureOne.GatherRed(sampler_PointClamp, BaseNormalizedBlockUV + float2(0.0f, _FullResDimensions.w * 2)); // in order L12, L13, L9, L8
    float4 HardwareDepthsFour  = _DepthTextureOne.GatherRed(sampler_PointClamp, BaseNormalizedBlockUV + _FullResDimensions.zw * 2); // in order L14, L15, L11, L10
    
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 0)]  = LinearEyeDepth(HardwareDepthsOne.w,   _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 1)]  = LinearEyeDepth(HardwareDepthsOne.z,   _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 2)]  = LinearEyeDepth(HardwareDepthsTwo.w,   _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 3)]  = LinearEyeDepth(HardwareDepthsTwo.z,   _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 4)]  = LinearEyeDepth(HardwareDepthsOne.x,   _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 5)]  = LinearEyeDepth(HardwareDepthsOne.y,   _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 6)]  = LinearEyeDepth(HardwareDepthsTwo.x,   _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 7)]  = LinearEyeDepth(HardwareDepthsTwo.y,   _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 8)]  = LinearEyeDepth(HardwareDepthsThree.w, _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 9)]  = LinearEyeDepth(HardwareDepthsThree.z, _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 10)] = LinearEyeDepth(HardwareDepthsFour.w,  _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 11)] = LinearEyeDepth(HardwareDepthsFour.z,  _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 12)] = LinearEyeDepth(HardwareDepthsThree.x, _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 13)] = LinearEyeDepth(HardwareDepthsThree.y, _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 14)] = LinearEyeDepth(HardwareDepthsFour.x,  _ZBufferParams);
    _DeinterleavedDepthTextureRW[uint3(GlobalThreadIndex.xy, 15)] = LinearEyeDepth(HardwareDepthsFour.y,  _ZBufferParams);   
}

#endif
