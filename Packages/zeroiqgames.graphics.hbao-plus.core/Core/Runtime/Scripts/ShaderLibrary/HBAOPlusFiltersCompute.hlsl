#ifndef HBAO_PLUS_FILTERS_COMPUTE_HLSL
#define HBAO_PLUS_FILTERS_COMPUTE_HLSL

#include "Packages/zeroiqgames.graphics.hbao-plus.core/Core/Runtime/Scripts/ShaderLibrary/HBAOPlusInput.hlsl"

#ifdef HBAO_PLUS_BLUR_KERNEL
	#if (defined(_HBAO_PLUS_BLUR_FILTER_BILATERAL) || defined(_HBAO_PLUS_BLUR_FILTER_GAUSSIAN)) && defined(BLUR_DIRECTION_HORIZONTAL)
		#define MULTI_PASS_BLUR_HORIZONTAL
		#define MULTI_PASS_BLUR
	#elif (defined(_HBAO_PLUS_BLUR_FILTER_BILATERAL) || defined(_HBAO_PLUS_BLUR_FILTER_GAUSSIAN)) && defined(BLUR_DIRECTION_VERTICAL)
		#define MULTI_PASS_BLUR_VERTICAL
		#define MULTI_PASS_BLUR
	#elif defined(COMPRESSED_DOUBLE_PASS_BLUR)
		//#define 
	#elif defined(_HBAO_PLUS_BLUR_FILTER_BILATERAL) || defined(_HBAO_PLUS_BLUR_FILTER_GAUSSIAN)
		#error Blur direction not specified!
	#else
		#define SINGLE_PASS_BLUR
	#endif
#endif

// For multi-pass blurs we have the option of choosing either a line shape or a rectangle shape.
// Line shape has a much higher percentage of active threads at the cost of very bad cache locality and memory coalescing compatibility.
// Rectangle shape has better cache locality at the cost of substantially lower active threads percentage at larger blur radii.
// Overall line shape seems to have better performance at any blur radius larger than 1 when tested on RTX 3060
#ifdef MULTI_PASS_BLUR
	#if defined(GROUP_SHAPE_RECTANGLE) && _BLUR_KERNEL_RADIUS == 4
		#define GROUP_LARGE_EDGE_SIZE 128 // Active thread count would be 120
		#define GROUP_SMALL_EDGE_SIZE 6
	#elif defined(GROUP_SHAPE_RECTANGLE) && (_BLUR_KERNEL_RADIUS == 2 || _BLUR_KERNEL_RADIUS == 3) 
		#define GROUP_LARGE_EDGE_SIZE 64 // Active thread count would be 60 or 58
		#define GROUP_SMALL_EDGE_SIZE 12
	#elif defined(GROUP_SHAPE_RECTANGLE) && _BLUR_KERNEL_RADIUS == 1 
		#define GROUP_LARGE_EDGE_SIZE 32 // Active thread count would be 30
		#define GROUP_SMALL_EDGE_SIZE 24
	#elif defined(GROUP_SHAPE_LINE)
		#define GROUP_LARGE_EDGE_SIZE 128
		#define GROUP_SMALL_EDGE_SIZE 1
	#else
		#error Group shape not defined! Please define either GROUP_SHAPE_RECTANGLE or GROUP_SHAPE_LINE
	#endif
#endif

#if defined(MULTI_PASS_BLUR_HORIZONTAL)
	/**
	 * @brief number of kernel threads in X direction
	 */
	#define BLUR_NUM_THREADS_X GROUP_LARGE_EDGE_SIZE

	/**
	 * @brief number of kernel threads in Y direction
	 */
	#define BLUR_NUM_THREADS_Y GROUP_SMALL_EDGE_SIZE

	/**
	 * @brief number of kernel threads in Z direction
	 */
	#define BLUR_NUM_THREADS_Z 1
