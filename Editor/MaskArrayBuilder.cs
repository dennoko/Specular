#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    // --------------------------------------------------
    // Scalar mask packing
    //   Users assign ordinary 2D textures to each mask slot. The shader never samples those;
    //   instead this builder resamples every assigned slot (with its channel resolved) into
    //   one single-channel Texture2DArray, so all masks cost one texture parameter.
    //   Tiling/Offset stays per slot and is applied in the shader.
    // --------------------------------------------------
    public static class DennokoMaskArrayBuilder
    {
        public const string ArrayProperty = "_DnkwMaskArray";
        public const string HashProperty = "_DnkwMaskArrayHash";
        private const string shaderName = "dennoko_specularex";
        private const string logPrefix = "[dennoko Specular] ";
        private const int formatVersion = 1;   // bump when the baked layout/format changes
        private const int maxSize = 4096;
        // Stripped material copies made by DennokoMaskArrayBuildHook live here during an upload.
        public const string BuildTempFolder = "Assets/__DnkwSpecularBuildTemp";

        public struct Slot
        {
            public string texture;  // 2D texture property the user edits
            public string channel;  // channel selector property, or null for R

            public Slot(string texture, string channel)
            {
                this.texture = texture;
                this.channel = channel;
            }
        }

        public static readonly Slot[] Slots =
        {
            new Slot("_SpecMask1", "_SpecMask1_Channel"),
            new Slot("_SpecMask2", "_SpecMask2_Channel"),
            new Slot("_SpecNoiseTex1", "_SpecNoiseTex1_Channel"),
            new Slot("_SpecNoiseTex2", "_SpecNoiseTex2_Channel"),
            new Slot("_SpecIntensityMap1", "_SpecIntensityMap1_Channel"),
            new Slot("_SpecIntensityMap2", "_SpecIntensityMap2_Channel"),
            new Slot("_SpecSmoothnessMap1", "_SpecSmoothnessMap1_Channel"),
            new Slot("_SpecSmoothnessMap2", "_SpecSmoothnessMap2_Channel"),
            new Slot("_CustomMatCap1_Mask", null),
        };

        public static bool IsTarget(Material mat)
        {
            return mat != null && mat.shader != null && mat.shader.name.Contains(shaderName) && mat.HasProperty(ArrayProperty);
        }

        static bool IsEditableMaterialPath(string path)
        {
            return path.StartsWith("Assets/") && !path.StartsWith(BuildTempFolder + "/");
        }

        // Array files carry the owner's GUID so a duplicated material (which still points at the
        // original's array) is detected and gets its own array instead of overwriting a shared one.
        static string OwnerSuffix(Material mat)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mat));
            return string.IsNullOrEmpty(guid) ? "" : "_" + guid.Substring(0, 8);
        }

        static bool IsOwnedArray(Material mat, Texture array)
        {
            if (array == null) return false;
            // Materials we cannot rebuild (in-memory, NDMF-generated, build copies) just use what they reference.
            if (!IsEditableMaterialPath(AssetDatabase.GetAssetPath(mat))) return true;
            string path = AssetDatabase.GetAssetPath(array);
            return path.StartsWith("Assets/") && Path.GetFileNameWithoutExtension(path).EndsWith(OwnerSuffix(mat));
        }

        static int GetChannel(Material mat, Slot slot)
        {
            return slot.channel != null ? Mathf.Clamp((int)mat.GetFloat(slot.channel), 0, 3) : 0;
        }

        // ------------------------------------------------------------------
        // Staleness
        // ------------------------------------------------------------------

        // Covers the assigned textures (including their import/content state) and channels.
        // Stored as a float, so it is truncated to 24 bits to stay exact.
        public static int ComputeHash(Material mat)
        {
            var sb = new StringBuilder();
            sb.Append(formatVersion);
            foreach (Slot slot in Slots)
            {
                Texture tex = mat.GetTexture(slot.texture);
                sb.Append('|').Append(slot.texture).Append(':');
                if (tex == null) continue;
                string path = AssetDatabase.GetAssetPath(tex);
                sb.Append(AssetDatabase.AssetPathToGUID(path)).Append(':')
                  .Append(AssetDatabase.GetAssetDependencyHash(path).ToString()).Append(':')
                  .Append(GetChannel(mat, slot));
            }

            // FNV-1a (string.GetHashCode is not guaranteed stable across runtimes)
            uint h = 2166136261;
            foreach (char c in sb.ToString())
            {
                h ^= c;
                h *= 16777619;
            }
            // 0 is reserved for "never built"
            return (int)(h & 0xFFFFFF) | 1;
        }

        public static bool IsUpToDate(Material mat)
        {
            if (!IsTarget(mat)) return true;
            bool hasSource = Slots.Any(s => mat.GetTexture(s.texture) != null);
            if (hasSource && !IsOwnedArray(mat, mat.GetTexture(ArrayProperty))) return false;
            if (!hasSource) return Slots.All(s => mat.GetFloat(s.texture + "_Slice") < 0f);
            return (int)mat.GetFloat(HashProperty) == ComputeHash(mat);
        }

        // ------------------------------------------------------------------
        // Automatic rebuild
        // ------------------------------------------------------------------

        private static readonly HashSet<Material> queued = new HashSet<Material>();

        // Safe to call from OnGUI: the rebuild runs after the current GUI pass.
        public static void QueueRebuildIfStale(Material mat)
        {
            if (mat == null || queued.Contains(mat) || IsUpToDate(mat)) return;
            queued.Add(mat);
            EditorApplication.delayCall += () =>
            {
                queued.Remove(mat);
                if (mat != null && !IsUpToDate(mat)) Build(mat);
            };
        }

        // Existing materials have every slice at -1 (all masks white) until their array is built.
        [UnityEditor.Callbacks.DidReloadScripts]
        static void OnScriptsReloaded()
        {
            EditorApplication.delayCall += () => RebuildAll(false);
        }

        [MenuItem("Tools/dennoko/Rebuild Specular Mask Arrays")]
        static void RebuildAllMenu()
        {
            RebuildAll(true);
        }

        static void RebuildAll(bool force)
        {
            int count = 0;
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Material"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!IsEditableMaterialPath(path)) continue;
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!IsTarget(mat)) continue;
                    if (!force && IsUpToDate(mat)) continue;
                    EditorUtility.DisplayProgressBar("dennoko Specular", $"Mask array: {mat.name}", 0f);
                    if (Build(mat)) count++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            if (count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"{logPrefix}Rebuilt mask arrays for {count} material(s).");
            }
        }

        // Rebuild materials whose masks were reimported while not shown in the inspector.
        class TexturePostprocessor : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                var changed = new HashSet<string>(imported.Where(p => AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(Texture2D)));
                if (changed.Count == 0) return;
                EditorApplication.delayCall += () =>
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:Material"))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!IsEditableMaterialPath(path)) continue;
                        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!IsTarget(mat)) continue;
                        if (!Slots.Any(s => mat.GetTexture(s.texture) is Texture t && changed.Contains(AssetDatabase.GetAssetPath(t)))) continue;
                        QueueRebuildIfStale(mat);
                    }
                };
            }
        }

        // ------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------

        // Returns false if the material could not be updated.
        // arrayPathOverride: where to save the array instead of next to the material (used for build copies).
        public static bool Build(Material mat, string arrayPathOverride = null)
        {
            if (!IsTarget(mat)) return false;
            string matPath = AssetDatabase.GetAssetPath(mat);
            if (arrayPathOverride == null && !IsEditableMaterialPath(matPath))
            {
                Debug.LogWarning($"{logPrefix}{mat.name}: Assets 外のマテリアルはマスク配列を生成できません ({matPath})。", mat);
                return false;
            }

            // Deduplicate identical (texture, channel) pairs into one slice.
            var sliceKeys = new List<(Texture tex, int channel)>();
            var slotSlices = new float[Slots.Length];
            for (int i = 0; i < Slots.Length; i++)
            {
                Texture tex = mat.GetTexture(Slots[i].texture);
                if (tex == null) { slotSlices[i] = -1f; continue; }
                var key = (tex, GetChannel(mat, Slots[i]));
                int index = sliceKeys.IndexOf(key);
                if (index < 0) { index = sliceKeys.Count; sliceKeys.Add(key); }
                slotSlices[i] = index;
            }

            Texture2DArray existing = mat.GetTexture(ArrayProperty) as Texture2DArray;
            Texture2DArray array = null;
            if (sliceKeys.Count > 0)
            {
                array = BuildArray(sliceKeys);
                string arrayPath = arrayPathOverride != null ? AssetDatabase.GenerateUniqueAssetPath(arrayPathOverride)
                    : existing != null && IsOwnedArray(mat, existing) ? AssetDatabase.GetAssetPath(existing)
                    : AssetDatabase.GenerateUniqueAssetPath(Path.Combine(Path.GetDirectoryName(matPath), $"{mat.name}_MaskArray{OwnerSuffix(mat)}.asset").Replace('\\', '/'));
                array = SaveArray(array, arrayPath);
            }

            mat.SetTexture(ArrayProperty, array);
            for (int i = 0; i < Slots.Length; i++) mat.SetFloat(Slots[i].texture + "_Slice", slotSlices[i]);
            mat.SetFloat(HashProperty, sliceKeys.Count > 0 ? ComputeHash(mat) : 0f);
            EditorUtility.SetDirty(mat);
            return true;
        }

        static Texture2DArray BuildArray(List<(Texture tex, int channel)> sliceKeys)
        {
            // All slices must share size/format/mips; use the largest source, rounded up to BC4's 4x4 blocks.
            int width = Mathf.Min(sliceKeys.Max(k => k.tex.width), maxSize);
            int height = Mathf.Min(sliceKeys.Max(k => k.tex.height), maxSize);
            width = (width + 3) & ~3;
            height = (height + 3) & ~3;

            Texture2DArray array = null;
            for (int i = 0; i < sliceKeys.Count; i++)
            {
                Texture2D slice = BakeSlice(sliceKeys[i].tex, sliceKeys[i].channel, width, height);
                try
                {
                    if (array == null)
                    {
                        array = new Texture2DArray(width, height, sliceKeys.Count, slice.format, true, true);
                        array.wrapMode = TextureWrapMode.Repeat;
                        array.filterMode = FilterMode.Trilinear;
                    }
                    for (int mip = 0; mip < slice.mipmapCount; mip++)
                    {
                        array.SetPixelData(slice.GetPixelData<byte>(mip), mip, i);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(slice);
                }
            }
            array.Apply(false, false);
            return array;
        }

        // Resamples one channel of src to width x height and compresses it to BC4 (falls back to R8).
        static Texture2D BakeSlice(Texture src, int channel, int width, int height)
        {
            TextureWrapMode prevWrap = src.wrapMode;
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            Color32[] pixels;
            try
            {
                // The old shader sampled with sampler_linear_repeat; linear target keeps the decoded values it saw.
                src.wrapMode = TextureWrapMode.Repeat;
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();
                pixels = readback.GetPixels32();
            }
            finally
            {
                src.wrapMode = prevWrap;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(readback);
            }

            var data = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                data[i] = channel == 0 ? c.r : channel == 1 ? c.g : channel == 2 ? c.b : c.a;
            }

            var slice = new Texture2D(width, height, TextureFormat.R8, true, true);
            slice.SetPixelData(data, 0);
            slice.Apply(true, false);
            EditorUtility.CompressTexture(slice, TextureFormat.BC4, TextureCompressionQuality.Normal);
            return slice;
        }

        static Texture2DArray SaveArray(Texture2DArray array, string path)
        {
            // CopySerialized also copies the name; keep it matching the file.
            array.name = Path.GetFileNameWithoutExtension(path);
            Texture2DArray asset = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (asset != null)
            {
                // Overwrite in place to keep the GUID (and references) stable.
                EditorUtility.CopySerialized(array, asset);
                Object.DestroyImmediate(array);
            }
            else
            {
                AssetDatabase.CreateAsset(array, path);
                asset = array;
            }

            // TODO: script-created arrays stay CPU-readable (extra RAM at runtime). Verify whether
            // clearing m_IsReadable keeps the pixel data in the saved asset before enabling it.
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
#endif
