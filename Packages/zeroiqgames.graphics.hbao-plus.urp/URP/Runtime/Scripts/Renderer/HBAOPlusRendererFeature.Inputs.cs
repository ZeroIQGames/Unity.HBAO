using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ZeroIQGames.Graphics.HBAOPlus.Core.Renderer;

namespace ZeroIQGames.Graphics.HBAOPlus.URP.Renderer
{
    public partial class HBAOPlusRendererFeature
    {
        /// <summary>
        /// Quality of the AO effect affecting the number of ray-march directions and samples per ray.
        /// </summary>
        /// <remarks>When set from code, you need to call <see cref="ScriptableRendererData.SetDirty"/> to reflect the changes made</remarks>
        [Tooltip("Quality of the AO effect affecting the number of ray-march directions and samples per ray")]
        public EQuality AOQuality = EQuality.High;

        /// <summary>
        /// Quality of the AO effect affecting the number of ray-march directions and samples per ray.
        /// </summary>
        /// <remarks>When set from code, you need to call <see cref="ScriptableRendererData.SetDirty"/> to reflect the changes made</remarks>
        [Tooltip("Quality of the blur effect applied (if enabled) affecting the algorithm used to calculate the blur. Note that blur kernel size is set using Blur Radius parameter.")]
        public EQuality BlurQuality = EQuality.High;

        /// <summary>
        /// Sources used for calculating bilateral blur sharpness. Only Used if blur quality is set to <see cref="EQuality.High"/>.
        /// </summary>
        /// <remarks>When set from code, you need to call <see cref="ScriptableRendererData.SetDirty"/> to reflect the changes made</remarks>
        [Tooltip("Sources used for calculating bilateral blur sharpness.")]
        public EBlurSharpnessSource SharpnessSource = EBlurSharpnessSource.Depth;

        /// <summary>
        /// Source of the geometry data for AO calculation. If set to <see cref="EGeometrySource.DepthOnly"/>, HBAO+ will use camera depth texture and reconstruct normals from it. <br />
        /// If set to <see cref="EGeometrySource.DepthNormal"/>, HBAO+ will use depth normal prepass data. In deferred and deferred+ render paths, GBuffer is used instead.
        /// </summary>
        /// <remarks>When set from code, you need to call <see cref="ScriptableRendererData.SetDirty"/> to reflect the changes made</remarks>
        [Tooltip("Source data for AO calculation")]
        public EGeometrySource GeometrySource = EGeometrySource.DepthNormal;

        /// <summary>
        /// Quality of the AO effect affecting the number of ray-march directions and samples per ray.
        /// </summary>
        /// <remarks>When set from code, you need to call <see cref="ScriptableRendererData.SetDirty"/> to reflect the changes made</remarks>
        [Tooltip("Quality of the reconstructed normals. Higher quality means more accurate normals but also more expensive.")]
        public EQuality NormalReconstructionQuality = EQuality.High;

        /// <summary>
        /// Radius of the gaussian blur kernel in pixels. Directly impacts the performance and visual quality.
        /// </summary>
        /// <remarks>When set from code, you need to call <see cref="ScriptableRendererData.SetDirty"/> to reflect the changes made</remarks>
        [Tooltip("Kernel radius of the blur pass in pixels. Directly impacts the performance and visual quality.")]
        public EBlurRadius BlurRadius = EBlurRadius.Two;

        /// <summary>
        /// When set to true, HBAO+ will use surface slope for calculating blur edges
        /// </summary>
        /// <remarks>When set from code, you need to call <see cref="ScriptableRendererData.SetDirty"/> to reflect the changes made</remarks>
        [Tooltip("When set to true, HBAO+ will use surface slope for calculating blur edges")]
        public bool UseSurfaceSlopeForDepthSharpness;

        /// <summary>
        /// When set to true, HBAO+ will use a random jitter value every frame. Can help prevent color banding and other similar artifacts when using temporal techniques but can introduce visible shimmer/noise when AO is too strong.
        /// </summary>
        /// <remarks>When set from code, you need to call <see cref="ScriptableRendererData.SetDirty"/> to reflect the changes made</remarks>
        [Tooltip("Change the jitter(randomness) value every frame. Can potentially help prevent color banding and other similar artifacts when using temporal techniques but can also introduce visible flicker/noise.")]
        public bool UsePerFrameRandomJitter;
        
        /// <summary>
        /// Current AO render path preference order. HBAO+ will try to use the first render path supported by the target platform. Lower index means higher priority.<br />
        /// Values should not be null and contain exactly 3 distinct elements of <see cref="EAORenderPath"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Throws if an invalid value is passed to the setter.</exception>
        /// <remarks>When set from code, you need to call <see cref="ScriptableRendererData.SetDirty"/> to reflect the changes made</remarks>
        public IReadOnlyList<EAORenderPath> AORenderPathPreferenceOrder
        {
            get => _RenderPathPreferenceOrderReadOnly ?? _AORenderPathPreferenceOrder;
            set
            {
                if (!HBAOPlusUtility.IsRenderPathPreferenceListValid(value))
                {
                    throw new ArgumentException("RenderPathPreferenceOrder must not be null and contain exactly 3 distinct elements of the enum EHBAORenderPath.", nameof(value));
                }
                
                _RenderPathPreferenceOrderReadOnly = value;
                _AORenderPathPreferenceOrder         = null; // We don't need the serialized value anymore
            }
        }

        /// <summary>
        /// Current blur preferred render path. HBAO+ will try to use the preferred path if supported by the target platform.<br />
        /// If the preferred path is not supported, HBAO+ will fall back to the first supported path.
        /// </summary>
        /// <remarks>When set from code, you need to call <see cref="ScriptableRendererData.SetDirty"/> to reflect the changes made</remarks>
        public EBlurRenderPath BlurRenderPath
        {
            get => _BlurPreferredRenderPath;
            set => _BlurPreferredRenderPath = value;
        }
        
        [SerializeField]
        [Tooltip("Preference order for the render path. Lower index means higher priority. Note that HBAO+ will ignore unsupported render paths on the target platform regardless of their priority.")]
        private EAORenderPath[] _AORenderPathPreferenceOrder = HBAOPlusUtility.DefaultRenderPrefsPC.ToArray();
        
        [SerializeField]
        [Tooltip("Preferred render path for the blur effect if enabled. Will fallback to the first supported render path if the target platform does not support the preferred render path.")]
        private EBlurRenderPath _BlurPreferredRenderPath = EBlurRenderPath.Automatic;

        /// <summary>
        /// Raster shader for HBAO+ automatically assigned.
        /// </summary>
        [SerializeField]
        [HideInInspector] // Automatically managed.
        [Tooltip("Shader to use for the HBAO effect")]
        private Shader _RasterShader;
        
        /// <summary>
        /// Compute shader for HBAO+ automatically assigned.
        /// </summary>
        [SerializeField]
        [HideInInspector] // Automatically managed.
        [Tooltip("Shader to use for the HBAO effect")]
        private ComputeShader _ComputeShader;
        
        /// <summary>
        /// Read-only reference of the render path preference order.
        /// </summary>
        private IReadOnlyList<EAORenderPath> _RenderPathPreferenceOrderReadOnly;
    }
}