//----------------------------------------------------------------------------------------------------------------------
// Macro

// VRC Light Volumes optional integration
#include "UnityCG.cginc"
#if defined(__has_include)
	#if __has_include("Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc")
		#include "Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc"
		#define DNKW_VRCLV_AVAILABLE 1
	#else
		#define DNKW_VRCLV_AVAILABLE 0
	#endif
#else
	#define DNKW_VRCLV_AVAILABLE 0
#endif

#if !DNKW_VRCLV_AVAILABLE
	#define DNKW_UNUSED(x) (void)(x)
	void dnkw_lightvolume_sh_fallback(float3 worldPos, out float3 L0, out float3 L1r, out float3 L1g, out float3 L1b)
	{
		DNKW_UNUSED(worldPos);
		L0 = float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
		/* 0.565 reduces SH ringing artefacts in probe L1 terms and matches LightVolumes.cginc fallback */
		L1r = unity_SHAr.xyz * 0.565f;
		L1g = unity_SHAg.xyz * 0.565f;
		L1b = unity_SHAb.xyz * 0.565f;
	}
	float3 dnkw_lightvolume_specular_fallback(float3 albedo, float smoothness, float metallic, float3 worldNormal, float3 viewDir, float3 L0, float3 L1r, float3 L1g, float3 L1b)
	{
		DNKW_UNUSED(albedo);
		DNKW_UNUSED(smoothness);
		DNKW_UNUSED(metallic);
		DNKW_UNUSED(worldNormal);
		DNKW_UNUSED(viewDir);
		DNKW_UNUSED(L0);
		DNKW_UNUSED(L1r);
		DNKW_UNUSED(L1g);
		DNKW_UNUSED(L1b);
		return 0;
	}
	#define DNKW_LIGHTVOLUME_SH(worldPos, L0, L1r, L1g, L1b) dnkw_lightvolume_sh_fallback((worldPos), (L0), (L1r), (L1g), (L1b))
	#define DNKW_LIGHTVOLUME_SPECULAR(albedo, smoothness, metallic, worldNormal, viewDir, L0, L1r, L1g, L1b) dnkw_lightvolume_specular_fallback((albedo), (smoothness), (metallic), (worldNormal), (viewDir), (L0), (L1r), (L1g), (L1b))
#else
	#define DNKW_LIGHTVOLUME_SH(worldPos, L0, L1r, L1g, L1b) LightVolumeSH((worldPos), (L0), (L1r), (L1g), (L1b))
	#define DNKW_LIGHTVOLUME_SPECULAR(albedo, smoothness, metallic, worldNormal, viewDir, L0, L1r, L1g, L1b) LightVolumeSpecular((albedo), (smoothness), (metallic), (worldNormal), (viewDir), (L0), (L1r), (L1g), (L1b))
#endif

// Custom variables
//#define LIL_CUSTOM_PROPERTIES \
//    float _CustomVariable;
#define LIL_CUSTOM_PROPERTIES \
	float _EnableSpec1; \
	float _EnableSpec2; \
	float4 _SpecColor1; \
	float4 _SpecColor2; \
	float _UseSpecColorMap1; \
	float _UseSpecColorMap2; \
	float _SpecIntensity1; \
	float _SpecIntensity2; \
	float _UseSpecIntensityMap1; \
	float _UseSpecIntensityMap2; \
	float _SpecSmoothness1; \
	float _SpecSmoothness2; \
	float _UseSpecSmoothnessMap1; \
	float _UseSpecSmoothnessMap2; \
	/* Channel selectors (0:R 1:G 2:B 3:A) */ \
	int _SpecMask1_Channel; \
	int _SpecMask2_Channel; \
	int _SpecNoiseTex1_Channel; \
	int _SpecNoiseTex2_Channel; \
	int _SpecIntensityMap1_Channel; \
	int _SpecIntensityMap2_Channel; \
	int _SpecSmoothnessMap1_Channel; \
	int _SpecSmoothnessMap2_Channel; \
	/* Tiling/Offset (_ST) for custom textures */ \
	float4 _SpecMask1_ST; \
	float4 _SpecMask2_ST; \
	float4 _SpecNoiseTex1_ST; \
	float4 _SpecNoiseTex2_ST; \
	float4 _SpecColorMap1_ST; \
	float4 _SpecColorMap2_ST; \
	float4 _SpecIntensityMap1_ST; \
	float4 _SpecIntensityMap2_ST; \
	float4 _SpecSmoothnessMap1_ST; \
	float4 _SpecSmoothnessMap2_ST; \
	float _SpecNormalStrength1; \
	float _SpecNormalStrength2; \
	/* Fresnel rim */ \
	float _SpecUseFresnel1; \
	float _SpecUseFresnel2; \
	float4 _SpecF0Color1; \
	float4 _SpecF0Color2; \
	float _SpecFresnelStrength1; \
	float _SpecFresnelStrength2; \
	/* Custom MatCap 1 */ \
	float _CustomMatCap1_Enable; \
	float4 _CustomMatCap1_Color; \
	int _CustomMatCap1_Blend; \
	int _CustomMatCap1_Mask_Channel; \
	float _CustomMatCap1_BumpScale; \
	int _CustomMatCap1_UseReflection; \
	int _CustomMatCap1_DisableBackface; \
	float _CustomMatCap1_EnableLighting; \
	float _CustomMatCap1_Blur; \
	float _CustomMatCap1_Alpha; \
	float4 _CustomMatCap1_Tex_ST; \
	float4 _CustomMatCap1_Tex_TexelSize; \
	float4 _CustomMatCap1_Mask_ST; \
	float4 _CustomMatCap1_NormalMap_ST;

