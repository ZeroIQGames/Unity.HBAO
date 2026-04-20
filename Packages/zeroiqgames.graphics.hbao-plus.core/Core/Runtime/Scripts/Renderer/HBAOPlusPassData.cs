using JetBrains.Annotations;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;


namespace ZeroIQGames.Graphics.HBAOPlus.Core.Renderer
{
    /// <summary>
    /// Resources used by the HBAO+ in a frame.
    /// </summary>
    internal struct HBAOPlusResources<TTextureHandle>
    {
        /// <summary>
        /// Camera depth texture.
        /// </summary>
        public TTextureHandle HardwareDepthTexture;
        
        /// <summary>
        /// Full res texture with absolute linearized depth values.
        /// </summary>
        public TTextureHandle LinearizedDepthTexture;
        
        /// <summary>
        /// Normals texture in view space. Only valid if normals are reconstructed from depth.
        /// </summary>
        public TTextureHandle SceneNormalsTexture;
        
        
        /// <summary>
        /// 16-layers deinterleaved absolute linearized depth texture array.
        /// </summary>
        public TTextureHandle ComputeDeinterleavedDepthTexture;
        
        /// <summary>
        /// Four-layer texture array holding the deinterleaved depth textures used in raster depth deinterleave. <br />
        /// Holds quarter res slices 00, 02, 08, 10 according to mapping below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c>
        /// </summary>
        public TTextureHandle RasterDeinterleavedDepthTextureOne;
        
        /// <summary>
        /// Four-layer texture array holding the deinterleaved depth textures used in raster depth deinterleave.
        /// Holds quarter res slices 01, 03, 09, 11 according to mapping below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c>
        /// </summary>
        public TTextureHandle RasterDeinterleavedDepthTextureTwo;
        
        /// <summary>
        /// Four-layer texture array holding the deinterleaved depth textures used in raster depth deinterleave.
        /// Holds quarter res slices 04, 06, 12, 14 according to mapping below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c>
        /// </summary>
        public TTextureHandle RasterDeinterleavedDepthTextureThree;
        
        /// <summary>
        /// Four-layer texture array holding the deinterleaved depth textures used in raster depth deinterleave.
        /// Holds quarter res slices 05, 07, 13, 15 according to mapping below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c>
        /// </summary>
        public TTextureHandle RasterDeinterleavedDepthTextureFour;
        
        /// <summary>
        /// 16-layers texture array holding raw AO data.
        /// </summary>
        public TTextureHandle DeinterleavedAOTexture;

        /// <summary>
        /// Temporary texture used for AO post-processing (blur). Only valid if blur is enabled.
        /// </summary>
        public TTextureHandle BlurTextureOne;
        
        /// <summary>
        /// Temporary texture used for AO post-processing (blur). Only valid if blur is enabled.
        /// </summary>
        public TTextureHandle BlurTextureTwo;
                
        /// <summary>
        /// Final AO texture to be used by the rest of the pipeline.
        /// </summary>
        public TTextureHandle FinalAOTexture;

        /// <summary>
        /// Full resolution dimensions of the camera (which should be the same as the depth texture).
        /// </summary>
        /// <remarks>Component values are: x->width, y->height, z->1/width, w->1/height</remarks>
        public Vector4 FullResDimensions;

        /// <summary>
        /// Quarter resolution texture array dimensions. Used by deinterleaved texture arrays.
        /// </summary>
        /// <remarks>Component values are: x->width, y->height, z->1/width, w->1/height</remarks>
        public Vector4 QuarterResDimensions;

        /// <summary>
        /// Parameters used to 
        /// </summary>
        public Vector4 UVToViewParams;

        /// <summary>
        /// Tangent of the half of the vertical FOV angle.
        /// </summary>
        public float TanHalfVerticalFOV;

        /// <summary>
        /// Tangent of the half of the horizontal FOV angle.
        /// </summary>
        public float TanHalfHorizontalFOV;

        /// <summary>
        /// Cached material property block used by the pass.
        /// </summary>
        public MaterialPropertyBlock PassPropertyBlock;
    }
    
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class DepthLinearizationPassData<TTextureHandle>
    {
        public TTextureHandle HardwareDepthTexture;
        
        public TTextureHandle LinearizedDepthTexture;

        public HBAOPlusSettings Settings;

        public MaterialPropertyBlock PassPropertyBlock;
    }

    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class DepthDeinterleaveComputePassData<TTextureHandle>
    {
        public TTextureHandle HardwareDepthTexture;
        
        public TTextureHandle DeinterleavedDepthTexture;

        public Vector4 DeinterleavedDepthTextureDimensions;
        
        public Vector4 DepthTextureDimensions;

        public HBAOPlusSettings Settings;
    }
    
