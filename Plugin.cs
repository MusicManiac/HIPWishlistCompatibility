using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace HIPWishlistCompatibility;

[BepInPlugin("com.musicmaniac.hipwishlistcompatibility", "HIP Wishlist Compatibility", "1.0.0")]
[BepInDependency("com.tyfon.hideoutinprogress")]
[BepInDependency("com.zgfuedkx.wishlistextended")]
public sealed class Plugin : BaseUnityPlugin
{
    private const string HideoutServiceName = "WishlistExtended.Features.HideoutDataService";
    private const string HideoutEntryName = "WishlistExtended.Models.WishlistHideoutData";

    private void Awake()
    {
        Type? service = AccessTools.TypeByName(HideoutServiceName);
        Type? entry = AccessTools.TypeByName(HideoutEntryName);
        MethodInfo? build = service == null ? null : AccessTools.Method(service, "BuildHideoutRequirements");
        FieldInfo? requirements = service == null ? null : AccessTools.Field(service, "_cachedRequirements");
        PropertyInfo? quantity = entry == null ? null : AccessTools.Property(entry, "Quantity");

        if (build == null || requirements == null || quantity == null || quantity.PropertyType != typeof(int))
        {
            Logger.LogError("WishlistExtended 1.2.0 hideout cache layout was not found; compatibility patch disabled.");
            return;
        }

        HideoutCachePatch.Requirements = requirements;
        HideoutCachePatch.Quantity = quantity;

        var harmony = new Harmony("com.musicmaniac.hipwishlistcompatibility");
        harmony.Patch(build, postfix: new HarmonyMethod(AccessTools.Method(typeof(HideoutCachePatch), nameof(HideoutCachePatch.Postfix))));
        Logger.LogInfo("HIP Wishlist Compatibility loaded.");
    }

    private static class HideoutCachePatch
    {
        internal static FieldInfo Requirements = null!;
        internal static PropertyInfo Quantity = null!;

        // HIP reduces ItemRequirement.BaseCount and makes WishlistExtended rebuild this cache.
        // WishlistExtended 1.2.0 adds entries with IntCount == 0; remove only those entries.
        // Positive future-stage entries for the same item stay in the list.
        public static void Postfix()
        {
            if (Requirements.GetValue(null) is not IDictionary cache)
            {
                return;
            }

            var emptyItems = new List<object>();
            foreach (DictionaryEntry item in cache)
            {
                if (item.Value is not IList upgrades)
                {
                    continue;
                }

                for (int index = upgrades.Count - 1; index >= 0; index--)
                {
                    object? upgrade = upgrades[index];
                    if (upgrade != null && (int)Quantity.GetValue(upgrade)! <= 0)
                    {
                        upgrades.RemoveAt(index);
                    }
                }

                if (upgrades.Count == 0)
                {
                    emptyItems.Add(item.Key);
                }
            }

            foreach (object itemId in emptyItems)
            {
                cache.Remove(itemId);
            }
        }
    }
}
