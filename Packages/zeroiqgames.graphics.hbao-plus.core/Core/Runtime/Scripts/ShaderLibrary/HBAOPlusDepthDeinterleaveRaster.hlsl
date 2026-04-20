#ifndef HBAO_PLUS_DEPTH_DEINTERLEAVE_RASTER_HLSL
#define HBAO_PLUS_DEPTH_DEINTERLEAVE_RASTER_HLSL

#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusMultilayerBlit.hlsl"

struct FragOutput
{
    /**
     * @brief 4 layer deinterleaved texture array of linearized depth. First texture of four.
     * @details Holds quarter res slices 00, 02, 08, 10 in slices 0,1,2,3 in order, according to mapping below: <br />
     * <c> 00 | 01 | 02 | 03 </c> <br />
     * <c> 04 | 05 | 06 | 07 </c> <br />
     * <c> 08 | 09 | 10 | 11 </c> <br />
     * <c> 12 | 13 | 14 | 15 </c>
     */
    float LinearZ01 : SV_Target0;
    
    /**
     * @brief 4 layer deinterleaved texture array of linearized depth. First texture of four.
     * @details Holds quarter res slices 01, 03, 09, 11 in slices 0,1,2,3 in order, according to mapping below: <br />
     * <c> 00 | 01 | 02 | 03 </c> <br />
     * <c> 04 | 05 | 06 | 07 </c> <br />
     * <c> 08 | 09 | 10 | 11 </c> <br />
     * <c> 12 | 13 | 14 | 15 </c>
     */
    float LinearZ02 : SV_Target1;
    
    /**
     * @brief 4 layer deinterleaved texture array of linearized depth. First texture of four.
     * @details Holds quarter res slices 04, 06, 12, 14 in slices 0,1,2,3 in order, according to mapping below: <br />
     * <c> 00 | 01 | 02 | 03 </c> <br />
     * <c> 04 | 05 | 06 | 07 </c> <br />
     * <c> 08 | 09 | 10 | 11 </c> <br />
     * <c> 12 | 13 | 14 | 15 </c>
     */
    float LinearZ03 : SV_Target2;
    
    /**
     * @brief 4 layer deinterleaved texture array of linearized depth. First texture of four.
     * @details Holds quarter res slices 05, 07, 13, 15 in slices 0,1,2,3 in order, according to mapping below: <br />
     * <c> 00 | 01 | 02 | 03 </c> <br />
     * <c> 04 | 05 | 06 | 07 </c> <br />
     * <c> 08 | 09 | 10 | 11 </c> <br />
     * <c> 12 | 13 | 14 | 15 </c>
     */
    float LinearZ04 : SV_Target3;
};

/**
 * @brief Fragment function for depth linearization. Takes one or two depth textures and linearize the result of both
 * @param Input 
 * @param Out 
 */
void DepthDeinterleaveFrag(MultilayerVaryings Input, out FragOutput Out)
{
    uint  SliceIndex                   = GetSliceIndex(Input);
    uint2 BlockFullResScreenPixelIndex = floor(Input.PositionCS.xy) * 4; // Index of the pixel in full screen where the first pixel is (0,0) and the last one is (width-1, height-1)
    uint2 SliceFullResPositionCS       = BlockFullResScreenPixelIndex + uint2(SliceIndex % 2, SliceIndex / 2) * 2; // Position of the lower left pixel of the slice in full res
    if (any(SliceFullResPositionCS.xy >= (uint2)_FullResDimensions.xy))
    {
        // Out of the screen
        Out.LinearZ01 = 0;
        Out.LinearZ02 = 0;
        Out.LinearZ03 = 0;
        Out.LinearZ04 = 0;
        
        return;
    }
    
#ifdef _HBAO_PLUS_SUPPORTS_SHADER_TARGET_45
    // Normalized UV of the center of our slice 4 pixels based on the mapping below
    // 00 | 01 | 02 | 03
    // 04 | 05 | 06 | 07
    // 08 | 09 | 10 | 11
    // 12 | 13 | 14 | 15
    // Slice index 0 is pixels 00, 01, 04, 05
    // Slice index 1 is pixels 02, 03, 06, 07
    // Slice index 2 is pixels 08, 09, 12, 13
    // Slice index 3 is pixels 10, 11, 14, 15
    float2 NormalizedBlockUV = (SliceFullResPositionCS + 1.0f) * _FullResDimensions.zw;
    
    // To speed up depth sampling, we use GatherRed in the center of each 4 adjacent pixels
    // Gather red has values in ccw order x: lower left, y: lower right, z: upper right, w: upper left
    // Note that coordinate origin goes from upper left (0,0) to lower right (1,1)
    // BaseNormalizedScreenUV calculated above is the uv in the center of L0, L1, L4, L5
    float4 HardwareDepths = GATHER_TEXTURE2D(_DepthTextureOne, sampler_PointClamp, NormalizedBlockUV);
#else
    // Get the 4 pixel centers for the current slice based on the mapping below
    // 00 | 01 | 02 | 03
    // 04 | 05 | 06 | 07
    // 08 | 09 | 10 | 11
    // 12 | 13 | 14 | 15
    // Slice index 0 is pixels 00, 01, 04, 05
    // Slice index 1 is pixels 02, 03, 06, 07
    // Slice index 2 is pixels 08, 09, 12, 13
    // Slice index 3 is pixels 10, 11, 14, 15
    float2 TopLeftCenterUV = (SliceFullResPositionCS + 0.5f) * _FullResDimensions.zw;
    float4 HardwareDepths  = float4(     // GatherRed requires SM 4.1, so in older shader models we just do 4 samples in the same clockwise order as Gather
        SAMPLE_TEXTURE2D(_DepthTextureOne, sampler_PointClamp, TopLeftCenterUV + float2(0, -_FullResDimensions.w)).x,                     // Bottom left
        SAMPLE_TEXTURE2D(_DepthTextureOne, sampler_PointClamp, TopLeftCenterUV + float2(+_FullResDimensions.z, -_FullResDimensions.w)).x, // Bottom right
        SAMPLE_TEXTURE2D(_DepthTextureOne, sampler_PointClamp, TopLeftCenterUV + float2(+_FullResDimensions.z, 0)).x,                     // Top right
        SAMPLE_TEXTURE2D(_DepthTextureOne, sampler_PointClamp, TopLeftCenterUV + float2(0, 0)).x                                          // Top left
    );
#endif
    
    Out.LinearZ01 = LinearEyeDepth(HardwareDepths.w, _ZBufferParams);
    Out.LinearZ02 = LinearEyeDepth(HardwareDepths.z, _ZBufferParams);
    Out.LinearZ03 = LinearEyeDepth(HardwareDepths.x, _ZBufferParams);
    Out.LinearZ04 = LinearEyeDepth(HardwareDepths.y, _ZBufferParams);
}



#endif
