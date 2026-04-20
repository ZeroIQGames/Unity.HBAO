using JetBrains.Annotations;
using UnityEngine;

namespace ZeroIQGames.Graphics.HBAOPlus.Core.Renderer
{
    /// <summary>
    /// Enum for possible sources of geometry data to calculate HBAO+ from.
    /// </summary>
    public enum EGeometrySource
    {
        [UsedImplicitly] // Used as a dropdown option
        [InspectorName("Depth Only")]
        DepthOnly,
        [InspectorName("Depth + Normal")]
        DepthNormal,
    }

    /// <summary>
    /// Quality levels enum
    /// </summary>
    public enum EQuality
    {
        [InspectorName("FPSmaxxing")]
        Low,
        [InspectorName("Lowkey Good")]
        Medium,
        [InspectorName("Absolute Cinema")]
        High,
    }
    
    /// <summary>
    /// Sources used for blur sharpening
    /// </summary>
    public enum EBlurSharpnessSource
    {
        /// <summary>
        /// Use only depth data for sharpening. AO will spill to nearby pixels on very thin crevices with small depth differences with surrounding pixels.
        /// </summary>
        [InspectorName("Depth")]
        Depth = 1 << 0,
        
        /// <summary>
        /// Use normal only. AO will spill to nearby pixels with similar normals but different depths.
        /// </summary>
        [InspectorName("Normal")]
        Normal = 1 << 1,
        
        /// <summary>
        /// Use both depth and normal data. Maximum sharpness at the cost of higher bandwidth usage and more arithmetic operations.
        /// </summary>
        [InspectorName("Both")]
        DepthNormal = Depth | Normal,
    }

    /// <summary>
    /// Radius of blur pass kernel
    /// </summary>
    public enum EBlurRadius
    {
        [InspectorName("1 Pixel")]
        One = 1,
        [InspectorName("2 Pixels")]
        Two = 2,
        [InspectorName("3 Pixels")]
        Three = 3,
        [InspectorName("4 Pixels")]
        Four = 4,
    }
    
    /// <summary>
    /// Shape of the blur kernel thread group.
    /// </summary>
    internal enum EBlurKernelShape
    {
        /// <summary>
        /// Blur kernel shape is a line with length hard-coded to 128.
        /// </summary>
        Line,
            
        /// <summary>
        /// Blur kernel is in the shape of a rectangle with edge length decided by the blur radius
        /// </summary>
        Rectangle,
    }
        
    /// <summary>
    /// Direction of the blur pass
    /// </summary>
    internal enum EBlurDirection
    {
        /// <summary>
        /// Applied in the vertical direction.
        /// </summary>
        Vertical,
            
        /// <summary>
        /// Applied in the horizontal direction.
        /// </summary>
        Horizontal,
            
        /// <summary>
        /// The blur type is two-dimensional and applied to both the vertical and horizontal directions at once.
        /// </summary>
        TwoDimensional,
    }
    
    /// <summary>
    /// Possible render paths for HBAO+. Support depends on the platform.
    /// </summary>
    public enum EAORenderPath
    {
        // Literal values are used for validation. DO NOT CHANGE.
        
        /// <summary>
        /// Prefer compute shader if supported on the device.
        /// </summary>
        [InspectorName("Compute")]
        Compute = 0,
        
        /// <summary>
        /// Prefer raster shader with geometry if supported on the device.
        /// </summary>
        [InspectorName("Raster")]
        Raster = 1,
        
        /// <summary>
        /// Prefer raster shader without geometry.
        /// </summary>
        /// <remarks>
        /// Note that if the target API does not support SV_RenderTargetArrayIndex, using this render path can result in noticeable performance hit. <br />
        /// Specifically, on iPhone 5s - iPhone 7, or OpenGL ES 3.1 android devices, using <see cref="Compute"/> over this option is recommended. <br />
        /// For WebGL or older devices/APIs than the above, this option is the only available one and will be chosen regardless of the <see cref="HBAOPlusRendererFeature.AORenderPathPreferenceOrder"/> setting.
        /// </remarks>
        [InspectorName("Raster (no geometry)")]
        RasterNoGeometry = 2,
    }
    
    /// <summary>
    /// Possible render paths for HBAO+ blur. Support depends on the platform.
    /// </summary>
    public enum EBlurRenderPath
    {
        /// <summary>
        /// Prefer compute shader compressed two-pass kernel for blur
        /// </summary>
        [InspectorName("Compute Single-Pass")]
        ComputeSinglePass,
        
        /// <summary>
        /// Prefer compute shader multi-pass kernel for blur
        /// </summary>
        [InspectorName("Compute Multi-Pass")]
        ComputeMultiPass,
        
        /// <summary>
        /// Prefer raster shader for blur passes.
        /// </summary>
        [InspectorName("Raster")]
        Raster,
        
        /// <summary>
        /// Let the system choose the best render path for blur passes based on the target platform and kernel radius.
        /// </summary>
        [InspectorName("Automatic")]
        Automatic,
    }

    /// <summary>
    /// HBAO+ raster shader pass enum.
    /// </summary>
    internal enum ERasterShaderPass
    {
        /// <summary>
        /// Hardware depth linearization pass.
        /// </summary>
        DepthLinearization = 0,
        
        /// <summary>
        /// Depth deinterleave raster pass with render target layer set in geometry shader.
        /// </summary>
        DepthDeinterleaveWithGS        = 1,
        
        /// <summary>
        /// Depth deinterleave raster pass with render target layer set in vertex shader.
        /// </summary>
        DepthDeinterleaveArrayTargetVS = 2,
        
        /// <summary>
        /// Depth deinterleave raster pass without layered rendering support.
        /// </summary>
        DepthDeinterleaveSimpleRaster  = 3,
        
        /// <summary>
        /// Scene normal reconstruction pass using scene depth.
        /// </summary>
        NormalReconstruction           = 4,
        
        /// <summary>
        /// Coarse AO raster pass with render target layer set in geometry shader.
        /// </summary>
        CoarseAOWithGS = 5,
        
        /// <summary>
        /// Coarse AO raster pass with render target layer set in vertex shader.
        /// </summary>
        CoarseAOArrayTargetVS = 6,
        
        /// <summary>
        /// Coarse AO raster pass without layered rendering support.
        /// </summary>
        CoarseAOSimpleRaster = 7,
        
        /// <summary>
        /// AO Reinterleave pass. Will be the final pass if blur is disabled.
        /// </summary>
        AOReinterleave = 8,
        
        /// <summary>
        /// Initial blur pass in Bilateral/Gaussian two pass blur.
        /// </summary>
        IntermediateBlur = 9,
        
        /// <summary>
        /// Final blur pass in Bilateral/Gaussian two pass blur or the only blur pass for Kawase blur.
        /// </summary>
        FinalBlur = 10,
    }
}