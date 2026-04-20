using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.RenderGraphModule;
using ZeroIQGames.Graphics.HBAOPlus.Core.Renderer;

namespace ZeroIQGames.Graphics.HBAOPlus.HDRP.Renderer
{

    public partial class HBAOPlusCustomPass
    {
        /// <summary>
        /// Material property block used by the pass.
        /// </summary>
        private readonly MaterialPropertyBlock _PassPropertyBlock = new();

        /// <summary>
        /// Settings object for the pass.
        /// </summary>
        private HBAOPlusSettings _HBAOPlusSettings;
        
        /// <summary>
        /// Names of depth deinterleave passes when compute/geometry/render target array index are not available
        /// </summary>
        private static readonly string[] _DepthDeinterleaveMultiPassNames = {
            "Depth Deinterleave First Pass", 
            "Depth Deinterleave Second Pass",
            "Depth Deinterleave Third Pass", 
            "Depth Deinterleave Fourth Pass",
        };

        /// <summary>
        /// Profiler that wraps render pass execution logic.
        /// </summary>
        private readonly ProfilingSampler _HBAOPlusRenderPassesMarker = new("HBAO+");
        
        /// <summary>
        /// Profiler that wraps render-graph recording.
        /// </summary>
        private static readonly ProfilingSampler _RenderGraphRecordingMarker = new("HBAO+ Record Render Graph");