// Custom textures (declare texture + sampler to be safe across SRPs)
#define LIL_CUSTOM_TEXTURES \
	TEXTURE2D(_SpecMask1); \
	TEXTURE2D(_SpecMask2); \
	TEXTURE2D(_SpecNoiseTex1); \
	TEXTURE2D(_SpecNoiseTex2); \
	TEXTURE2D(_SpecColorMap1); \
	TEXTURE2D(_SpecColorMap2); \
	TEXTURE2D(_SpecIntensityMap1); \
	TEXTURE2D(_SpecIntensityMap2); \
	TEXTURE2D(_SpecSmoothnessMap1); \
	TEXTURE2D(_SpecSmoothnessMap2); \
	TEXTURE2D(_CustomMatCap1_Tex); \
	TEXTURE2D(_CustomMatCap1_Mask); \


// (note) _ST variables declared inside LIL_CUSTOM_PROPERTIES above

// Add vertex shader input
//#define LIL_REQUIRE_APP_POSITION
//#define LIL_REQUIRE_APP_TEXCOORD0
//#define LIL_REQUIRE_APP_TEXCOORD1
//#define LIL_REQUIRE_APP_TEXCOORD2
//#define LIL_REQUIRE_APP_TEXCOORD3
//#define LIL_REQUIRE_APP_TEXCOORD4
//#define LIL_REQUIRE_APP_TEXCOORD5
//#define LIL_REQUIRE_APP_TEXCOORD6
//#define LIL_REQUIRE_APP_TEXCOORD7
//#define LIL_REQUIRE_APP_COLOR
//#define LIL_REQUIRE_APP_NORMAL
//#define LIL_REQUIRE_APP_TANGENT
//#define LIL_REQUIRE_APP_VERTEXID

// Add vertex shader output
//#define LIL_V2F_FORCE_TEXCOORD0
//#define LIL_V2F_FORCE_TEXCOORD1
//#define LIL_V2F_FORCE_POSITION_OS
//#define LIL_V2F_FORCE_POSITION_WS
//#define LIL_V2F_FORCE_POSITION_SS
//#define LIL_V2F_FORCE_NORMAL
//#define LIL_V2F_FORCE_TANGENT
//#define LIL_V2F_FORCE_BITANGENT
//#define LIL_CUSTOM_V2F_MEMBER(id0,id1,id2,id3,id4,id5,id6,id7)

// Add vertex copy
#define LIL_CUSTOM_VERT_COPY

// Inserting a process into the vertex shader
//#define LIL_CUSTOM_VERTEX_OS
//#define LIL_CUSTOM_VERTEX_WS

// Inserting a process into pixel shader
//#define BEFORE_xx
//#define OVERRIDE_xx

// ---------------------------------------------------------------------------------------------------------------------
// Specular logic (Blinn-Phong) injected before final output

// Local sampling helpers (macros) to keep compatibility across pipelines
#define DNKW_TEXCOORD(uv, st) ((uv) * (st).xy + (st).zw)
#define DNKW_SAMPLE(tex, st, uv) (LIL_SAMPLE_2D(tex, sampler_linear_repeat, DNKW_TEXCOORD((uv),(st))))
#define DNKW_SAMPLE_COLOR(tex, st, uv) (DNKW_SAMPLE(tex, st, uv).rgb)
/* channel: 0 R, 1 G, 2 B, 3 A */
float dnkw_pick_channel(float4 v, int channel)
{
	float4 arr = float4(v.r, v.g, v.b, v.a);
	return arr[channel];
}
#define DNKW_SAMPLE_SCALAR_CH(tex, st, uv, ch) (dnkw_pick_channel(DNKW_SAMPLE(tex, st, uv), ch))