#elif defined(MULTI_PASS_BLUR_VERTICAL)
	/**
	 * @brief number of kernel threads in X direction
	 */
	#define BLUR_NUM_THREADS_X GROUP_SMALL_EDGE_SIZE

	/**
	 * @brief number of kernel threads in Y direction
	 */
	#define BLUR_NUM_THREADS_Y GROUP_LARGE_EDGE_SIZE

	/**
	 * @brief number of kernel threads in Z direction
	 */
	#define BLUR_NUM_THREADS_Z 1
#elif defined(COMPRESSED_DOUBLE_PASS_BLUR) 
	/**
	* @brief number of kernel threads in X direction
	*/
	#define BLUR_NUM_THREADS_X 32

	/**
	* @brief number of kernel threads in Y direction
	*/
	#define BLUR_NUM_THREADS_Y 24

	/**
	* @brief number of kernel threads in Z direction
	*/
	#define BLUR_NUM_THREADS_Z 1
#else // Kawase blur
	/**
	* @brief number of kernel threads in X direction
	*/
	#define BLUR_NUM_THREADS_X 8

	/**
	* @brief number of kernel threads in Y direction
	*/
	#define BLUR_NUM_THREADS_Y 8

	/**
	* @brief number of kernel threads in Z direction
	*/
	#define BLUR_NUM_THREADS_Z 1
#endif

/**
 * @brief number of kernel threads in all 3 directions in their respective component
 */
static const uint3 BLUR_NUM_THREADS = uint3(BLUR_NUM_THREADS_X, BLUR_NUM_THREADS_Y, BLUR_NUM_THREADS_Z);

#if defined(MULTI_PASS_BLUR_HORIZONTAL)

	/**
	 * @brief Number of threads in each side of the thread group that don't write to render target texture
	 * @remarks Since screen pixel sizes are usually a multiple of 24 (e.g., 720, 1080, 1920, etc.) we always use a halo length of 4 on each side, so our active thread count would be 120
	 */
	static const uint3 HALO_RADIUS =  
	#ifdef GROUP_SHAPE_LINE
		uint3(4, 0, 0);
	#else
		uint3(BLUR_KERNEL_RADIUS, 0, 0);
	#endif
	
	/**
	 * @brief Smallest thread ID that is inside the active zone (not in the edge threads)
	 * @remarks Since screen pixel sizes are usually a multiple of 24 (e.g., 720, 1080, 1920, etc.) we always use a halo length of 4 on each side, so our active thread count would be 120
	 */
	static const uint3 MIN_INNER_ID = HALO_RADIUS;

	/**
	 * @brief Biggest thread ID that is inside the active zone (not in the edge threads)
	 * @remarks Since screen pixel sizes are usually a multiple of 24 (e.g., 720, 1080, 1920, etc.) we always use a halo length of 4 on each side, so our active thread count would be 120
	 */
	static const uint3 MAX_INNER_ID = BLUR_NUM_THREADS - HALO_RADIUS;

	/**
	 * @brief Number of active threads (threads not in the outer sampling-only zone) in each direction
	 */
	static const uint3 INNER_THREAD_COUNT = MAX_INNER_ID - MIN_INNER_ID;
#elif defined(MULTI_PASS_BLUR_VERTICAL)
	/**
	 * @brief Number of threads in each side of the thread group that don't write to render target texture
	 * @remarks Since screen pixel sizes are usually a multiple of 24 (e.g., 720, 1080, 1920, etc.) we always use a halo length of 4 on each side, so our active thread count would be 120
	 */
	static const uint3 HALO_RADIUS = 
	#ifdef GROUP_SHAPE_LINE
		uint3(0, 4, 0);
	#else
		uint3(0, BLUR_KERNEL_RADIUS, 0);
	#endif

	/**
	 * @brief Smallest thread ID that is inside the active zone (not in the edge threads)
	 * @remarks Since screen pixel sizes are usually a multiple of 24 (e.g., 720, 1080, 1920, etc.) we always use a halo length of 4 on each side, so our active thread count would be 120
	 */
	static const uint3 MIN_INNER_ID = HALO_RADIUS;

	/**
	 * @brief Biggest thread ID that is inside the active zone (not in the edge threads)
	 * @remarks Since screen pixel sizes are usually a multiple of 24 (e.g., 720, 1080, 1920, etc.) we always use a halo length of 4 on each side, so our active thread count would be 120
	 */
	static const uint3 MAX_INNER_ID = BLUR_NUM_THREADS - HALO_RADIUS;

	/**
	 * @brief Number of active threads (threads not in the outer sampling-only zone) in each direction
	 */
	static const uint3 INNER_THREAD_COUNT = MAX_INNER_ID - MIN_INNER_ID;
