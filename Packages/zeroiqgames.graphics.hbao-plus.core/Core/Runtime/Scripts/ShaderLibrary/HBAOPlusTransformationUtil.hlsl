#ifndef HBAO_PLUS_TRANSFORMATION_UTIL_HLSL
#define HBAO_PLUS_TRANSFORMATION_UTIL_HLSL

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusCommon.hlsl"

/**
 * @brief Convert the input hardware depth into view space depth
 * @param HardwareDepth Depth as used in a depth texture in [0, 1] range.
 * @return \c HardwareDepth linearized to view space depth.
 * @remarks Works with both orthographic and perspective cameras
 */
float GetLinearEyeDepth(float HardwareDepth)
{
#ifdef _ORTHOGRAPHIC
    return LinearDepthToEyeDepth(HardwareDepth);
#else
    return LinearEyeDepth(HardwareDepth, _ZBufferParams);
#endif
}

/**
 * @brief Convert the input hardware depth into view space depth
 * @param DepthTexture Texture that holds hardware depth data
 * @param SamplerInstance Sampler to use for depth texture sampling
 * @param NormalizedScreenSpaceUV Depth as used in a depth texture in [0, 1] range.
 * @return Sampled hardwareDepth linearized to view space depth.
 * @remarks Works with both orthographic and perspective cameras
 */
float GetLinearEyeDepth(TEXTURE2D_PARAM(DepthTexture, SamplerInstance), float2 NormalizedScreenSpaceUV)
{
    float HardwareDepth = SAMPLE_DEPTH_TEXTURE(DepthTexture, SamplerInstance, NormalizedScreenSpaceUV);
    
    return GetLinearEyeDepth(HardwareDepth);
}

float SampleLinearizedDepth(float2 NormalizedScreenSpaceUV)
{
    return SAMPLE_TEXTURE2D(_LinearizedDepth, sampler_PointClamp, NormalizedScreenSpaceUV).r;
}

/**
 * @brief Convert the given
 * @param NormalizedScreenSpaceUV 
 * @param ViewDepth 
 * @return 
 */
float3 UVToView(float2 NormalizedScreenSpaceUV, float ViewDepth)
{
    // UNITY LH CONVENTION (Positive Z is forward)
    // viewDepth = linear View-Space Z distance

    // Step 1: Calculate the Ray Direction Slope.
    // Replace the expensive Matrix Mul with a simple Scale & Bias vector operation.
    // Math maps UV [0..1] to required tangent slopes [-tan, +tan].
    float2 RayDirSlope = NormalizedScreenSpaceUV * _UVToViewParams.xy + _UVToViewParams.zw;

    // Step 2: Form the View-Space Position vector
    // Standard perspective math: X/Y are proportional to Z distance
    // x = tangent slope * Z
    // y = tangent slope * Z
    return float3(RayDirSlope * ViewDepth, ViewDepth);
}

/**
 * @brief Get the world space position of the screen space pixel with the specified UV
 * @param NormalizedScreenSpaceUV Normalized UV of the pixel on the screen
 * @param ViewDepth Linearized depth value in view space coordinates
 * @return World position of the pixel reconstructed from depth info
 */
float3 ReconstructPositionVS(float2 NormalizedScreenSpaceUV, float ViewDepth)
{
    return UVToView(NormalizedScreenSpaceUV, ViewDepth);
}

/**
 * @brief Get the world space position of the screen space pixel with the specified UV
 * @param NormalizedScreenSpaceUV Normalized UV of the pixel on the screen
 * @return World position of the pixel reconstructed from depth info
 */
float3 ReconstructPositionVS(float2 NormalizedScreenSpaceUV)
{
    float ViewDepth = SampleLinearizedDepth(NormalizedScreenSpaceUV);
    
    return ReconstructPositionVS(NormalizedScreenSpaceUV, ViewDepth);
}

/**
 * @brief Find the difference vector with the smaller size between a center position and two side positions
 * @param PosCenter Center position two calculate the difference from
 * @param SidePos First side position (Either top/bottom/right/left)
 * @param OppositePos Second side position on the opposite side of \p SidePos. I.e., if SidePos is to the right of the center, position to the left of the center
 * @return Difference vector with smaller size
 */
float3 MinDiff(float3 PosCenter, float3 SidePos, float3 OppositePos)
{
    float3 V1 = SidePos - PosCenter;
    float3 V2 = PosCenter - OppositePos;
    
    return (dot(V1,V1) < dot(V2,V2)) ? V1 : V2;
}

