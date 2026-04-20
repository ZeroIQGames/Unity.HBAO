#ifndef HBAO_PLUS_DEPTH_LINEARIZATION
#define HBAO_PLUS_DEPTH_LINEARIZATION


#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusTransformationUtil.hlsl"

struct FragOutput
{
    /**
     * @brief Depth texture output with 2 elements when we have two input depth textures. The first element is minimum depth while the second element is max depth.
     */
    float LinearizedDepth : SV_Target0;
};

/**
 * @brief Fragment function for depth linearization. Takes one or two depth textures and linearize the result of both
 * @param Input 
 * @param Out 
 */
void DepthLinearizationFrag(Varyings Input, out FragOutput Out)
{
    // Convert to linearized depth and fill output data
    Out.LinearizedDepth = GetLinearEyeDepth(TEXTURE2D_ARGS(_DepthTextureOne, sampler_PointClamp), Input.texcoord);
}

#endif