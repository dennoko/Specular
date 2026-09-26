#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    // --------------------------------------------------
    // Material schema migration
    //   v0 → v1: linear smoothness → log scale
    //            pow(2, lerp(3,10,s)) replaces lerp(8,1024,s)
    //            s_new = (log2(8 + 1016*s_old) - 3) / 7
    //   v1 → v2: _SpecIntensityMap* / _SpecSmoothnessMap* textures were removed
    //            (texture count limit). Intensity/Smoothness now read channels of _SpecMask*.
    //            Trivial cases are fixed automatically; cases that need a new packed
    //            texture are baked only on user request (inspector button / menu).
    // --------------------------------------------------
    public static class DennokoSpecularMigration
    {
        public const int CurrentSchemaVersion = 2;
        private const string shaderName = "dennoko_specularex";
        private const string logPrefix = "[dennoko Specular] ";
        private const int maxBakeSize = 4096;

        private static readonly string[] kLayers = {"1", "2"};
        private static readonly string[] kKinds = {"Intensity", "Smoothness"};

        private struct TexEnv
        {
            public Texture tex;
            public Vector2 scale;
            public Vector2 offset;
        }

        // ------------------------------------------------------------------
        // Automatic migration
        // ------------------------------------------------------------------

        [UnityEditor.Callbacks.DidReloadScripts]
        static void OnScriptsReloaded()
        {
            EditorApplication.delayCall += MigrateAllMaterials;
        }

        [MenuItem("Tools/dennoko/Migrate Specular Materials")]
        static void MigrateAllMaterials()
        {
            int count = 0;
            foreach (Material mat in FindSpecularMaterials())
            {
                if (MigrateMaterial(mat)) count++;
            }
            if (count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"{logPrefix}Migrated {count} material(s) to schema v{CurrentSchemaVersion}.");
            }

            int bakeCount = FindSpecularMaterials().Count(NeedsBake);
            if (bakeCount > 0)
            {
                Debug.LogWarning($"{logPrefix}{bakeCount} 個のマテリアルに旧バージョンの強度/スムースネスマップが残っています。インスペクターの「Mask にパックして変換」または Tools/dennoko/Bake Legacy Specular Maps で変換してください。");
            }
        }

        static IEnumerable<Material> FindSpecularMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == null) continue;
                if (!mat.shader.name.Contains(shaderName)) continue;
                yield return mat;
            }
        }

        static int GetSchemaVersion(Material mat)
        {
            // Materials saved before _SchemaVersion existed return the shader default when read through the API,
            // so check the raw YAML. Materials saved with a v1-era shader contain "_SchemaVersion: 0", which meant v1.
            string path = AssetDatabase.GetAssetPath(mat);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                string yaml = File.ReadAllText(path);
                if (!yaml.Contains("_SchemaVersion")) return 0;
                return Mathf.Max(1, (int)mat.GetFloat("_SchemaVersion"));
            }
            return (int)mat.GetFloat("_SchemaVersion");
        }

        // Returns true if the material was migrated.
        static bool MigrateMaterial(Material mat)
        {
            int version = GetSchemaVersion(mat);
            if (version >= CurrentSchemaVersion) return false;

            // Legacy texture entries must be cleared before any Material.Set* call so the SerializedObject is not stale.
            var toggleOff = new List<string>();
            if (version < 2)
            {
                var so = new SerializedObject(mat);
                foreach (string l in kLayers)
                foreach (string kind in kKinds)
                {
                    string toggle = $"_UseSpec{kind}Map{l}";
                    SerializedProperty entry = FindTexEnv(so, $"_Spec{kind}Map{l}");
                    Texture legacy = entry != null ? (Texture)entry.FindPropertyRelative("second.m_Texture").objectReferenceValue : null;

                    if (mat.GetFloat(toggle) <= 0.5f)
                    {
                        // Had no effect before; drop the stale reference.
                        if (legacy != null) ClearTexEnv(entry);
                    }
                    else if (legacy == null)
                    {
                        // Unassigned texture sampled as white (1.0) → same as the map being off.
                        toggleOff.Add(toggle);
                    }
                    else if (IsSameAsMask(mat, l, ReadTexEnv(entry)))
                    {
                        // The channel already points into the mask texture; nothing to bake.
                        ClearTexEnv(entry);
                    }
                    // Otherwise leave it for BakeMaterial.
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (version < 1)
            {
                float s1 = Mathf.Clamp01(mat.GetFloat("_SpecSmoothness1"));
                float s2 = Mathf.Clamp01(mat.GetFloat("_SpecSmoothness2"));
                mat.SetFloat("_SpecSmoothness1", Mathf.Clamp01((Mathf.Log(Mathf.Max(8f + 1016f * s1, 1e-6f), 2f) - 3f) / 7f));
                mat.SetFloat("_SpecSmoothness2", Mathf.Clamp01((Mathf.Log(Mathf.Max(8f + 1016f * s2, 1e-6f), 2f) - 3f) / 7f));
            }
            foreach (string toggle in toggleOff) mat.SetFloat(toggle, 0f);

            mat.SetFloat("_SchemaVersion", CurrentSchemaVersion);
            EditorUtility.SetDirty(mat);
            return true;
        }

        // ------------------------------------------------------------------
        // Legacy map baking (user-triggered)
        // ------------------------------------------------------------------

        public static bool NeedsBake(Material mat)
        {
            if (mat == null) return false;
            var so = new SerializedObject(mat);
            foreach (string l in kLayers)
            {
                if (LayerNeedsBake(mat, so, l)) return true;
            }
            return false;
        }

        static bool LayerNeedsBake(Material mat, SerializedObject so, string l)
        {
            foreach (string kind in kKinds)
            {
                if (mat.GetFloat($"_UseSpec{kind}Map{l}") <= 0.5f) continue;
                SerializedProperty entry = FindTexEnv(so, $"_Spec{kind}Map{l}");
                if (entry == null) continue;
                TexEnv legacy = ReadTexEnv(entry);
                if (legacy.tex != null && !IsSameAsMask(mat, l, legacy)) return true;
            }
            return false;
        }

        [MenuItem("Tools/dennoko/Bake Legacy Specular Maps")]
        static void BakeAllMaterialsMenu()
        {
            Material[] mats = FindSpecularMaterials().Where(NeedsBake).ToArray();
            if (mats.Length == 0)
            {
                EditorUtility.DisplayDialog("dennoko Specular", "変換が必要なマテリアルはありません。", "OK");
                return;
            }
            string list = string.Join("\n", mats.Take(20).Select(AssetDatabase.GetAssetPath));
            if (mats.Length > 20) list += $"\n...ほか {mats.Length - 20} 件";
            if (!EditorUtility.DisplayDialog("dennoko Specular",
                $"{mats.Length} 個のマテリアルの旧強度/スムースネスマップを Mask テクスチャにパックします。\nパック済みテクスチャは各マテリアルと同じフォルダに作成されます。\n\n{list}",
                "変換", "キャンセル")) return;
            BakeMaterials(mats);
        }

        public static void BakeMaterials(IEnumerable<Material> mats)
        {
            int count = 0;
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bake Legacy Specular Maps");
            try
            {
                foreach (Material mat in mats)
                {
                    if (mat == null) continue;
                    if (BakeMaterial(mat)) count++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(group);
            }
            if (count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"{logPrefix}Baked legacy specular maps for {count} material(s).");
            }
        }

        // Returns true if any layer was baked.
        static bool BakeMaterial(Material mat)
        {
            string matPath = AssetDatabase.GetAssetPath(mat);
            if (string.IsNullOrEmpty(matPath) || !matPath.StartsWith("Assets/"))
            {
                Debug.LogWarning($"{logPrefix}{mat.name}: Assets 外のマテリアルは変換できません ({matPath})。Assets 内にコピーしてから変換してください。", mat);
                return false;
            }

            // Bake assumes the v2 auto-migration has already resolved the trivial cases.
            MigrateMaterial(mat);

            bool baked = false;
            foreach (string l in kLayers)
            {
                var so = new SerializedObject(mat);
                if (!LayerNeedsBake(mat, so, l)) continue;
                EditorUtility.DisplayProgressBar("dennoko Specular", $"{mat.name} (Layer {l})", 0f);
                if (BakeLayer(mat, so, l, matPath)) baked = true;
            }
            return baked;
        }

        static bool BakeLayer(Material mat, SerializedObject so, string l, string matPath)
        {
            string maskName = $"_SpecMask{l}";
            TexEnv mask = new TexEnv
            {
                tex = mat.GetTexture(maskName),
                scale = mat.GetTextureScale(maskName),
                offset = mat.GetTextureOffset(maskName),
            };

            // Packed layout: R = mask, G = intensity, B = smoothness, A = 1
            var sources = new TexEnv?[3];
            var channels = new int[3];
            sources[0] = mask;
            channels[0] = (int)mat.GetFloat($"{maskName}_Channel");
            for (int k = 0; k < kKinds.Length; k++)
            {
                string kind = kKinds[k];
                channels[k + 1] = (int)mat.GetFloat($"_Spec{kind}Map{l}_Channel");
                if (mat.GetFloat($"_UseSpec{kind}Map{l}") <= 0.5f) continue;
                SerializedProperty entry = FindTexEnv(so, $"_Spec{kind}Map{l}");
                TexEnv? legacy = entry != null ? ReadTexEnv(entry) : (TexEnv?)null;
                // Migration cleared (or never had) the legacy ref → the channel already reads the mask.
                sources[k + 1] = legacy.HasValue && legacy.Value.tex != null ? legacy : mask;
            }

            // The packed texture lives in the mask slot, so its tiling is the mask's.
            // If the mask was unassigned, adopt the first legacy texture's tiling instead.
            Vector2 baseScale = mask.scale;
            Vector2 baseOffset = mask.offset;
            if (mask.tex == null)
            {
                TexEnv? first = sources.Skip(1).FirstOrDefault(s => s.HasValue && s.Value.tex != null);
                if (first.HasValue)
                {
                    baseScale = first.Value.scale;
                    baseOffset = first.Value.offset;
                }
            }
            if (Mathf.Approximately(baseScale.x, 0f)) baseScale.x = 1f;
            if (Mathf.Approximately(baseScale.y, 0f)) baseScale.y = 1f;

            int width = 0, height = 0;
            foreach (TexEnv? s in sources)
            {
                if (!s.HasValue || s.Value.tex == null) continue;
                width = Mathf.Max(width, s.Value.tex.width);
                height = Mathf.Max(height, s.Value.tex.height);
            }
            if (width == 0 || height == 0) return false;
            width = Mathf.Min(width, maxBakeSize);
            height = Mathf.Min(height, maxBakeSize);

            var packed = new Color[width * height];
            for (int i = 0; i < packed.Length; i++) packed[i] = Color.white;

            for (int k = 0; k < sources.Length; k++)
            {
                if (!sources[k].HasValue || sources[k].Value.tex == null) continue;
                TexEnv src = sources[k].Value;

                // Old shader: srcUV = uv * S + O, packed texture: maskUV = uv * Sb + Ob
                // → srcUV = maskUV * (S / Sb) + (O - Ob * S / Sb)
                var ratio = new Vector2(src.scale.x / baseScale.x, src.scale.y / baseScale.y);
                var offset = new Vector2(src.offset.x - baseOffset.x * ratio.x, src.offset.y - baseOffset.y * ratio.y);
                if (!IsInteger(ratio.x) || !IsInteger(ratio.y))
                {
                    Debug.LogWarning($"{logPrefix}{mat.name} (Layer {l}): {src.tex.name} の Tiling が Mask の整数倍ではないため、タイリングの継ぎ目で見た目が完全には一致しません。", mat);
                }

                Color[] pixels = ReadPixels(src.tex, width, height, ratio, offset);
                int ch = Mathf.Clamp(channels[k], 0, 3);
                for (int i = 0; i < packed.Length; i++) packed[i][k] = pixels[i][ch];
            }

            Texture2D packedTex = SavePackedTexture(packed, width, height,
                Path.Combine(Path.GetDirectoryName(matPath), $"{mat.name}_SpecMask{l}_Packed.png").Replace('\\', '/'));
            if (packedTex == null) return false;

            // Record before touching the material so undo restores both the legacy refs and the mask slot.
            Undo.RecordObject(mat, "Bake Legacy Specular Maps");
            var clearSo = new SerializedObject(mat);
            foreach (string kind in kKinds)
            {
                SerializedProperty entry = FindTexEnv(clearSo, $"_Spec{kind}Map{l}");
                if (entry != null) ClearTexEnv(entry);
            }
            clearSo.ApplyModifiedPropertiesWithoutUndo();

            mat.SetTexture(maskName, packedTex);
            mat.SetTextureScale(maskName, baseScale);
            mat.SetTextureOffset(maskName, baseOffset);
            mat.SetFloat($"{maskName}_Channel", 0);
            mat.SetFloat($"_SpecIntensityMap{l}_Channel", 1);
            mat.SetFloat($"_SpecSmoothnessMap{l}_Channel", 2);
            EditorUtility.SetDirty(mat);
            Debug.Log($"{logPrefix}{mat.name} (Layer {l}): {AssetDatabase.GetAssetPath(packedTex)} を作成して Mask に設定しました (R=マスク, G=強度, B=スムースネス)。", mat);
            return true;
        }

        static Color[] ReadPixels(Texture src, int width, int height, Vector2 scale, Vector2 offset)
        {
            // The shader sampled every map with sampler_linear_repeat, regardless of the texture's own wrap mode.
            TextureWrapMode prevWrap = src.wrapMode;
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                src.wrapMode = TextureWrapMode.Repeat;
                // Linear target: sRGB sources are decoded on sampling, matching what the shader saw.
                Graphics.Blit(src, rt, scale, offset);
                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();
                return readback.GetPixels();
            }
            finally
            {
                src.wrapMode = prevWrap;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(readback);
            }
        }

        static Texture2D SavePackedTexture(Color[] pixels, int width, int height, string path)
        {
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                tex.SetPixels(pixels);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                // Data texture: store the linear values the old shader sampled.
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        static bool IsSameAsMask(Material mat, string l, TexEnv legacy)
        {
            string maskName = $"_SpecMask{l}";
            return legacy.tex == mat.GetTexture(maskName)
                && legacy.scale == mat.GetTextureScale(maskName)
                && legacy.offset == mat.GetTextureOffset(maskName);
        }

        static bool IsInteger(float v)
        {
            return !Mathf.Approximately(v, 0f) && Mathf.Abs(v - Mathf.Round(v)) < 1e-4f;
        }

        // Textures removed from the shader stay in m_SavedProperties, but Material.GetTexture can no longer read them.
        static SerializedProperty FindTexEnv(SerializedObject so, string name)
        {
            SerializedProperty texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (texEnvs == null) return null;
            for (int i = 0; i < texEnvs.arraySize; i++)
            {
                SerializedProperty entry = texEnvs.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == name) return entry;
            }
            return null;
        }

        static TexEnv ReadTexEnv(SerializedProperty entry)
        {
            return new TexEnv
            {
                tex = entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture,
                scale = entry.FindPropertyRelative("second.m_Scale").vector2Value,
                offset = entry.FindPropertyRelative("second.m_Offset").vector2Value,
            };
        }

        static void ClearTexEnv(SerializedProperty entry)
        {
            entry.FindPropertyRelative("second.m_Texture").objectReferenceValue = null;
        }
    }
}
#endif
