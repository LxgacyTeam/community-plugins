using HarmonyLib;
using UnityEngine;

// A separate slot keeps the stock cameras' FOV, position and first-person state intact.
internal static class EnhancedCameraMode
{
    internal const Cam.Type SlotType = (Cam.Type)1001;

    internal static bool IsActive(Cam cam)
    {
        return cam && cam.cur_slot != null && cam.cur_slot.type == SlotType && !cam.isFirstPerson;
    }

    internal static void AddSlot(Cam cam, Control control)
    {
        CamRuntime.Reset();
        if (!control || !control.ToCar()) return;
        foreach (Cam.Slot slot in cam.slots)
            if (slot.type == SlotType) return;
        var enhanced = new Cam.Slot();
        foreach (Cam.Slot slot in cam.slots)
            if (slot.type == Cam.Type.Cam_BackLow) { enhanced.CopyFrom(slot); break; }
        enhanced.type = SlotType;
        enhanced.is_statik = false;
        enhanced.pos = new Vector3(0f, CamSettings.SlotPosY, CamSettings.SlotPosZ);
        enhanced.fovy = CamSettings.SlotFovY;
        cam.slots.Add(enhanced);
    }

    internal static bool NextSlot(Cam cam)
    {
        // Do not replace the native cycle on foot or before the vehicle slots exist.
        bool hasEnhanced = false;
        foreach (Cam.Slot slot in cam.slots)
            if (slot.type == SlotType) { hasEnhanced = true; break; }
        if (!hasEnhanced) return true;

        int current = cam.cur_slot == null ? -1 : (int)cam.cur_slot.type;
        Cam.Slot next = null;
        // Same native type order and wheel exclusion as Cam.NextSlot, followed by ECC.
        foreach (Cam.Slot slot in cam.slots)
        {
            int type = (int)slot.type;
            if (type < 0 || (type >= (int)Cam.Type.Count && slot.type != SlotType)) continue;
            if (!Application.isEditor && slot.type == Cam.Type.Cam_Wheel) continue;
            if (type > current && (next == null || type < (int)next.type)) next = slot;
        }
        if (next == null)
            foreach (Cam.Slot slot in cam.slots)
            {
                int type = (int)slot.type;
                if (type < 0 || (type >= (int)Cam.Type.Count && slot.type != SlotType)) continue;
                if (!Application.isEditor && slot.type == Cam.Type.Cam_Wheel) continue;
                if (next == null || type < (int)next.type) next = slot;
            }
        if (next == null) return true;
        CamRuntime.Reset();
        cam.SelectSlotByType(next.type);
        return false;
    }
}

[HarmonyPatch(typeof(Cam), "FromObject")]
internal static class EnhancedCameraSlotsPatch
{
    private static void Postfix(Cam __instance, Control control) => EnhancedCameraMode.AddSlot(__instance, control);
}

[HarmonyPatch(typeof(Cam), "NextSlot")]
internal static class EnhancedCameraCyclePatch
{
    private static bool Prefix(Cam __instance) => EnhancedCameraMode.NextSlot(__instance);
}