#define BEFORE_DISTANCE_FADE \
{ \
	if (_EnableSpec1 > 0.5 || _EnableSpec2 > 0.5) { \
		float2 uvMain = fd.uvMain; \
		float3 Norig = fd.origN; \
		float3 Nmap  = fd.N; \
		float3 V = fd.V; \
		float3 L = fd.L; \
		float3 H = normalize(L + V); \
		float atten = fd.attenuation * fd.shadowmix; \
		/* Fixed metallic=1 uses baseCol as F0 (specular color) for LightVolumeSpecular */ \
		const float LV_F0_METALLIC = 1.0; \
		float3 specAccum = 0; \
		float3 lvSpecAccum = 0; \
		float3 L0, L1r, L1g, L1b; \
		DNKW_LIGHTVOLUME_SH(fd.positionWS, L0, L1r, L1g, L1b); \
		/* Layer 1 */ \
		if(_EnableSpec1 > 0.5) { \
			float mask1 = DNKW_SAMPLE_SCALAR_CH(_SpecMask1, _SpecMask1_ST, uvMain, _SpecMask1_Channel); \
			float noise1 = DNKW_SAMPLE_SCALAR_CH(_SpecNoiseTex1, _SpecNoiseTex1_ST, uvMain, _SpecNoiseTex1_Channel); \
			float overall1 = saturate(mask1 * noise1); \
			if (overall1 > 0.0001) { \
				float s1 = _SpecNormalStrength1; \
				float3 N1 = normalize(lerp(Norig, Nmap, s1)); \
				float nl1 = saturate(dot(N1, L)); \
				float nh1 = saturate(dot(N1, H)); \
				float3 baseCol1 = (_UseSpecColorMap1 > 0.5 ? DNKW_SAMPLE_COLOR(_SpecColorMap1, _SpecColorMap1_ST, uvMain) : float3(1,1,1)) * _SpecColor1.rgb; \
				float intensity1 = _SpecIntensity1 * (_UseSpecIntensityMap1 > 0.5 ? DNKW_SAMPLE_SCALAR_CH(_SpecIntensityMap1, _SpecIntensityMap1_ST, uvMain, _SpecIntensityMap1_Channel) : 1.0); \
				float smooth1 = saturate(_SpecSmoothness1 * (_UseSpecSmoothnessMap1 > 0.5 ? DNKW_SAMPLE_SCALAR_CH(_SpecSmoothnessMap1, _SpecSmoothnessMap1_ST, uvMain, _SpecSmoothnessMap1_Channel) : 1.0)); \
				float power1 = pow(2.0, lerp(3.0, 10.0, smooth1)); \
				float specTerm1 = pow(nh1, power1) * nl1; \
				specAccum += overall1 * baseCol1 * intensity1 * specTerm1; \
				/* LightVolumeSpecular computes from baseCol1/F0 internally; do not multiply baseCol again */ \
				lvSpecAccum += overall1 * intensity1 * DNKW_LIGHTVOLUME_SPECULAR(baseCol1, smooth1, LV_F0_METALLIC, N1, V, L0, L1r, L1g, L1b); \
				if (_SpecUseFresnel1 > 0.5) { \
					float VdotN1 = saturate(dot(V, N1)); \
					float rim1 = pow(1.0 - VdotN1, 5.0); \
					specAccum += overall1 * _SpecF0Color1.rgb * _SpecFresnelStrength1 * rim1; \
				} \
			} \
		} \
		/* Layer 2 */ \
		if(_EnableSpec2 > 0.5) { \
			float mask2 = DNKW_SAMPLE_SCALAR_CH(_SpecMask2, _SpecMask2_ST, uvMain, _SpecMask2_Channel); \
			float noise2 = DNKW_SAMPLE_SCALAR_CH(_SpecNoiseTex2, _SpecNoiseTex2_ST, uvMain, _SpecNoiseTex2_Channel); \
			float overall2 = saturate(mask2 * noise2); \
			if (overall2 > 0.0001) { \
				float s2 = _SpecNormalStrength2; \
				float3 N2 = normalize(lerp(Norig, Nmap, s2)); \
				float nl2 = saturate(dot(N2, L)); \
				float nh2 = saturate(dot(N2, H)); \
				float3 baseCol2 = (_UseSpecColorMap2 > 0.5 ? DNKW_SAMPLE_COLOR(_SpecColorMap2, _SpecColorMap2_ST, uvMain) : float3(1,1,1)) * _SpecColor2.rgb; \
				float intensity2 = _SpecIntensity2 * (_UseSpecIntensityMap2 > 0.5 ? DNKW_SAMPLE_SCALAR_CH(_SpecIntensityMap2, _SpecIntensityMap2_ST, uvMain, _SpecIntensityMap2_Channel) : 1.0); \
				float smooth2 = saturate(_SpecSmoothness2 * (_UseSpecSmoothnessMap2 > 0.5 ? DNKW_SAMPLE_SCALAR_CH(_SpecSmoothnessMap2, _SpecSmoothnessMap2_ST, uvMain, _SpecSmoothnessMap2_Channel) : 1.0)); \
				float power2 = pow(2.0, lerp(3.0, 10.0, smooth2)); \
				float specTerm2 = pow(nh2, power2) * nl2; \
				specAccum += overall2 * baseCol2 * intensity2 * specTerm2; \
				/* LightVolumeSpecular computes from baseCol2/F0 internally; do not multiply baseCol again */ \
				lvSpecAccum += overall2 * intensity2 * DNKW_LIGHTVOLUME_SPECULAR(baseCol2, smooth2, LV_F0_METALLIC, N2, V, L0, L1r, L1g, L1b); \
				if (_SpecUseFresnel2 > 0.5) { \
					float VdotN2 = saturate(dot(V, N2)); \
					float rim2 = pow(1.0 - VdotN2, 5.0); \
					specAccum += overall2 * _SpecF0Color2.rgb * _SpecFresnelStrength2 * rim2; \
				} \
			} \
		} \
		fd.col.rgb += specAccum * (fd.lightColor * atten + fd.addLightColor); \
		fd.col.rgb += lvSpecAccum; \
	} \
}

