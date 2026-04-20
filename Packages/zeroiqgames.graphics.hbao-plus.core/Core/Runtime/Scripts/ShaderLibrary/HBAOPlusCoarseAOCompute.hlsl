#ifndef HBAO_PLUS_COARSE_AO_COMPUTE_HLSL
#define HBAO_PLUS_COARSE_AO_COMPUTE_HLSL

#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusCoarseAO.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusTransformationUtil.hlsl"

/**
 * @brief Set up the static \c SliceIndex variable to be used by the fragment shader
 * @param GlobalThreadIndex Global thread index of the compute thread
 */
void SetupSliceIndex(uint3 GlobalThreadIndex)
{
	DeinterleavedDepthSliceIndex = GlobalThreadIndex.z;
}

/**
 * 
 * @return 
 */
float2 GetSliceUVOffset()
{
	return float2(DeinterleavedDepthSliceIndex % 4 + 0.5f, floor(DeinterleavedDepthSliceIndex / 4.0f) + 0.5f);
}

// We use 8x8 thread groups (64 threads per group)
// Thread group count should be (QuarterRes/8, QuarterRes/8, 16)
// Each thread group calculates an 8x8 block on a single layer
[numthreads(8, 8, 1)]
void DrawCoarseAO(uint3 GlobalThreadID : SV_DispatchThreadID)
{
	if (any(GlobalThreadID.xy >= (uint2)_QuarterResDimensions.xy))
	{
		// Quarter res not divisible by 8, and we are out of bounds so we early out
		return;
	}
	
	SetupSliceIndex(GlobalThreadID);
	float2 FullResPositionCS              = floor(GlobalThreadID.xy) * 4.0 + GetSliceUVOffset();
	float2 FullResNormalizedScreenSpaceUV = FullResPositionCS * _FullResDimensions.zw;
	float3 PositionVS                     = FetchQuarterResPositionVS(FullResNormalizedScreenSpaceUV);
	
	if (TransformToLinearEyeDepth01(PositionVS.z, _ProjectionParams) > SKY_NORMALIZED_DEPTH_VALUE) 
	{
		// we are almost at the far plane / skybox and can assume no AO
		_DeinterleavedAOTextureRW[GlobalThreadID] = 1.0;
		
		return;
	}
	
	// Get radius params
	AORadiusParams RadiusParams = GetRadiusParams(PositionVS.z);
	
	UNITY_BRANCH
	if (RadiusParams.RadiusPixels < 1.0)
	{
		// Coverage is less than 1 screen space pixel so we early out
		_DeinterleavedAOTextureRW[GlobalThreadID] = 1.0;
		
		return;
	}
	
	half3 NormalVS = SampleNormalVS(FullResNormalizedScreenSpaceUV);
	float AOValue  = ComputeCoarseAO(FullResNormalizedScreenSpaceUV, PositionVS, NormalVS, RadiusParams);
	
	_DeinterleavedAOTextureRW[GlobalThreadID] = saturate(1.0 - AOValue * 2.0);
}

#endif
