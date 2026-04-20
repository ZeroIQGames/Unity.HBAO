using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace ZeroIQGames.Graphics.HBAOPlus.Core.Renderer
{
    /// <summary>
    /// Struct to access sets of random but pre-set jitter values.
    /// </summary>
    internal struct HBAOPlusJitter
    {
        /// <summary>
        /// Get the jitter value for a given slice index based on the current random view index. 
        /// </summary>
        /// <param name="SliceIndex">Slice index to get the random value for. Valid range is [0, 16)</param>
        /// <returns>A random vector value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 GetJitterValue(int SliceIndex) => _JitterValues[math.mad(_CurrentRandomViewIndex, 8, SliceIndex)];

        /// <summary>
        /// Populate the passed array of length at least 16 with the current random view jitter values.
        /// </summary>
        /// <param name="JitterView">Jitter Array to populate</param>
        /// <returns>The same <paramref name="JitterView"/> array passed as the argument</returns>
        /// <exception cref="ArgumentException">Throws if the length of the array is not at least 16</exception>
        public Vector4[] GetJitterViewNoAlloc(Vector4[] JitterView)
        {
            if (JitterView is not { Length: >= 16})
            {
                throw new ArgumentException("JitterView must be non-null with length larger than or equal to 16");
            }
            Array.Copy(_JitterValues, _CurrentRandomViewIndex * 8, JitterView, 0, 16);

            return JitterView;
        }

        /// <summary>
        /// Move to the next random view changing the jitter value for all slices.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceRandomView() => _CurrentRandomViewIndex = (_CurrentRandomViewIndex + 1) % 8;

        static HBAOPlusJitter()
        {
            var Rand = new Random();
            Rand.InitState();
            for (int Index = 0; Index < _JitterValues.Length; Index++)
            {
                float Rand1 = Rand.NextFloat();
                float Rand2 = Rand.NextFloat();
                float Rand3 = Rand.NextFloat();
                float Angle = 2.0f * math.PI * Rand1 / 8; // Use random rotation angles in [0,2PI/NUM_DIRECTIONS).
                // Set random components
                _JitterValues[Index].x = math.cos(Angle);
                _JitterValues[Index].y = math.sin(Angle);
                _JitterValues[Index].z = Rand2;
                _JitterValues[Index].w = Rand3;
            }
        }

        /// <summary>
        /// Random jitter values generated once at the startup.
        /// </summary>
        private static readonly Vector4[] _JitterValues = new Vector4[4 * 4 * 8];

        /// <summary>
        /// Texture array index
        /// </summary>
        private int _CurrentRandomViewIndex;
    }
}