#if !defined(UNITY_PASS_SHADOWCASTER)
#define BEFORE_MATCAP \
{ \
	/* Custom MatCap 1 Hardcoded Implementation */ \
	if (_CustomMatCap1_Enable > 0.5 && _CustomMatCap1_Tex_TexelSize.z > 16.0) { \
		float2 uvMain = fd.uvMain; \
		float bumpScale1 = _CustomMatCap1_BumpScale; \
        /* Use main normal map strength adjustment */ \
        float3 N_orig = normalize(fd.origN); \
        float3 N_main = normalize(fd.N); \
        float3 N_mc = normalize(lerp(N_orig, N_main, bumpScale1)); \
 \
        if (_CustomMatCap1_UseReflection > 0.5) { \
            N_mc = reflect(-fd.V, N_mc); \
        } \
 \
		float3 N_vs = mul((float3x3)UNITY_MATRIX_V, N_mc); \
		N_vs.z *= -1.0; /* Correct for Unity view space */ \
		float2 uv_mc = N_vs.xy * 0.5 + 0.5; \
		float4 mcTex = LIL_SAMPLE_2D_LOD(_CustomMatCap1_Tex, sampler_linear_clamp, uv_mc, _CustomMatCap1_Blur * 8.0); \
		float3 mcColor = mcTex.rgb * _CustomMatCap1_Color.rgb; \
		float mask1 = DNKW_SAMPLE(_CustomMatCap1_Mask, _CustomMatCap1_Mask_ST, uvMain).r; \
		if (_CustomMatCap1_DisableBackface && fd.facing < 0) mask1 = 0.0; \
		mask1 *= saturate(_CustomMatCap1_Alpha); /* Apply Opacity */ \
		/* Improved Blend Logic: Apply Mask via Lerp */ \
		float3 targetColor = fd.col.rgb; \
		int blend1 = _CustomMatCap1_Blend; \
		float shadowFac = fd.attenuation * fd.shadowmix; \
		float3 lightFac = lerp(float3(1,1,1), fd.lightColor, _CustomMatCap1_EnableLighting); \
		mcColor *= shadowFac * lightFac; \
		if (blend1 == 0) targetColor += mcColor; /* Add */ \
		else if (blend1 == 1) targetColor = targetColor + mcColor - targetColor * mcColor; /* Screen (HDR safe) */ \
		else if (blend1 == 2) targetColor *= mcColor; /* Multiply */ \
		fd.col.rgb = lerp(fd.col.rgb, targetColor, mask1); \
	} \
}
#else
#define BEFORE_MATCAP
#endif

//----------------------------------------------------------------------------------------------------------------------
// Information about variables
//----------------------------------------------------------------------------------------------------------------------

