using System;
namespace WinterPack
{
    [Serializable] public sealed class TextureMap { public Entry[] entries; public TreeBinding[] treeBindings; }
    [Serializable] public sealed class Entry
    {
        public string key, name; public int pathId, width, height, wrap, filter, format; public bool mipmaps;
    }
    [Serializable] public sealed class TreeBinding
    {
        public string tree,mesh; public int meshPathId,vertexCount; public int[] indexCounts; public TreeSlot[] slots;
    }
    [Serializable] public sealed class TreeSlot { public int index,pathId; public string property,key; }
}