// Try reconstructing normal accurately from depth buffer.
// Low:    DDX/DDY on the current pixel
// Medium: 3 taps on each direction | x | * | y |
// High:   5 taps on each direction: | z | x | * | y | w |
// https://atyuwen.github.io/posts/normal-reconstruction/
// https://wickedengine.net/2019/09/22/improved-normal-reconstruction-from-depth/
/**
 * @brief Reconstruct the view space normal at the specified screen space UV using depth data
 * @param NormalizedScreenSpaceUV 
 * @param LinearizedDepth 
 * @param PositionVS 
 * @param PixelDensity 
 * @return 
 */
float3 ReconstructNormalVS(float2 NormalizedScreenSpaceUV, float LinearizedDepth, float3 PositionVS, float2 PixelDensity)
{
#ifdef _SOURCE_DEPTH_LOW
    return half3(normalize(cross(ddy(PositionVS), ddx(PositionVS))));
#else
    float2 Delta = float2(_LinearizedDepth_TexelSize.xy * 2.0) * rcp(PixelDensity);

    // Sample the neighbor fragments
    float2 LeftDelta  = float2(-Delta.x, 0.0);
    float2 RightDelta = float2(Delta.x, 0.0);
    float2 UpDelta    = float2(0.0, Delta.y);
    float2 DownDelta  = float2(0.0, -Delta.y);

    float3 L1 = float3(NormalizedScreenSpaceUV + LeftDelta, 0.0);  L1.z = SampleLinearizedDepth(L1.xy); // Left1
    float3 R1 = float3(NormalizedScreenSpaceUV + RightDelta, 0.0); R1.z = SampleLinearizedDepth(R1.xy); // Right1
    float3 U1 = float3(NormalizedScreenSpaceUV + UpDelta, 0.0);    U1.z = SampleLinearizedDepth(U1.xy); // Up1
    float3 D1 = float3(NormalizedScreenSpaceUV + DownDelta, 0.0);  D1.z = SampleLinearizedDepth(D1.xy); // Down1

    // Determine the closest horizontal and vertical pixels...
    // Horizontal: left = 0.0 right = 1.0
    // Vertical  : down = 0.0 up = 1.0
#ifdef _SOURCE_DEPTH_MEDIUM
     uint ClosestHorizontal = L1.z > R1.z ? 0 : 1;
     uint ClosestVertical   = D1.z > U1.z ? 0 : 1;
#else
    float3 L2 = float3(NormalizedScreenSpaceUV + LeftDelta * 2.0, 0.0);  L2.z = SampleLinearizedDepth(L2.xy); // Left2
    float3 R2 = float3(NormalizedScreenSpaceUV + RightDelta * 2.0, 0.0); R2.z = SampleLinearizedDepth(R2.xy); // Right2
    float3 U2 = float3(NormalizedScreenSpaceUV + UpDelta * 2.0, 0.0);    U2.z = SampleLinearizedDepth(U2.xy); // Up2
    float3 D2 = float3(NormalizedScreenSpaceUV + DownDelta * 2.0, 0.0);  D2.z = SampleLinearizedDepth(D2.xy); // Down2

    const uint ClosestHorizontal = abs( (2.0 * L1.z - L2.z) - LinearizedDepth) < abs( (2.0 * R1.z - R2.z) - LinearizedDepth) ? 0 : 1;
    const uint ClosestVertical   = abs( (2.0 * D1.z - D2.z) - LinearizedDepth) < abs( (2.0 * U1.z - U2.z) - LinearizedDepth) ? 0 : 1;
#endif

    // Calculate the triangle, in a counter-clockwise order, to
    // use based on the closest horizontal and vertical depths.
    // h == 0.0 && v == 0.0: p1 = left,  p2 = down
    // h == 1.0 && v == 0.0: p1 = down,  p2 = right
    // h == 1.0 && v == 1.0: p1 = right, p2 = up
    // h == 0.0 && v == 1.0: p1 = up,    p2 = left
    // Calculate the view space positions for the three points...
    float3 P1;
    float3 P2;
    if (ClosestVertical == 0)
    {
        P1 = ClosestHorizontal == 0 ? L1 : D1;
        P2 = ClosestHorizontal == 0 ? D1 : R1;
    }
    else
    {
        P1 = ClosestHorizontal == 0 ? U1 : R1;
        P2 = ClosestHorizontal == 0 ? L1 : U1;
    }

    // Use the cross-product to calculate the normal...
    return half3(normalize(cross(ReconstructPositionVS(P2.xy, P2.z) - PositionVS, ReconstructPositionVS(P1.xy, P1.z) - PositionVS)));
#endif
}

float3 ReconstructNormalVS(float2 NormalizedScreenSpaceUV, float2 PixelDensity)
{
    float ViewDepth = SampleLinearizedDepth(NormalizedScreenSpaceUV);
    
    return ReconstructNormalVS(NormalizedScreenSpaceUV, ViewDepth, ReconstructPositionVS(NormalizedScreenSpaceUV, ViewDepth), PixelDensity);
}

