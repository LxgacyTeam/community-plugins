using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

namespace WinterPack
{
    // Renderer geometry identifies the serialized tree, including identical diffuse
    // images belonging to different PathIDs. Never depends on shader names/colors.
    public sealed class TreeTextureSwapper
    {
        private readonly TreeBinding[] bindings;
        private readonly IDictionary<string,Texture2D> textures;
        private readonly Dictionary<Mesh,TreeBinding> meshCache=new Dictionary<Mesh,TreeBinding>();
        private readonly Dictionary<Material,Material> sources=new Dictionary<Material,Material>();
        private readonly Dictionary<Material,Dictionary<string,Material>> variants=new Dictionary<Material,Dictionary<string,Material>>();
        private readonly HashSet<int> reported=new HashSet<int>();
        public int VariantCount { get { return sources.Count; } }
        public int BindingCount { get { return reported.Count; } }

        public TreeTextureSwapper(TreeBinding[] bindings,IDictionary<string,Texture2D> textures)
        {
            this.bindings=bindings;this.textures=textures;
        }

        public TreeBinding Resolve(Mesh mesh)
        {
            if(!mesh) return null;
            TreeBinding result;
            if(meshCache.TryGetValue(mesh,out result)) return result;
            foreach(var binding in bindings)
            {
                if(mesh.vertexCount!=binding.vertexCount || mesh.subMeshCount!=binding.indexCounts.Length) continue;
                bool matches=true;
                for(int i=0;i<binding.indexCounts.Length;i++)
                    if(mesh.GetIndexCount(i)!=(uint)binding.indexCounts[i]) { matches=false;break; }
                if(matches) { result=binding;break; }
            }
            meshCache.Add(mesh,result);
            return result;
        }

        public bool Apply(MeshFilter filter,Action<string> report=null)
        {
            if(!filter) return false;
            var renderer=filter.GetComponent<Renderer>();
            if(!renderer) return false;
            var binding=Resolve(filter.sharedMesh);
            // Unity may replace the mesh when static batching. Original tree names
            // are an additional identity only for a Tree or a statically batched renderer.
            if(binding==null && (renderer.isPartOfStaticBatch || filter.GetComponent<Tree>()))
            {
                string name=filter.gameObject.name.Replace("(Clone)","").Trim();
                foreach(var candidate in bindings) if(candidate.tree==name) { binding=candidate;break; }
            }
            if(binding==null) return false;
            var materials=renderer.sharedMaterials;
            bool changed=false;
            foreach(var slot in binding.slots)
            {
                if(slot.index>=materials.Length || !materials[slot.index]) continue;
                var current=materials[slot.index];Material source;
                if(!sources.TryGetValue(current,out source)) source=current;
                if(!source || !source.HasProperty(slot.property)) continue;
                var original=source.GetTexture(slot.property);
                if(!original) continue;
                Dictionary<string,Material> choices;
                if(!variants.TryGetValue(source,out choices)) variants.Add(source,choices=new Dictionary<string,Material>());
                string key=slot.key+":"+slot.property;
                Material replacement;
                if(!choices.TryGetValue(key,out replacement) || !replacement)
                {
                    replacement=new Material(source) { name=source.name,hideFlags=HideFlags.DontSave };
                    replacement.SetTexture(slot.property,textures[slot.key]);
                    choices[key]=replacement;sources.Add(replacement,source);
                }
                if(current!=replacement) { materials[slot.index]=replacement;changed=true; }
            }
            if(changed)
            {
                renderer.sharedMaterials=materials;
                if(reported.Add(binding.meshPathId) && report!=null)
                    report("Tree binding: "+binding.tree+" -> "+binding.slots[0].key+" (mesh "+binding.meshPathId+")");
            }
            return changed;
        }

        public void Restore()
        {
            if(sources.Count==0) return;
            // Includes inactive prefabs and instances created after winter began.
            foreach(var renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if(!renderer) continue;
                var materials=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<materials.Length;i++)
                {
                    Material source;
                    if(materials[i] && sources.TryGetValue(materials[i],out source) && source)
                    { materials[i]=source;changed=true; }
                }
                if(changed) renderer.sharedMaterials=materials;
            }
            foreach(var material in sources.Keys)
                if(material) { if(Application.isPlaying) Object.Destroy(material);else Object.DestroyImmediate(material); }
            sources.Clear();variants.Clear();meshCache.Clear();reported.Clear();
        }
    }
}
