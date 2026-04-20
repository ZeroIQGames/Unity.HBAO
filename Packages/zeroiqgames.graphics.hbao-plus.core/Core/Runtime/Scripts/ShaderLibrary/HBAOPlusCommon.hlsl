#ifndef HBAO_PLUS_COMMON_HLSL
#define HBAO_PLUS_COMMON_HLSL

/**
 * @brief Normalized linear depth of the skybox
 */
static const float SKY_NORMALIZED_DEPTH_VALUE = 0.999f;

/**
 * @brief UV of the top left area of the view port
 */
static const float2 VIEWPORT_TOP_LEFT = float2(0.0, 0.0);

/**
 * @brief Constant half zero value
 */
static const half2 HALF_ZERO = half(0.0);

/**
 * @brief Constant half one value
 */
static const half2 HALF_ONE = half(1.0);

/**
 * @brief Constant half2 zero value
 */
static const half2 HALF2_ZERO = half2(0.0, 0.0);

/**
 * @brief Constant half2 one value
 */
static const half2 HALF2_ONE = half2(1.0, 1.0);

/**
 * @brief Constant half3 zero value
 */
static const half3 HALF3_ZERO = half3(0.0, 0.0, 0.0);


/**
 * @brief Constant half3 one value
 */
static const half3 HALF3_ONE = half3(1.0, 1.0, 1.0);

/**
 * @brief Constant half4 zero value
 */
static const half4 HALF4_ZERO = half4(0.0, 0.0, 0.0, 0.0);

/**
 * @brief Constant half4 one value
 */
static const half4 HALF4_ONE = half4(1.0, 1.0, 1.0, 1.0);



#endif