/**
 * @brief Sample the normal texture and return the value without any modification
 * @param NormalizedScreenSpaceUV Normalized UV on the screen to sample
 * @return xyz value of the normal texture without any modification
 */
half3 SampleNormalRaw(float2 NormalizedScreenSpaceUV)
{
    
    return SAMPLE_TEXTURE2D_LOD(_ViewSpaceNormalsTexture, sampler_PointClamp, NormalizedScreenSpaceUV, 0.0).xyz;
}

half3 SampleNormalVS(float2 NormalizedScreenSpaceUV)
{
    half3 SampledNormal = SampleNormalRaw(NormalizedScreenSpaceUV);
#ifdef _HBAO_PLUS_NORMALS_IN_WORLD_SPACE
    SampledNormal  = mul((float3x3)UNITY_MATRIX_V, SampledNormal);
    SampledNormal *= half3(1.0f, 1.0f, -1.0f); // We use Z+: forward, so we need to reverse the unity z sign. Unity uses Z-: forward. TODO: Refactor to use the same space as unity
#endif

    return SampledNormal;
}

/**
 * @brief Get the correct deinterleaved depth texture array slice index for the current AO slice index being rendered
 * @return Current depth slice index corresponding to the coarse AO slice index being rendered
 */
uint GetDepthTextureSliceIndex()
{
#ifdef _HBAO_PLUS_RASTER_ONLY_RENDER_PATH
    return (uint)_DeinterleavedDepthTextureIndices[DeinterleavedDepthSliceIndex];
#else
    return DeinterleavedDepthSliceIndex;
#endif
}

/**
 * @brief Reconstruct the view space position using \c _DeinterleavedDepthTexture 
 * @param NormalizedScreenSpaceUV Normalized UV of the fragment in the screen space
 * @param SliceIndex Index of the slice to sample. This should be the same as the index where AO is being calculated.
 * @return Reconstructed view space position of the fragment
 */
float3 FetchQuarterResPositionVS(float2 NormalizedScreenSpaceUV, uint SliceIndex)
{
    float ViewDepth = SAMPLE_TEXTURE2D_ARRAY_LOD(_DeinterleavedDepthTexture, sampler_PointClamp, NormalizedScreenSpaceUV, SliceIndex, 0).r;
    
    return ReconstructPositionVS(NormalizedScreenSpaceUV, ViewDepth);
}

/**
 * @brief Reconstruct the view space position using \c _DeinterleavedDepthTexture 
 * @param NormalizedScreenSpaceUV Normalized UV of the fragment in the screen space
 * @return Reconstructed view space position of the fragment
 * @remarks Uses \c GetDepthTextureSliceIndex() to get the depth texture slice index. The correct slice index of the texture should be set on the C# side.
 */
float3 FetchQuarterResPositionVS(float2 NormalizedScreenSpaceUV)
{
    return FetchQuarterResPositionVS(NormalizedScreenSpaceUV, GetDepthTextureSliceIndex());
}

/**
 * 
 * @param BaseVector Vector to rotate
 * @param RotationVector Rotation unit vector that is incidentally in the form of (cos(alpha),sin(alpha)) where alpha is the rotation angle. Note that Sine2 + Cosine2 = 1.
 * @return \p Vector rotated by the angle of \p RotationCosSin
 */
float2 RotateDirection(float2 BaseVector, float2 RotationVector)
{
    // RotationCosSin is (cos(alpha),sin(alpha)) where alpha is the rotation angle
    // A 2D rotation matrix is applied (see https://en.wikipedia.org/wiki/Rotation_matrix)
    return float2(BaseVector.x * RotationVector.x - BaseVector.y * RotationVector.y, BaseVector.x * RotationVector.y + BaseVector.y * RotationVector.x);
}

/**
 * @brief Convert absolute linearized eye depth to normalized linearized eye depth in range [0, 1]
 * @param AbsoluteLinearEyeDepth Absolute linear eye depth
 * @param ProjectionParams Projection params for the camera. Should be passed automatically by unity to \c _ProjectionParams
 * @return Linear eye depth normalized to [0, 1] range.
 */
float TransformToLinearEyeDepth01(float AbsoluteLinearEyeDepth, float4 ProjectionParams)
{
    return AbsoluteLinearEyeDepth * ProjectionParams.w;
}

/**
 * @brief Convert normalized linearized eye depth in range [0, 1] to absolute linearized eye depth 
 * @param NormalizedLinearEyeDepth Normalized linear eye depth
 * @param ProjectionParams Projection params for the camera. Should be passed automatically by unity to \c _ProjectionParams
 * @return absolute linearized eye depth between far plane and near plane
 */
