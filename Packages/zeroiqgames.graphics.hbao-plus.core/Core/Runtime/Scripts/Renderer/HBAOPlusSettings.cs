using UnityEngine;

namespace ZeroIQGames.Graphics.HBAOPlus.Core.Renderer
{
    internal class HBAOPlusSettings
    {
        /// <summary>
        /// Raster shader parameters and keywords
        /// </summary>
        public HBAOPlusRasterShaderParams RasterShaderParams;
        
        /// <summary>
        /// Compute shader parameters and keywords
        /// </summary>
        public HBAOPlusComputeShaderParams ComputeShaderParams;
        
        /// <summary>
        /// Jitter instance used to generate random jitter values to prevent color banding artifacts.
        /// </summary>
        public HBAOPlusJitter Jitter;

        /// <summary>
        /// The first supported AO render path with the highest priority.
        /// </summary>
        public EAORenderPath AORenderPath;
        
        /// <summary>
        /// The first supported blur render path with the highest priority.
        /// </summary>
        public EBlurRenderPath BlurRenderPath;

        /// <summary>
        /// Whether if depth normal prepass should be used. If set false, normals will be reconstructed from depth.
        /// </summary>
        public bool bUseDepthNormal;
        
        #region Shaders and materials

        /// <summary>
        /// Raster passes material.
        /// </summary>
        public Material RasterPassMaterial;

        /// <summary>
        /// Compute passes shader.
        /// </summary>
        public ComputeShader ComputePassShader;
        
        #endregion

        #region Visual Settings

        /// <summary>
        /// Quality level of the AO affecting how many directions are ray marched and how many times each ray is sampled
        /// </summary>
        public EQuality AOQuality;
        
        /// <summary>
        /// Radius at which objects can occlude each other.
        /// </summary>
        public float AORadius;
        
        /// <summary>
        /// Intensity of the AO effect. Affects both small scale and large scale AO.
        /// </summary>
        public float IntensityExponent;

        /// <summary>
        /// Falloff strength of the AO effect. Higher values result in less AO from further objects.
        /// </summary>
        public float Falloff;
        
        /// <summary>
        /// Percentage of AO applied to direct light compared to ambient light.
        /// </summary>
        public float DirectLightingStrength;

        /// <summary>
        /// Whether if foreground AO should be enabled to cap the AO effect on the objects in the foreground.
        /// </summary>
        public bool bForegroundAOEnabled => ForegroundAOViewDepth > 0;
        
        /// <summary>
        /// View depth before which objects are considered foreground and their AO radius is capped.
        /// </summary>
        public float ForegroundAOViewDepth;
        
        /// <summary>
        /// Whether if background AO should be enabled to guarantee a minimum AO effect in the distance.
        /// </summary>
        public bool bBackgroundAOEnabled => BackgroundAOViewDepth > 0;
        
        /// <summary>
        /// View depth after which background AO is applied.
        /// </summary>
        public float BackgroundAOViewDepth;

        /// <summary>
        /// Bias for occlusion. Higher values result in less AO. Especially effective in preventing AO artifacts from micro surface details.
        /// </summary>
        public float Bias;

        /// <summary>
        /// Small Scale AO Strength. This affects AO due to immediate neighbors.
        /// </summary>
        public float SmallScaleAO;
        
        /// <summary>
        /// Large Scale AO Strength. This affects AO due to neighbors other than immediate neighbors.
        /// </summary>
        public float LargeScaleAO;

        /// <summary>
        /// Whether if blur should be applied to the AO.
        /// </summary>
        public bool bUseBlur;

        /// <summary>
        /// Whether if variable blur should be applied based on view depth.
        /// </summary>
        /// <seealso cref="BlurForegroundSharpness"/>
        /// <seealso cref="BlurBackgroundSharpness"/>
        /// <seealso cref="BlurForegroundViewDepth"/>
        /// <seealso cref="BlurBackgroundViewDepth"/>
        public bool bDepthDependentBlurSharpness;

        /// <summary>
        /// Quality of the blur applied to the coarse AO. Determines the blue kernel radius.
        /// </summary>
        public EQuality BlurQuality;
        
        /// <summary>
        /// Sharpness source for the blur when <seealso cref="BlurQuality"/> is set to <see cref="EQuality.High"/>.
        /// </summary>
        public EBlurSharpnessSource BlurSharpnessSource;

        /// <summary>
        /// Uniform blur sharpness value applied to blur pass if <see cref="bDepthDependentBlurSharpness"/> is false.
        /// </summary>
        public float BlurUniformSharpness;

        /// <summary>
        /// Sharpness of the blur applied to objects closer than <see cref="BlurForegroundViewDepth"/> to the camera.
        /// </summary>
        public float BlurForegroundSharpness;
        
        /// <summary>
        /// Sharpness of the blur applied to objects further than <see cref="BlurBackgroundViewDepth"/> from the camera.
        /// </summary>
        public float BlurBackgroundSharpness;

        /// <summary>
        /// Depth before which <see cref="BlurForegroundSharpness"/> is applied.
        /// </summary>
        /// <remarks>Between <see cref="BlurBackgroundViewDepth"/> and BlurForegroundViewDepth, blur value sharpness is linearly interpolated</remarks>
        public float BlurForegroundViewDepth;

        /// <summary>
        /// Depth after which <see cref="BlurBackgroundSharpness"/> is applied.
        /// </summary>
        /// <remarks>Between <see cref="BlurForegroundViewDepth"/> and BlurBackgroundViewDepth, blur sharpness value is linearly interpolated</remarks>
        public float BlurBackgroundViewDepth;
        
        /// <summary>
        /// Radius of blur kernel in pixels.
        /// </summary>
        public EBlurRadius BlurKernelRadius;

        #endregion
    }
}