#elif defined(COMPRESSED_DOUBLE_PASS_BLUR) // Double pass blur done in one kernel dispatch
	/**
	* @brief Number of threads in each side of the thread group that don't write to render target texture
	* @remarks Since screen pixel sizes are usually a multiple of 24 (e.g., 720, 1080, 1920, etc.) we always use a halo length of 4 on each side, so our active thread count would be 120
	*/
	static const uint3 HALO_RADIUS = uint3(BLUR_KERNEL_RADIUS, BLUR_KERNEL_RADIUS, 0);

	/**
	 * @brief Smallest thread ID that is inside the active zone (not in the edge threads)
	 * @remarks Since screen pixel sizes are usually a multiple of 24 (e.g., 720, 1080, 1920, etc.) we always use a halo length of 4 on each side, so our active thread count would be 120
	 */
	static const uint3 MIN_INNER_ID = HALO_RADIUS;

	/**
	 * @brief Biggest thread ID that is inside the active zone (not in the edge threads)
	 * @remarks Since screen pixel sizes are usually a multiple of 24 (e.g., 720, 1080, 1920, etc.) we always use a halo length of 4 on each side, so our active thread count would be 120
	 */
	static const uint3 MAX_INNER_ID = BLUR_NUM_THREADS - HALO_RADIUS;

	/**
	 * @brief Number of active threads (threads not in the outer sampling-only zone) in each direction
	 */
	static const uint3 INNER_THREAD_COUNT = MAX_INNER_ID - MIN_INNER_ID;
#endif

/**
 * @brief Checks whether if a thread corresponds to a pixel that is only read from by other pixels and not written into (halo thread)
 * @param GroupThreadID ID of the thread in its thread group
 * @return True if a halo thread, false otherwise
 */
bool IsHaloThread(uint3 GroupThreadID)
{
#if defined(MULTI_PASS_BLUR) || defined(COMPRESSED_DOUBLE_PASS_BLUR)
	return any(GroupThreadID.xy < MIN_INNER_ID.xy || GroupThreadID.xy >= MAX_INNER_ID.xy);
#else
	return false;
#endif
}

/**
 * @brief Checks whether if a thread corresponds to a pixel that is only read from by other pixels and not written into (halo thread) on the Y-axis
 * @param GroupThreadID ID of the thread in its thread group
 * @return True if a halo thread, false otherwise
 * @remarks Note that a thread can be on the halo on the X-axis, and this function will return false.
 */
bool IsVerticalHaloThread(uint3 GroupThreadID)
{
#if defined(MULTI_PASS_BLUR) || defined(COMPRESSED_DOUBLE_PASS_BLUR)
	return GroupThreadID.y < MIN_INNER_ID.y || GroupThreadID.y >= MAX_INNER_ID.y;
#else
	return false;
#endif
}

/**
 * @brief Checks whether if a thread corresponds to a pixel that is only read from by other pixels and not written into (halo thread) on the X-axis
 * @param GroupThreadID ID of the thread in its thread group
 * @return True if a halo thread, false otherwise
 * @remarks Note that a thread can be on the halo on the Y-axis, and this function will return false.
 */
