using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

// ReSharper disable MemberCanBePrivate.Global
namespace ZeroIQGames.Graphics.HBAOPlus.Core.Renderer
{
    internal static partial class HBAOPlusUtility
    {
        /// <summary>
        /// The default render path preference order used for platforms other than mobile if the user set value is not valid.
        /// </summary>
        public static readonly IReadOnlyList<EAORenderPath> DefaultRenderPrefsPC = new List<EAORenderPath>
        {
            EAORenderPath.Compute,
            EAORenderPath.Raster,
            EAORenderPath.RasterNoGeometry,
        };
        
        /// <summary>
        /// The default render path preference order used for mobile platforms  if the user set value is not valid.
        /// </summary>
        public static readonly IReadOnlyList<EAORenderPath> DefaultRenderPrefsMobile = new List<EAORenderPath>
        {
            EAORenderPath.Raster,
            EAORenderPath.Compute,
            EAORenderPath.RasterNoGeometry,
        };
        
        /// <summary>
        /// Check if a list is valid (not null) and contains all and only three values in the render path enum with no duplicates
        /// </summary>
        /// <param name="RenderPathPrefs">List to verify</param>
        /// <returns>True if all values are present, false otherwise</returns>
        public static bool IsRenderPathPreferenceListValid(IReadOnlyList<EAORenderPath> RenderPathPrefs)
        {
            if (RenderPathPrefs is not { Count: 3 })
            {
                return false;
            }
            
            Span<bool> Seen = stackalloc bool[3];
            foreach (EAORenderPath path in RenderPathPrefs)
            {
                // If already true it's a duplicate
                if (Seen[(int)path])
                {
                    return false;
                }
                
                // Set seen to true
                Seen[(int)path] = true;
            }
            
            return true;
        }

        /// <summary>
        /// Get the supported render path with the highest priority based on the render path preference order.
        /// </summary>
        /// <param name="RenderPathPrefs">Priority of render paths with smaller index showing higher priority</param>
        /// <returns>The first supported render path in <paramref name="RenderPathPrefs"/></returns>
        /// <remarks>If <paramref name="RenderPathPrefs"/> is not valid, uses the default preference order for the current platform</remarks>
        /// <seealso cref="IsRenderPathPreferenceListValid"/>
        /// <seealso cref="DefaultRenderPrefsPC"/>
        /// <seealso cref="DefaultRenderPrefsMobile"/>
        public static EAORenderPath GetAORenderPath(IReadOnlyList<EAORenderPath> RenderPathPrefs)
        {
            if (!IsRenderPathPreferenceListValid(RenderPathPrefs))
            {
                Debug.LogWarning("RenderPathPrefs value is not valid. Using default value.");
                
                RenderPathPrefs = _GetDefaultRenderPrefs();
            }

            foreach (EAORenderPath Preference in RenderPathPrefs)
            {
                if (DoesPlatformSupportRenderPath(Preference))
                {
                    return Preference;
                }
            }
            
            // Safety check. Should never happen
            Assert.IsTrue(false, "No supported render path found.");
            
            return EAORenderPath.RasterNoGeometry;
        }

        /// <summary>
        /// Get the blur render path that should be used based on the preferred path and the current platform compatibility.
        /// </summary>
        /// <param name="PreferredPath">Render path preferred by the HBAO+ settings</param>
        /// <param name="BlurQuality">Quality of the blur applied which affects the algorithm used</param>
        /// <param name="SharpnessSource">Source of the blur sharpening</param>
        /// <param name="BlurRadius">Radius of the blur that will be applied</param>
        /// <returns>The render path that should be used</returns>
        /// <exception cref="ArgumentOutOfRangeException">Throws if <paramref name="PreferredPath"/> is an invalid value.</exception>
        public static EBlurRenderPath GetBlurRenderPath(EBlurRenderPath PreferredPath, EQuality BlurQuality, EBlurSharpnessSource SharpnessSource, int BlurRadius)
        {
            switch (PreferredPath)
            {
                case EBlurRenderPath.ComputeSinglePass when SystemInfo.supportsComputeShaders:
                    return EBlurRenderPath.ComputeSinglePass;
                case EBlurRenderPath.ComputeMultiPass when SystemInfo.supportsComputeShaders:
                    return EBlurRenderPath.ComputeMultiPass;
                case EBlurRenderPath.Raster:
                    return EBlurRenderPath.Raster;
                case EBlurRenderPath.Automatic:
                    if (!SystemInfo.supportsComputeShaders)
                    {
                        // Compute not supported, fallback to raster
                        return EBlurRenderPath.Raster;
                    }

                    if (BlurQuality is not EQuality.High)
                    {
                        // We are bottle-necked by bandwidth not math, so raster is faster
                        return EBlurRenderPath.Raster;
                    }

                    if (SharpnessSource is EBlurSharpnessSource.Depth)
                    {
                        // We are bottle-necked by bandwidth not math, so raster is faster
                        return EBlurRenderPath.Raster;
                    }

                    if (SystemInfo.deviceType is not DeviceType.Handheld)
                    {
                        // Raster is typically faster than compute for 2 pixels or fewer radius 
                        return BlurRadius > 2 ? EBlurRenderPath.ComputeMultiPass : EBlurRenderPath.Raster;
                    }
                    else
                    {
                        // For handheld devices, ALU throughput is typically lower and more math can also result in thermal throttling.
                        // So even with kernel size 2, we use compute over raster for handheld devices
                        return BlurRadius > 1 ? EBlurRenderPath.ComputeMultiPass : EBlurRenderPath.Raster;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(PreferredPath), PreferredPath, "Invalid value for PreferredPath.");
            }
        }
        
        /// <summary>
        /// Checks whether if the runtime platform supports the specified render path
        /// </summary>
        /// <param name="RenderPath">Render path to check the platform support for</param>
        /// <returns>True if supported, false otherwise</returns>
        /// <exception cref="ArgumentOutOfRangeException">Throws if <paramref name="RenderPath"/> is an invalid value.</exception>
        public static bool DoesPlatformSupportRenderPath(EAORenderPath RenderPath)
        {
            return RenderPath switch
            {
                EAORenderPath.Compute          => SystemInfo.supportsComputeShaders,
                EAORenderPath.Raster           => SystemInfo.supportsGeometryShaders,
                EAORenderPath.RasterNoGeometry => true,
                _                                => throw new ArgumentOutOfRangeException(nameof(RenderPath), RenderPath, "Invalid value for RenderPath."),
            };
        }

        /// <summary>
        /// Gets the default render path preference order based on the current platform.
        /// </summary>
        /// <returns>Default render path preference</returns>
        private static IReadOnlyList<EAORenderPath> _GetDefaultRenderPrefs()
        {
            return Application.isMobilePlatform ? DefaultRenderPrefsMobile : DefaultRenderPrefsPC;
        }
    }
}