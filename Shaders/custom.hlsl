//----------------------------------------------------------------------------------------------------------------------
// Macro

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
	/* Normal strength per layer */ \
	float _SpecNormalStrength1; \
	float _SpecNormalStrength2; \
	/* Anisotropic specular (hair) */ \
	float _EnableAniso; \
	float4 _AnisoSpecColor; \
	float _AnisoIntensity; \
	float _AnisoRoughnessX; \
	float _AnisoRoughnessY;

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
	TEXTURE2D(_SpecSmoothnessMap2);

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
	if(channel == 1) return v.g;
	if(channel == 2) return v.b;
	if(channel == 3) return v.a;
	return v.r;
}
#define DNKW_SAMPLE_SCALAR_CH(tex, st, uv, ch) (dnkw_pick_channel(DNKW_SAMPLE(tex, st, uv), ch))


// Provide a no-op macro for outline-related passes and a full implementation otherwise
#if defined(LIL_PASS_OUTLINE) || defined(LIL_OUTLINE) || defined(PASS_OUTLINE)
	#define BEFORE_DISTANCE_FADE { /* outline: skip custom specular */ }
#else
	#define BEFORE_DISTANCE_FADE \
	{ \
		/* Per-layer overall control */ \
		float mask1 = DNKW_SAMPLE_SCALAR_CH(_SpecMask1, _SpecMask1_ST, fd.uvMain, _SpecMask1_Channel); \
		float noise1 = DNKW_SAMPLE_SCALAR_CH(_SpecNoiseTex1, _SpecNoiseTex1_ST, fd.uvMain, _SpecNoiseTex1_Channel); \
		float overall1 = saturate(mask1 * noise1); \
		float mask2 = DNKW_SAMPLE_SCALAR_CH(_SpecMask2, _SpecMask2_ST, fd.uvMain, _SpecMask2_Channel); \
		float noise2 = DNKW_SAMPLE_SCALAR_CH(_SpecNoiseTex2, _SpecNoiseTex2_ST, fd.uvMain, _SpecNoiseTex2_Channel); \
		float overall2 = saturate(mask2 * noise2); \
		if(overall1 > 0.0001 || overall2 > 0.0001 || _EnableAniso > 0.5) { \
			float3 Norig = normalize(fd.origN); \
			float3 Nmap  = normalize(fd.N); \
			float3 V = normalize(fd.V); \
			float3 L = normalize(fd.L); \
			float3 H = normalize(L + V); \
			float atten = fd.attenuation * fd.shadowmix; \
			float3 lightCol = fd.lightColor; \
			float3 specAccum = 0; \
			/* Layer 1 */ \
			if(_EnableSpec1 > 0.5 && overall1 > 0.0001) { \
				float s1 = clamp(_SpecNormalStrength1, 0.0, 3.0); \
				float3 N1 = normalize(lerp(Norig, Nmap, s1)); \
				float nl1 = saturate(dot(N1, L)); \
				float nh1 = saturate(dot(N1, H)); \
				float3 colTex1 = DNKW_SAMPLE_COLOR(_SpecColorMap1, _SpecColorMap1_ST, fd.uvMain); \
				float mapI1 = DNKW_SAMPLE_SCALAR_CH(_SpecIntensityMap1, _SpecIntensityMap1_ST, fd.uvMain, _SpecIntensityMap1_Channel); \
				float mapS1 = DNKW_SAMPLE_SCALAR_CH(_SpecSmoothnessMap1, _SpecSmoothnessMap1_ST, fd.uvMain, _SpecSmoothnessMap1_Channel); \
				float3 baseCol1 = (_UseSpecColorMap1 > 0.5 ? colTex1 : float3(1,1,1)) * _SpecColor1.rgb; \
				float intensity1 = _SpecIntensity1 * (_UseSpecIntensityMap1 > 0.5 ? mapI1 : 1.0); \
				float smooth1 = saturate(_SpecSmoothness1 * (_UseSpecSmoothnessMap1 > 0.5 ? mapS1 : 1.0)); \
				float power1 = lerp(8.0, 1024.0, smooth1); \
				float specTerm1 = pow(nh1, power1) * nl1; \
				specAccum += overall1 * baseCol1 * intensity1 * specTerm1; \
			} \
			/* Layer 2 */ \
			if(_EnableSpec2 > 0.5 && overall2 > 0.0001) { \
				float s2 = clamp(_SpecNormalStrength2, 0.0, 3.0); \
				float3 N2 = normalize(lerp(Norig, Nmap, s2)); \
				float nl2 = saturate(dot(N2, L)); \
				float nh2 = saturate(dot(N2, H)); \
				float3 colTex2 = DNKW_SAMPLE_COLOR(_SpecColorMap2, _SpecColorMap2_ST, fd.uvMain); \
				float mapI2 = DNKW_SAMPLE_SCALAR_CH(_SpecIntensityMap2, _SpecIntensityMap2_ST, fd.uvMain, _SpecIntensityMap2_Channel); \
				float mapS2 = DNKW_SAMPLE_SCALAR_CH(_SpecSmoothnessMap2, _SpecSmoothnessMap2_ST, fd.uvMain, _SpecSmoothnessMap2_Channel); \
				float3 baseCol2 = (_UseSpecColorMap2 > 0.5 ? colTex2 : float3(1,1,1)) * _SpecColor2.rgb; \
				float intensity2 = _SpecIntensity2 * (_UseSpecIntensityMap2 > 0.5 ? mapI2 : 1.0); \
				float smooth2 = saturate(_SpecSmoothness2 * (_UseSpecSmoothnessMap2 > 0.5 ? mapS2 : 1.0)); \
				float power2 = lerp(8.0, 1024.0, smooth2); \
				float specTerm2 = pow(nh2, power2) * nl2; \
				specAccum += overall2 * baseCol2 * intensity2 * specTerm2; \
			} \
			/* Anisotropic GGX (Cook-Torrance) for hair */ \
			if(_EnableAniso > 0.5) { \
				float3 T = normalize(fd.T); \
				float3 B = normalize(fd.B); \
				float3 N = normalize(fd.N); \
				// Local (T,B,N) space components \
				float3 Vl = float3(dot(V,T), dot(V,B), saturate(dot(V,N))); \
				float3 Ll = float3(dot(L,T), dot(L,B), saturate(dot(L,N))); \
				float3 Hl = normalize(Vl + Ll); \
				float rx = clamp(_AnisoRoughnessX, 0.02, 1.0); \
				float ry = clamp(_AnisoRoughnessY, 0.02, 1.0); \
				// GGX anisotropic NDF \
				float hx = Hl.x / rx; \
				float hy = Hl.y / ry; \
				float hz = max(Hl.z, 1e-4); \
				float denom = (hx*hx + hy*hy + hz*hz); \
				float D = 1.0 / (UNITY_PI * rx * ry * denom * denom); \
				// Smith masking-shadowing (approx isotropic using geometric mean) \
				float riso = saturate(sqrt(rx * ry)); \
				float a2 = riso * riso; \
				float NdotV = saturate(Vl.z); \
				float NdotL = saturate(Ll.z); \
				float Gv = NdotV / (NdotV * (1.0 - a2) + a2); \
				float Gl = NdotL / (NdotL * (1.0 - a2) + a2); \
				float G = Gv * Gl; \
				// Fresnel (Schlick) with scalar F0 \
				float VdotH = saturate(dot(normalize(V), normalize(H))); \
				float F0s = 0.04; \
				float F = F0s + (1.0 - F0s) * pow(1.0 - VdotH, 5.0); \
				float denomCG = max(4.0 * NdotV * NdotL, 1e-4); \
				float3 ct = (D * G * F / denomCG) * _AnisoIntensity; \
				float on = step(0.0, NdotV) * step(0.0, NdotL); \
				specAccum += on * ct * _AnisoSpecColor.rgb; \
			} \
			float3 specFinal = specAccum * lightCol * atten; \
			fd.col.rgb += specFinal; \
		} \
	}
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