bool IsHorizontalHaloThread(uint3 GroupThreadID)
{
#if defined(MULTI_PASS_BLUR) || defined(COMPRESSED_DOUBLE_PASS_BLUR)
	return GroupThreadID.x < MIN_INNER_ID.x || GroupThreadID.x >= MAX_INNER_ID.x;
#else
	return false;
#endif
}

/**
 * @brief Get the global id (x and y) of the pixel on the screen. The lower left pixel would be (0,0), and the upper right pixel would be (ScreenWidth - 1, ScreenHeight - 1)
 * @param GroupID ID of the thread group
 * @param GroupThreadID ID of the thread inside the thread group
 * @param DispatchThreadID Global ID of the thread
 * @return ID of the pixel the thread corresponds to on the screen
 */
uint2 GetGlobalPixelID(uint3 GroupID, uint3 GroupThreadID, uint3 DispatchThreadID)
{
#if defined(MULTI_PASS_BLUR_HORIZONTAL)
	return uint2(mad(GroupID.x, INNER_THREAD_COUNT.x, GroupThreadID.x - HALO_RADIUS.x), DispatchThreadID.y);
#elif defined(MULTI_PASS_BLUR_VERTICAL)
	return uint2(DispatchThreadID.x, mad(GroupID.y, INNER_THREAD_COUNT.y, GroupThreadID.y - HALO_RADIUS.y));
#elif defined(COMPRESSED_DOUBLE_PASS_BLUR)
	return mad(GroupID.xy, INNER_THREAD_COUNT.xy, GroupThreadID.xy - HALO_RADIUS.xy);
#else // Single-pass
	return DispatchThreadID.xy;
#endif
}

/**
 * @brief Get normalized screen space UV of the specified pixel
 * @param GlobalPixelID Pixel ID in screen space. The lower left pixel would be (0,0), and the upper right pixel would be (ScreenWidth - 1, ScreenHeight - 1)
 * @return Normalized UV of the specified pixel ID in screen space
 */
float2 GetPixelNormalizedScreenSpaceUV(uint2 GlobalPixelID)
{
#if defined(BLUR_TRANSPOSE_WRITE) && defined(FINAL_BLUR_PASS)
	// Intermediate texture is transposed by the previous pass
	return (float2(GlobalPixelID) + 0.5) * _FullResDimensions.wz;
#else
	return (float2(GlobalPixelID) + 0.5) * _FullResDimensions.zw;
#endif
}

/**
 * @brief Get normalized screen space UV and global pixel ID of the thread
 * @param GroupID ID of the group that the thread belongs to
 * @param GroupThreadID ID of the thread in its thread group
 * @param DispatchThreadID Global ID of the thread
 * @param GlobalPixelID ID of the pixel the thread corresponds to on the screen
 * @return Normalized UV of the pixel this thread corresponds to in screen space
 */
float2 GetThreadNormalizedScreenSpaceUV(uint3 GroupID, uint3 GroupThreadID, uint3 DispatchThreadID, out uint2 GlobalPixelID)
{
	GlobalPixelID = GetGlobalPixelID(GroupID, GroupThreadID, DispatchThreadID);
	
	return GetPixelNormalizedScreenSpaceUV(GlobalPixelID);
}

/**
 * @brief Get the flattened index of the thread in its thread group from its 3d group thread ID. Can be used to access group shared memory. Unique per group thread ID.
 * @param GroupThreadID ID of the thread in its thread group
 * @return Row-major index if the thread in its thread group
 */
uint GetGroupThreadIndex(uint3 GroupThreadID)
{
#if defined(MULTI_PASS_BLUR_VERTICAL) && defined(GROUP_SHAPE_LINE)
	return GroupThreadID.y;
#elif defined(MULTI_PASS_BLUR_HORIZONTAL) && defined(GROUP_SHAPE_LINE)
	return GroupThreadID.x;
#else
	// Z thread count is always one, so we don't include it in the calculation
	return mad(GroupThreadID.y, BLUR_NUM_THREADS.x, GroupThreadID.x);
#endif
}

#endif

