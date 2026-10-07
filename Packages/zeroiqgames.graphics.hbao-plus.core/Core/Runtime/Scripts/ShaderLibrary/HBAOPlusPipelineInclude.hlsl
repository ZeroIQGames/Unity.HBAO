#ifndef HBAO_PLUS_PIPELINE_INCLUDE
#define HBAO_PLUS_PIPELINE_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#if defined(UNIVERSAL_RENDER_PIPELINE) //URP only
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#else
#error Pipeline not specified!
#endif

#endif
