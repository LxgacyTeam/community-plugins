using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace WinterPack
{
    [BepInPlugin(Guid, "WinterPack", "1.0.1")]
    [BepInDependency("com.bigcitylegacy.baseplugin", "1.4.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid="com.dvwhrf.winterpack";
        private const string SettingId=Guid+".season", PrefKey=Guid+".Season";
        private const string BundleResourceName="WinterPack.Resources.winterpack.bundle";
        private AssetBundle bundle;
        private TextureMap map;
        private bool winter;
        private readonly Dictionary<string,Texture2D> replacements=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
        private TreeTextureSwapper treeSwapper;
        private TileOverlayComposer tileComposer;
        private int lastTreeCount=-1;
        private readonly Dictionary<Texture,Texture> originals=new Dictionary<Texture,Texture>();
        private readonly List<Change> changes=new List<Change>();
        private readonly Dictionary<Sprite,Sprite> sprites=new Dictionary<Sprite,Sprite>();
        private sealed class Change
        {
            public Object Owner; public Func<Object> Read; public Action<Object> Write; public Object Before,After;
        }

        private void Start()
        {
            var item=LegacySettingsMenuHelper.RegisterTextChoice(SettingId,"TODO",Vector2.zero,
                new LegacyLocalizedText("Season","Время года"),
                LegacyTextChoiceSettingBinding.PlayerPrefs(PrefKey,1,new[] {
                    new LegacyLocalizedText("Winter","Зима"),new LegacyLocalizedText("Summer","Лето")
                },index=>SetSeason(index==0)));
            item.UseAnchoredPosition=false;
            item.OnCreated=PlaceSeason;
            SetSeason(PlayerPrefs.GetInt(PrefKey,1)==0);
            StartCoroutine(WatchResources());
            Logger.LogInfo("WinterPack ready; default season: Summer. GUID: "+Guid);
        }

        private static void PlaceSeason(GameObject clone)
        {
            if(!clone || !clone.transform.parent) return;
            var parent=clone.transform.parent;
            var anchor=(parent.Find("BigCityLegacySetting_todo.player_profile") ??
                parent.Find("BigCityLegacySetting_todo.weather_lock") ?? parent.Find("TimeWeather")) as RectTransform;
            var rect=clone.transform as RectTransform;
            if(!anchor || !rect) return;
            float shift=anchor.sizeDelta.y+8f;
            rect.anchoredPosition=anchor.anchoredPosition-new Vector2(0,shift);
            foreach(Transform child in parent)
            {
                if(child==anchor || child==rect) continue;
                var other=child as RectTransform;
                if(other && other.anchoredPosition.y<anchor.anchoredPosition.y-1f)
                    other.anchoredPosition-=new Vector2(0,shift);
            }
        }

        private bool LoadBundle()
        {
            if(bundle) return true;
            try
            {
                using(var stream=typeof(Plugin).Assembly.GetManifestResourceStream(BundleResourceName))
                {
                    if(stream==null) throw new IOException("Missing embedded resource: "+BundleResourceName);
                    using(var buffer=new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        bundle=AssetBundle.LoadFromMemory(buffer.ToArray());
                    }
                }
                if(!bundle) throw new IOException("Cannot load embedded AssetBundle: "+BundleResourceName);
                var manifest=bundle.LoadAsset<TextAsset>("winterpack-map");
                if(!manifest) throw new IOException("Missing winterpack-map");
                map=TextureMapReader.Parse(manifest.text);
                foreach(var entry in map.entries)
                {
                    var tex=bundle.LoadAsset<Texture2D>("winter/"+entry.key);
                    if(!tex) throw new IOException("Missing winter/"+entry.key);
                    replacements.Add(entry.key,tex);
                }
                treeSwapper=new TreeTextureSwapper(map.treeBindings,replacements);
                tileComposer=new TileOverlayComposer(bundle.LoadAsset<Shader>("tile-overlay-shader"));
                Logger.LogInfo("Loaded "+map.entries.Length+" winter textures.");
                return true;
            }
            catch(Exception e)
            {
                Logger.LogError(e);
                if(tileComposer!=null) tileComposer.Dispose();
                tileComposer=null;
                if(bundle) bundle.Unload(true);
                bundle=null;map=null;treeSwapper=null;replacements.Clear();
                return false;
            }
        }

        private void SetSeason(bool value)
        {
            if(value && !LoadBundle())
            {
                PlayerPrefs.SetInt(PrefKey,1);PlayerPrefs.Save();value=false;
            }
            winter=value;
            if(winter) ScanSafely(); else Restore();
        }

        private IEnumerator WatchResources()
        {
            var delay=new WaitForSecondsRealtime(2f);
            while(true) { yield return delay; if(winter) ScanSafely(); }
        }

        private void ScanSafely()
        {
            try { Scan(); }
            catch(Exception e) { Logger.LogError("Texture replacement failed: "+e);winter=false;Restore();PlayerPrefs.SetInt(PrefKey,1);PlayerPrefs.Save(); }
        }

        private void Scan()
        {
            changes.RemoveAll(c=>!c.Owner);
            foreach(var filter in Resources.FindObjectsOfTypeAll<MeshFilter>())
                treeSwapper.Apply(filter,message=>Logger.LogInfo(message));
            if(lastTreeCount!=treeSwapper.BindingCount)
            {
                lastTreeCount=treeSwapper.BindingCount;
                Logger.LogInfo("Diffuse tree bindings: "+lastTreeCount+"/"+map.treeBindings.Length+"; winter material variants: "+treeSwapper.VariantCount);
            }
            var materials=Resources.FindObjectsOfTypeAll<Material>();
            foreach(var material in materials)
            {
                if(!material || !material.shader) continue;
                foreach(string property in material.GetTexturePropertyNames())
                {
                    var original=material.GetTexture(property);
                    var replacement=Replacement(original);
                    if(!replacement) continue;
                    var owner=material; var slot=property;
                    Apply(owner,()=>owner.GetTexture(slot),v=>owner.SetTexture(slot,(Texture)v),original,replacement);
                }
            }
            foreach(var raw in Resources.FindObjectsOfTypeAll<RawImage>())
            {
                var replacement=Replacement(raw.texture);
                if(replacement) { var owner=raw;Apply(owner,()=>owner.texture,v=>owner.texture=(Texture)v,owner.texture,replacement); }
            }
            foreach(var image in Resources.FindObjectsOfTypeAll<Image>())
            {
                var original=image.overrideSprite;
                if(!original || !Replacement(original.texture)) continue;
                Sprite replacement;
                if(!sprites.TryGetValue(original,out replacement))
                {
                    var texture=Replacement(original.texture);
                    // Preserve atlas rect, pivot, border and pixel density; source PNGs may have different sizes.
                    float sx=(float)texture.width/original.texture.width,sy=(float)texture.height/original.texture.height;
                    var r=original.rect;var b=original.border;
                    replacement=Sprite.Create(texture,new Rect(r.x*sx,r.y*sy,r.width*sx,r.height*sy),
                        new Vector2(original.pivot.x/r.width,original.pivot.y/r.height),original.pixelsPerUnit*sx,0,
                        SpriteMeshType.FullRect,new Vector4(b.x*sx,b.y*sy,b.z*sx,b.w*sy));
                    replacement.name="WinterPack_"+original.name;sprites.Add(original,replacement);
                }
                var owner=image;
                // Preserve the actual override state (null means use Image.sprite).
                var before=owner.overrideSprite==owner.sprite ? null : owner.overrideSprite;
                Apply(owner,()=>owner.overrideSprite,v=>owner.overrideSprite=(Sprite)v,before,replacement);
            }
            foreach(var layer in Resources.FindObjectsOfTypeAll<TerrainLayer>())
            {
                var replacement=Replacement(layer.diffuseTexture);
                if(replacement) { var owner=layer;Apply(owner,()=>owner.diffuseTexture,v=>owner.diffuseTexture=(Texture2D)v,owner.diffuseTexture,replacement); }
            }
        }

        private Texture2D Replacement(Texture texture)
        {
            if(!texture) return null;
            Texture2D result;
            if(texture.name=="diffuse") return null;
            if(!replacements.TryGetValue(texture.name,out result) || texture==result) return null;
            if(texture.name=="Tile_3") return tileComposer.Get(texture,result);
            return result;
        }

        private void Apply(Object owner,Func<Object> read,Action<Object> write,Object before,Object after)
        {
            var original=before as Texture;var replacement=after as Texture;
            if(original && replacement && !originals.ContainsKey(replacement)) originals.Add(replacement,original);
            write(after);changes.Add(new Change {Owner=owner,Read=read,Write=write,Before=before,After=after});
        }

        private void Restore()
        {
            for(int i=changes.Count-1;i>=0;i--)
            {
                var c=changes[i];
                if(c.Owner && c.Read()==c.After) c.Write(c.Before);
            }
            // A material/UI object instantiated during winter can inherit a replacement
            // without ever passing through Apply. Restore those references as well.
            if(originals.Count>0)
            {
                foreach(var material in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if(!material || !material.shader) continue;
                    foreach(string property in material.GetTexturePropertyNames())
                    {
                        var texture=material.GetTexture(property);Texture original;
                        if(texture && originals.TryGetValue(texture,out original) && original) material.SetTexture(property,original);
                    }
                }
                foreach(var raw in Resources.FindObjectsOfTypeAll<RawImage>())
                {
                    Texture original;
                    if(raw.texture && originals.TryGetValue(raw.texture,out original) && original) raw.texture=original;
                }
                foreach(var layer in Resources.FindObjectsOfTypeAll<TerrainLayer>())
                {
                    Texture original;
                    if(layer.diffuseTexture && originals.TryGetValue(layer.diffuseTexture,out original) && original) layer.diffuseTexture=(Texture2D)original;
                }
                foreach(var image in Resources.FindObjectsOfTypeAll<Image>())
                    foreach(var pair in sprites)
                        if(image.overrideSprite==pair.Value && pair.Key) { image.overrideSprite=pair.Key;break; }
            }
            changes.Clear();
            if(treeSwapper!=null) treeSwapper.Restore();
            lastTreeCount=-1;
            originals.Clear();
            if(tileComposer!=null) tileComposer.Clear();
            foreach(var sprite in sprites.Values) if(sprite) Object.Destroy(sprite);
            sprites.Clear();
        }
        private void OnDisable() { winter=false;Restore(); }
        private void OnDestroy()
        {
            LegacySettingsMenuHelper.Unregister(SettingId);
            Restore();if(tileComposer!=null) tileComposer.Dispose();
            if(bundle) bundle.Unload(true);
        }
    }
}
