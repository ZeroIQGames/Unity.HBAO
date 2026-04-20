Shader "Hidden/HABOPlus"
{
    HLSLINCLUDE
        //#pragma enable_d3d11_debug_symbols
    ENDHLSL

    Properties
    {
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
        }
        
        HLSLINCLUDE
            #define HIGH_DEFINITION_RENDER_PIPELINE
            #pragma target 4.5 _HBAO_PLUS_SUPPORTS_SHADER_TARGET_45
            #pragma require instancing : _HBAO_PLUS_SUPPORTS_INSTANCING
            #pragma require integers : _HBAO_PLUS_SUPPORTS_INTEGERS
            
            #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusPipelineInclude.hlsl"
        ENDHLSL
        
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off
            Name "Depth Linearization"

            HLSLPROGRAM
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusDepthLinearizationRaster.hlsl"
                
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_DOUBLE_DEPTH_TEXTURES
                
                #pragma vertex Vert
                #pragma fragment DepthLinearizationFrag
            ENDHLSL
        }

        // Depth deinterleave with SV_RenderTargetArrayIndex set in geometry shader
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off
            Name "Depth Deinterleave"

            HLSLPROGRAM
                #define HBAO_PLUS_SUPPORTS_GEOMETRY_STAGE
                
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusMultilayerBlit.hlsl"
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusDepthDeinterleaveRaster.hlsl"
                
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_DOUBLE_DEPTH_TEXTURES
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_SUPPORTS_SHADER_TARGET_45
                #pragma multi_compile_local _ _HBAO_PLUS_SUPPORTS_INSTANCING

                #pragma vertex MultilayerBlitVert
                #pragma geometry MultilayerBlitGeometry
                #pragma fragment DepthDeinterleaveFrag
            ENDHLSL
        }

        // Depth deinterleave with SV_RenderTargetArrayIndex set in vertex shader
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off
            Name "Depth Deinterleave"

            HLSLPROGRAM
                #define HBAO_PLUS_SUPPORTS_SET_ARRAY_INDEX_FROM_ANY_STAGE
                
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusMultilayerBlit.hlsl"
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusDepthDeinterleaveRaster.hlsl"
                
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_DOUBLE_DEPTH_TEXTURES
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_SUPPORTS_SHADER_TARGET_45
                #pragma multi_compile_local _ _HBAO_PLUS_SUPPORTS_INSTANCING
                
                #pragma vertex MultilayerBlitVert
                #pragma fragment DepthDeinterleaveFrag
            ENDHLSL
        }
        
        // Depth deinterleave without SV_RenderTargetArrayIndex semantic
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off
            Name "Depth Deinterleave"

            HLSLPROGRAM
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusMultilayerBlit.hlsl"
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusDepthDeinterleaveRaster.hlsl"
                
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_DOUBLE_DEPTH_TEXTURES
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_SUPPORTS_SHADER_TARGET_45
                
                #pragma vertex Vert
                #pragma fragment DepthDeinterleaveFrag
            ENDHLSL
        }
        
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off
            Name "Normal Reconstruction"

            HLSLPROGRAM
                #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusNormalReconstruction.hlsl"
                
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_DOUBLE_DEPTH_TEXTURES
                
                #pragma vertex Vert
                #pragma fragment NormalReconstructionFrag
            ENDHLSL
        }

        // Coarse AO with geometry
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off
            Name "Coarse AO"

            HLSLPROGRAM
                #define HBAO_PLUS_SUPPORTS_GEOMETRY_STAGE
                #define HBAO_PLUS_COARSE_AO_PASS
                
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusMultilayerBlit.hlsl"
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusCoarseAORaster.hlsl"
                
                #pragma multi_compile_local_fragment _HBAO_PLUS_EIGHT_STEPS _HBAO_PLUS_FOUR_STEPS
                #pragma multi_compile_local_fragment _HBAO_PLUS_EIGHT_DIRECTIONS _HBAO_PLUS_FOUR_DIRECTIONS
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_NORMALS_IN_WORLD_SPACE
                #pragma multi_compile_local _ _HBAO_PLUS_RASTER_ONLY_RENDER_PATH
                #pragma multi_compile_local _ _HBAO_PLUS_SUPPORTS_INSTANCING
                
                
                #pragma vertex MultilayerBlitVert
                #pragma geometry MultilayerBlitGeometry
                #pragma fragment CoarseAOFrag
            ENDHLSL
        }

        // Coarse AO with SV_RenderTargetArrayIndex set in vertex shader
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off
            Name "Coarse AO"

            HLSLPROGRAM
                #define HBAO_PLUS_SUPPORTS_SET_ARRAY_INDEX_FROM_ANY_STAGE
                #define HBAO_PLUS_COARSE_AO_PASS
                
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusMultilayerBlit.hlsl"
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusCoarseAORaster.hlsl"
                
                #pragma multi_compile_local_fragment _HBAO_PLUS_EIGHT_STEPS _HBAO_PLUS_FOUR_STEPS
                #pragma multi_compile_local_fragment _HBAO_PLUS_EIGHT_DIRECTIONS _HBAO_PLUS_FOUR_DIRECTIONS
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_NORMALS_IN_WORLD_SPACE
                #pragma multi_compile_local _ _HBAO_PLUS_RASTER_ONLY_RENDER_PATH
                #pragma multi_compile_local _ _HBAO_PLUS_SUPPORTS_INSTANCING
                
                #pragma vertex MultilayerBlitVert
                #pragma fragment CoarseAOFrag
            ENDHLSL
        }

        // Coarse AO with slice index set in the fragment shader and render attachment changed per pass.
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off
            Name "Coarse AO"

            HLSLPROGRAM
                
                #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusCoarseAORaster.hlsl"
                
                #pragma multi_compile_local_fragment _HBAO_PLUS_EIGHT_STEPS _HBAO_PLUS_FOUR_STEPS
                #pragma multi_compile_local_fragment _HBAO_PLUS_EIGHT_DIRECTIONS _HBAO_PLUS_FOUR_DIRECTIONS
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_NORMALS_IN_WORLD_SPACE
                
                #pragma vertex Vert
                #pragma fragment CoarseAOFrag
                
            ENDHLSL
        }

        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off
            Name "Reinterleave AO"

            HLSLPROGRAM
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusAOReinterleave.hlsl"
                
                #pragma multi_compile_local_fragment _HBAO_PLUS_EIGHT_STEPS _HBAO_PLUS_FOUR_STEPS
                #pragma multi_compile_local_fragment _HBAO_PLUS_EIGHT_DIRECTIONS _HBAO_PLUS_FOUR_DIRECTIONS
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_BLUR_ENABLED
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_SUPPORTS_INTEGERS
                #pragma multi_compile_local_fragment _HBAO_PLUS_BLUR_FILTER_BILATERAL _HBAO_PLUS_BLUR_FILTER_GAUSSIAN _HBAO_PLUS_BLUR_FILTER_KAWASE
                #pragma multi_compile_local_fragment _HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH _HBAO_PLUS_BLUR_SHARPNESS_FROM_NORMAL _HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH_NORMAL
                
                #pragma vertex Vert
                #pragma fragment ReinterleaveAO
            ENDHLSL
        }

        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off
            Name "Intermediate Blur"

            HLSLPROGRAM
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusBlurRaster.hlsl"
                
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_DEPTH_DEPENDENT_BLUR_SHARPNESS
                #pragma multi_compile_local_fragment _ _BILATERAL_FILTER_DEPTH_FROM_SLOPE
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_NORMALS_IN_WORLD_SPACE
                #pragma multi_compile_local_fragment _BLUR_KERNEL_RADIUS_1 _BLUR_KERNEL_RADIUS_2 _BLUR_KERNEL_RADIUS_3 _BLUR_KERNEL_RADIUS_4
                #pragma multi_compile_local_fragment _HBAO_PLUS_BLUR_FILTER_BILATERAL _HBAO_PLUS_BLUR_FILTER_GAUSSIAN _HBAO_PLUS_BLUR_FILTER_KAWASE
                #pragma multi_compile_local_fragment _HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH _HBAO_PLUS_BLUR_SHARPNESS_FROM_NORMAL _HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH_NORMAL
                
                #pragma vertex Vert
                #pragma fragment BlurIntermediateFrag
            ENDHLSL
        }

        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off
            Name "Final Blur"

            HLSLPROGRAM
                #include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusBlurRaster.hlsl"
                
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_DEPTH_DEPENDENT_BLUR_SHARPNESS
                #pragma multi_compile_local_fragment _ _BILATERAL_FILTER_DEPTH_FROM_SLOPE
                #pragma multi_compile_local_fragment _ _HBAO_PLUS_NORMALS_IN_WORLD_SPACE
                #pragma multi_compile_local_fragment _BLUR_KERNEL_RADIUS_1 _BLUR_KERNEL_RADIUS_2 _BLUR_KERNEL_RADIUS_3 _BLUR_KERNEL_RADIUS_4
                #pragma multi_compile_local_fragment _HBAO_PLUS_BLUR_FILTER_BILATERAL _HBAO_PLUS_BLUR_FILTER_GAUSSIAN _HBAO_PLUS_BLUR_FILTER_KAWASE
                #pragma multi_compile_local_fragment _HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH _HBAO_PLUS_BLUR_SHARPNESS_FROM_NORMAL _HBAO_PLUS_BLUR_SHARPNESS_FROM_DEPTH_NORMAL
                
                #pragma vertex Vert
                #pragma fragment BlurFinalFrag
            ENDHLSL
        }
    }
}