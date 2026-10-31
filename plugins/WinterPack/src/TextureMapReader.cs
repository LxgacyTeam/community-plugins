using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace WinterPack
{
    // BepInEx assemblies are not Unity script assemblies. Parse their DTOs with
    // managed JSON serialization instead of relying on Unity's native type metadata.
    public static class TextureMapReader
    {
        public static TextureMap Parse(string json)
        {
            var map=JsonConvert.DeserializeObject<TextureMap>(json);
            if(map==null || map.entries==null || map.entries.Length==0 || map.treeBindings==null)
                throw new InvalidDataException("WinterPack texture map must contain entries and treeBindings arrays.");
            var keys=new HashSet<string>(StringComparer.Ordinal);
            var diffuseKeys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in map.entries)
            {
                if(entry==null || string.IsNullOrEmpty(entry.key) || string.IsNullOrEmpty(entry.name) ||
                    entry.width<=0 || entry.height<=0 || !keys.Add(entry.key))
                    throw new InvalidDataException("WinterPack texture map contains an invalid or duplicate entry.");
                if(entry.name=="diffuse") diffuseKeys.Add(entry.key);
            }
            var anchored=new HashSet<string>(StringComparer.Ordinal);
            var shapes=new HashSet<string>(StringComparer.Ordinal);
            foreach(var tree in map.treeBindings)
            {
                if(tree==null || string.IsNullOrEmpty(tree.tree) || tree.vertexCount<=0 ||
                    tree.indexCounts==null || tree.indexCounts.Length==0 || tree.slots==null || tree.slots.Length==0)
                    throw new InvalidDataException("WinterPack texture map contains an invalid tree binding.");
                string shape=tree.vertexCount+":"+string.Join(",",tree.indexCounts);
                if(!shapes.Add(shape)) throw new InvalidDataException("Ambiguous WinterPack tree mesh signature: "+shape);
                foreach(var slot in tree.slots)
                {
                    if(slot==null || string.IsNullOrEmpty(slot.key) || !diffuseKeys.Contains(slot.key) ||
                        string.IsNullOrEmpty(slot.property) || slot.index<0 || slot.index>=tree.indexCounts.Length)
                        throw new InvalidDataException("WinterPack texture map contains an invalid tree material slot.");
                    anchored.Add(slot.key);
                }
            }
            if(!diffuseKeys.SetEquals(anchored))
                throw new InvalidDataException("WinterPack texture map is missing diffuse material anchors.");
            return map;
        }
    }
}
