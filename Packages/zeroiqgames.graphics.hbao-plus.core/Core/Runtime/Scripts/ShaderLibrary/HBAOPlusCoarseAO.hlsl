#ifndef HBAO_PLUS_COARSE_AO_HLSL
#define HBAO_PLUS_COARSE_AO_HLSL

#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusTransformationUtil.hlsl"

struct AORadiusParams
{
    float RadiusPixels;
    float NegativeInvR2;
};

void ScaleAORadius(inout AORadiusParams Params, float ScaleFactor)
{
    Params.RadiusPixels  *= ScaleFactor;
    Params.NegativeInvR2 *= 1.0 / (ScaleFactor * ScaleFactor);
}

AORadiusParams GetRadiusParams(float LinearDepthVS)
{
    AORadiusParams Params;
    Params.RadiusPixels  = _AOVerticalRadiusToScreenPixels / LinearDepthVS;
    Params.NegativeInvR2 = _AONegativeInvR2;

    UNITY_BRANCH
    if (_BackgroundAORadiusPixels != -1.f)
    {
        ScaleAORadius(Params, max(1.0, _BackgroundAORadiusPixels / Params.RadiusPixels));
    }

    UNITY_BRANCH
    if (_ForegroundAORadiusPixels != -1.f)
    {
        ScaleAORadius(Params, min(1.0, _ForegroundAORadiusPixels / Params.RadiusPixels));
    }

    return Params;
}

/**
 * 
 * @param DistanceSquare 
 * @param Params 
 * @return 
 */
float Falloff(float DistanceSquare, in AORadiusParams Params)
{
    // 1 scalar mad instruction
    return mad(DistanceSquare, Params.NegativeInvR2, 1.0);
}

/**
 * @brief Given the kernel position and normals of the kernel center and a nearby pixel, calculate the AO applied to the kernel due to the pixel
 * @param KernelPosVS Position of the kernel center in the view space
 * @param KernelNormalVS Normal of the kernel center in the view space
 * @param PixelPosVS Position of a nearby pixel in the view space
 * @param RadiusParams Radius parameters for the kernel center
 * @return AO value on the kernel center due to the specified pixel position
 */
float ComputeAO(float3 KernelPosVS, float3 KernelNormalVS, float3 PixelPosVS, in AORadiusParams RadiusParams)
{
    float3 PositionDiff      = PixelPosVS - KernelPosVS;
    float  PositionDifLenSqr = dot(PositionDiff, PositionDiff);
    float  NormalDotPosDiff  = dot(KernelNormalVS, PositionDiff) * rsqrt(PositionDifLenSqr);

    // Apparently saturate(x) is faster than max(x,0.f)
    return saturate(NormalDotPosDiff - _TessellationBias) * saturate(Falloff(PositionDifLenSqr, RadiusParams));
}

/**
 * 
 * @param AccumulatedAO 
 * @param RayPixels 
 * @param StepSizePixels 
 * @param Direction 
 * @param FullResUV Normalized screen space UV in full resolution
 * @param KernelPositionVS Position of the kernel center which we are calculating the AO for in view space
 * @param KernelNormalVS Normal of the kernel center in view space
 * @param RadiusParams Radius parameters calculated based on AO options using \c GetRadiusParams
 */
void AccumulateAO(
    inout float AccumulatedAO,
    inout float RayPixels,
    float StepSizePixels,
    float2 Direction,
    float2 FullResUV,
    float3 KernelPositionVS,
    float3 KernelNormalVS,
    AORadiusParams RadiusParams
)
{
    float2 PixelDiff  = round(RayPixels * Direction);
    float2 SnappedUV  = mad(_QuarterResDimensions.zw, PixelDiff, FullResUV);
    float3 PixelPosVS = FetchQuarterResPositionVS(SnappedUV);

    RayPixels += StepSizePixels;
    
    // For some reason the top of the screen gets a few pixels width non-zero AO even if physically there shouldn&t be any AO there
    // TODO: this should not be needed, but somehow it is -_- find the cause and fix
    UNITY_FLATTEN
    if (!any(SnappedUV > 1.0f || SnappedUV < 0.0f))
    {
        AccumulatedAO += ComputeAO(KernelPositionVS, KernelNormalVS, PixelPosVS, RadiusParams);
    }
}

/**
 * 
 * @param FullResUV 
 * @param ViewPosition 
 * @param ViewNormal 
 * @param Params 
 * @return 
 */
float ComputeCoarseAO(float2 FullResUV, float3 ViewPosition, float3 ViewNormal, AORadiusParams Params)
{
    // Divide by NUM_STEPS+1 so that the farthest samples are not fully attenuated
    float StepSizePixels = (Params.RadiusPixels / 4.0) / (HBAO_STEPS + 1);
    
    float SmallScaleAO = 0;
    float LargeScaleAO = 0;
    
    UNITY_UNROLL
    for (int DirectionIndex = 0; DirectionIndex < HBAO_DIRECTIONS; DirectionIndex++)
    {
        // Jitter starting sample within the first step
        float RayPixels = mad(_AOJitterArray[DeinterleavedDepthSliceIndex].z, StepSizePixels, 1.0);
        float Angle     = HBAO_RAY_ANGLE_DIFF * DirectionIndex;

        // Rotate the unit vector created from Angle by the jitter amount
        float2 Direction = RotateDirection(float2(cos(Angle), sin(Angle)), _AOJitterArray[DeinterleavedDepthSliceIndex].xy);
        
        // Small scale AO
        AccumulateAO(SmallScaleAO, RayPixels, StepSizePixels, Direction, FullResUV, ViewPosition, ViewNormal, Params);
        
        // Large scale AO
        UNITY_UNROLL
        for (int StepIndex = 1; StepIndex < HBAO_STEPS; StepIndex++)
        {
            AccumulateAO(LargeScaleAO, RayPixels, StepSizePixels, Direction, FullResUV, ViewPosition, ViewNormal, Params);
        }
    }

    // Total accumulated AO will go up as we increase steps. We divide by steps to get average. Higher steps should give higher accuracy
    float AccumulatedAO = (SmallScaleAO * _SmallScaleAOAmount) + (LargeScaleAO * _LargeScaleAOAmount);
    float AverageAO     = AccumulatedAO / (HBAO_DIRECTIONS * HBAO_STEPS);

    return AverageAO;
}

#endif