        /// <summary>
        /// Prepare resources object.
        /// </summary>
        /// <param name="PassContext">Rendering context of the pass</param>
        /// <param name="PassPropertyBlock">Property block used by the pass</param>
        /// <param name="Settings">HBAO Settings object for the effect</param>
        /// <param name="OutResources">Output resources object</param>
        private static void _PreparePassResources(CustomPassContext PassContext, MaterialPropertyBlock PassPropertyBlock, HBAOPlusSettings Settings, out HBAOPlusResources<RTHandle> OutResources)
        {
            // Texture descriptor of the camera depth texture
            RenderTextureDescriptor DepthTextureDesc = PassContext.cameraDepthBuffer.rt.descriptor;
            
            // Texture dimensions
            OutResources.FullResDimensions = new Vector4(
                DepthTextureDesc.width,
                DepthTextureDesc.height,
                1.0f / DepthTextureDesc.width,
                1.0f / DepthTextureDesc.height
            );
            OutResources.QuarterResDimensions = new Vector4(
                Mathf.CeilToInt((float)DepthTextureDesc.width / 4),
                Mathf.CeilToInt((float)DepthTextureDesc.height / 4),
                1.0f / Mathf.CeilToInt((float)DepthTextureDesc.width / 4),
                1.0f / Mathf.CeilToInt((float)DepthTextureDesc.height / 4)
            );
            
            // Camera parameters
            float CameraVerticalFOV      = PassContext.hdCamera.camera.fieldOfView;
            float CameraAspectRatio      = PassContext.hdCamera.camera.aspect;
            float HalfVerticalFOVRadians = CameraVerticalFOV * 0.5f * Mathf.Deg2Rad;
            
            // Parameters for conversion from screen space to view space
            OutResources.TanHalfVerticalFOV   = Mathf.Tan(HalfVerticalFOVRadians);
            OutResources.TanHalfHorizontalFOV = OutResources.TanHalfVerticalFOV * CameraAspectRatio;
            OutResources.UVToViewParams       = new Vector4(
                2.0f * OutResources.TanHalfHorizontalFOV,
                2.0f * OutResources.TanHalfVerticalFOV,
                -OutResources.TanHalfHorizontalFOV,
                -OutResources.TanHalfVerticalFOV
            );
            
            // Linearized depth texture descriptor
            var LinearizedDepthTexDesc = new RenderTextureDescriptor((int)OutResources.FullResDimensions.x, (int)OutResources.FullResDimensions.y)
            {
                graphicsFormat   = GraphicsFormat.R32_SFloat, // We write to the R channel only.
                sRGB             = false,  
                mipCount         = DepthTextureDesc.mipCount,
                dimension        = TextureDimension.Tex2D,
                vrUsage          = DepthTextureDesc.vrUsage,
                autoGenerateMips = false,
                useMipMap        = false,
                useDynamicScale  = DepthTextureDesc.useDynamicScale,
            };
            
            // Deinterleaved depth texture descriptor
            var DeinterleavedDepthTexDesc = new RenderTextureDescriptor((int)OutResources.QuarterResDimensions.x, (int)OutResources.QuarterResDimensions.y)
            {
                graphicsFormat    = GraphicsFormat.R32_SFloat, // We write to the R channel only. TODO: Check if we can use R16 when normal reconstruction is not needed
                dimension         = TextureDimension.Tex2DArray,
                volumeDepth       = Settings.AORenderPath is EAORenderPath.Compute ? 16 : 4, // One 16-slice array for the compute path, Four 4-slice arrays for the raster path
                enableRandomWrite = Settings.AORenderPath is EAORenderPath.Compute, // Required for compute shader write
                autoGenerateMips  = false,
                useMipMap         = false,
                useDynamicScale  = DepthTextureDesc.useDynamicScale,
            };

            // Texture used for normal reconstruction
            var ReconstructedNormalsTexDesc = new RenderTextureDescriptor((int)OutResources.FullResDimensions.x, (int)OutResources.FullResDimensions.y)
            {
                graphicsFormat    = GraphicsFormat.R8G8B8A8_SNorm,
                dimension         = TextureDimension.Tex2D,
                enableRandomWrite = false,
                autoGenerateMips  = false,
                useMipMap         = false,
                useDynamicScale   = DepthTextureDesc.useDynamicScale,
            };

            var DeinterleavedAOTexDesc = new RenderTextureDescriptor((int)OutResources.QuarterResDimensions.x, (int)OutResources.QuarterResDimensions.y)
            {
                graphicsFormat    = GraphicsFormat.R16_UNorm,
                dimension         = TextureDimension.Tex2DArray,
                volumeDepth       = 16,
                enableRandomWrite = Settings.AORenderPath is EAORenderPath.Compute, // Required for compute shader write
                autoGenerateMips  = false,
                useMipMap         = false,
                useDynamicScale   = DepthTextureDesc.useDynamicScale,
            };
            
            
            // Texture descriptor for the final AO texture
            var FinalAOTexDesc = new RenderTextureDescriptor((int)OutResources.FullResDimensions.x, (int)OutResources.FullResDimensions.y)
            {
                graphicsFormat    = SystemInfo.GetCompatibleFormat(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Sample | GraphicsFormatUsage.Render),
                enableRandomWrite = HBAOPlusUtility.IsBlurRenderPathCompute(Settings.BlurRenderPath),
                autoGenerateMips  = false,
                useMipMap         = false,
                useDynamicScale   = DepthTextureDesc.useDynamicScale,
            };
            
            // Allocate resources
            OutResources.HardwareDepthTexture   = PassContext.cameraDepthBuffer;
            OutResources.LinearizedDepthTexture = RTHandles.Alloc(LinearizedDepthTexDesc, wrapMode: TextureWrapMode.Clamp, name: "HBAO_Plus_Linearized_Depth_Texture" );
            OutResources.SceneNormalsTexture    = Settings.bUseDepthNormal && PassContext.cameraNormalBuffer?.rt != null ? PassContext.cameraNormalBuffer : RTHandles.Alloc(ReconstructedNormalsTexDesc, wrapMode: TextureWrapMode.Clamp, name: "HBAO_Plus_Reconstructed_Normals_Texture");
            OutResources.DeinterleavedAOTexture = RTHandles.Alloc(DeinterleavedAOTexDesc, wrapMode: TextureWrapMode.Clamp, name: "HBAO_Plus_Deinterleaved_AO_Texture");
            OutResources.FinalAOTexture         = RTHandles.Alloc(FinalAOTexDesc, wrapMode: TextureWrapMode.Clamp, name: "HBAO_Plus_Screen_Space_AO_Texture");
            OutResources.BlurTextureOne         = null;
            OutResources.BlurTextureTwo         = null;
            
            // Blur temporary shaders
            if (Settings.bUseBlur)
            {
                var BlurTextureDesc = new RenderTextureDescriptor((int)OutResources.FullResDimensions.x, (int)OutResources.FullResDimensions.y)
                {
                    graphicsFormat = HBAOPlusUtility.GetBlurTextureFormat(Settings),
                    enableRandomWrite = HBAOPlusUtility.IsBlurRenderPathCompute(Settings.BlurRenderPath),
                    autoGenerateMips  = false,
                    useMipMap         = false,
                    useDynamicScale   = DepthTextureDesc.useDynamicScale,
                };
                
                OutResources.BlurTextureOne = RTHandles.Alloc(BlurTextureDesc, wrapMode: TextureWrapMode.Clamp, name: "HBAO_Plus_Screen_Space_AO_Blur_Texture_One");
                OutResources.BlurTextureTwo = RTHandles.Alloc(BlurTextureDesc, wrapMode: TextureWrapMode.Clamp, name: "HBAO_Plus_Screen_Space_AO_Blur_Texture_Two");
            }

            if (Settings.AORenderPath is EAORenderPath.Compute) // Use compute shader path
            {
                OutResources.ComputeDeinterleavedDepthTexture     = RTHandles.Alloc(DeinterleavedDepthTexDesc, wrapMode: TextureWrapMode.Clamp, name: "HBAO_Plus_Deinterleaved_AO_Texture");
                OutResources.RasterDeinterleavedDepthTextureOne   = null;
                OutResources.RasterDeinterleavedDepthTextureTwo   = null;
                OutResources.RasterDeinterleavedDepthTextureThree = null;
                OutResources.RasterDeinterleavedDepthTextureFour  = null;
            }
            else // Use raster path
            {
                
                OutResources.ComputeDeinterleavedDepthTexture     = null;
                OutResources.RasterDeinterleavedDepthTextureOne   = RTHandles.Alloc(DeinterleavedDepthTexDesc, wrapMode: TextureWrapMode.Clamp, name : "HBAO_Plus_Deinterleaved_AO_Texture_One");
                OutResources.RasterDeinterleavedDepthTextureTwo   = RTHandles.Alloc(DeinterleavedDepthTexDesc, wrapMode: TextureWrapMode.Clamp, name : "HBAO_Plus_Deinterleaved_AO_Texture_Two");
                OutResources.RasterDeinterleavedDepthTextureThree = RTHandles.Alloc(DeinterleavedDepthTexDesc, wrapMode: TextureWrapMode.Clamp, name : "HBAO_Plus_Deinterleaved_AO_Texture_Three");
                OutResources.RasterDeinterleavedDepthTextureFour  = RTHandles.Alloc(DeinterleavedDepthTexDesc, wrapMode: TextureWrapMode.Clamp, name : "HBAO_Plus_Deinterleaved_AO_Texture_Four");
            }
            
            // Update the SSAO texture of the pass
            //ResourceData.ssaoTexture = OutResources.FinalAOTexture;
            
            // Material property block for the pass
            OutResources.PassPropertyBlock = PassPropertyBlock;
        }

