#ifndef HBAO_PLUS_NORMAL_RECONSTRUCTION_HLSL
#define HBAO_PLUS_NORMAL_RECONSTRUCTION_HLSL


#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusTransformationUtil.hlsl"

/**
 * @brief Reconstruct the screen space normal of a point based on its position in world space and neighboring pixels depth data
 * @param NormalizedScreenSpaceUV UV of the screen pixel in the [0,1] range
 * @param PosCenterWS Position of the screen pixel in world space
 * @return Screen space normal reconstructed from depth data
 */
float3 ReconstructNormalVS(float2 NormalizedScreenSpaceUV, float3 PosCenterWS)
{
    float3 PosRightWS  = ReconstructPositionVS(NormalizedScreenSpaceUV + float2(_FullResDimensions.z, 0));
    float3 PosLeftWS   = ReconstructPositionVS(NormalizedScreenSpaceUV + float2(-_FullResDimensions.z, 0));
    float3 PosTopWS    = ReconstructPositionVS(NormalizedScreenSpaceUV + float2(0, _FullResDimensions.w));
    float3 PosBottomWS = ReconstructPositionVS(NormalizedScreenSpaceUV + float2(0, -_FullResDimensions.w));
    
    return normalize(cross(MinDiff(PosCenterWS, PosTopWS, PosBottomWS), MinDiff(PosCenterWS, PosRightWS, PosLeftWS)));
}

struct FragOutput
{
    /**
     * @brief Depth texture output with 2 elements when we have two input depth textures. The first element is minimum depth while the second element is max depth.
     */
    half3 ReconstructedNormal : SV_Target0;
};

void NormalReconstructionFrag(Varyings Input, out FragOutput Output)
{
    float ViewDepth = SampleLinearizedDepth(Input.texcoord);
    
    // Far-plane/skybox so we return early
    if (TransformToLinearEyeDepth01(ViewDepth, _ProjectionParams) >= SKY_NORMALIZED_DEPTH_VALUE)
    {
        Output.ReconstructedNormal = HALF3_ZERO;
        
        return;
    }
    
    // Pixel density for VR/XR
    float2 PixelDensity;
#ifdef SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER
    UNITY_BRANCH if (_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
    {
        pixelDensity = RemapFoveatedRenderingDensity(RemapFoveatedRenderingNonUniformToLinear(uv));
    }
    else
#endif
    {
        PixelDensity = float2(1.0f, 1.0f);
    }
    
    // Position of the fragment in view space
    float3 PositionVS = ReconstructPositionVS(Input.texcoord, ViewDepth);
    
    // Reconstructed normal
    float3 ViewNormal = ReconstructNormalVS(Input.texcoord, ViewDepth, PositionVS, PixelDensity);

    // Final output value
    Output.ReconstructedNormal = ViewNormal;
}

#endif
