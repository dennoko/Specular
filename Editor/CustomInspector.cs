#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    public class DennokoExtensionSpecularInspector : lilToonInspector
    {
        // Custom properties
        // Per-layer Mask/Noise
        private MaterialProperty _SpecMask1;         // Specular適用範囲のマスク(1層目)
        private MaterialProperty _SpecMask1_Channel; // 使用チャンネル
        private MaterialProperty _SpecNoiseTex1;     // スペキュラー用ノイズ(1層目)
        private MaterialProperty _SpecNoiseTex1_Channel;
        private MaterialProperty _SpecMask2;         // Specular適用範囲のマスク(2層目)
        private MaterialProperty _SpecMask2_Channel;
        private MaterialProperty _SpecNoiseTex2;     // スペキュラー用ノイズ(2層目)
        private MaterialProperty _SpecNoiseTex2_Channel;

        // 1st layer
        private MaterialProperty _EnableSpec1;
        private MaterialProperty _SpecColor1;
        private MaterialProperty _UseSpecColorMap1;
        private MaterialProperty _SpecColorMap1;
        private MaterialProperty _SpecIntensity1;
        private MaterialProperty _UseSpecIntensityMap1;
        private MaterialProperty _SpecIntensityMap1;
    private MaterialProperty _SpecIntensityMap1_Channel;
        private MaterialProperty _SpecSmoothness1;
        private MaterialProperty _UseSpecSmoothnessMap1;
        private MaterialProperty _SpecSmoothnessMap1;
    private MaterialProperty _SpecSmoothnessMap1_Channel;
        private MaterialProperty _SpecNormalStrength1;

        // 2nd layer
        private MaterialProperty _EnableSpec2;
        private MaterialProperty _SpecColor2;
        private MaterialProperty _UseSpecColorMap2;
        private MaterialProperty _SpecColorMap2;
        private MaterialProperty _SpecIntensity2;
        private MaterialProperty _UseSpecIntensityMap2;
        private MaterialProperty _SpecIntensityMap2;
    private MaterialProperty _SpecIntensityMap2_Channel;
        private MaterialProperty _SpecSmoothness2;
        private MaterialProperty _UseSpecSmoothnessMap2;
        private MaterialProperty _SpecSmoothnessMap2;
    private MaterialProperty _SpecSmoothnessMap2_Channel;
        private MaterialProperty _SpecNormalStrength2;

        private static bool isShowCustomProperties;
        private static bool isShowSpec1;
        private static bool isShowSpec2;
        private const string shaderName = "dennoko_extension_specular";

        protected override void LoadCustomProperties(MaterialProperty[] props, Material material)
        {
            isCustomShader = true;

            // If you want to change rendering modes in the editor, specify the shader here
            ReplaceToCustomShaders();
            isShowRenderMode = !material.shader.name.Contains("Optional");

            // If not, set isShowRenderMode to false
            //isShowRenderMode = false;

            //LoadCustomLanguage("");

            // Per-layer Mask/Noise
            _SpecMask1              = FindProperty("_SpecMask1", props);
            _SpecMask1_Channel      = FindProperty("_SpecMask1_Channel", props);
            _SpecNoiseTex1          = FindProperty("_SpecNoiseTex1", props);
            _SpecNoiseTex1_Channel  = FindProperty("_SpecNoiseTex1_Channel", props);
            _SpecMask2              = FindProperty("_SpecMask2", props);
            _SpecMask2_Channel      = FindProperty("_SpecMask2_Channel", props);
            _SpecNoiseTex2          = FindProperty("_SpecNoiseTex2", props);
            _SpecNoiseTex2_Channel  = FindProperty("_SpecNoiseTex2_Channel", props);

            // 1st layer
            _EnableSpec1            = FindProperty("_EnableSpec1", props);
            _SpecColor1             = FindProperty("_SpecColor1", props);
            _UseSpecColorMap1       = FindProperty("_UseSpecColorMap1", props);
            _SpecColorMap1          = FindProperty("_SpecColorMap1", props);
            _SpecIntensity1         = FindProperty("_SpecIntensity1", props);
            _UseSpecIntensityMap1   = FindProperty("_UseSpecIntensityMap1", props);
            _SpecIntensityMap1      = FindProperty("_SpecIntensityMap1", props);
            _SpecIntensityMap1_Channel = FindProperty("_SpecIntensityMap1_Channel", props);
            _SpecSmoothness1        = FindProperty("_SpecSmoothness1", props);
            _UseSpecSmoothnessMap1  = FindProperty("_UseSpecSmoothnessMap1", props);
            _SpecSmoothnessMap1     = FindProperty("_SpecSmoothnessMap1", props);
            _SpecSmoothnessMap1_Channel = FindProperty("_SpecSmoothnessMap1_Channel", props);
            _SpecNormalStrength1    = FindProperty("_SpecNormalStrength1", props);

            // 2nd layer
            _EnableSpec2            = FindProperty("_EnableSpec2", props);
            _SpecColor2             = FindProperty("_SpecColor2", props);
            _UseSpecColorMap2       = FindProperty("_UseSpecColorMap2", props);
            _SpecColorMap2          = FindProperty("_SpecColorMap2", props);
            _SpecIntensity2         = FindProperty("_SpecIntensity2", props);
            _UseSpecIntensityMap2   = FindProperty("_UseSpecIntensityMap2", props);
            _SpecIntensityMap2      = FindProperty("_SpecIntensityMap2", props);
            _SpecIntensityMap2_Channel = FindProperty("_SpecIntensityMap2_Channel", props);
            _SpecSmoothness2        = FindProperty("_SpecSmoothness2", props);
            _UseSpecSmoothnessMap2  = FindProperty("_UseSpecSmoothnessMap2", props);
            _SpecSmoothnessMap2     = FindProperty("_SpecSmoothnessMap2", props);
            _SpecSmoothnessMap2_Channel = FindProperty("_SpecSmoothnessMap2_Channel", props);
            _SpecNormalStrength2    = FindProperty("_SpecNormalStrength2", props);
        }

        protected override void DrawCustomProperties(Material material)
        {
            // GUIStyles Name   Description
            // ---------------- ------------------------------------
            // boxOuter         outer box
            // boxInnerHalf     inner box
            // boxInner         inner box without label
            // customBox        box (similar to unity default box)
            // customToggleFont label for box

            isShowCustomProperties = Foldout("Custom Properties", "Custom Properties", isShowCustomProperties);
            if(isShowCustomProperties)
            {
                EditorGUILayout.BeginVertical(boxOuter);
//                EditorGUILayout.LabelField(GetLoc("dennoko_extension"), customToggleFont);
                EditorGUILayout.BeginVertical(boxInnerHalf);

                // moved mask/noise into each layer foldout

                // Specular 1st
                isShowSpec1 = Foldout("Specular1st", "Specular 1st parameters", isShowSpec1);
                if(isShowSpec1)
                {
                    EditorGUILayout.BeginVertical(boxOuter);
                    EditorGUILayout.LabelField("Specular 1st", customToggleFont);
                    EditorGUILayout.BeginVertical(boxInner);

                    m_MaterialEditor.ShaderProperty(_EnableSpec1, new GUIContent("有効化", "スペキュラー1層目を有効にします。"));

                    // Mask / Noise for 1st layer
                    m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Mask 1", "1層目のスペキュラー適用範囲マスク。使用チャンネルでR/G/B/Aから選択します。"), _SpecMask1);
                    m_MaterialEditor.TextureScaleOffsetProperty(_SpecMask1);
                    DrawChannelPopup(_SpecMask1_Channel, "Mask 1 Channel", "マスクで使用するチャンネル (R/G/B/A)");
                    m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Noise 1", "1層目のスペキュラー強度に乗算するノイズ。使用チャンネルを選択できます。"), _SpecNoiseTex1);
                    m_MaterialEditor.TextureScaleOffsetProperty(_SpecNoiseTex1);
                    DrawChannelPopup(_SpecNoiseTex1_Channel, "Noise 1 Channel", "ノイズで使用するチャンネル (R/G/B/A)");

                    // Color
                    m_MaterialEditor.ShaderProperty(_UseSpecColorMap1, new GUIContent("カラーマップ使用", "スペキュラーカラーにテクスチャ(RGB)を使用します。OFFのときは下の色を使用します。"));
                    if(_UseSpecColorMap1.floatValue > 0.5f)
                    {
                        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Color Map (RGB)", "スペキュラーカラーマップ(RGB)。"), _SpecColorMap1);
                        m_MaterialEditor.TextureScaleOffsetProperty(_SpecColorMap1);
                    }
                    // Color with tooltip
                    EditorGUI.showMixedValue = _SpecColor1.hasMixedValue;
                    var col1 = _SpecColor1.colorValue;
                    var newCol1 = EditorGUILayout.ColorField(new GUIContent("色", "スペキュラーのベースカラー。カラーマップ未使用時に適用されます。"), col1, true, true, true);
                    if(newCol1 != col1) _SpecColor1.colorValue = newCol1;
                    EditorGUI.showMixedValue = false;

                    // Intensity
                    m_MaterialEditor.ShaderProperty(_UseSpecIntensityMap1, new GUIContent("強度マップ使用", "スペキュラー強度にテクスチャのチャンネルを使用します。OFFのときは下のスライダー値を使用します。"));
                    if(_UseSpecIntensityMap1.floatValue > 0.5f)
                    {
                        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Intensity Map", "強度マップ。使用チャンネルを選択できます。"), _SpecIntensityMap1);
                        m_MaterialEditor.TextureScaleOffsetProperty(_SpecIntensityMap1);
                        DrawChannelPopup(_SpecIntensityMap1_Channel, "Intensity Channel", "強度で使用するチャンネル (R/G/B/A)");
                    }
                    else
                    {
                        m_MaterialEditor.ShaderProperty(_SpecIntensity1, new GUIContent("強度", "スペキュラーの明るさ/寄与度。"));
                    }

                    // Smoothness
                    m_MaterialEditor.ShaderProperty(_UseSpecSmoothnessMap1, new GUIContent("スムースネスマップ使用", "ハイライトの鋭さ(スムースネス)にテクスチャのチャンネルを使用します。OFFのときは下のスライダー値を使用します。"));
                    if(_UseSpecSmoothnessMap1.floatValue > 0.5f)
                    {
                        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Smoothness Map", "スムースネスマップ。使用チャンネルを選択できます。"), _SpecSmoothnessMap1);
                        m_MaterialEditor.TextureScaleOffsetProperty(_SpecSmoothnessMap1);
                        DrawChannelPopup(_SpecSmoothnessMap1_Channel, "Smoothness Channel", "スムースネスで使用するチャンネル (R/G/B/A)");
                    }
                    else
                    {
                        m_MaterialEditor.ShaderProperty(_SpecSmoothness1, new GUIContent("スムースネス", "ハイライトの鋭さ。大きいほど鋭く小さいほど広がります。"));
                    }

                    // Normal Strength 1
                    m_MaterialEditor.ShaderProperty(_SpecNormalStrength1, new GUIContent("ノーマル強度", "1層目のノーマルマップ強度。0で無効、1でそのまま、2以上で傾きを強くします。値域が破綻しないよう内部で補間します。"));

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndVertical();
                }

                // Specular 2nd
                isShowSpec2 = Foldout("Specular2nd", "Specular 2nd parameters", isShowSpec2);
                if(isShowSpec2)
                {
                    EditorGUILayout.BeginVertical(boxOuter);
                    EditorGUILayout.LabelField("Specular 2nd", customToggleFont);
                    EditorGUILayout.BeginVertical(boxInner);

                    m_MaterialEditor.ShaderProperty(_EnableSpec2, new GUIContent("有効化", "スペキュラー2層目を有効にします。"));

                    // Mask / Noise for 2nd layer
                    m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Mask 2", "2層目のスペキュラー適用範囲マスク。使用チャンネルでR/G/B/Aから選択します。"), _SpecMask2);
                    m_MaterialEditor.TextureScaleOffsetProperty(_SpecMask2);
                    DrawChannelPopup(_SpecMask2_Channel, "Mask 2 Channel", "マスクで使用するチャンネル (R/G/B/A)");
                    m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Noise 2", "2層目のスペキュラー強度に乗算するノイズ。使用チャンネルを選択できます。"), _SpecNoiseTex2);
                    m_MaterialEditor.TextureScaleOffsetProperty(_SpecNoiseTex2);
                    DrawChannelPopup(_SpecNoiseTex2_Channel, "Noise 2 Channel", "ノイズで使用するチャンネル (R/G/B/A)");

                    // Color
                    m_MaterialEditor.ShaderProperty(_UseSpecColorMap2, new GUIContent("カラーマップ使用", "スペキュラーカラーにテクスチャ(RGB)を使用します。OFFのときは下の色を使用します。"));
                    if(_UseSpecColorMap2.floatValue > 0.5f)
                    {
                        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Color Map (RGB)", "スペキュラーカラーマップ(RGB)。"), _SpecColorMap2);
                        m_MaterialEditor.TextureScaleOffsetProperty(_SpecColorMap2);
                    }
                    // Color with tooltip
                    EditorGUI.showMixedValue = _SpecColor2.hasMixedValue;
                    var col2 = _SpecColor2.colorValue;
                    var newCol2 = EditorGUILayout.ColorField(new GUIContent("色", "スペキュラーのベースカラー。カラーマップ未使用時に適用されます。"), col2, true, true, true);
                    if(newCol2 != col2) _SpecColor2.colorValue = newCol2;
                    EditorGUI.showMixedValue = false;

                    // Intensity
                    m_MaterialEditor.ShaderProperty(_UseSpecIntensityMap2, new GUIContent("強度マップ使用", "スペキュラー強度にテクスチャのチャンネルを使用します。OFFのときは下のスライダー値を使用します。"));
                    if(_UseSpecIntensityMap2.floatValue > 0.5f)
                    {
                        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Intensity Map (R)", "強度マップ。使用チャンネルを選択できます。"), _SpecIntensityMap2);
                        m_MaterialEditor.TextureScaleOffsetProperty(_SpecIntensityMap2);
                        DrawChannelPopup(_SpecIntensityMap2_Channel, "Intensity Channel", "強度で使用するチャンネル (R/G/B/A)");
                    }
                    else
                    {
                        m_MaterialEditor.ShaderProperty(_SpecIntensity2, new GUIContent("強度", "スペキュラーの明るさ/寄与度。"));
                    }

                    // Smoothness
                    m_MaterialEditor.ShaderProperty(_UseSpecSmoothnessMap2, new GUIContent("スムースネスマップ使用", "ハイライトの鋭さ(スムースネス)にテクスチャのチャンネルを使用します。OFFのときは下のスライダー値を使用します。"));
                    if(_UseSpecSmoothnessMap2.floatValue > 0.5f)
                    {
                        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Smoothness Map (R)", "スムースネスマップ。使用チャンネルを選択できます。"), _SpecSmoothnessMap2);
                        m_MaterialEditor.TextureScaleOffsetProperty(_SpecSmoothnessMap2);
                        DrawChannelPopup(_SpecSmoothnessMap2_Channel, "Smoothness Channel", "スムースネスで使用するチャンネル (R/G/B/A)");
                    }
                    else
                    {
                        m_MaterialEditor.ShaderProperty(_SpecSmoothness2, new GUIContent("スムースネス", "ハイライトの鋭さ。大きいほど鋭く小さいほど広がります。"));
                    }

                    // Normal Strength 2
                    m_MaterialEditor.ShaderProperty(_SpecNormalStrength2, new GUIContent("ノーマル強度", "2層目のノーマルマップ強度。0で無効、1でそのまま、2以上で傾きを強くします。値域が破綻しないよう内部で補間します。"));

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
            }
        }

        private static readonly string[] kChannels = {"R", "G", "B", "A"};
        private void DrawChannelPopup(MaterialProperty prop, string label, string tooltip)
        {
            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int newVal = EditorGUILayout.Popup(new GUIContent(label, tooltip), (int)prop.floatValue, kChannels);
            if(EditorGUI.EndChangeCheck()) prop.floatValue = newVal;
            EditorGUI.showMixedValue = false;
        }

        protected override void ReplaceToCustomShaders()
        {
            lts         = Shader.Find(shaderName + "/lilToon");
            ltsc        = Shader.Find("Hidden/" + shaderName + "/Cutout");
            ltst        = Shader.Find("Hidden/" + shaderName + "/Transparent");
            ltsot       = Shader.Find("Hidden/" + shaderName + "/OnePassTransparent");
            ltstt       = Shader.Find("Hidden/" + shaderName + "/TwoPassTransparent");

            ltso        = Shader.Find("Hidden/" + shaderName + "/OpaqueOutline");
            ltsco       = Shader.Find("Hidden/" + shaderName + "/CutoutOutline");
            ltsto       = Shader.Find("Hidden/" + shaderName + "/TransparentOutline");
            ltsoto      = Shader.Find("Hidden/" + shaderName + "/OnePassTransparentOutline");
            ltstto      = Shader.Find("Hidden/" + shaderName + "/TwoPassTransparentOutline");

            ltsoo       = Shader.Find(shaderName + "/[Optional] OutlineOnly/Opaque");
            ltscoo      = Shader.Find(shaderName + "/[Optional] OutlineOnly/Cutout");
            ltstoo      = Shader.Find(shaderName + "/[Optional] OutlineOnly/Transparent");

            ltsl        = Shader.Find(shaderName + "/lilToonLite");
            ltslc       = Shader.Find("Hidden/" + shaderName + "/Lite/Cutout");
            ltslt       = Shader.Find("Hidden/" + shaderName + "/Lite/Transparent");
            ltslot      = Shader.Find("Hidden/" + shaderName + "/Lite/OnePassTransparent");
            ltsltt      = Shader.Find("Hidden/" + shaderName + "/Lite/TwoPassTransparent");

            ltslo       = Shader.Find("Hidden/" + shaderName + "/Lite/OpaqueOutline");
            ltslco      = Shader.Find("Hidden/" + shaderName + "/Lite/CutoutOutline");
            ltslto      = Shader.Find("Hidden/" + shaderName + "/Lite/TransparentOutline");
            ltsloto     = Shader.Find("Hidden/" + shaderName + "/Lite/OnePassTransparentOutline");
            ltsltto     = Shader.Find("Hidden/" + shaderName + "/Lite/TwoPassTransparentOutline");

            // Priority A variants (Tessellation, Refraction, Fur, Gem, Multi, Overlay/FakeShadow) have been removed.
        }

        // You can create a menu like this
        /*
        [MenuItem("Assets/TemplateFull/Convert material to custom shader", false, 1100)]
        private static void ConvertMaterialToCustomShaderMenu()
        {
            if(Selection.objects.Length == 0) return;
            TemplateFullInspector inspector = new TemplateFullInspector();
            for(int i = 0; i < Selection.objects.Length; i++)
            {
                if(Selection.objects[i] is Material)
                {
                    inspector.ConvertMaterialToCustomShader((Material)Selection.objects[i]);
                }
            }
        }
        */
    }
}
#endif