//----------------------------------------------------------------------------------------------------------------------
// Vertex shader inputs (appdata structure)
//
// Type     Name                    Description
// -------- ----------------------- --------------------------------------------------------------------
// float4   input.positionOS        POSITION
// float2   input.uv0               TEXCOORD0
// float2   input.uv1               TEXCOORD1
// float2   input.uv2               TEXCOORD2
// float2   input.uv3               TEXCOORD3
// float2   input.uv4               TEXCOORD4
// float2   input.uv5               TEXCOORD5
// float2   input.uv6               TEXCOORD6
// float2   input.uv7               TEXCOORD7
// float4   input.color             COLOR
// float3   input.normalOS          NORMAL
// float4   input.tangentOS         TANGENT
// uint     vertexID                SV_VertexID

//----------------------------------------------------------------------------------------------------------------------
// Vertex shader outputs or pixel shader inputs (v2f structure)
//
// The structure depends on the pass.
// Please check lil_pass_xx.hlsl for details.
//
// Type     Name                    Description
// -------- ----------------------- --------------------------------------------------------------------
// float4   output.positionCS       SV_POSITION
// float2   output.uv01             TEXCOORD0 TEXCOORD1
// float2   output.uv23             TEXCOORD2 TEXCOORD3
// float3   output.positionOS       object space position
// float3   output.positionWS       world space position
// float3   output.normalWS         world space normal
// float4   output.tangentWS        world space tangent

//----------------------------------------------------------------------------------------------------------------------
// Variables commonly used in the forward pass
//
// These are members of `lilFragData fd`
//
// Type     Name                    Description
// -------- ----------------------- --------------------------------------------------------------------
// float4   col                     lit color
// float3   albedo                  unlit color
// float3   emissionColor           color of emission
// -------- ----------------------- --------------------------------------------------------------------
// float3   lightColor              color of light
// float3   indLightColor           color of indirectional light
// float3   addLightColor           color of additional light
// float    attenuation             attenuation of light
// float3   invLighting             saturate((1.0 - lightColor) * sqrt(lightColor));
// -------- ----------------------- --------------------------------------------------------------------
// float2   uv0                     TEXCOORD0
// float2   uv1                     TEXCOORD1
// float2   uv2                     TEXCOORD2
// float2   uv3                     TEXCOORD3
// float2   uvMain                  Main UV
// float2   uvMat                   MatCap UV
// float2   uvRim                   Rim Light UV
// float2   uvPanorama              Panorama UV
// float2   uvScn                   Screen UV
// bool     isRightHand             input.tangentWS.w > 0.0;
// -------- ----------------------- --------------------------------------------------------------------
// float3   positionOS              object space position
// float3   positionWS              world space position
// float4   positionCS              clip space position
// float4   positionSS              screen space position
// float    depth                   distance from camera
// -------- ----------------------- --------------------------------------------------------------------
// float3x3 TBN                     tangent / bitangent / normal matrix
// float3   T                       tangent direction
// float3   B                       bitangent direction
// float3   N                       normal direction
// float3   V                       view direction
// float3   L                       light direction
// float3   origN                   normal direction without normal map
// float3   origL                   light direction without sh light
// float3   headV                   middle view direction of 2 cameras
// float3   reflectionN             normal direction for reflection
// float3   matcapN                 normal direction for reflection for MatCap
// float3   matcap2ndN              normal direction for reflection for MatCap 2nd
// float    facing                  VFACE
// -------- ----------------------- --------------------------------------------------------------------
// float    vl                      dot(viewDirection, lightDirection);
// float    hl                      dot(headDirection, lightDirection);
// float    ln                      dot(lightDirection, normalDirection);
// float    nv                      saturate(dot(normalDirection, viewDirection));
// float    nvabs                   abs(dot(normalDirection, viewDirection));
// -------- ----------------------- --------------------------------------------------------------------
// float4   triMask                 TriMask (for lite version)
// float3   parallaxViewDirection   mul(tbnWS, viewDirection);
// float2   parallaxOffset          parallaxViewDirection.xy / (parallaxViewDirection.z+0.5);
// float    anisotropy              strength of anisotropy
// float    smoothness              smoothness
// float    roughness               roughness
// float    perceptualRoughness     perceptual roughness
// float    shadowmix               this variable is 0 in the shadow area
// float    audioLinkValue          volume acquired by AudioLink
// -------- ----------------------- --------------------------------------------------------------------
// uint     renderingLayers         light layer of object (for URP / HDRP)
// uint     featureFlags            feature flags (for HDRP)
// uint2    tileIndex               tile index (for HDRP)