        /// <summary>
        /// Add the depth linearization pass to the render graph.
        /// </summary>
        /// <param name="RenderGraph">Render graph instance</param>
        /// <param name="Settings">HBAO+ settings instance</param>
        /// <param name="Resources">Resources used by HBAO+</param>
        private static void _AddDepthLinearizationPass(RenderGraph RenderGraph, HBAOPlusSettings Settings, in HBAOPlusResources<TextureHandle> Resources)
        {
            using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass("Depth Linearization", out DepthLinearizationPassData<TextureHandle> PassData))
            {
                Builder.AllowPassCulling(false);
                
                Builder.UseTexture(Resources.HardwareDepthTexture);
                Builder.SetRenderAttachment(Resources.LinearizedDepthTexture, 0, AccessFlags.WriteAll);
                HBAOPlusUtility.InitializeDepthLinearizationPassData(PassData, Settings, in Resources);
                
                Builder.SetRenderFunc(static (DepthLinearizationPassData<TextureHandle> PassData, RasterGraphContext Context) =>
                {
                    Blitter.BlitTexture(Context.cmd, PassData.HardwareDepthTexture, Vector2.one, PassData.Settings.RasterPassMaterial, (int)ERasterShaderPass.DepthLinearization);
                });
            }
        }

        /// <summary>
        /// Add the depth deinterleaving pass to the render graph.
        /// </summary>
        /// <param name="RenderGraph">Render graph instance</param>
        /// <param name="Settings">HBAO+ settings instance</param>
        /// <param name="Resources">Resources used by HBAO+</param>
        private static void _AddDepthDeinterleavePass(RenderGraph RenderGraph, HBAOPlusSettings Settings, in HBAOPlusResources<TextureHandle> Resources)
        {
            if (Settings.AORenderPath is EAORenderPath.Compute) // Compute shader supported and preferred
            {
                using (IComputeRenderGraphBuilder Builder = RenderGraph.AddComputePass("Deinterleave Depth Pass", out DepthDeinterleaveComputePassData<TextureHandle> PassData))
                {
                    Builder.AllowPassCulling(false);
                
                    Builder.UseTexture(Resources.HardwareDepthTexture);
                    Builder.UseTexture(Resources.ComputeDeinterleavedDepthTexture, AccessFlags.WriteAll);
                    HBAOPlusUtility.InitializeDepthDeinterleavePassData(PassData, Settings, in Resources);
                
                    Builder.SetRenderFunc(static (DepthDeinterleaveComputePassData<TextureHandle> PassData, ComputeGraphContext Context) =>
                    {
                        HBAOPlusUtility.ApplyDepthDeinterleavePassParams(Context.cmd, PassData);
                        Context.cmd.DispatchCompute(
                            PassData.Settings.ComputePassShader, 
                            PassData.Settings.ComputeShaderParams.DeinterleaveDepthKernelId,
                            Mathf.CeilToInt(PassData.DeinterleavedDepthTextureDimensions.x / 8),
                            Mathf.CeilToInt(PassData.DeinterleavedDepthTextureDimensions.y / 8),
                            1
                        );
                    });
                }
            }
            else if (
                Settings.AORenderPath is EAORenderPath.Raster ||
                (Settings.AORenderPath is EAORenderPath.RasterNoGeometry && SystemInfo.supportsRenderTargetArrayIndexFromVertexShader)
            ) // Geometry shader or target array index from vertex shader semantic support
            {
                using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass("Deinterleave Depth Pass", out DepthDeinterleaveRasterPassData<TextureHandle> PassData))
                {
                    Builder.AllowPassCulling(false);
                
                    Builder.UseTexture(Resources.HardwareDepthTexture);
                    Builder.SetRenderAttachment(Resources.RasterDeinterleavedDepthTextureOne,   0, AccessFlags.WriteAll, 0, -1);
                    Builder.SetRenderAttachment(Resources.RasterDeinterleavedDepthTextureTwo,   1, AccessFlags.WriteAll, 0, -1);
                    Builder.SetRenderAttachment(Resources.RasterDeinterleavedDepthTextureThree, 2, AccessFlags.WriteAll, 0, -1);
                    Builder.SetRenderAttachment(Resources.RasterDeinterleavedDepthTextureFour,  3, AccessFlags.WriteAll, 0, -1);
                    HBAOPlusUtility.InitializeDepthDeinterleavePassData(PassData, Settings, in Resources);
                
                    Builder.SetRenderFunc(static (DepthDeinterleaveRasterPassData<TextureHandle> PassData, RasterGraphContext Context) =>
                    {
                        HBAOPlusUtility.ApplyDepthDeinterleavePassParams(PassData);
                        if (SystemInfo.supportsInstancing)
                        {
                            // Instanced draw of four full-screen triangles
                            Context.cmd.DrawProcedural(
                                Matrix4x4.identity,
                                PassData.Settings.RasterPassMaterial,
                                SystemInfo.supportsRenderTargetArrayIndexFromVertexShader ? (int)ERasterShaderPass.DepthDeinterleaveArrayTargetVS : (int)ERasterShaderPass.DepthDeinterleaveWithGS, 
                                MeshTopology.Triangles,
                                3,
                                4,
                                PassData.PassPropertyBlock
                            );
                        }
                        else
                        {
                            // 1 draw call per full-screen trangle for a total of 4 draw calls
                            for (int SliceIndex = 0; SliceIndex < 4; SliceIndex++)
                            {
                                HBAOPlusUtility.ApplyDepthDeinterleavePerSliceParams(PassData, SliceIndex);
                                CoreUtils.DrawFullScreen(
                                    Context.cmd,
                                    PassData.Settings.RasterPassMaterial, 
                                    PassData.PassPropertyBlock, 
                                    SystemInfo.supportsRenderTargetArrayIndexFromVertexShader ? (int)ERasterShaderPass.DepthDeinterleaveArrayTargetVS : (int)ERasterShaderPass.DepthDeinterleaveWithGS
                                );
                            }
                        }
                    });
                }
            }
            else
            {
                // Simple raster with no target array index semantic support
                // We want to avoid this as changing the render target between draw calls has a considerable overhead
                for (int SliceIndex = 0; SliceIndex < 4; SliceIndex++)
                {
                    using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass(_DepthDeinterleaveMultiPassNames[SliceIndex], out DepthDeinterleaveMultiRasterPassData<TextureHandle> PassData))
                    {
                        Builder.AllowPassCulling(false);
                    
                        Builder.UseTexture(Resources.HardwareDepthTexture);
                        Builder.SetRenderAttachment(Resources.RasterDeinterleavedDepthTextureOne,   0, AccessFlags.WriteAll, 0, SliceIndex);
                        Builder.SetRenderAttachment(Resources.RasterDeinterleavedDepthTextureTwo,   1, AccessFlags.WriteAll, 0, SliceIndex);
                        Builder.SetRenderAttachment(Resources.RasterDeinterleavedDepthTextureThree, 2, AccessFlags.WriteAll, 0, SliceIndex);
                        Builder.SetRenderAttachment(Resources.RasterDeinterleavedDepthTextureFour,  3, AccessFlags.WriteAll, 0, SliceIndex);
                        HBAOPlusUtility.InitializeDepthDeinterleavePassData(PassData, Settings, in Resources);
                        PassData.SliceIndex = SliceIndex;
    
                        Builder.SetRenderFunc(static (DepthDeinterleaveMultiRasterPassData<TextureHandle> PassData, RasterGraphContext Context) =>
                        {
                            HBAOPlusUtility.ApplyDepthDeinterleavePassParams(PassData);
                            HBAOPlusUtility.ApplyDepthDeinterleavePerSliceParams(PassData, PassData.SliceIndex);
                            CoreUtils.DrawFullScreen(
                                Context.cmd,
                                PassData.Settings.RasterPassMaterial, 
                                PassData.PassPropertyBlock, 
                                (int)ERasterShaderPass.DepthDeinterleaveSimpleRaster
                            );
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Add the normal reconstruction pass to the render graph.
        /// </summary>
        /// <param name="PassContext"></param>
        /// <param name="Settings">HBAO+ settings instance</param>
        /// <param name="Resources">Resources used by HBAO+</param>
        private static void _AddNormalReconstructionPass(CustomPassContext PassContext, HBAOPlusSettings Settings, in HBAOPlusResources<RTHandle> Resources)
        {
            if (Settings.bUseDepthNormal && PassContext.cameraNormalBuffer?.rt != null)
            {
                return;
            }
            
            if (Settings.bUseDepthNormal)
            {
                Debug.LogWarning("Camera normals texture is not available while resource is set to Depth Normals. Forcing normal reconstruction.");
            }
            
            NormalReconstructionPassData<RTHandle> PassData = GenericPool<NormalReconstructionPassData<RTHandle>>.Get();
            PassContext.cmd.SetRenderTarget(Resources.SceneNormalsTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            HBAOPlusUtility.InitializeNormalReconstructionPassData(PassData, Settings, in Resources);
            HBAOPlusUtility.ApplyNormalReconstructionPassParams(PassData);
            CoreUtils.DrawFullScreen(PassContext.cmd, PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.NormalReconstruction);
            GenericPool<NormalReconstructionPassData<RTHandle>>.Release(PassData);
        }
        
        /// <summary>
        /// Add the coarse AO pass to the render graph.
        /// </summary>
        /// <param name="PassContext">Render pass context instance</param>
        /// <param name="Settings">HBAO+ settings instance</param>
        /// <param name="Resources">Resources used by HBAO+</param>
        private static void _AddCoarseAOPass(CustomPassContext PassContext, HBAOPlusSettings Settings, in HBAOPlusResources<RTHandle> Resources)
        {
            if (Settings.AORenderPath is EAORenderPath.Compute)
            {
                CoarseAOComputePassData<RTHandle> PassData = GenericPool<CoarseAOComputePassData<RTHandle>>.Get();
                HBAOPlusUtility.InitializeCoarseAOPassData(PassData, Settings, in Resources);
                HBAOPlusUtility.ApplyCoarseAOComputePassParams(PassContext.cmd, PassData);
                PassContext.cmd.DispatchCompute(
                    PassData.Settings.ComputePassShader,
                    PassData.Settings.ComputeShaderParams.CoarseAOKernelId,
                    PassData.ThreadGroupsCount.x,
                    PassData.ThreadGroupsCount.y,
                    PassData.ThreadGroupsCount.z
                );
                GenericPool<CoarseAOComputePassData<RTHandle>>.Release(PassData);
            }
            else if (
                Settings.AORenderPath is EAORenderPath.Raster ||
                (Settings.AORenderPath is EAORenderPath.RasterNoGeometry && SystemInfo.supportsRenderTargetArrayIndexFromVertexShader)
            ) // Using SV_RenderTargetArrayIndex semantic in either geometry or vertex shader
            {
                CoarseAORasterPassData<RTHandle> PassData = GenericPool<CoarseAORasterPassData<RTHandle>>.Get();
                PassContext.cmd.SetRenderTarget(Resources.DeinterleavedAOTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                HBAOPlusUtility.InitializeCoarseAOPassData(PassData, Settings, in Resources);
                HBAOPlusUtility.ApplySharedCoarseAOPassParams(PassData);
                if (SystemInfo.supportsInstancing)
                {
                    // Instanced draw of four full-screen triangles
                    for (int DepthTextureIndex = 0; DepthTextureIndex < 4; DepthTextureIndex++)
                    {
                        HBAOPlusUtility.ApplyInstancedCoarseAOPassParams(PassData, DepthTextureIndex);
                        PassContext.cmd.DrawProcedural(
                            Matrix4x4.identity,
                            PassData.Settings.RasterPassMaterial,
                            PassData.PassIndex, 
                            MeshTopology.Triangles,
                            3,
                            4,
                            PassData.PassPropertyBlock
                        );
                    }
                }
                else
                {
                    for (int SliceIndex = 0; SliceIndex < 16; SliceIndex++)
                    {
                        HBAOPlusUtility.ApplyPerSliceCoarseAOPassParams(PassData, SliceIndex);
                        CoreUtils.DrawFullScreen(PassContext.cmd,  PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, PassData.PassIndex);
                    }
                }
                GenericPool<CoarseAORasterPassData<RTHandle>>.Release(PassData);
            }
            else // Using simple raster pass with no target array index semantic support
            {
                for (int SliceIndex = 0; SliceIndex < 16; SliceIndex++)
                {
                        CoarseAORasterMultiPassData<RTHandle> PassData = GenericPool<CoarseAORasterMultiPassData<RTHandle>>.Get();
                        PassContext.cmd.SetRenderTarget(Resources.DeinterleavedAOTexture, SliceIndex is 0 ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
                        HBAOPlusUtility.InitializeCoarseAOPassData(PassData, Settings, in Resources);
                        HBAOPlusUtility.ApplySharedCoarseAOPassParams(PassData);
                        HBAOPlusUtility.ApplyPerSliceCoarseAOPassParams(PassData, SliceIndex);
                        CoreUtils.DrawFullScreen(PassContext.cmd,  PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.CoarseAOSimpleRaster);
                        GenericPool<CoarseAORasterMultiPassData<RTHandle>>.Release(PassData);
                }
            }
        }

        /// <summary>
        /// Add AO reinterleave pass from the deinterleaved AO texture to the render graph.
        /// </summary>
        /// <param name="PassContext">Render pass context instance</param>
        /// <param name="Settings">HBAO+ settings instance</param>
        /// <param name="Resources">Resources used by HBAO+</param>
        private static void _AddAOReinterleavePass(CustomPassContext PassContext, HBAOPlusSettings Settings, in HBAOPlusResources<RTHandle> Resources)
        {
            AOReinterleavePassData<RTHandle> PassData = GenericPool<AOReinterleavePassData<RTHandle>>.Get();
            // Render into a temporary RT
            PassContext.cmd.SetRenderTarget(!Settings.bUseBlur ? Resources.FinalAOTexture : Resources.BlurTextureOne, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            HBAOPlusUtility.InitializeAOReinterleavePassData(PassData, Settings, in Resources);
            HBAOPlusUtility.ApplyAOReinterleavePassParams(PassData);
            CoreUtils.DrawFullScreen(PassContext.cmd,  PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.AOReinterleave);
            
            // If blur is disabled this is the final pass
            if (!PassData.Settings.bUseBlur)
            {
                HBAOPlusUtility.SetAmbientOcclusionParamsShaderProperty(CommandBufferHelpers.GetRasterCommandBuffer(PassContext.cmd), PassData.Settings);
                HBAOPlusUtility.SetAmbientOcclusionGlobalKeyword(CommandBufferHelpers.GetRasterCommandBuffer(PassContext.cmd), true);
                PassContext.cmd.SetGlobalTexture(HBAOPlusShaderParams.SSAOTexturePropertyId, Resources.FinalAOTexture);
            }
            
            GenericPool<AOReinterleavePassData<RTHandle>>.Release(PassData);
        }
        
        /// <summary>
        /// Add blur passes to the render graph if blur is enabled based on the blur quality.
        /// </summary>
        /// <param name="PassContext">Render pass context instance</param>
        /// <param name="Settings">HBAO+ settings instance</param>
        /// <param name="Resources">Resources used by HBAO+</param>
        private static void _AddBlurPass(CustomPassContext PassContext, HBAOPlusSettings Settings, in HBAOPlusResources<RTHandle> Resources)
        {
            if (!Settings.bUseBlur)
            {
                // No blur pass
                return;
            }

            switch (Settings.BlurQuality)
            {
                case EQuality.Low:
                    _AddKawaseBlurPass(PassContext, Settings, in Resources);
                    break;
                case EQuality.Medium:
                    _AddGaussianBlurPass(PassContext, Settings, in Resources);
                    break;
                case EQuality.High:
                    _AddBilateralBlurPass(PassContext, Settings, in Resources);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Settings.BlurQuality), Settings.BlurQuality, "Invalid blur quality");
            }
        }

        /// <summary>
        /// Add bilateral blur passes to the render graph.
        /// </summary>
        /// <param name="PassContext">Render pass context instance</param>
        /// <param name="Settings">HBAO+ settings instance</param>
        /// <param name="Resources">Resources used by HBAO+</param>
        private static void _AddBilateralBlurPass(CustomPassContext PassContext, HBAOPlusSettings Settings, in HBAOPlusResources<RTHandle> Resources)
        {
            if (Settings.BlurRenderPath is EBlurRenderPath.ComputeSinglePass)
            {
                ComputeBlurPassData<RTHandle> PassData = GenericPool<ComputeBlurPassData<RTHandle>>.Get();
                HBAOPlusUtility.InitializeCompressedBlurPassData(
                    PassData, 
                    Settings, 
                    in Resources, 
                    HBAOPlusComputeShaderParams.GetCompressedBlurKernelThreadNum(
                        Settings.BlurKernelRadius, 
                        true
                    )
                );
                HBAOPlusUtility.ApplyBlurPassParams(PassContext.cmd, PassData);
                PassContext.cmd.DispatchCompute(
                    PassData.Settings.ComputePassShader, 
                    PassData.KernelIndex,
                    PassData.ThreadGroupsCount.x,
                    PassData.ThreadGroupsCount.y,
                    PassData.ThreadGroupsCount.z
                );
                HBAOPlusUtility.SetAmbientOcclusionParamsShaderProperty(CommandBufferHelpers.GetComputeCommandBuffer(PassContext.cmd), PassData.Settings);
                HBAOPlusUtility.SetAmbientOcclusionGlobalKeyword(CommandBufferHelpers.GetComputeCommandBuffer(PassContext.cmd), true);
                PassContext.cmd.SetGlobalTexture(HBAOPlusShaderParams.SSAOTexturePropertyId, Resources.FinalAOTexture);
                
                GenericPool<ComputeBlurPassData<RTHandle>>.Release(PassData);
            }
            else if (Settings.BlurRenderPath is EBlurRenderPath.ComputeMultiPass)
            {
                ComputeBlurPassData<RTHandle> PassData = GenericPool<ComputeBlurPassData<RTHandle>>.Get();
                
                // Horizontal pass
                HBAOPlusUtility.InitializeBlurPassData(
                    PassData, 
                    Settings, 
                    in Resources, 
                    HBAOPlusComputeShaderParams.GetBlurKernelThreadNum(
                        Settings.BlurQuality,
                        EBlurDirection.Horizontal,
                        Settings.BlurKernelRadius,
                        EBlurKernelShape.Line,
                        true
                    ),
                    true
                );

                HBAOPlusUtility.ApplyBlurPassParams(PassContext.cmd, PassData);
                PassContext.cmd.DispatchCompute(
                    PassData.Settings.ComputePassShader, 
                    PassData.KernelIndex,
                    PassData.ThreadGroupsCount.x,
                    PassData.ThreadGroupsCount.y,
                    PassData.ThreadGroupsCount.z
                );
                
                // Vertical pass
                PassContext.cmd.SetGlobalTexture(HBAOPlusShaderParams.SSAOTexturePropertyId, Resources.FinalAOTexture);
                HBAOPlusUtility.InitializeBlurPassData(
                    PassData, 
                    Settings, 
                    in Resources, 
                    HBAOPlusComputeShaderParams.GetBlurKernelThreadNum(
                        Settings.BlurQuality,
                        EBlurDirection.Vertical,
                        Settings.BlurKernelRadius,
                        EBlurKernelShape.Line,
                        true
                    ), 
                    false
                );
                
                HBAOPlusUtility.ApplyBlurPassParams(PassContext.cmd, PassData);
                PassContext.cmd.DispatchCompute(
                    PassData.Settings.ComputePassShader, 
                    PassData.KernelIndex,
                    PassData.ThreadGroupsCount.x,
                    PassData.ThreadGroupsCount.y,
                    PassData.ThreadGroupsCount.z
                );
                HBAOPlusUtility.SetAmbientOcclusionParamsShaderProperty(CommandBufferHelpers.GetComputeCommandBuffer(PassContext.cmd), PassData.Settings);
                HBAOPlusUtility.SetAmbientOcclusionGlobalKeyword(CommandBufferHelpers.GetComputeCommandBuffer(PassContext.cmd), true);
                
                GenericPool<ComputeBlurPassData<RTHandle>>.Release(PassData);
            }
            else
            { 
                RasterBlurPassData<RTHandle> PassData = GenericPool<RasterBlurPassData<RTHandle>>.Get();

                PassContext.cmd.SetRenderTarget(Resources.BlurTextureTwo, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                HBAOPlusUtility.InitializeBlurPassData(PassData, Settings, in Resources);
                

                HBAOPlusUtility.ApplyBlurPassParams(PassData, true);
                CoreUtils.DrawFullScreen(PassContext.cmd,  PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.IntermediateBlur);
                
                PassContext.cmd.SetRenderTarget(Resources.FinalAOTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                HBAOPlusUtility.InitializeBlurPassData(PassData, Settings, in Resources);
                
                HBAOPlusUtility.ApplyBlurPassParams(PassData, false);
                CoreUtils.DrawFullScreen(PassContext.cmd, PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.FinalBlur);
                HBAOPlusUtility.SetAmbientOcclusionParamsShaderProperty(CommandBufferHelpers.GetRasterCommandBuffer(PassContext.cmd), PassData.Settings);
                HBAOPlusUtility.SetAmbientOcclusionGlobalKeyword(CommandBufferHelpers.GetRasterCommandBuffer(PassContext.cmd), true);
                
                PassContext.cmd.SetGlobalTexture(HBAOPlusShaderParams.SSAOTexturePropertyId, Resources.FinalAOTexture);
                
                GenericPool<RasterBlurPassData<RTHandle>>.Release(PassData);
            }
        }
        
        /// <summary>
        /// Add Gaussian blur passes to the render graph.
        /// </summary>
        /// <param name="PassContext">Render pass context instance</param>
        /// <param name="Settings">HBAO+ settings instance</param>
        /// <param name="Resources">Resources used by HBAO+</param>
        private static void _AddGaussianBlurPass(CustomPassContext PassContext, HBAOPlusSettings Settings, in HBAOPlusResources<RTHandle> Resources)
        {

        }
        
        /// <summary>
        /// Add Kawase blur pass to the render graph.
        /// </summary>
        /// <param name="PassContext">Render pass context instance</param>
        /// <param name="Settings">HBAO+ settings instance</param>
        /// <param name="Resources">Resources used by HBAO+</param>
        private static void _AddKawaseBlurPass(CustomPassContext PassContext, HBAOPlusSettings Settings, in HBAOPlusResources<RTHandle> Resources)
        {
        }
    }
}