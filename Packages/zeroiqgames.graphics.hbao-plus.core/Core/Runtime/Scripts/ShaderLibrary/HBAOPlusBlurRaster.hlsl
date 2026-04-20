#ifndef HBAO_PLUS_BLUR_RASTER_HLSL
#define HBAO_PLUS_BLUR_RASTER_HLSL

#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusKawaseFilter.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusGaussianFilter.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusBilateralFilterRaster.hlsl"





/**
 * @brief Fragment function for the horizontal blur pass
 * @param Input Output from the full-screen triangle vertex shader
 * @return Horizontally blurred AO (x component) and pixel linearized depth (y component)
 */
half4 BlurIntermediateFrag(Varyings Input) : SV_Target
{
#if defined(_HBAO_PLUS_BLUR_FILTER_BILATERAL)
    return CalculateBilateralBlurredAOHorizontal(Input.texcoord);
#elif defined(_HBAO_PLUS_BLUR_FILTER_GAUSSIAN)
    return CalculateBilateralBlurredAOHorizontal(Input.texcoord);
#elif defined(_HBAO_PLUS_BLUR_FILTER_KAWASE)
    return CalculateBilateralBlurredAOHorizontal(Input.texcoord);
#endif
}

/**
 * @brief Fragment function for the vertical blur pass
 * @param Input Output from the full-screen triangle vertex shader
 * @return Vertically blurred AO (x component) and pixel linearized depth (y component)
 * @remarks This also applied \c _IntensityExponent and should be run after the horizontal blur
 */
half BlurFinalFrag(Varyings Input) : SV_Target
{
    half RawAO;
#if defined(_HBAO_PLUS_BLUR_FILTER_BILATERAL)
    RawAO = CalculateBilateralBlurredAOVertical(Input.texcoord).x;
#elif defined(_HBAO_PLUS_BLUR_FILTER_GAUSSIAN)
    RawAO = CalculateBilateralBlurredAOVertical(Input.texcoord).x;
#elif defined(_HBAO_PLUS_BLUR_FILTER_KAWASE)
    RawAO = CalculateBilateralBlurredAOVertical(Input.texcoord).x;
#endif
    // AO value intensity exponent is not applied pre-blur, so we apply it here after the final blur pass
    return pow(saturate(RawAO), _IntensityExponent);
}

#endif