    /// <summary>
    /// Data for depth deinterleaving pass
    /// </summary>
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class DepthDeinterleaveRasterPassData<TTextureHandle>
    {
        /// <summary>
        /// Camera depth texture.
        /// </summary>
        public TTextureHandle HardwareDepthTexture;
        
        /// <summary>
        /// Four-layer texture array holding the deinterleaved depth textures used in raster depth deinterleave. <br />
        /// Holds quarter res slices 00, 02, 08, 10 according to mapping below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c>
        /// </summary>
        public TTextureHandle DeinterleavedDepthTextureOne;
        
        /// <summary>
        /// Four-layer texture array holding the deinterleaved depth textures used in raster depth deinterleave.
        /// Holds quarter res slices 01, 03, 09, 11 according to mapping below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c>
        /// </summary>
        public TTextureHandle DeinterleavedDepthTextureTwo;
        
        /// <summary>
        /// Four-layer texture array holding the deinterleaved depth textures used in raster depth deinterleave.
        /// Holds quarter res slices 04, 06, 12, 14 according to mapping below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c>
        /// </summary>
        public TTextureHandle DeinterleavedDepthTextureThree;
        
        /// <summary>
        /// Four-layer texture array holding the deinterleaved depth textures used in raster depth deinterleave.
        /// Holds quarter res slices 05, 07, 13, 15 according to mapping below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c>
        /// </summary>
        public TTextureHandle DeinterleavedDepthTextureFour;
        
        /// <summary>
        /// Camera rendering dimensions. Should be the same as the depth texture dimensions.
        /// </summary>
        public Vector4 FullResDimensions;

        /// <summary>
        /// Settings instance
        /// </summary>
        public HBAOPlusSettings Settings;
        
        /// <summary>
        /// Property block used for the pass
        /// </summary>
        public MaterialPropertyBlock PassPropertyBlock;
    }
    
    /// <summary>
    /// Data for multi-deinterleave pass when geometry/compute/render target array index semantic is not available.
    /// </summary>
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class DepthDeinterleaveMultiRasterPassData<TTextureHandle> : DepthDeinterleaveRasterPassData<TTextureHandle>
    {
        /// <summary>
        /// Index of the slice for this pass
        /// </summary>
        public int SliceIndex;
    }
    
