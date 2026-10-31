using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WinterPack;

public static class ValidateWinterPack
{
    public static void Run()
    {
        Validate(false);
    }

    public static void RunEmbedded()
    {
        Validate(true);
    }

    private static void Validate(bool embedded)
    {
        try
        {
            AssetBundle bundle;
            System.Reflection.Assembly assembly=null;
            if(embedded)
            {
                assembly=System.Reflection.Assembly.Load(File.ReadAllBytes(Path.GetFullPath("../bin/WinterPack.dll")));
                using(var stream=assembly.GetManifestResourceStream("WinterPack.Resources.winterpack.bundle"))
                using(var buffer=new MemoryStream())
                {
                    if(stream==null) throw new Exception("Embedded bundle resource missing");
                    stream.CopyTo(buffer);
                    var bytes=buffer.ToArray();
                    using(var hash=System.Security.Cryptography.SHA256.Create())
                        if(Convert.ToBase64String(hash.ComputeHash(bytes))!=Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes("../assetbundle/winterpack.bundle"))))
                            throw new Exception("Embedded bundle differs from build input");
                    bundle=AssetBundle.LoadFromMemory(bytes);
                }
            }
            else bundle=AssetBundle.LoadFromFile(Path.GetFullPath("../assetbundle/winterpack.bundle"));
            if(!bundle) throw new Exception("Bundle missing");
            var json=bundle.LoadAsset<TextAsset>("winterpack-map").text;
            var map=JsonUtility.FromJson<TextureMap>(json);
            if(embedded)
            {
                string managed="R:/BigCityLegacy/prod_game/game_Data/Managed";
                var args=Environment.GetCommandLineArgs();
                for(int i=0;i+1<args.Length;i++) if(args[i]=="-winterPackManagedDir") managed=args[i+1];
                System.Reflection.Assembly.LoadFrom(Path.Combine(managed,"Newtonsoft.Json.dll"));
                var parse=assembly.GetType("WinterPack.TextureMapReader",true).GetMethod("Parse");
                var runtimeMap=parse.Invoke(null,new object[]{json});
                AssertSameData(map,runtimeMap,"map");
                foreach(var invalid in new[]{"null","{}","{\"entries\":[],\"rules\":[]}",
                    json.Replace("\"entries\"","\"missingEntries\""),json.Replace("\"treeBindings\"","\"missingBindings\"")})
                {
                    bool rejected=false;
                    try { parse.Invoke(null,new object[]{invalid}); }
                    catch(System.Reflection.TargetInvocationException error)
                    {
                        if(!(error.InnerException is InvalidDataException)) throw;
                        rejected=true;
                    }
                    if(!rejected) throw new Exception("Runtime map reader accepted invalid JSON structure");
                }
                Debug.Log("WINTERPACK_RUNTIME_PARSER_OK: actual DLL parser matches all map fields; 5 malformed maps rejected.");
            }
            var textures=new Dictionary<string,Texture2D>();
            foreach(var e in map.entries)
            {
                var texture=bundle.LoadAsset<Texture2D>("winter/"+e.key);
                if(!texture) throw new Exception("Missing winter texture: "+e.key);
                textures.Add(e.key,texture);
            }
            if(bundle.GetAllAssetNames().Length!=map.entries.Length+1) throw new Exception("Unexpected bundled asset count");
            if(embedded) ValidateTreeSwapping(assembly,map,textures,json);
            bundle.Unload(true);
            Debug.Log("WINTERPACK_VALIDATION_OK: "+map.entries.Length+" replacements, "+map.treeBindings.Length+" tree mesh bindings.");
            if(embedded) Debug.Log("WINTERPACK_EMBEDDED_VALIDATION_OK: resource hash matches, AssetBundle loaded from DLL memory.");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
    }

    private static void ValidateTreeSwapping(System.Reflection.Assembly assembly,TextureMap map,Dictionary<string,Texture2D> textures,string json)
    {
        var reader=assembly.GetType("WinterPack.TextureMapReader").GetMethod("Parse");
        var runtimeMap=reader.Invoke(null,new object[]{json});
        var runtimeBindings=runtimeMap.GetType().GetField("treeBindings").GetValue(runtimeMap);
        var type=assembly.GetType("WinterPack.TreeTextureSwapper",true);
        var swapper=Activator.CreateInstance(type,new object[]{runtimeBindings,textures});
        var apply=type.GetMethod("Apply");var restore=type.GetMethod("Restore");
        var objects=new List<GameObject>();var meshes=new List<Mesh>();
        var original=new Texture2D(16,16) { name="diffuse" };
        // The same source material/texture is deliberately shared by every tree.
        // In particular, PathIDs 1771 and 2005 must still select different variants.
        var source=new Material(Shader.Find("Standard")) { name="Renamed fallback material",mainTexture=original };
        try
        {
            foreach(var binding in map.treeBindings)
            {
                var mesh=new Mesh { name="Renamed unreadable tree mesh" };
                mesh.vertices=new Vector3[binding.vertexCount];mesh.subMeshCount=binding.indexCounts.Length;
                for(int i=0;i<binding.indexCounts.Length;i++) mesh.SetIndices(new int[binding.indexCounts[i]],MeshTopology.Triangles,i);
                mesh.UploadMeshData(true);meshes.Add(mesh);
                var go=new GameObject("Runtime renamed tree");objects.Add(go);
                var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{source,source};
                if(!(bool)apply.Invoke(swapper,new object[]{filter,null})) throw new Exception("Tree not bound: "+binding.tree);
                foreach(var slot in binding.slots)
                {
                    var material=renderer.sharedMaterials[slot.index];
                    if(material.GetTexture(slot.property)!=textures[slot.key] || material.shader!=source.shader)
                        throw new Exception("Incorrect tree slot replacement: "+binding.tree+" "+slot.index);
                }
                if((bool)apply.Invoke(swapper,new object[]{filter,null})) throw new Exception("Repeated application created another replacement");
            }
            if(source.mainTexture!=original) throw new Exception("Shared original material was modified");
            var inherited=UnityEngine.Object.Instantiate(objects[0]);objects.Add(inherited);
            var late=new GameObject("Late tree");objects.Add(late);
            var lateFilter=late.AddComponent<MeshFilter>();lateFilter.sharedMesh=meshes[0];
            late.AddComponent<MeshRenderer>().sharedMaterials=new[]{source,source};
            if(!(bool)apply.Invoke(swapper,new object[]{lateFilter,null})) throw new Exception("Late-loaded tree was skipped");
            var unknown=new GameObject("Unrelated object");objects.Add(unknown);
            var otherMesh=new Mesh();otherMesh.vertices=new Vector3[3];otherMesh.triangles=new[]{0,1,2};meshes.Add(otherMesh);
            var otherFilter=unknown.AddComponent<MeshFilter>();otherFilter.sharedMesh=otherMesh;
            unknown.AddComponent<MeshRenderer>().sharedMaterial=source;
            if((bool)apply.Invoke(swapper,new object[]{otherFilter,null})) throw new Exception("Unrelated mesh was modified");
            restore.Invoke(swapper,null);
            foreach(var go in objects) foreach(var material in go.GetComponent<Renderer>().sharedMaterials)
                if(material!=source) throw new Exception("Summer failed to restore original material: "+go.name);
            // Repeated winter/summer cycles must discard the previous variant caches.
            foreach(var go in objects) apply.Invoke(swapper,new object[]{go.GetComponent<MeshFilter>(),null});
            restore.Invoke(swapper,null);
            foreach(var go in objects) foreach(var material in go.GetComponent<Renderer>().sharedMaterials)
                if(material!=source) throw new Exception("Second summer cycle did not restore materials");
            Debug.Log("WINTERPACK_TREE_SWAP_OK: 17 unreadable meshes / 34 slots, renamed Standard materials, shared originals, inherited/late instances, 2 winter/summer cycles.");
        }
        finally
        {
            restore.Invoke(swapper,null);
            foreach(var go in objects) UnityEngine.Object.DestroyImmediate(go);
            foreach(var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(original);
        }
    }

    private static void AssertSameData(object expected,object actual,string path)
    {
        if(expected==null || actual==null)
        {
            if(expected!=null || actual!=null) throw new Exception("Null data mismatch at "+path);
            return;
        }
        var array=expected as Array;
        if(array!=null)
        {
            var other=actual as Array;
            if(other==null || array.Length!=other.Length) throw new Exception("Array mismatch at "+path);
            for(int i=0;i<array.Length;i++) AssertSameData(array.GetValue(i),other.GetValue(i),path+"["+i+"]");
            return;
        }
        var type=expected.GetType();
        if(type.IsPrimitive || expected is string)
        {
            if(!expected.Equals(actual)) throw new Exception("Value mismatch at "+path);
            return;
        }
        foreach(var field in type.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
        {
            var other=actual.GetType().GetField(field.Name);
            if(other==null) throw new Exception("Missing field at "+path+"."+field.Name);
            AssertSameData(field.GetValue(expected),other.GetValue(actual),path+"."+field.Name);
        }
    }
}
