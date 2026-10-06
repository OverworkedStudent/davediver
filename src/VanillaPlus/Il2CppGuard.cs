using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace VanillaPlus;

internal static class Il2CppGuard
{
    // Small game methods can share one native body with unrelated classes, so a Harmony hook may be handed
    // an object of a completely different type. Touching its members as if it were T can crash the game.
    public static bool Is<T>(Il2CppObjectBase obj) where T : Il2CppObjectBase
    {
        if (obj is null || obj.Pointer == IntPtr.Zero) return false;
        IntPtr actual = IL2CPP.il2cpp_object_get_class(obj.Pointer);
        return actual != IntPtr.Zero &&
               IL2CPP.il2cpp_class_is_assignable_from(Il2CppClassPointerStore<T>.NativeClassPtr, actual);
    }
}
