using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using ZeroIQGames.Graphics.HBAOPlus.Core.Renderer;

namespace ZeroIQGames.Graphics.HBAOPlus.URP.Renderer
{
    public partial class HBAOPlusRendererFeature
    {
        /// <summary>
        /// Private
        /// </summary>
        private class RenderPass : ScriptableRenderPass
        {
            public bool Setup(HBAOPlusSettings PassSettings)
            {
                if (PassSettings.RasterPassMaterial == null)
                {
                    Debug.LogError("HBAO+ RenderMaterial is null");

                    return false;
                }

                if (PassSettings.ComputePassShader == null)
                {
                    Debug.LogError("HBAO+ ComputePassShader is null");
                    
                    return false;
                }
                _HBAOPlusSettings = PassSettings;
                
                // We need depth at the least
                ConfigureInput(ScriptableRenderPassInput.Depth | ( PassSettings.bUseDepthNormal ? ScriptableRenderPassInput.Normal : ScriptableRenderPassInput.None));
                
                return true;
            }
            
            public override void RecordRenderGraph(RenderGraph RenderGraph, ContextContainer FrameData)
            {
                using (new ProfilingScope(_RenderGraphRecordingMarker))
                {
                    var  ResourceData       = FrameData.Get<UniversalResourceData>();
                    var  CameraData         = FrameData.Get<UniversalCameraData>();
                    bool bDepthTextureValid = ResourceData.cameraDepthTexture.IsValid(); // Depth texture is always required
                    if (!bDepthTextureValid)
                    {
                        // Technically should never happen
                        Debug.LogError("HBAO+ requires a depth texture but one is not available.");
                    
                        return;
                    }
                
                    _PreparePassResources(RenderGraph, FrameData, _PassPropertyBlock, _HBAOPlusSettings, out HBAOPlusResources<TextureHandle> Resources);
                    HBAOPlusUtility.SetupShaderKeywords(_HBAOPlusSettings);
                    HBAOPlusUtility.SetupPerViewShaderParameters(CameraData.camera, _HBAOPlusSettings, in Resources);
                    
                    RenderGraph.BeginProfilingSampler(_HBAOPlusRenderPassesMarker);
                    
                    _AddDepthLinearizationPass(RenderGraph, _HBAOPlusSettings, in Resources);
                    _AddNormalReconstructionPass(RenderGraph, FrameData, _HBAOPlusSettings, in Resources);
                    _AddDepthDeinterleavePass(RenderGraph, _HBAOPlusSettings, in Resources);
                    _AddCoarseAOPass(RenderGraph, _HBAOPlusSettings, in Resources);
                    _AddAOReinterleavePass(RenderGraph, _HBAOPlusSettings, in Resources);
                    _AddBlurPass(RenderGraph, _HBAOPlusSettings, in Resources);
                    
                    RenderGraph.EndProfilingSampler(_HBAOPlusRenderPassesMarker);
                }
            }
            
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
            /// <param name="RenderGraph">Render graph instance</param>
            /// <param name="FrameData">Context container holding various per render context data</param>
            /// <param name="PassPropertyBlock">Property block used by the pass</param>
            /// <param name="Settings">HBAO Settings object for the effect</param>
            /// <param name="OutResources">Output resources object</param>
            private static void _PreparePassResources(RenderGraph RenderGraph, ContextContainer FrameData, MaterialPropertyBlock PassPropertyBlock, HBAOPlusSettings Settings, out HBAOPlusResources<TextureHandle> OutResources)
            {
                var CameraData    = FrameData.Get<UniversalCameraData>();
                var ResourceData  = FrameData.Get<UniversalResourceData>();
                
                // Texture descriptor of the camera depth texture
                TextureDesc DepthTextureDesc = ResourceData.cameraDepthTexture.GetDescriptor(RenderGraph);
                
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
                float CameraVerticalFOV      = CameraData.camera.fieldOfView;
                float CameraAspectRatio      = CameraData.camera.aspect;
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
                var LinearizedDepthTexDesc = new TextureDesc(DepthTextureDesc)
                {
                    clearBuffer       = false, // We write the whole texture, so clearing the buffer is not required
                    colorFormat       = GraphicsFormat.R32_SFloat, // We write to the R channel only.
                    name              = "HBAO_Plus_Linearized_Depth_Texture",
                };
                
                // Deinterleaved depth texture descriptor
                var DeinterleavedDepthTexDesc = new TextureDesc(DepthTextureDesc)
                {
                    clearBuffer       = false, // We write the whole texture, so clearing the buffer is not required
                    colorFormat       = GraphicsFormat.R32_SFloat, // We write to the R channel only. TODO: Check if we can use R16 when normal reconstruction is not needed
                    name              = "HBAO_Plus_Deinterleaved_Depth_Texture",
                    width             = (int)OutResources.QuarterResDimensions.x,
                    height            = (int)OutResources.QuarterResDimensions.y,
                    dimension         = TextureDimension.Tex2DArray,
                    slices            = Settings.AORenderPath is EAORenderPath.Compute ? 16 : 4, // One 16-slice array for the compute path, Four 4-slice arrays for the raster path
                    enableRandomWrite = Settings.AORenderPath is EAORenderPath.Compute, // Required for compute shader write
                };

                // Texture used for normal reconstruction
                var ReconstructedNormalsTexDesc = new TextureDesc((int)OutResources.FullResDimensions.x, (int)OutResources.FullResDimensions.y)
                {
                    clearBuffer       = false,
                    colorFormat       = GraphicsFormat.R8G8B8A8_SNorm,
                    name              = "HBAO_Plus_Reconstructed_Normals_Texture",
                    dimension         = TextureDimension.Tex2D,
                    enableRandomWrite = false,
                    useMipMap         = false,
                    filterMode        = FilterMode.Point,
                    wrapMode          = TextureWrapMode.Clamp,
                    anisoLevel        = 0,
                    scale             = DepthTextureDesc.scale,
                };

                var DeinterleavedAOTexDesc = new TextureDesc(DeinterleavedDepthTexDesc)
                {
                    colorFormat = GraphicsFormat.R16_UNorm,
                    name        = "HBAO_Plus_Deinterleaved_AO_Texture",
                    slices      = 16,
                };
                
                
                // Texture descriptor for the final AO texture
                var FinalAOTexDesc = new TextureDesc(DepthTextureDesc)
                {
                    colorFormat       = SystemInfo.GetCompatibleFormat(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Sample | GraphicsFormatUsage.Render),
                    name              = "HBAO_Plus_Screen_Space_AO_Texture",
                    clearBuffer       = false, // We write the whole texture, so clearing the buffer is not required
                    useMipMap         = false,
                    filterMode        = FilterMode.Point,
                    wrapMode          = TextureWrapMode.Clamp,
                    enableRandomWrite = HBAOPlusUtility.IsBlurRenderPathCompute(Settings.BlurRenderPath),
                    anisoLevel        = 0,
                };
                
                // Allocate resources
                OutResources.HardwareDepthTexture   = ResourceData.cameraDepthTexture;
                OutResources.LinearizedDepthTexture = RenderGraph.CreateTexture(LinearizedDepthTexDesc);
                OutResources.SceneNormalsTexture    = Settings.bUseDepthNormal && ResourceData.cameraNormalsTexture.IsValid() ? ResourceData.cameraNormalsTexture : RenderGraph.CreateTexture(ReconstructedNormalsTexDesc);
                OutResources.DeinterleavedAOTexture = RenderGraph.CreateTexture(DeinterleavedAOTexDesc);
                OutResources.FinalAOTexture         = RenderGraph.CreateTexture(FinalAOTexDesc);
                OutResources.BlurTextureOne         = default;
                OutResources.BlurTextureTwo         = default;
                
                // Blur temporary shaders
                if (Settings.bUseBlur)
                {
                    var BlurTextureOneDesc = new TextureDesc(FinalAOTexDesc)
                    {
                        colorFormat = HBAOPlusUtility.GetBlurTextureFormat(Settings),
                        name        = "HBAO_Plus_Screen_Space_AO_Blur_Texture_One",
                    };
                    var BlurTextureTwoDesc = new TextureDesc(BlurTextureOneDesc)
                    {
                        name   = "HBAO_Plus_Screen_Space_AO_Blur_Texture_Two",
                    };
                    
                    OutResources.BlurTextureOne = RenderGraph.CreateTexture(BlurTextureOneDesc);
                    OutResources.BlurTextureTwo = RenderGraph.CreateTexture(BlurTextureTwoDesc);
                }

                if (Settings.AORenderPath is EAORenderPath.Compute) // Use compute shader path
                {
                    OutResources.ComputeDeinterleavedDepthTexture     = RenderGraph.CreateTexture(DeinterleavedDepthTexDesc);
                    OutResources.RasterDeinterleavedDepthTextureOne   = default;
                    OutResources.RasterDeinterleavedDepthTextureTwo   = default;
                    OutResources.RasterDeinterleavedDepthTextureThree = default;
                    OutResources.RasterDeinterleavedDepthTextureFour  = default;
                }
                else // Use raster path
                {
                    OutResources.ComputeDeinterleavedDepthTexture     = default;
                    OutResources.RasterDeinterleavedDepthTextureOne   = RenderGraph.CreateTexture(new TextureDesc(DeinterleavedDepthTexDesc) { name  = "HBAO_Plus_Deinterleaved_AO_Texture_One" });
                    OutResources.RasterDeinterleavedDepthTextureTwo   = RenderGraph.CreateTexture(new TextureDesc(DeinterleavedDepthTexDesc) { name  = "HBAO_Plus_Deinterleaved_AO_Texture_Two" });
                    OutResources.RasterDeinterleavedDepthTextureThree = RenderGraph.CreateTexture(new TextureDesc(DeinterleavedDepthTexDesc) { name  = "HBAO_Plus_Deinterleaved_AO_Texture_Three" });
                    OutResources.RasterDeinterleavedDepthTextureFour  = RenderGraph.CreateTexture(new TextureDesc(DeinterleavedDepthTexDesc) { name  = "HBAO_Plus_Deinterleaved_AO_Texture_Four" });
                }
                
                // Update the SSAO texture of the pass
                ResourceData.ssaoTexture = OutResources.FinalAOTexture;
                
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
                        HBAOPlusUtility.ApplyDepthLinearizationPassParams(Context.cmd, PassData);
                        CoreUtils.DrawFullScreen(
                            Context.cmd,
                            PassData.Settings.RasterPassMaterial, 
                            PassData.PassPropertyBlock, 
                            (int)ERasterShaderPass.DepthLinearization
                        );
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
            /// <param name="RenderGraph">Render graph instance</param>
            /// <param name="FrameData">Context container for frame shared data</param>
            /// <param name="Settings">HBAO+ settings instance</param>
            /// <param name="Resources">Resources used by HBAO+</param>
            private static void _AddNormalReconstructionPass(RenderGraph RenderGraph, ContextContainer FrameData, HBAOPlusSettings Settings, in HBAOPlusResources<TextureHandle> Resources)
            {
                var ResourceData = FrameData.Get<UniversalResourceData>();
                if (Settings.bUseDepthNormal && ResourceData.cameraNormalsTexture.IsValid())
                {
                    return;
                }
                
                if (Settings.bUseDepthNormal)
                {
                    Debug.LogWarning("Camera normals texture is not available while resource is set to Depth Normals. Forcing normal reconstruction.");
                }
                
                using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass("Normal Reconstruction Pass", out NormalReconstructionPassData<TextureHandle> PassData))
                {
                    Builder.AllowPassCulling(false);

                    Builder.UseTexture(Resources.LinearizedDepthTexture);
                    Builder.SetRenderAttachment(Resources.SceneNormalsTexture, 0, AccessFlags.WriteAll);
                    HBAOPlusUtility.InitializeNormalReconstructionPassData(PassData, Settings, in Resources);
                    
                    Builder.SetRenderFunc(static (NormalReconstructionPassData<TextureHandle> PassData, RasterGraphContext Context) =>
                    {
                        HBAOPlusUtility.ApplyNormalReconstructionPassParams(PassData);
                        CoreUtils.DrawFullScreen(Context.cmd, PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.NormalReconstruction);
                    });
                }
            }
            
