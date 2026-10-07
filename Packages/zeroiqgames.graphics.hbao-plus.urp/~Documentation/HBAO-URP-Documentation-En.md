# HBAO+ URP Documentation

## Installation
See [here](../../../readme.md).

## Usage
1. __Add the Renderer Feature__
    * Select the Universal Renderer Data asset
    * Press the Add Renderer Feature button
    * From the menu that opens, select `Horizon-Based Ambient Occlusion`
2. __Add a Volume__
   * Add a Volume component to the scene
   * Assign a Volume Profile to the component
   * In the Volume Profile inspector, press the Add Override button
   * Select `Lighting -> HBAO+`
   * Override the `Enabled` setting and turn it on

## Appearance Settings

### AO Settings
* **Enabled**
  * Whether the effect is on. Unchecked, HBAO+ is disabled
* **Intensity**
  * Overall strength of the effect
* **Radius** (effect radius)
  * Maximum distance at which AO is gathered from the surrounding geometry
  * Unit: meters
* **Direct Lighting Strength**
  * Proportion of AO applied to direct lights
  * At 1, the ambient light and ordinary light sources receive the same AO
* **Background AO** (minimum AO in the background)
  * Keeps the AO pixel radius of very distant objects from falling below a fixed value
  * Objects farther than `Background View Depth` use the same pixel radius as `Background View Depth`
  * AO becomes stronger on objects farther than `Background View Depth`
* **Foreground AO** (maximum AO in the foreground)
  * Keeps the AO pixel radius of objects very close to the camera from rising above a fixed value
  * Objects nearer than `Foreground View Depth` use the same pixel radius as `Foreground View Depth`
  * AO becomes weaker on objects nearer than `Foreground View Depth`
* **Small Scale AO**
  * Coefficient of the AO received from objects within one quarter of `Radius`
* **Large Scale AO**
  * Coefficient of the AO received from objects beyond one quarter of `Radius`
* **Bias**
  * Value used to fix micro-occlusion artifacts
  * The higher it is set, the greater the minimum occlusion required for AO to appear

### Blur Settings
* **Enabled**
  * Whether the blur effect is on. Unchecked, the blur effect is disabled
* **Uniform Sharpness**
  * Coefficient of the maximum depth difference at which the bilateral filter treats two pixels as belonging to different objects
  * The larger it is, the smaller that maximum depth difference becomes
  * Used only when `Depth Dependent Sharpness` is disabled
  * Ignored at any blur quality other than High
* **Depth Dependent Sharpness**
  * Interpolates the applied `Sharpness` by distance from the camera
* **Foreground Sharpness**
  * `Sharpness` value applied to objects nearer than `Foreground View Depth`
  * Used only when `Depth Dependent Sharpness` is enabled
  * `Sharpness` is interpolated for objects between `Foreground View Depth` and `Background View Depth`
* **Background Sharpness**
  * `Sharpness` value applied to objects farther than `Background View Depth`
  * Used only when `Depth Dependent Sharpness` is enabled
  * `Sharpness` is interpolated for objects between `Foreground View Depth` and `Background View Depth`

## Quality Settings
Quality settings are adjusted on the [Renderer Feature](#usage).

* **AO Quality**
   * Three settings: High (`Absolute Cinema`), Medium (`Lowkey Good`), Low (`FPSmaxxing`)
   * Higher settings raise the number of rays and the number of steps per ray, at a higher cost
   * The Low setting can produce artifacts
* **Blur Quality**
  * Three settings: High (`Absolute Cinema`), Medium (`Lowkey Good`), Low (`FPSmaxxing`)
    * High uses the bilateral filter
      * Selecting High enables a `Sharpness Source` setting
      * `Sharpness Source` decides which source must match for a neighbouring pixel to be blended in when its AO is blurred
      * `Depth` blends only when the difference in distance from the camera is within a fixed value
      * `Normal` blends only when the difference in surface normal is within a fixed value
      * `Both` blends only when both of the conditions above are met
      * Cost order: Depth (cheapest) -> Normal -> Both (most expensive)
    * Medium uses the Gaussian filter
    * Low uses the Kawase filter
    * Medium/Low blend every neighbouring pixel regardless of its depth or normal
* **Geometry Source**
  * Source of the scene geometry used to calculate AO
  * `Depth Only` uses only the camera depth buffer and reconstructs normals from the depth data
  * `Depth + Normal` uses both the camera depth buffer and the normals
  * Depending on the situation `Depth Only` can be cheaper, but `Depth + Normal` is recommended
* **Normal Reconstruction Quality**
  * Three settings: High (`Absolute Cinema`), Medium (`Lowkey Good`), Low (`FPSmaxxing`)
  * Used only when `Geometry Source` is set to `Depth Only`
* **Blur Radius**
  * Unit: pixels
  * Specifies how many surrounding pixels the blur effect uses
  * Cost rises linearly with the pixel count
  * 2 Pixels has the best cost-to-quality ratio
* **Use Surface Slope For Depth Sharpness**
  * When enabled, HBAO+ uses the surface slope when calculating blur edges
* **Use Per Frame Random Jitter**
  * When checked, the ray direction changes every frame
  * Reduces banding artifacts and similar when temporal effects (TAA and so on) are used
  * It introduces noise, so disabling it is recommended when it is not needed
* **AO Render Path Preference Order**
  * Specifies the priority order of the render paths used to calculate AO
  * The supported path with the highest priority is used
  * A path the device does not support is never used, however high its priority
  * If all three paths are not specified, the setting is ignored and the default priority is used
* **Blur Preferred Render Path**
  * Render path used for the blur effect
  * When set to Automatic, the setting judged to perform best is chosen automatically
