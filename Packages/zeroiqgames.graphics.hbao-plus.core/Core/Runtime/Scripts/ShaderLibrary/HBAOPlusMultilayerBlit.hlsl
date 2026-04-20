#ifndef HBAO_PLUS_MULTILAYER_BLIT_HLSL
#define HBAO_PLUS_MULTILAYER_BLIT_HLSL

#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"

// Fragment shader supports render target array index if either geometry or vertex stage support it
#if (defined(HBAO_PLUS_SUPPORTS_SET_ARRAY_INDEX_FROM_ANY_STAGE) || defined(HBAO_PLUS_SUPPORTS_GEOMETRY_STAGE)) && defined(SHADER_STAGE_FRAGMENT)
    #define SUPPORTS_RENDER_TARGET_ARRAY_INDEX
// Vertex shader supports render target array index if any stage can set the layer index
#elif defined(HBAO_PLUS_SUPPORTS_SET_ARRAY_INDEX_FROM_ANY_STAGE) && defined(SHADER_STAGE_VERTEX)
    #define SUPPORTS_RENDER_TARGET_ARRAY_INDEX
// Geometry shader supports render target array index if geometry shader is supported
#elif defined(HBAO_PLUS_SUPPORTS_GEOMETRY_STAGE) && defined(SHADER_STAGE_GEOMETRY)
    #define SUPPORTS_RENDER_TARGET_ARRAY_INDEX
#endif

// If we are using raster-only path, we will be having 4 different depth textures, and each will have its own instanced draw call
// The coarse AO texture though will be 16 layers meaning we can't use instance id as layer index directly
// In this case we instead do one level of indirection using _InstanceIdToRenderTargetIndexId set using MPB to get the render target array index
#if defined(_HBAO_PLUS_RASTER_ONLY_RENDER_PATH) && defined(HBAO_PLUS_COARSE_AO_PASS) && defined(_HBAO_PLUS_SUPPORTS_INSTANCING)
    #define RENDER_TARGET_ARRAY_INDEX_FROM_CBUFFER
#endif

struct MultilayerAttributes
{
    uint VertexId : SV_VertexID;

#ifdef _HBAO_PLUS_SUPPORTS_INSTANCING // Instanced drawing
    uint InstanceId : SV_InstanceID;
#endif
};

struct MultilayerVaryings
{
    float4 PositionCS : SV_POSITION;
    float2 Texcoord   : TEXCOORD0;

#ifdef _HBAO_PLUS_SUPPORTS_INSTANCING  // Instanced drawing
    nointerpolation uint InstanceId : TEXCOORD1;
#endif

#ifdef SUPPORTS_RENDER_TARGET_ARRAY_INDEX // Supports setting render target array index
    uint LayerIndex : SV_RenderTargetArrayIndex;
#endif

    UNITY_VERTEX_OUTPUT_STEREO
};

/**
 * @Get Get the current slice being processed in a multilayer blit operation
 * @return Index of the slice currently being rendered
 */
uint GetSliceIndex(in MultilayerVaryings Input)
{
#ifdef SUPPORTS_RENDER_TARGET_ARRAY_INDEX
    return Input.LayerIndex;
#else
    return _SliceIndex;
#endif
}

/**
 * @brief Get the index of the render target texture array to render into
 * @param Input Input to the vertex stage
 * @return Index of the render target array to render into
 */
uint GetRenderTargetArrayIndexFromInstanceId(MultilayerAttributes Input)
{
#if defined(RENDER_TARGET_ARRAY_INDEX_FROM_CBUFFER)
    return _InstanceIdToRenderTargetIndexId[Input.InstanceId]; // Render index is set inside _DeinterleavedAOInstancedIndices
#elif defined(_HBAO_PLUS_SUPPORTS_INSTANCING)
    return Input.InstanceId; // Render index is the same as instance ID
#else
    return _SliceIndex;
#endif
}

/**
 * @brief Get the index of the render target texture array to render into
 * @param Input Input to the geometry stage
 * @return Index of the render target array to render into
 */
uint GetRenderTargetArrayIndexFromInstanceId(MultilayerVaryings Input)
{
#if defined(RENDER_TARGET_ARRAY_INDEX_FROM_CBUFFER)
    return _InstanceIdToRenderTargetIndexId[Input.InstanceId]; // Render index is set inside _DeinterleavedAOInstancedIndices
#elif defined(_HBAO_PLUS_SUPPORTS_INSTANCING)
    return Input.InstanceId; // Render index is the same as instance ID
#else
    return _SliceIndex;
#endif
}

/**
 * @brief Simple full-screen triangle vertex shader that binds the correct index of a texture 2d array to the full-screen triangle so the same render attachment can be used with a multi pass blit
 * @param Input Vertex attributes
 * @param Out Vertex output with the global slice index assigned to SV_RenderTargetArrayIndex
 */
void MultilayerBlitVert(MultilayerAttributes Input, out MultilayerVaryings Out)
{
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(Out);
    
    float4 Pos = GetFullScreenTriangleVertexPosition(Input.VertexId);
    float2 UV  = GetFullScreenTriangleTexCoord(Input.VertexId);

    Out.PositionCS = Pos;
    Out.Texcoord   = DYNAMIC_SCALING_APPLY_SCALEBIAS(UV);
    
#ifdef _HBAO_PLUS_SUPPORTS_INSTANCING
    Out.InstanceId = Input.InstanceId;
#endif
    
#ifdef SUPPORTS_RENDER_TARGET_ARRAY_INDEX
    Out.LayerIndex = GetRenderTargetArrayIndexFromInstanceId(Input);
#endif
    
}

/**
 * @brief Simple geometry shader that binds the correct index of a texture 2d array to the full-screen triangle so the same render attachment can be used with a multi pass blit
 * @param Input Full-screen triangle of the pass
 * @param Out The same triangle with the global slice index assigned to SV_RenderTargetArrayIndex
 */
[maxvertexcount(3)]
void MultilayerBlitGeometry(triangle MultilayerVaryings Input[3], inout TriangleStream<MultilayerVaryings> Out)
{
    MultilayerVaryings OutVertex;
#ifdef _HBAO_PLUS_SUPPORTS_INSTANCING
    OutVertex.InstanceId = Input[0].InstanceId;
#endif

#ifdef SUPPORTS_RENDER_TARGET_ARRAY_INDEX
    OutVertex.LayerIndex = GetRenderTargetArrayIndexFromInstanceId(OutVertex);
#endif

    [unroll]
    for (int VertexID = 0; VertexID < 3; VertexID++)
    {
        OutVertex.PositionCS = Input[VertexID].PositionCS;
        OutVertex.Texcoord   = Input[VertexID].Texcoord;
        // Transfer the stereo data to the new output vertex
        UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(Input[VertexID], OutVertex);
        
        Out.Append(OutVertex);
    }
}

#endif
