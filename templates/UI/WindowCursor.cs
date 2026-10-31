using UnityEngine;

namespace TemplatePlugin
{
    // MadOut2 9.4 native cursor request. Keep it alive through window fade-out.
    // Use a separate object: BepInEx plugins can share their manager GameObject.
    internal sealed class WindowCursor
    {
        private readonly GameObject host;
        internal WindowCursor(Transform parent)
        {
            host = new GameObject("Template.WindowCursor");
            host.SetActive(false);
            host.transform.SetParent(parent, false);
            host.AddComponent<CursorRequred>();
        }
        internal void SetVisible(bool visible)
        {
            if (host && hVR.me && hVR.me.cursor && host.activeSelf != visible)
                host.SetActive(visible);
        }
        internal void Dispose() { if (host) Object.Destroy(host); }
    }
}
