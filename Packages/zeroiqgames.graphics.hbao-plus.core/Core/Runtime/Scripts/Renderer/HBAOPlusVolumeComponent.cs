using UnityEngine;
using UnityEngine.Rendering;

namespace ZeroIQGames.Graphics.HBAOPlus.Core.Renderer
{
    [VolumeComponentMenu("Lighting/HBAO+")]
    public class HBAOPlusVolumeComponent : VolumeComponent
    {
        [InspectorName("Enabled")]
        public BoolParameter bEnabled = new(HBAOPlusDefaultSettings.ENABLED);
        
        [InspectorName("Intensity")]
        [Tooltip("Intensity exponent of the raw calculated AO effect. Raw value is raised to the power of this value. Higher value means more intense AO effect. Note that the intensity change is neither uniform nor linear.")]
        public ClampedFloatParameter IntensityExponent = new(HBAOPlusDefaultSettings.INTENSITY_EXPONENT, 1.0f, 4.0f);
       
        [InspectorName("Radius")]
        [Tooltip("Radius of AO effect")]
        public MinFloatParameter AORadius = new(HBAOPlusDefaultSettings.AO_RADIUS, 0.0f);
        
        [InspectorName("Direct Lighting Strength")]
        [Tooltip("Strength of the AO applied to direct lights compared to the ambient light (GI/SH/etc)")]
        public ClampedFloatParameter DirectLightingStrength = new(HBAOPlusDefaultSettings.DIRECT_LIGHTING_STRENGTH, 0.0f, 1.0f);
        
        [Header("AO Settings")]
        [InspectorName("Background AO")]
        [Tooltip("Whether if background AO should be enabled to add large scale occlusion in the distance")]
        public BoolParameter bBackgroundAO = new(HBAOPlusDefaultSettings.ENABLE_BACKGROUND_AO);
        
        [Indent]
        [InspectorName("Background View Depth")]
        [Tooltip("The distance after which background AO setting will be applied")]
        public MinFloatParameter BackgroundAOViewDepth = new(HBAOPlusDefaultSettings.BACKGROUND_VIEW_DEPTH, 0.0f);
        
        [InspectorName("Foreground AO")]
        [Tooltip("Whether if foreground AO should be enabled to clamp the AO effect on the objects in the foreground")]
        public BoolParameter bForegroundAO = new(HBAOPlusDefaultSettings.ENABLE_FOREGROUND_AO);
        
        [Indent]
        [InspectorName("Foreground View Depth")]
        [Tooltip("The distance where closer objects AO will be clamped")]
        public MinFloatParameter ForegroundAOViewDepth = new(HBAOPlusDefaultSettings.FOREGROUND_AO_VIEW_DEPTH, 0.0f);
        
        [InspectorName("Small Scale AO")]
        [Tooltip("Small scale AO strength. The higher the value, the darker the AO effect will be on the objects --------------")]
        public ClampedFloatParameter SmallScaleAO = new (HBAOPlusDefaultSettings.SMALL_SCALE_AO_FACTOR, 0.0f, 2.0f);
        
        [InspectorName("Large Scale AO")]
        [Tooltip("Large scale AO strength. The higher the value, the darker the AO effect will be on the objects --------------")]
        public ClampedFloatParameter LargeScaleAO = new (HBAOPlusDefaultSettings.LARGE_SCALE_AO_FACTOR, 0.0f, 2.0f);
        
        [InspectorName("Bias")]
        [AdditionalProperty]
        [Tooltip("Value to hide low tesselation artifacts.")]
        public ClampedFloatParameter Bias = new (HBAOPlusDefaultSettings.TESSELATION_BIAS, 0.0f, 0.5f);
        
        [Header("Blur Settings")]
        [InspectorName("Enabled")]
        public BoolParameter bBlurEnabled = new(HBAOPlusDefaultSettings.BLUR_ENABLED);
        
        [InspectorName("Uniform Sharpness")]
        [Tooltip("Uniform blur sharpness applied at all distances if depth dependent sharpness is disabled.")]
        public ClampedFloatParameter UniformBlurSharpness = new(HBAOPlusDefaultSettings.BLUR_SHARPNESS, 1.0f, 16.0f);
        