float TransformToLinearEyeDepth(float NormalizedLinearEyeDepth, float4 ProjectionParams)
{
    return NormalizedLinearEyeDepth * ProjectionParams.z;
}

//----------------------------------------------------------------------------------
void AddViewportOrigin(inout float2 PositionCS, inout float2 TexCoord)
{
    PositionCS += VIEWPORT_TOP_LEFT;
    TexCoord    = PositionCS * _FullResDimensions.zw;
}

//----------------------------------------------------------------------------------
void SubtractViewportOrigin(inout float2 PositionCS, inout float2 TexCoord)
{
    PositionCS -= VIEWPORT_TOP_LEFT;
    TexCoord    = TexCoord * _FullResDimensions.zw;
}

/**
 * Pack a unit vector from [-1, +1] range to [0, 1] range.
 * @param UnitVector Original unit vector
 * @return Packed vector that is not necessarily a unit vector
 */
half3 PackVector01(half3 UnitVector)
{
    return (UnitVector + HALF3_ONE) * half(0.5);
}

/**
 * Pack a unit vector from [-1, +1] range to [0, 1] range.
 * @param UnitVector Original unit vector
 * @return Packed vector that is not necessarily a unit vector
 */
float3 PackVector01(float3 UnitVector)
{
    return (UnitVector + 1.0f) * 0.5f;
}

/**
 * Unpack a vector from [0, 1] range to [-1, +1] range.
 * @param UnitVector Unit vector packed by \c PackVector01()
 * @return Unpacked unit vector
 */
half3 UnpackVector01(half3 UnitVector)
{
    return UnitVector * half(2.0) - HALF3_ONE;
}

/**
 * Unpack a vector from [0, 1] range to [-1, +1] range.
 * @param UnitVector Unit vector packed by \c PackVector01()
 * @return Unpacked unit vector
 */
float3 UnpackVector01(float3 UnitVector)
{
    return UnitVector * 2.0f - 1.0f;
}

/**
 * Pack a unit vector from [-1, +1] range to [0, 1] range.
 * @param UnitVector Original unit vector
 * @return Packed vector that is not necessarily a unit vector
 */
half2 PackVector01(half2 UnitVector)
{
    return (UnitVector + HALF2_ONE) * half(0.5);
}

/**
 * Pack a unit vector from [-1, +1] range to [0, 1] range.
 * @param UnitVector Original unit vector
 * @return Packed vector that is not necessarily a unit vector
 */
float2 PackVector01(float2 UnitVector)
{
    return (UnitVector + 1.0f) * 0.5f;
}

/**
 * Unpack a normal vector from [0, 1] range to [-1, +1] range.
 * @param PackedVector Unit vector packed by \c PackVector01()
 * @return Unpacked unit vector
 */
half2 UnpackVector01(half2 PackedVector)
{
    return PackedVector * half(2.0) - HALF2_ONE;
}

/**
 * Unpack a normal vector from [0, 1] range to [-1, +1] range.
 * @param PackedVector Unit vector packed by \c PackVector01()
 * @return Unpacked unit vector
 */
float2 UnpackVector01(float2 PackedVector)
{
    return PackedVector * 2.0f - 1.0f;
}

/**
 * Pack and encode a unit normal vector from [-1, +1] range to [0, 1] range using octahedral quad encoding.
 * @param Normal Original unit normal vector
 * @return Packed and encoded normal vector
 */
half2 PackAndEncodeNormal(half3 Normal)
{
    return PackVector01(PackNormalOctQuadEncode(Normal));
}

/**
 * Pack and encode a unit normal vector from [-1, +1] range to [0, 1] range using octahedral quad encoding.
 * @param Normal Original unit normal vector
 * @return Packed and encoded normal vector
 */
float2 PackAndEncodeNormal(float3 Normal)
{
    return PackVector01(PackNormalOctQuadEncode(Normal));
}

/**
 * Unpack and decode a normal vector from [0, 1] range to [-1, +1] range.
 * @param PackedEncodedNormal Normal vector packed and encoded by \c PackAndEncodeNormal()
 * @return Unpacked unit normal vector
 */
half3 UnpackAndDecodeNormal(half2 PackedEncodedNormal)
{
    return UnpackNormalOctQuadEncode(UnpackVector01(PackedEncodedNormal));
}

/**
 * Unpack and decode a normal vector from [0, 1] range to [-1, +1] range.
 * @param PackedEncodedNormal Normal vector packed and encoded by \c PackAndEncodeNormal()
 * @return Unpacked unit normal vector
 */
float3 UnpackAndDecodeNormal(float2 PackedEncodedNormal)
{
    return UnpackNormalOctQuadEncode(UnpackVector01(PackedEncodedNormal));
}

#endif
