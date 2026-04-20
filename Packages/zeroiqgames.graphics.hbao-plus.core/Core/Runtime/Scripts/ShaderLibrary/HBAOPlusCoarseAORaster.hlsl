#ifndef HBAO_PLUS_COARSE_AO_RASTER_HLSL
#define HBAO_PLUS_COARSE_AO_RASTER_HLSL

#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusCommon.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusCoarseAO.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusMultilayerBlit.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusTransformationUtil.hlsl"


/**
 * @brief Set up the static \c SliceIndex variable to be used by the fragment shader
 * @param Input Input to the fragment shader
 */
void SetupSliceIndex(MultilayerVaryings Input)
{
#ifdef _RASTER_ONLY_RENDER_PATH
	DeinterleavedDepthSliceIndex = _InstanceIdToRenderTargetIndexId[GetSliceIndex(Input)];
#else
	DeinterleavedDepthSliceIndex = GetSliceIndex(Input);
#endif
}

/**
 * 
 * @return 
 */
float2 GetSliceUVOffset()
{
	return float2(DeinterleavedDepthSliceIndex % 4 + 0.5f, floor(DeinterleavedDepthSliceIndex / 4.0f) + 0.5f);
}

half CoarseAOFrag(MultilayerVaryings Input) : SV_Target
{
	SetupSliceIndex(Input);
	float2 FullResPositionCS              = floor(Input.PositionCS.xy) * 4.0 + GetSliceUVOffset();
	float2 FullResNormalizedScreenSpaceUV = FullResPositionCS * _FullResDimensions.zw;
	float3 PositionVS                     = FetchQuarterResPositionVS(FullResNormalizedScreenSpaceUV);
	if (TransformToLinearEyeDepth01(PositionVS.z, _ProjectionParams) > SKY_NORMALIZED_DEPTH_VALUE) 
	{
		// we are almost at the far plane / skybox and can assume no AO
		return 1.0;
	}
	
	// Get radius params
	AORadiusParams RadiusParams = GetRadiusParams(PositionVS.z);
	
	// Coverage is less than 1 screen space pixel so we early out
	UNITY_BRANCH
	if (RadiusParams.RadiusPixels < 1.0)
	{
		return 1.0;
	}
	
	half3 NormalVS = SampleNormalVS(FullResNormalizedScreenSpaceUV);
	float AOValue  = ComputeCoarseAO(FullResNormalizedScreenSpaceUV, PositionVS, NormalVS, RadiusParams);
	
	return saturate(1.0 - AOValue * 2.0);
}

#endif
