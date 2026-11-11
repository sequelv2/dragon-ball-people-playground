using System;
using System.IO;
using UnityEngine;

namespace DBPU
{
    public static class AssetBundleLoader
    {
        private static object LoadAssetFromAssetBundle(string assemblyPath, string bundlePath, string assetName, Type assetType)
        {
            string fullBundlePath = Path.Combine(ModAPI.Metadata.MetaLocation, bundlePath);
            AssetBundle assetBundle = AssetBundle.LoadFromFile(fullBundlePath);
            if (assetBundle == null)
            {
                Debug.Log("Failed to load AssetBundle!");
                return null;
            }

            UnityEngine.Object asset = assetBundle.LoadAsset(assetName, assetType);
            assetBundle.Unload(false);

            if (asset == null)
            {
                Debug.Log($"Failed to load {assetType.Name} from AssetBundle!");
                return null;
            }

            return asset;
        }

        public static GameObject LoadGameObjectFromAssetBundle(string assemblyPath, string bundlePath, string objectName)
        {
            return (GameObject)LoadAssetFromAssetBundle(assemblyPath, bundlePath, objectName, typeof(GameObject));
        }

        public static Material LoadMaterialFromAssetBundle(string assemblyPath, string bundlePath, string materialName)
        {
            return (Material)LoadAssetFromAssetBundle(assemblyPath, bundlePath, materialName, typeof(Material));
        }

        public static Material LoadMaterialFromAssetBundle(string assemblyPath, string bundlePath, params string[] materialNames)
        {
            if (materialNames == null) return null;
            for (int i = 0; i < materialNames.Length; i++)
            {
                if (string.IsNullOrEmpty(materialNames[i])) continue;
                Material mat = LoadMaterialFromAssetBundle(assemblyPath, bundlePath, materialNames[i]);
                if (mat != null) return mat;
            }
            return null;
        }

        public static ParticleSystem LoadParticleSystemFromAssetBundle(string assemblyPath, string bundlePath, string particleSystemName)
        {
            return (ParticleSystem)LoadAssetFromAssetBundle(assemblyPath, bundlePath, particleSystemName, typeof(ParticleSystem));
        }
    }
}

// Originally uploaded by 'Sequel'. Do not reupload without their explicit permission.