        [InspectorName("Depth Dependent Sharpness")]
        [Tooltip("If enabled, blur sharpness will be adjusted based on distance.")]
        public BoolParameter bDepthDependentBlurSharpness = new(HBAOPlusDefaultSettings.DEPTH_DEPENDENT_BLUR_SHARPNESS);
        
        [Indent]
        [InspectorName("Foreground Sharpness")]
        public ClampedFloatParameter ForegroundBlurSharpness = new(HBAOPlusDefaultSettings.BLUR_SHARPNESS, 1.0f, 16.0f);
        
        [Indent(2)]
        [InspectorName("Foreground View Depth")]
        public MinFloatParameter ForegroundBlurViewDepth = new(HBAOPlusDefaultSettings.FOREGROUND_BLUR_VIEW_DEPTH, 0.0f);
        
        [Indent]
        [InspectorName("Background Sharpness")]
        public ClampedFloatParameter BackgroundBlurSharpness = new(HBAOPlusDefaultSettings.BLUR_SHARPNESS, 1.0f, 16.0f);
        
        [Indent(2)]
        [InspectorName("Background View Depth")]
        public MinFloatParameter BackgroundBlurViewDepth = new(HBAOPlusDefaultSettings.BACKGROUND_BLUR_VIEW_DEPTH, 0.0f);
    }
    
    internal static class HBAOPlusDefaultSettings
    {
        /// <summary>
        /// Whether if HBAO+ effect is enabled
        /// </summary>
        public const bool ENABLED = false;
        
        /// <summary>
        /// Radius of AO effect
        /// </summary>
        public const float AO_RADIUS = 0.3f;
        
        /// <summary>
        /// Intensity exponent of the raw calculated AO effect. Raw value is raised to the power of this value.
        /// </summary>
        public const float INTENSITY_EXPONENT = 1.5f;
        
        /// <summary>
        /// Strength of the AO applied to direct light compared to the ambient light (GI/SH/etc)
        /// </summary>
        public const float DIRECT_LIGHTING_STRENGTH = 0.5f;
        
        /// <summary>
        /// Whether if background AO should be enabled to add large scale occlusion in the distance
        /// </summary>
        public const bool ENABLE_BACKGROUND_AO = false;

        /// <summary>
        /// The distance after which background AO setting will be applied
        /// </summary>
        public const float BACKGROUND_VIEW_DEPTH = 200.0f;
        
        /// <summary>
        /// weather if foreground AO should be enabled to clamp the AO effect on the objects in the foreground
        /// </summary>
        public const bool ENABLE_FOREGROUND_AO = false;
        
        /// <summary>
        /// The distance where closer objects AO will be clamped
        /// </summary>
        public const float FOREGROUND_AO_VIEW_DEPTH = 2.0f;

        /// <summary>
        /// Strength of the small scale AO effect
        /// </summary>
        public const float SMALL_SCALE_AO_FACTOR = 1.0f;

        /// <summary>
        /// Strength of the large-scale AO effect
        /// </summary>
        public const float LARGE_SCALE_AO_FACTOR = 1.0f;

        /// <summary>
        /// Value to hide low-tessellation artifacts.
        /// </summary>
        public const float TESSELATION_BIAS = 0.25f;
        
        /// <summary>
        /// Default settings of the blur.
        /// </summary>
        public const bool BLUR_ENABLED = true;
        
        /// <summary>
        /// Whether if adaptive blur should be used with different sharpness based on distance.
        /// </summary>
        public const bool DEPTH_DEPENDENT_BLUR_SHARPNESS = false;
        
        /// <summary>
        /// Default value for blur sharpness
        /// </summary>
        public const float BLUR_SHARPNESS = 2.0f;
        
        public const float FOREGROUND_BLUR_VIEW_DEPTH = 2.0f;
        
        public const float BACKGROUND_BLUR_VIEW_DEPTH = 200.0f;
    }
}