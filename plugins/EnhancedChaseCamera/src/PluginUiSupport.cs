using BepInEx;
using BepInEx.Bootstrap;
using BigCityLegacy.UI;
using UnityEngine;

// Compiled into each plugin so the projects remain independently distributable.
internal sealed class PluginUiSupport
{
    private ILegacyUIBackdrop backdrop;
    private float nextLookup;
    private GameObject cursorOwner;

    public ILegacyUIBackdrop Backdrop
    {
        get
        {
            if (backdrop != null && (backdrop as Object)) return backdrop;
            backdrop = null;
            if (Time.unscaledTime < nextLookup) return null;
            nextLookup = Time.unscaledTime + 1f;
            PluginInfo plugin;
            if (!Chainloader.PluginInfos.TryGetValue("com.bigcitylegacy.baseplugin", out plugin) || !plugin.Instance)
                return null;
            foreach (MonoBehaviour component in plugin.Instance.GetComponents<MonoBehaviour>())
                if (component && component.isActiveAndEnabled &&
                    component.GetType().FullName == "BigCityLegacy.UI.Blur.LegacyBclBlur")
                { backdrop = component as ILegacyUIBackdrop; break; }
            return backdrop;
        }
    }

    public void SetCursor(MonoBehaviour owner, bool visible)
    {
        if (!cursorOwner && visible)
        {
            cursorOwner = new GameObject(owner.GetType().Name + ".Cursor");
            cursorOwner.SetActive(false);
            cursorOwner.transform.SetParent(owner.transform, false);
            cursorOwner.AddComponent<CursorRequred>();
        }
        if (cursorOwner && cursorOwner.activeSelf != visible) cursorOwner.SetActive(visible);
    }

    public void Dispose()
    {
        if (cursorOwner) { cursorOwner.SetActive(false); Object.Destroy(cursorOwner); }
    }
}