            /// <summary>
            /// Add the coarse AO pass to the render graph.
            /// </summary>
            /// <param name="RenderGraph">Render graph instance</param>
            /// <param name="Settings">HBAO+ settings instance</param>
            /// <param name="Resources">Resources used by HBAO+</param>
            private static void _AddCoarseAOPass(RenderGraph RenderGraph, HBAOPlusSettings Settings, in HBAOPlusResources<TextureHandle> Resources)
            {
                if (Settings.AORenderPath is EAORenderPath.Compute)
                {
                    using (IComputeRenderGraphBuilder Builder = RenderGraph.AddComputePass("Coarse AO Pass", out CoarseAOComputePassData<TextureHandle> PassData))
                    {
                        Builder.AllowPassCulling(false);
                        
                        Builder.UseTexture(Resources.SceneNormalsTexture);
                        Builder.UseTexture(Resources.ComputeDeinterleavedDepthTexture);
                        Builder.UseTexture(Resources.DeinterleavedAOTexture, AccessFlags.WriteAll);
                        HBAOPlusUtility.InitializeCoarseAOPassData(PassData, Settings, in Resources);
                        
                        Builder.SetRenderFunc(static (CoarseAOComputePassData<TextureHandle> PassData, ComputeGraphContext Context) =>
                        {
                            HBAOPlusUtility.ApplyCoarseAOComputePassParams(Context.cmd, PassData);
                            Context.cmd.DispatchCompute(
                                PassData.Settings.ComputePassShader,
                                PassData.Settings.ComputeShaderParams.CoarseAOKernelId,
                                PassData.ThreadGroupsCount.x,
                                PassData.ThreadGroupsCount.y,
                                PassData.ThreadGroupsCount.z
                            );
                        });
                    }
                    /*using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass("Coarse AO Pass", out CoarseAOPassData PassData))
                    {
                        Builder.AllowPassCulling(false);
                        
                        Builder.UseTexture(Resources.ViewSpaceNormalsTexture);
                        Builder.UseTexture(Resources.ComputeDeinterleavedDepthTexture);
                        Builder.SetRenderAttachment(Resources.DeinterleavedAOTexture, 0, AccessFlags.WriteAll, 0, -1);
                        HBAOPlusUtility.InitializeCoarseAOPassData(PassData, Settings, in Resources);
                    
                        Builder.SetRenderFunc(static (CoarseAOPassData PassData, RasterGraphContext Context) =>
                        {
                            HBAOPlusUtility.ApplySharedCoarseAOPassParams(PassData);
                            HBAOPlusUtility.ApplyInstancedCoarseAOPassParams(PassData);
                            Context.cmd.DrawProcedural(
                                Matrix4x4.identity,
                                PassData.Settings.RasterPassMaterial,
                                PassData.PassIndex, 
                                MeshTopology.Triangles,
                                3,
                                16,
                                PassData.PassPropertyBlock
                            );
                        });
                    }*/
                }
                else if (
                    Settings.AORenderPath is EAORenderPath.Raster ||
                    (Settings.AORenderPath is EAORenderPath.RasterNoGeometry && SystemInfo.supportsRenderTargetArrayIndexFromVertexShader)
                ) // Using SV_RenderTargetArrayIndex semantic in either geometry or vertex shader
                {
                    using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass("Coarse AO Pass", out CoarseAORasterPassData<TextureHandle> PassData))
                    {
                        Builder.AllowPassCulling(false);
                    
                        
                        Builder.UseTexture(Resources.SceneNormalsTexture);
                        Builder.UseTexture(Resources.RasterDeinterleavedDepthTextureOne);
                        Builder.UseTexture(Resources.RasterDeinterleavedDepthTextureTwo);
                        Builder.UseTexture(Resources.RasterDeinterleavedDepthTextureThree);
                        Builder.UseTexture(Resources.RasterDeinterleavedDepthTextureFour);
                        Builder.SetRenderAttachment(Resources.DeinterleavedAOTexture, 0, AccessFlags.WriteAll, 0, -1);
                        HBAOPlusUtility.InitializeCoarseAOPassData(PassData, Settings, in Resources);
                    
                        Builder.SetRenderFunc(static (CoarseAORasterPassData<TextureHandle> PassData, RasterGraphContext Context) =>
                        {
                            HBAOPlusUtility.ApplySharedCoarseAOPassParams(PassData);
                            if (SystemInfo.supportsInstancing)
                            {
                                // Instanced draw of four full-screen triangles
                                for (int DepthTextureIndex = 0; DepthTextureIndex < 4; DepthTextureIndex++)
                                {
                                    HBAOPlusUtility.ApplyInstancedCoarseAOPassParams(PassData, DepthTextureIndex);
                                    Context.cmd.DrawProcedural(
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
                                    CoreUtils.DrawFullScreen(Context.cmd,  PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, PassData.PassIndex);
                                }
                            }
                        });
                    }
                }
                else // Using simple raster pass with no target array index semantic support
                {
                    for (int SliceIndex = 0; SliceIndex < 16; SliceIndex++)
                    {
                        // Should be automatically merged by render graph into multiple native subpasses
                        using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass("Coarse AO Pass", out CoarseAORasterMultiPassData<TextureHandle> PassData))
                        {
                            Builder.AllowPassCulling(false);
                        
                            
                            Builder.UseTexture(Resources.SceneNormalsTexture);
                            Builder.UseTexture(Resources.RasterDeinterleavedDepthTextureOne);
                            Builder.UseTexture(Resources.RasterDeinterleavedDepthTextureTwo);
                            Builder.UseTexture(Resources.RasterDeinterleavedDepthTextureThree);
                            Builder.UseTexture(Resources.RasterDeinterleavedDepthTextureFour);
                            Builder.SetRenderAttachment(Resources.DeinterleavedAOTexture, 0, AccessFlags.WriteAll, 0, -1);
                            HBAOPlusUtility.InitializeCoarseAOPassData(PassData, Settings, in Resources);
                            PassData.SliceIndex = SliceIndex;
                            
                            Builder.SetRenderFunc(static (CoarseAORasterMultiPassData<TextureHandle> PassData, RasterGraphContext Context) =>
                            {
                                HBAOPlusUtility.ApplySharedCoarseAOPassParams(PassData);
                                HBAOPlusUtility.ApplyPerSliceCoarseAOPassParams(PassData, PassData.SliceIndex);
                                CoreUtils.DrawFullScreen(Context.cmd,  PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.CoarseAOSimpleRaster);
                            });
                        }
                    }
                }
            }