    /// <summary>
    /// Data for normal reconstruction pass.
    /// </summary>
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class NormalReconstructionPassData<TTextureHandle>
    {
        public TTextureHandle LinearizedDepthTexture;

        public HBAOPlusSettings Settings;
        
        public MaterialPropertyBlock PassPropertyBlock;
    }

    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class CoarseAOComputePassData<TTextureHandle>
    {
        /// <summary>
        /// Sixteen-layer texture array holding the deinterleaved depth textures populated in compute depth deinterleave.
        /// </summary>
        /// <remarks>Is only valid if <see cref="HBAOPlusSettings.AORenderPath"/> is set to <see cref="EAORenderPath.Compute"/></remarks>
        public TTextureHandle DeinterleavedDepthTexture;
        
        /// <summary>
        /// Texture with scene normals. Can be in either view-space or world-space.
        /// </summary>
        public TTextureHandle SceneNormalsTexture;
        
        /// <summary>
        /// Coarse AO texture array that will be written into
        /// </summary>
        public TTextureHandle DeinterleavedCoarseAOTexture;
        
        /// <summary>
        /// HBAO+ settings.
        /// </summary>
        public HBAOPlusSettings Settings;

        /// <summary>
        /// Number of thread groups in x,y,z directions.
        /// </summary>
        public int3 ThreadGroupsCount;
    }

    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class CoarseAORasterPassData<TTextureHandle>
    {
        /// <summary>
        /// Sixteen-layer texture array holding the deinterleaved depth textures populated in compute depth deinterleave.
        /// </summary>
        /// <remarks>Is only valid if <see cref="HBAOPlusSettings.AORenderPath"/> is set to <see cref="EAORenderPath.Compute"/></remarks>
        public TTextureHandle DeinterleavedDepthTexture;
        
        /// <summary>
        /// Four-layer texture array holding the deinterleaved depth textures used in raster depth deinterleave. <br />
        /// Holds quarter res slices 00, 02, 08, 10 according to mapping below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c>
        /// </summary>
        /// <remarks>Is only valid if <see cref="HBAOPlusSettings.AORenderPath"/> is not set to <see cref="EAORenderPath.Compute"/></remarks>
        public TTextureHandle DeinterleavedDepthTextureOne;
        
        /// <summary>
        /// Four-layer texture array holding the deinterleaved depth textures used in raster depth deinterleave.
        /// Holds quarter res slices 01, 03, 09, 11 according to mapping below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c>
        /// </summary>
        /// <remarks>Is only valid if <see cref="HBAOPlusSettings.AORenderPath"/> is not set to <see cref="EAORenderPath.Compute"/></remarks>
        public TTextureHandle DeinterleavedDepthTextureTwo;
        
        /// <summary>
        /// Four-layer texture array holding the deinterleaved depth textures used in raster depth deinterleave.
        /// Holds quarter res slices 04, 06, 12, 14 according to mapping below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c>
        /// </summary>
        /// <remarks>Is only valid if <see cref="HBAOPlusSettings.AORenderPath"/> is not set to <see cref="EAORenderPath.Compute"/></remarks>
        public TTextureHandle DeinterleavedDepthTextureThree;
        
        /// <summary>
        /// Four-layer texture array holding the deinterleaved depth textures used in raster depth deinterleave.
        /// Holds quarter res slices 05, 07, 13, 15 according to mapping below: <br />
        /// <c> 00 | 01 | 02 | 03 </c> <br />
        /// <c> 04 | 05 | 06 | 07 </c> <br />
        /// <c> 08 | 09 | 10 | 11 </c> <br />
        /// <c> 12 | 13 | 14 | 15 </c>
        /// </summary>
        /// <remarks>Is only valid if <see cref="HBAOPlusSettings.AORenderPath"/> is not set to <see cref="EAORenderPath.Compute"/></remarks>
        public TTextureHandle DeinterleavedDepthTextureFour;
        
        /// <summary>
        /// Texture with scene normals. Can be in either view-space or world-space.
        /// </summary>
        public TTextureHandle SceneNormalsTexture;
        
        /// <summary>
        /// HBAO+ settings.
        /// </summary>
        public HBAOPlusSettings Settings;
        
        /// <summary>
        /// Material property block used for the pass.
        /// </summary>
        public MaterialPropertyBlock PassPropertyBlock;

        /// <summary>
        /// Index of the pass to be used based on the settings
        /// </summary>
        public int PassIndex;
    }

    /// <summary>
    /// Data for multi coarse AO pass when geometry/compute/render target array index semantic is not available.
    /// </summary>
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class CoarseAORasterMultiPassData<TTextureHandle> : CoarseAORasterPassData<TTextureHandle>
    {
        /// <summary>
        /// Index of the slice for this pass
        /// </summary>
        public int SliceIndex;
    }

    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class AOReinterleavePassData<TTextureHandle>
    {
        public TTextureHandle DeinterleavedAOTexture;
        public TTextureHandle ViewSpaceNormalsTexture;
        public TTextureHandle LinearizedDepthTexture;
        
        public HBAOPlusSettings Settings;
        
        public MaterialPropertyBlock PassPropertyBlock;
    }

    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class RasterBlurPassData<TTextureHandle>
    {
        public TTextureHandle IntermediateBlurTextureOne;
        public TTextureHandle IntermediateBlurTextureTwo;
        public TTextureHandle SceneNormalsTexture;

        public EBlurSharpnessSource SharpnessSource;
        
        public HBAOPlusSettings Settings;
        
        public MaterialPropertyBlock PassPropertyBlock;
    }
    
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class ComputeBlurPassData<TTextureHandle>
    {
        public TTextureHandle IntermediateBlurTexture;
        public TTextureHandle BlurRenderTargetTexture;
        public TTextureHandle SceneNormalsTexture;
        public int KernelIndex;

        public EBlurSharpnessSource SharpnessSource;
        
        public HBAOPlusSettings Settings;
        
        /// <summary>
        /// Number of thread groups in x,y,z directions.
        /// </summary>
        public int3 ThreadGroupsCount;
    }
}