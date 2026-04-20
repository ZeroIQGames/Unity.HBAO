#ifndef HBAO_PLUS_HDRP_INCLUDE
#define HBAO_PLUS_HDRP_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#if defined(HIGH_DEFINITION_RENDER_PIPELINE) // HDRP only
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
#elif defined(UNIVERSAL_RENDER_PIPELINE) //URP only
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#else
#error Pipline not specified!
#endif

#endif
