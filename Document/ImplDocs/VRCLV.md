# Light Volumes: Specular Highlight Calculation from L1 Spherical Harmonics

## Overview of the SH Data Model

Light Volumes stores indirect lighting as **L1 Spherical Harmonics** split into four components:
- `L0` — ambient/omnidirectional color (RGB)
- `L1r`, `L1g`, `L1b` — directional vectors for Red, Green, and Blue channels respectively, where the **magnitude of each vector encodes light power** and the **direction encodes dominant light direction per color channel** [1](#2-0) 

These are sampled from a 3D texture atlas by unpacking three atlas samples and re-packing into the four SH components.

---

## Step 1: Retrieving SH Data

Before calculating speculars, your shader must first retrieve the SH components using one of the sampling functions.

### `LightVolumeSH()` — Primary function for dynamic objects
This is the main entry point. It automatically falls back to Unity's light probes (with de-ringing) if Light Volumes are not present in the scene. [2](#2-1) 

The de-ringing fallback to Unity light probes scales L1 components by `0.565` to suppress ringing artifacts from Bakery L1 bakes: [3](#2-2) 

### `LightVolumeAdditiveSH()` — For lightmapped geometry
Only samples additive volumes (returns zeros otherwise), intended to be added on top of lightmapped color: [4](#2-3) 

### L0-only variants (cheaper, no directionality)
- `LightVolumeSH_L0()` and `LightVolumeAdditiveSH_L0()` return only ambient color — useful for particles and fog where directional data isn't needed. [5](#2-4) 

---

## Step 2: The Two Specular Calculation Methods

### Method A: `LightVolumeSpecular()` — Per-channel (R/G/B) colored speculars

This is the **primary, richer specular method** recommended for avatars. It calculates **three independent specular highlights**, one for each color channel's L1 direction, producing colored multi-light speculars.

**How it works internally:**

1. **Reflection dot product per channel** — For each of R, G, B, it reflects the incoming view direction against the respective L1 direction vector and dots with the view direction to get a per-channel specular color contribution: [6](#2-5) 

2. **GGX Distribution** — It uses a GGX microfacet distribution for each channel's half-vector (computed from normalized L1 direction + view direction), with roughness derived as `1 - smoothness * 0.9`: [7](#2-6) 

3. **Blending colored and white speculars** — The final result lerps between a mix of `coloredSpecs + specs * L0` (ambient-tinted) and `coloredSpecs * 3` (purely colored), controlled by smoothness, scaled by `0.5`: [8](#2-7) 

**Two overloads available:**
- Taking `albedo`, `smoothness`, `metallic` — computes F0 internally via `lerp(0.04, albedo, metallic)`: [9](#2-8) 
- Taking a pre-computed `specColor` (F0/specular color directly): [10](#2-9) 

---

### Method B: `LightVolumeSpecularDominant()` — Single dominant direction specular

This **cheaper alternative** is better suited for static hard-surface PBR objects. It sums all three L1 vectors into a **single dominant direction**, computes one GGX lobe against it, and multiplies by L0 and F0: [11](#2-10) 

Also has two overloads (albedo/smoothness/metallic, or direct specColor).

---

## Step 3: Evaluating Diffuse (for context)

The `LightVolumeEvaluate()` function evaluates the full L1 SH per channel against a world normal for diffuse — it uses the classic `L0 + dot(L1, n)` formula: [12](#2-11) [13](#2-12) 

---

## Complete Integration Flow

```
Fragment shader:
   1. LightVolumeSH(worldPos, L0, L1r, L1g, L1b)
   2. diffuse = LightVolumeEvaluate(worldNormal, L0, L1r, L1g, L1b) * albedo * (1 - metallic)
   3. specular = LightVolumeSpecular(albedo, smoothness, metallic, worldNormal, viewDir, L0, L1r, L1g, L1b)
        OR
      specular = LightVolumeSpecularDominant(albedo, smoothness, metallic, worldNormal, viewDir, L0, L1r, L1g, L1b)
   4. emission += diffuse + specular   // DO NOT multiply specular by albedo again
```

This is exactly how the sample PBR shader does it: [14](#2-13) 

---

## Utility / Scene State Functions [15](#2-14) 

- `LightVolumesEnabled()` — returns `1` if Light Volumes are active; note that `LightVolumeSH()` already handles fallback automatically.
- `LightVolumesVersion()` — returns the system version number.

---

## Amplify Shader Editor (ASE) Nodes

For ASE users, dedicated function nodes are provided under `Packages/VRC Light Volumes/Shaders/ASE Shaders`: [16](#2-15) 

The `LightVolumeSpecular` ASE node wraps the same `LightVolumeSpecular()` / `LightVolumeSpecularDominant()` HLSL functions, with a `Dominant Direction` toggle flag.

---

## Notes

- **Do NOT multiply specular output by albedo** — the specular functions already incorporate F0 (derived from albedo + metallic) internally.
- The `LightVolumeSpecular()` method computes three GGX lobes (one per RGB L1 direction), which is more visually accurate for multi-colored lighting environments (e.g., avatar lighting with multiple colored lights). `LightVolumeSpecularDominant()` collapses this to one lobe for performance.
- Both specular functions work with **any** L1 SH data — including Unity's built-in `unity_SHAr/SHAg/SHAb` light probes, not just Light Volume data.
- The include order matters: `UnityCG.cginc` **must** be included before `LightVolumes.cginc` to enable the automatic light probe fallback. [17](#2-16)

### Citations

**File:** Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc (L143-146)
```text
float LV_DistributionGGX(float NoH, float roughness) {
    float f = (roughness - 1) * ((roughness + 1) * (NoH * NoH)) + 1;
    return (roughness * roughness) / ((float) LV_PI * f * f);
}
```

**File:** Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc (L159-161)
```text
float LV_EvaluateSH(float L0, float3 L1, float3 n) {
    return L0 + dot(L1, n);
}
```

**File:** Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc (L483-494)
```text
// Samples 3 SH textures and packing them into L1 channels
void LV_SampleLightVolumeTex(float3 uvw0, float3 uvw1, float3 uvw2, out float3 L0, out float3 L1r, out float3 L1g, out float3 L1b) {
    // Sampling 3D Atlas
    float4 tex0 = LV_SAMPLE(_UdonLightVolume, uvw0);
    float4 tex1 = LV_SAMPLE(_UdonLightVolume, uvw1);
    float4 tex2 = LV_SAMPLE(_UdonLightVolume, uvw2);
    // Packing final data
    L0 = tex0.rgb;
    L1r = float3(tex1.r, tex2.r, tex0.a);
    L1g = float3(tex1.g, tex2.g, tex1.a);
    L1b = float3(tex1.b, tex2.b, tex2.a);
}
```

**File:** Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc (L513-518)
```text
void LV_SampleLightProbeDering(inout float3 L0, inout float3 L1r, inout float3 L1g, inout float3 L1b) {
    L0 += float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
    L1r += unity_SHAr.xyz * 0.565f;
    L1g += unity_SHAg.xyz * 0.565f;
    L1b += unity_SHAb.xyz * 0.565f;
}
```

**File:** Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc (L829-856)
```text
float3 LightVolumeSpecular(float3 f0, float smoothness, float3 worldNormal, float3 viewDir, float3 L0, float3 L1r, float3 L1g, float3 L1b) {
    
    float3 specColor = max(float3(dot(reflect(-L1r, worldNormal), viewDir), dot(reflect(-L1g, worldNormal), viewDir), dot(reflect(-L1b, worldNormal), viewDir)), 0);
    
    float3 rDir = normalize(normalize(L1r) + viewDir);
    float3 gDir = normalize(normalize(L1g) + viewDir);
    float3 bDir = normalize(normalize(L1b) + viewDir);
    
    float rNh = saturate(dot(worldNormal, rDir));
    float gNh = saturate(dot(worldNormal, gDir));
    float bNh = saturate(dot(worldNormal, bDir));
    
    float roughness = 1 - smoothness * 0.9f;
    float roughExp = roughness * roughness;
    
    float rSpec = LV_DistributionGGX(rNh, roughExp);
    float gSpec = LV_DistributionGGX(gNh, roughExp);
    float bSpec = LV_DistributionGGX(bNh, roughExp);
    
    float3 specs = (rSpec + gSpec + bSpec) * f0;
    float3 coloredSpecs = specs * specColor;
    
    float3 a = coloredSpecs + specs * L0;
    float3 b = coloredSpecs * 3;
    
    return max(lerp(a, b, smoothness) * 0.5f, 0.0);
    
}
```

**File:** Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc (L859-862)
```text
float3 LightVolumeSpecular(float3 albedo, float smoothness, float metallic, float3 worldNormal, float3 viewDir, float3 L0, float3 L1r, float3 L1g, float3 L1b) {
    float3 specularf0 = lerp(0.04f, albedo, metallic);
    return LightVolumeSpecular(specularf0, smoothness, worldNormal, viewDir, L0, L1r, L1g, L1b);
}
```

**File:** Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc (L865-884)
```text
float3 LightVolumeSpecularDominant(float3 f0, float smoothness, float3 worldNormal, float3 viewDir, float3 L0, float3 L1r, float3 L1g, float3 L1b) {
    
    float3 dominantDir = L1r + L1g + L1b;
    float3 dir = normalize(normalize(dominantDir) + viewDir);
    float nh = saturate(dot(worldNormal, dir));
    
    float roughness = 1 - smoothness * 0.9f;
    float roughExp = roughness * roughness;
    
    float spec = LV_DistributionGGX(nh, roughExp);
    
    return max(spec * L0 * f0, 0.0) * 1.5f;
    
}

// Calculates speculars for light volumes or any SH L1 data, but simplified, with only one dominant direction
float3 LightVolumeSpecularDominant(float3 albedo, float smoothness, float metallic, float3 worldNormal, float3 viewDir, float3 L0, float3 L1r, float3 L1g, float3 L1b) {
    float3 specularf0 = lerp(0.04f, albedo, metallic);
    return LightVolumeSpecularDominant(specularf0, smoothness, worldNormal, viewDir, L0, L1r, L1g, L1b);
}
```

**File:** Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc (L887-889)
```text
float3 LightVolumeEvaluate(float3 worldNormal, float3 L0, float3 L1r, float3 L1g, float3 L1b) {
    return float3(LV_EvaluateSH(L0.r, L1r, worldNormal), LV_EvaluateSH(L0.g, L1g, worldNormal), LV_EvaluateSH(L0.b, L1b, worldNormal));
}
```

**File:** Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc (L892-901)
```text
void LightVolumeSH(float3 worldPos, out float3 L0, out float3 L1r, out float3 L1g, out float3 L1b, float3 worldPosOffset = 0) {
    L0 = 0; L1r = 0; L1g = 0; L1b = 0;
    if (_UdonLightVolumeEnabled == 0) {
        LV_SampleLightProbeDering(L0, L1r, L1g, L1b);
    } else {
        float4 occlusion = 1;
        LV_LightVolumeSH(worldPos + worldPosOffset, L0, L1r, L1g, L1b, occlusion);
        LV_PointLightVolumeSH(worldPos, occlusion, L0, L1r, L1g, L1b);
    }
}
```

**File:** Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc (L904-911)
```text
void LightVolumeAdditiveSH(float3 worldPos, out float3 L0, out float3 L1r, out float3 L1g, out float3 L1b, float3 worldPosOffset = 0) {
    L0 = 0; L1r = 0; L1g = 0; L1b = 0;
    if (_UdonLightVolumeEnabled != 0) {
        float4 occlusion = 1;
        LV_LightVolumeAdditiveSH(worldPos + worldPosOffset, L0, L1r, L1g, L1b, occlusion);
        LV_PointLightVolumeSH(worldPos, occlusion, L0, L1r, L1g, L1b);
    }
}
```

**File:** Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc (L914-937)
```text
float3 LightVolumeSH_L0(float3 worldPos, float3 worldPosOffset = 0) {
    if (_UdonLightVolumeEnabled == 0) {
        return float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
    } else {
        float3 L0 = 0; float4 occlusion = 1;
        float3 unused_L1; // Let's just pray that compiler will strip everything x.x
        LV_LightVolumeSH(worldPos + worldPosOffset, L0, unused_L1, unused_L1, unused_L1, occlusion);
        LV_PointLightVolumeSH(worldPos, occlusion, L0, unused_L1, unused_L1, unused_L1);
        return L0;
    }
}

// Calculates L0 SH based on the world position from additive volumes only. Samples both light volumes and point lights.
float3 LightVolumeAdditiveSH_L0(float3 worldPos, float3 worldPosOffset = 0) {
    if (_UdonLightVolumeEnabled == 0) {
        return 0;
    } else {
        float3 L0 = 0; float4 occlusion = 1;
        float3 unused_L1; // Let's just pray that compiler will strip everything x.x
        LV_LightVolumeAdditiveSH(worldPos + worldPosOffset, L0, unused_L1, unused_L1, unused_L1, occlusion);
        LV_PointLightVolumeSH(worldPos, occlusion, L0, unused_L1, unused_L1, unused_L1);
        return L0;
    }
}
```

**File:** Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc (L940-947)
```text
float LightVolumesEnabled() {
    return _UdonLightVolumeEnabled;
}

// Returns the light volumes version
float LightVolumesVersion() {
    return _UdonLightVolumeVersion == 0 ? _UdonLightVolumeEnabled : _UdonLightVolumeVersion;
}
```

**File:** Packages/red.sim.lightvolumes/Shaders/ASE Shaders/Light Volume PBR.shader (L155-215)
```text
			float3 localLightVolumeEvaluate2_g222 = LightVolumeEvaluate( worldNormal2_g222 , L02_g222 , L1r2_g222 , L1g2_g222 , L1b2_g222 );
			float4 tex2DNode50 = tex2D( _MetallicGlossMap, uv_MainTex );
			float temp_output_54_0 = ( tex2DNode50.r * _Metallic );
			float Metallic334 = ( temp_output_54_0 * temp_output_54_0 );
			float3 temp_output_406_0 = ( localLightVolumeEvaluate2_g222 * Albedo337 * ( 1.0 - Metallic334 ) );
			float3 temp_output_138_0_g220 = Albedo337;
			float3 albedo157_g220 = temp_output_138_0_g220;
			float Smoothness109 = ( tex2DNode50.a * _Glossiness );
			float temp_output_3_0_g220 = Smoothness109;
			float smoothness157_g220 = temp_output_3_0_g220;
			float temp_output_137_0_g220 = Metallic334;
			float metallic157_g220 = temp_output_137_0_g220;
			float3 temp_output_2_0_g220 = World_Normal112;
			float3 worldNormal157_g220 = temp_output_2_0_g220;
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_positionWS );
			float3 ase_viewDirSafeWS = Unity_SafeNormalize( ase_viewVectorWS );
			float3 temp_output_9_0_g220 = ase_viewDirSafeWS;
			float3 viewDir157_g220 = temp_output_9_0_g220;
			float3 temp_output_65_0_g220 = L098;
			float3 L0157_g220 = temp_output_65_0_g220;
			float3 temp_output_1_0_g220 = L1r99;
			float3 L1r157_g220 = temp_output_1_0_g220;
			float3 temp_output_36_0_g220 = L1g100;
			float3 L1g157_g220 = temp_output_36_0_g220;
			float3 temp_output_37_0_g220 = L1b101;
			float3 L1b157_g220 = temp_output_37_0_g220;
			float3 localLightVolumeSpecular157_g220 = LightVolumeSpecular( albedo157_g220 , smoothness157_g220 , metallic157_g220 , worldNormal157_g220 , viewDir157_g220 , L0157_g220 , L1r157_g220 , L1g157_g220 , L1b157_g220 );
			float3 temp_output_138_0_g221 = Albedo337;
			float3 albedo158_g221 = temp_output_138_0_g221;
			float temp_output_3_0_g221 = Smoothness109;
			float smoothness158_g221 = temp_output_3_0_g221;
			float temp_output_137_0_g221 = Metallic334;
			float metallic158_g221 = temp_output_137_0_g221;
			float3 temp_output_2_0_g221 = World_Normal112;
			float3 worldNormal158_g221 = temp_output_2_0_g221;
			float3 temp_output_9_0_g221 = ase_viewDirSafeWS;
			float3 viewDir158_g221 = temp_output_9_0_g221;
			float3 temp_output_65_0_g221 = L098;
			float3 L0158_g221 = temp_output_65_0_g221;
			float3 temp_output_1_0_g221 = L1r99;
			float3 L1r158_g221 = temp_output_1_0_g221;
			float3 temp_output_36_0_g221 = L1g100;
			float3 L1g158_g221 = temp_output_36_0_g221;
			float3 temp_output_37_0_g221 = L1b101;
			float3 L1b158_g221 = temp_output_37_0_g221;
			float3 localLightVolumeSpecularDominant158_g221 = LightVolumeSpecularDominant( albedo158_g221 , smoothness158_g221 , metallic158_g221 , worldNormal158_g221 , viewDir158_g221 , L0158_g221 , L1r158_g221 , L1g158_g221 , L1b158_g221 );
			#ifdef _DOMINANTDIRSPECULARS_ON
				float3 staticSwitch410 = localLightVolumeSpecularDominant158_g221;
			#else
				float3 staticSwitch410 = localLightVolumeSpecular157_g220;
			#endif
			float lerpResult57 = lerp( 1.0 , tex2DNode50.g , _OcclusionStrength);
			float AO357 = lerpResult57;
			float3 Speculars412 = ( staticSwitch410 * AO357 );
			#ifdef _SPECULARS_ON
				float3 staticSwitch361 = ( temp_output_406_0 + Speculars412 );
			#else
				float3 staticSwitch361 = temp_output_406_0;
			#endif
			float3 IndirectAndSpeculars444 = ( staticSwitch361 * AO357 );
			float3 Emission452 = ( ( _EmissionColor.rgb * tex2D( _EmissionMap, uv_MainTex ).rgb ) + IndirectAndSpeculars444 );
```

**File:** Documentation/ForShaderDevelopers.md (L21-28)
```markdown
| ASE Node | Description |
| --- | --- |
| Light Volume | Required to get the Spherical Harmonics components. Using the output values you get from it, you can calculate the speculars for your custom lighting setup. <br/> `AdditiveOnly` flag specifies if you need to only sample additive volumes. Useful for static lightmapped meshes. |
| Light Volume L0 | Required to get the L0 spherical harmonics component, or just the overall ambient color, with no directionality. This is much lighter than the LightVolume node, and recommended to use in places where there are no directionality needed. <br/> `AdditiveOnly` flag specifies if you need to only sample additive volumes. Useful for static lightmapped meshes. |
| Light Volume Evaluate | Calculates the final color you get from the light volume in some kind of a physically realistic way. But alternatively you can implement your own "Evaluate" function to make the result matching your toon shader, for example. <br/> You should usually multiply it by your "Albedo" and add to the final color, as an emission. |
| Light Volume Specular | Calculates approximated speculars based on SH components. Can be used with Light Volumes or even with any other SH L1 values, like Unity default light probes. The result should be added to the final color, just like emission. You should NOT multiply this by albedo color! <br/> `Dominant Direction` flag specifies if you want to use a simpler and lighter way of generating speculars. Generates one color specular for the dominant light direction instead of three color speculars in a regular method. |
| Is Light Volumes | Returns `0` if there are no light volumes support on the current scene, or `1` if light volumes system is provided. |
| Light Volumes Version | Returns the light volumes version. `0` means that light volumes are not presented in the scene. `1`, `2` or any other values in future, shows the global light volumes verison presented in the scene. |
```

**File:** Documentation/ForShaderDevelopers.md (L32-34)
```markdown
First of all, you need to include the "LightVolumes.cginc" file provided with this asset, into your shader:  `#include "LightVolumes.cginc"`. 
Also be sure that you included the "UnityCG.cginc" file **BEFORE** to support the fallback to unity's light probes:  `#include "UnityCG.cginc"`

```
