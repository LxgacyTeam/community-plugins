using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WinterPack;

public static class BuildWinterPack
{
    public static void Build()
    {
        try
        {
            var map=JsonUtility.FromJson<TextureMap>(File.ReadAllText("Assets/winterpack-map.json"));
            var paths=new List<string> { "Assets/winterpack-map.json" };
            var names=new List<string> { "winterpack-map" };
            foreach(var e in map.entries)
            {
                string path="Assets/WinterTextures/"+e.key+".png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;
                importer.sRGBTexture=true;
                importer.mipmapEnabled=e.mipmaps;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency=false;
                importer.npotScale=TextureImporterNPOTScale.None;
                importer.maxTextureSize=8192;
                importer.wrapMode=(TextureWrapMode)e.wrap;
                importer.filterMode=(FilterMode)e.filter;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.isReadable=false;
                importer.SaveAndReimport();
                paths.Add(path); names.Add("winter/"+e.key);
            }
            AssetDatabase.SaveAssets();
            string output=Path.GetFullPath("../assetbundle");
            Directory.CreateDirectory(output);
            var result=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild {
                assetBundleName="winterpack.bundle",assetNames=paths.ToArray(),addressableNames=names.ToArray()
            }},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64);
            if(!result) throw new Exception("AssetBundle build failed");
            var bundle=AssetBundle.LoadFromFile(Path.Combine(output,"winterpack.bundle"));
            if(!bundle || bundle.GetAllAssetNames().Length!=paths.Count) throw new Exception("Bundle validation failed");
            foreach(var name in names) if(!bundle.LoadAsset(name)) throw new Exception("Missing asset: "+name);
            bundle.Unload(true);
            Debug.Log("WINTERPACK_BUILD_OK: "+map.entries.Length+" replacement textures, "+map.treeBindings.Length+" tree bindings, "+paths.Count+" assets.");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
    }
}