            /// <summary>
            /// Add AO reinterleave pass from the deinterleaved AO texture to the render graph.
            /// </summary>
            /// <param name="RenderGraph">Render graph instance</param>
            /// <param name="Settings">HBAO+ settings instance</param>
            /// <param name="Resources">Resources used by HBAO+</param>
            private static void _AddAOReinterleavePass(RenderGraph RenderGraph, HBAOPlusSettings Settings, in HBAOPlusResources<TextureHandle> Resources)
            {
                using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass("AO Reinterleave Pass", out AOReinterleavePassData<TextureHandle> PassData))
                {
                    Builder.AllowPassCulling(false);
                    
                    Builder.UseTexture(Resources.DeinterleavedAOTexture);
                    Builder.UseTexture(Resources.LinearizedDepthTexture);
                    Builder.UseTexture(Resources.SceneNormalsTexture);
                    
                    if (!Settings.bUseBlur) // If blur is disabled, this will be the final AO texture; otherwise we set this after blur pass
                    {
                        Builder.AllowGlobalStateModification(true); // Also set global AO params if this is the final AO pass
                        Builder.SetGlobalTextureAfterPass(Resources.FinalAOTexture, HBAOPlusShaderParams.SSAOTexturePropertyId);
                        Builder.SetRenderAttachment(Resources.FinalAOTexture, 0, AccessFlags.WriteAll);
                    }
                    else
                    {
                        // Render into a temporary RT
                        Builder.SetRenderAttachment(Resources.BlurTextureOne, 0, AccessFlags.WriteAll);
                    }
                    HBAOPlusUtility.InitializeAOReinterleavePassData(PassData, Settings, in Resources);
                    
                    Builder.SetRenderFunc(static (AOReinterleavePassData<TextureHandle> PassData, RasterGraphContext Context) =>
                    {
                        HBAOPlusUtility.ApplyAOReinterleavePassParams(PassData);
                        CoreUtils.DrawFullScreen(Context.cmd,  PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.AOReinterleave);
                        if (!PassData.Settings.bUseBlur)
                        {
                            HBAOPlusUtility.SetAmbientOcclusionParamsShaderProperty(Context.cmd, PassData.Settings);
                            HBAOPlusUtility.SetAmbientOcclusionGlobalKeyword(Context.cmd, true);
                        }
                    });
                }
            }
            
