#ifndef HBAO_PLUS_AO_REINTERLEAVE_HLSL
#define HBAO_PLUS_AO_REINTERLEAVE_HLSL


#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusTransformationUtil.hlsl"

#if defined(_HBAO_PLUS_BLUR_ENABLED) && defined(_HBAO_PLUS_BLUR_FILTER_BILATERAL)
#define NEEDS_EXTRA_DATA_FOR_BLUR
#endif


struct FragOutput
{
#if !defined(NEEDS_EXTRA_DATA_FOR_BLUR)
	/**
	 * @brief AO value for the fragment
	 * @remarks Will be raw value if blue is disabled and final value if blur is enabled
	 */
	half AO : SV_Target;
#elif _HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH
	/**
	 * @brief Raw AO (x) and depth (y) value for the fragment
	 */
	half2 AODepth : SV_Target;
#elif defined(_HBAO_PLUS_BLUR_SHARPNESS_FROM_NORMAL)
	/**
	 * @brief Raw AO (x) and packed normal (yzw) value for the fragment
	 */
	half4 AONormal : SV_Target;
#else // _HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH_NORMAL
	/**
	 * @brief Raw AO (x) and depth (y) and packed encoded normal (zw) value for the fragment
	 */
	half4 AODepthNormal : SV_Target;
#endif
	
};

FragOutput ReinterleaveAO(Varyings Input)
{
	FragOutput Out;
	
#if _HBAO_PLUS_SUPPORTS_INTEGERS
	int2   FullResPos    = int2(Input.positionCS.xy);
	int2   Offset        = FullResPos & 3;
	int    SliceId       = Offset.y * 4 + Offset.x;
	int2   QuarterResPos = FullResPos >> 2;
#else
	float2 FullResPos    = floor(Input.positionCS.xy);
	float2 Offset        = fmod(abs(FullResPos), float2(4,4));
	float  SliceId       = Offset.y * 4.0 + Offset.x;
	float2 QuarterResPos = FullResPos / 4.0;
#endif
	
	// Sample AO value from the deinterleaved texture array
	float SampledAO = LOAD_TEXTURE2D_ARRAY_LOD(_DeinterleavedAOTexture, QuarterResPos, SliceId, 0).r;
#if !defined(_HBAO_PLUS_BLUR_ENABLED)
	Out.AO = half(pow(saturate(SampledAO), _IntensityExponent)); // Apply HBAO power here
#elif !defined(_HBAO_PLUS_BLUR_FILTER_BILATERAL)
	Out.AO = SampledAO; // Apply power after blur. Gaussian or Kawase blur
#elif defined(_HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH)
	Out.AODepth = half2(saturate(SampledAO), TransformToLinearEyeDepth01(SampleLinearizedDepth(Input.texcoord), _ProjectionParams)); // Apply power after blur. y is normalized view depth
#elif defined(_HBAO_PLUS_BLUR_SHARPNESS_FROM_NORMAL) 
	Out.AONormal = half4(saturate(SampledAO), PackVector01(SampleNormalRaw(Input.texcoord))); // Apply power after blur. yzw is 01 packed normal
#elif defined (_HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH_NORMAL)
	// Apply power after blur. y is normalized view depth, zw is 01 packed and octahedral encoded normal
	Out.AODepthNormal = half4(saturate(SampledAO), TransformToLinearEyeDepth01(SampleLinearizedDepth(Input.texcoord), _ProjectionParams), PackAndEncodeNormal(SampleNormalRaw(Input.texcoord)));
#endif

	return Out;
}

#endif
