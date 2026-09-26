#if UNITY_EDITOR && DNKW_VRCSDK3_AVATARS
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace lilToon
{
    // --------------------------------------------------
    // Upload-time stripping of the 2D mask sources
    //   The shader only reads _DnkwMaskArray, but the material still references the 2D textures
    //   the user assigned, which would pull them into the avatar bundle as well.
    //   On the build clone, swap each material for a copy without those references.
    //   Copies are persisted in a temporary folder (the SDK saves the clone as a prefab)
    //   and deleted after the build.
    // --------------------------------------------------
    public class DennokoMaskArrayBuildHook : IVRCSDKPreprocessAvatarCallback, IVRCSDKPostprocessAvatarCallback
    {
        private const string tempFolder = DennokoMaskArrayBuilder.BuildTempFolder;
        private const string logPrefix = "[dennoko Specular] ";

        // After NDMF (-11000 .. -1025) and lilToon (100) have produced the final materials.
        public int callbackOrder => 1000;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            try
            {
                var copies = new Dictionary<Material, Material>();
                foreach (Renderer renderer in avatarGameObject.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material src = mats[i];
                        if (!DennokoMaskArrayBuilder.IsTarget(src)) continue;
                        if (!copies.TryGetValue(src, out Material copy))
                        {
                            copy = CreateStrippedCopy(src);
                            copies.Add(src, copy);
                        }
                        mats[i] = copy;
                        changed = true;
                    }
                    if (changed) renderer.sharedMaterials = mats;
                }
                if (copies.Count > 0) AssetDatabase.SaveAssets();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"{logPrefix}ビルド前のマスク配列処理に失敗しました。");
                return false;
            }
        }

        public void OnPostprocessAvatar()
        {
            DeleteTempFolder();
        }

        static Material CreateStrippedCopy(Material src)
        {
            if (!AssetDatabase.IsValidFolder(tempFolder)) AssetDatabase.CreateFolder("Assets", tempFolder.Substring("Assets/".Length));

            var copy = new Material(src) { name = src.name };
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath($"{tempFolder}/{src.name}.mat"));

            // Stale arrays would upload wrong masks: the source may never have been inspected after a texture
            // edit, or an earlier build step (e.g. atlasing) may have replaced the mask textures.
            // Rebuild on the copy so the user's assets are never modified during an upload.
            if (!DennokoMaskArrayBuilder.IsUpToDate(src) || !DennokoMaskArrayBuilder.IsUpToDate(copy))
            {
                DennokoMaskArrayBuilder.Build(copy, $"{tempFolder}/{src.name}_MaskArray.asset");
            }

            foreach (DennokoMaskArrayBuilder.Slot slot in DennokoMaskArrayBuilder.Slots)
            {
                copy.SetTexture(slot.texture, null);
            }
            EditorUtility.SetDirty(copy);
            return copy;
        }

        // Also cleans up after a build that was aborted before the postprocess callback.
        [UnityEditor.Callbacks.DidReloadScripts]
        static void DeleteTempFolder()
        {
            if (AssetDatabase.IsValidFolder(tempFolder)) AssetDatabase.DeleteAsset(tempFolder);
        }
    }
}
#endif