            /// <summary>
            /// Add blur passes to the render graph if blur is enabled based on the blur quality.
            /// </summary>
            /// <param name="RenderGraph">Render graph instance</param>
            /// <param name="Settings">HBAO+ settings instance</param>
            /// <param name="Resources">Resources used by HBAO+</param>
            private static void _AddBlurPass(RenderGraph RenderGraph, HBAOPlusSettings Settings, in HBAOPlusResources<TextureHandle> Resources)
            {
                if (!Settings.bUseBlur)
                {
                    // No blur pass
                    return;
                }

                switch (Settings.BlurQuality)
                {
                    case EQuality.Low:
                        _AddKawaseBlurPass(RenderGraph, Settings, in Resources);
                        break;
                    case EQuality.Medium:
                        _AddGaussianBlurPass(RenderGraph, Settings, in Resources);
                        break;
                    case EQuality.High:
                        _AddBilateralBlurPass(RenderGraph, Settings, in Resources);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(Settings.BlurQuality), Settings.BlurQuality, "Invalid blur quality");
                }
            }

            /// <summary>
            /// Add bilateral blur passes to the render graph.
            /// </summary>
            /// <param name="RenderGraph">Render graph instance</param>
            /// <param name="Settings">HBAO+ settings instance</param>
            /// <param name="Resources">Resources used by HBAO+</param>
            private static void _AddBilateralBlurPass(RenderGraph RenderGraph, HBAOPlusSettings Settings, in HBAOPlusResources<TextureHandle> Resources)
            {
                if (Settings.BlurRenderPath is EBlurRenderPath.ComputeSinglePass)
                {
                    using (IComputeRenderGraphBuilder Builder = RenderGraph.AddComputePass("AO Blur Compressed Single-Pass", out ComputeBlurPassData<TextureHandle> PassData))
                    {
                        Builder.AllowPassCulling(false);
                        Builder.AllowGlobalStateModification(true);
                        
                        Builder.UseTexture(Resources.BlurTextureOne);
                        Builder.UseTexture(Resources.SceneNormalsTexture);
                        Builder.UseTexture(Resources.FinalAOTexture, AccessFlags.WriteAll);
                        Builder.SetGlobalTextureAfterPass(Resources.FinalAOTexture, HBAOPlusShaderParams.SSAOTexturePropertyId);
                        HBAOPlusUtility.InitializeCompressedBlurPassData(
                            PassData, 
                            Settings, 
                            in Resources, 
                            HBAOPlusComputeShaderParams.GetCompressedBlurKernelThreadNum(
                                Settings.BlurKernelRadius, 
                                true
                            )
                        );
                        
                        Builder.SetRenderFunc(static (ComputeBlurPassData<TextureHandle> PassData, ComputeGraphContext Context) =>
                        {
                            HBAOPlusUtility.ApplyBlurPassParams(Context.cmd, PassData);
                            Context.cmd.DispatchCompute(
                                PassData.Settings.ComputePassShader, 
                                PassData.KernelIndex,
                                PassData.ThreadGroupsCount.x,
                                PassData.ThreadGroupsCount.y,
                                PassData.ThreadGroupsCount.z
                            );
                            HBAOPlusUtility.SetAmbientOcclusionParamsShaderProperty(Context.cmd, PassData.Settings);
                            HBAOPlusUtility.SetAmbientOcclusionGlobalKeyword(Context.cmd, true);
                        });
                    }

                }
                else if (Settings.BlurRenderPath is EBlurRenderPath.ComputeMultiPass)
                {
                    using (IComputeRenderGraphBuilder Builder = RenderGraph.AddComputePass("AO Intermediate Blur Pass", out ComputeBlurPassData<TextureHandle> PassData))
                    {
                        Builder.AllowPassCulling(false);
                        
                        Builder.UseTexture(Resources.BlurTextureOne);
                        Builder.UseTexture(Resources.SceneNormalsTexture);
                        Builder.UseTexture(Resources.BlurTextureTwo, AccessFlags.WriteAll);
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
                        
                        Builder.SetRenderFunc(static (ComputeBlurPassData<TextureHandle> PassData, ComputeGraphContext Context) =>
                        {
                            HBAOPlusUtility.ApplyBlurPassParams(Context.cmd, PassData);
                            Context.cmd.DispatchCompute(
                                PassData.Settings.ComputePassShader, 
                                PassData.KernelIndex,
                                PassData.ThreadGroupsCount.x,
                                PassData.ThreadGroupsCount.y,
                                PassData.ThreadGroupsCount.z
                            );
                        });
                    }
                    
                    using (IComputeRenderGraphBuilder Builder = RenderGraph.AddComputePass("AO Final Blur Pass", out ComputeBlurPassData<TextureHandle> PassData))
                    {
                        Builder.AllowPassCulling(false);
                        Builder.AllowGlobalStateModification(true);
                        
                        Builder.UseTexture(Resources.BlurTextureTwo);
                        Builder.UseTexture(Resources.SceneNormalsTexture);
                        Builder.UseTexture(Resources.FinalAOTexture, AccessFlags.WriteAll);
                        Builder.SetGlobalTextureAfterPass(Resources.FinalAOTexture, HBAOPlusShaderParams.SSAOTexturePropertyId);
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
                        
                        Builder.SetRenderFunc(static (ComputeBlurPassData<TextureHandle> PassData, ComputeGraphContext Context) =>
                        {
                            HBAOPlusUtility.ApplyBlurPassParams(Context.cmd, PassData);
                            Context.cmd.DispatchCompute(
                                PassData.Settings.ComputePassShader, 
                                PassData.KernelIndex,
                                PassData.ThreadGroupsCount.x,
                                PassData.ThreadGroupsCount.y,
                                PassData.ThreadGroupsCount.z
                            );
                            HBAOPlusUtility.SetAmbientOcclusionParamsShaderProperty(Context.cmd, PassData.Settings);
                            HBAOPlusUtility.SetAmbientOcclusionGlobalKeyword(Context.cmd, true);
                        });
                    }
                }
                else
                { 
                    using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass("AO Intermediate Blur Pass", out RasterBlurPassData<TextureHandle> PassData))
                    {
                        Builder.AllowPassCulling(false);
                        
                        Builder.UseTexture(Resources.BlurTextureOne);
                        Builder.UseTexture(Resources.SceneNormalsTexture);
                        Builder.SetRenderAttachment(Resources.BlurTextureTwo, 0, AccessFlags.WriteAll);
                        HBAOPlusUtility.InitializeBlurPassData(PassData, Settings, in Resources);
                        
                        Builder.SetRenderFunc(static (RasterBlurPassData<TextureHandle> PassData, RasterGraphContext Context) =>
                        {
                            HBAOPlusUtility.ApplyBlurPassParams(PassData, true);
                            CoreUtils.DrawFullScreen(Context.cmd,  PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.IntermediateBlur);
                        });
                    }
                    
                    using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass("AO Final Blur Pass", out RasterBlurPassData<TextureHandle> PassData))
                    {
                        Builder.AllowPassCulling(false);
                        Builder.AllowGlobalStateModification(true);
                        
                        Builder.UseTexture(Resources.BlurTextureTwo);
                        Builder.UseTexture(Resources.SceneNormalsTexture);
                        Builder.SetRenderAttachment(Resources.FinalAOTexture, 0, AccessFlags.WriteAll);
                        Builder.SetGlobalTextureAfterPass(Resources.FinalAOTexture, HBAOPlusShaderParams.SSAOTexturePropertyId);
                        HBAOPlusUtility.InitializeBlurPassData(PassData, Settings, in Resources);
                        
                        Builder.SetRenderFunc(static (RasterBlurPassData<TextureHandle> PassData, RasterGraphContext Context) =>
                        {
                            HBAOPlusUtility.ApplyBlurPassParams(PassData, false);
                            CoreUtils.DrawFullScreen(Context.cmd, PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.FinalBlur);
                            HBAOPlusUtility.SetAmbientOcclusionParamsShaderProperty(Context.cmd, PassData.Settings);
                            HBAOPlusUtility.SetAmbientOcclusionGlobalKeyword(Context.cmd, true);
                        });
                    }
                }
            }
            
            /// <summary>
            /// Add Gaussian blur passes to the render graph.
            /// </summary>
            /// <param name="RenderGraph">Render graph instance</param>
            /// <param name="Settings">HBAO+ settings instance</param>
            /// <param name="Resources">Resources used by HBAO+</param>
            private static void _AddGaussianBlurPass(RenderGraph RenderGraph, HBAOPlusSettings Settings, in HBAOPlusResources<TextureHandle> Resources)
            {
                using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass("AO Horizontal Blur Pass", out RasterBlurPassData<TextureHandle> PassData))
                {
                    Builder.AllowPassCulling(false);
                    
                    Builder.UseTexture(Resources.BlurTextureOne);
                    Builder.SetRenderAttachment(Resources.BlurTextureTwo, 0, AccessFlags.WriteAll);
                    HBAOPlusUtility.InitializeBlurPassData(PassData, Settings, in Resources);
                    
                    Builder.SetRenderFunc(static (RasterBlurPassData<TextureHandle> PassData, RasterGraphContext Context) =>
                    {
                        HBAOPlusUtility.ApplyBlurPassParams(PassData, true);
                        CoreUtils.DrawFullScreen(Context.cmd,  PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.FinalBlur);
                    });
                }
                
                using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass("AO Vertical Blur Pass", out RasterBlurPassData<TextureHandle> PassData))
                {
                    Builder.AllowPassCulling(false);
                    Builder.AllowGlobalStateModification(true);
                    
                    Builder.UseTexture(Resources.BlurTextureTwo);
                    Builder.SetRenderAttachment(Resources.FinalAOTexture, 0, AccessFlags.WriteAll);
                    Builder.SetGlobalTextureAfterPass(Resources.FinalAOTexture, HBAOPlusShaderParams.SSAOTexturePropertyId);
                    HBAOPlusUtility.InitializeBlurPassData(PassData, Settings, in Resources);
                    
                    Builder.SetRenderFunc(static (RasterBlurPassData<TextureHandle> PassData, RasterGraphContext Context) =>
                    {
                        HBAOPlusUtility.ApplyBlurPassParams(PassData, false);
                        CoreUtils.DrawFullScreen(Context.cmd, PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.IntermediateBlur);
                        HBAOPlusUtility.SetAmbientOcclusionParamsShaderProperty(Context.cmd, PassData.Settings);
                        HBAOPlusUtility.SetAmbientOcclusionGlobalKeyword(Context.cmd, true);
                    });
                }
            }
            
            /// <summary>
            /// Add Kawase blur pass to the render graph.
            /// </summary>
            /// <param name="RenderGraph">Render graph instance</param>
            /// <param name="Settings">HBAO+ settings instance</param>
            /// <param name="Resources">Resources used by HBAO+</param>
            private static void _AddKawaseBlurPass(RenderGraph RenderGraph, HBAOPlusSettings Settings, in HBAOPlusResources<TextureHandle> Resources)
            {
                using (IRasterRenderGraphBuilder Builder = RenderGraph.AddRasterRenderPass("AO Vertical Blur Pass", out RasterBlurPassData<TextureHandle> PassData))
                {
                    Builder.AllowPassCulling(false);
                    Builder.AllowGlobalStateModification(true);
                    
                    Builder.UseTexture(Resources.BlurTextureTwo);
                    Builder.SetRenderAttachment(Resources.FinalAOTexture, 0, AccessFlags.WriteAll);
                    Builder.SetGlobalTextureAfterPass(Resources.FinalAOTexture, HBAOPlusShaderParams.SSAOTexturePropertyId);
                    HBAOPlusUtility.InitializeBlurPassData(PassData, Settings, in Resources);
                    
                    Builder.SetRenderFunc(static (RasterBlurPassData<TextureHandle> PassData, RasterGraphContext Context) =>
                    {
                        HBAOPlusUtility.ApplyBlurPassParams(PassData, false);
                        CoreUtils.DrawFullScreen(Context.cmd, PassData.Settings.RasterPassMaterial, PassData.PassPropertyBlock, (int)ERasterShaderPass.IntermediateBlur);
                        HBAOPlusUtility.SetAmbientOcclusionParamsShaderProperty(Context.cmd, PassData.Settings);
                        HBAOPlusUtility.SetAmbientOcclusionGlobalKeyword(Context.cmd, true);
                    });
                }
            }
        }
    }
}