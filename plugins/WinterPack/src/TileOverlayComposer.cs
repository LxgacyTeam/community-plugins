using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

namespace WinterPack
{
    // Terrain shaders use diffuse RGB as an opaque surface. The snow PNG is an
    // overlay, so composite it over the actual original tile before assigning it.
    public sealed class TileOverlayComposer : IDisposable
    {
        private readonly Material material;
        private readonly Dictionary<Texture,Texture2D> cache=new Dictionary<Texture,Texture2D>();

        public TileOverlayComposer(Shader shader)
        {
            if(!shader || !shader.isSupported) throw new InvalidOperationException("WinterPack tile overlay shader is unavailable.");
            material=new Material(shader) { hideFlags=HideFlags.HideAndDontSave };
        }

        public Texture2D Get(Texture original,Texture2D overlay)
        {
            if(!original || !overlay) throw new ArgumentNullException("Tile composition requires original and overlay textures.");
            Texture2D result;
            if(cache.TryGetValue(original,out result) && result) return result;
            var previous=RenderTexture.active;
            bool srgb=GL.sRGBWrite;
            RenderTexture target=null;
            result=null;
            try
            {
                target=RenderTexture.GetTemporary(original.width,original.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
                material.SetTexture("_BaseTex",original);
                Graphics.Blit(overlay,target,material);
                RenderTexture.active=target;
                var original2D=original as Texture2D;
                bool mipmaps=original2D && original2D.mipmapCount>1;
                result=new Texture2D(original.width,original.height,TextureFormat.RGBA32,mipmaps,false) {
                    name="WinterPack_Tile_3_Composed",wrapMode=original.wrapMode,
                    filterMode=original.filterMode,anisoLevel=original.anisoLevel,mipMapBias=original.mipMapBias,
                    hideFlags=HideFlags.DontSave
                };
                result.ReadPixels(new Rect(0,0,original.width,original.height),0,0,false);
                result.Apply(mipmaps,true);
                cache[original]=result;
                return result;
            }
            catch { Destroy(result);throw; }
            finally
            {
                RenderTexture.active=previous;GL.sRGBWrite=srgb;
                if(target) RenderTexture.ReleaseTemporary(target);
                material.SetTexture("_BaseTex",null);
                material.SetTexture("_MainTex",null);
            }
        }

        public void Clear()
        {
            foreach(var texture in cache.Values) Destroy(texture);
            cache.Clear();
        }

        public void Dispose() { Clear();Destroy(material); }

        private static void Destroy(Object value)
        {
            if(!value) return;
            if(Application.isPlaying) Object.Destroy(value);else Object.DestroyImmediate(value);
        }
    }
}
