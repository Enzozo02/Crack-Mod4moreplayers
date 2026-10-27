// Work order (daily task) fixes for crews larger than 4:
//  * quotas keep scaling past 4 players (+ QuotaMultiplier ramped in over players 5-8, host-side)
//  * shop terminals always reflect the current work order step (catalog unlocks)
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CrackMorePlayers
{
    // ---- Quotas ---------------------------------------------------------------
    //
    // Vanilla: a step's target comes from DailyTaskManager.TargetFor(step):
    //   * steps with amountByCrew (e.g. [2,3,3,4]) -> CrewAmount(players) clamps the index,
    //     so 5-8 players get the 4-player value;
    //   * steps that ScaleWithCrew -> ceil(amount * (1 + (players-1) * 0.5)), already linear;
    //   * everything else (arcade score, slot win, upgrades, yolk...) is a fixed goal.

    [HarmonyPatch(typeof(WorkOrderStep), "CrewAmount")]
    static class WorkOrderStep_CrewAmount
    {
        static readonly FieldInfo ByCrew = AccessTools.Field(typeof(WorkOrderStep), "amountByCrew");

        // Past the end of amountByCrew, keep the table's average step per player.
        static void Postfix(WorkOrderStep __instance, int players, ref int __result)
        {
            var table = (int[])ByCrew.GetValue(__instance);
            if (table == null || table.Length < 2 || players <= table.Length) return;
            int last = table[table.Length - 1];
            float perPlayer = Mathf.Max(0f, (last - table[0]) / (float)(table.Length - 1));
            __result = Mathf.Max(__result, Mathf.CeilToInt(last + perPlayer * (players - table.Length)));
        }
    }

    [HarmonyPatch(typeof(DailyTaskManager), "TargetFor")]
    static class DailyTaskManager_TargetFor
    {
        // Extra difficulty for big crews only: 1-4 players keep the vanilla targets.
        static void Postfix(WorkOrderStep step, ref int __result)
        {
            if (step == null || __result <= 1) return;
            if (GameManager.instance == null || GameManager.instance.players.Count <= Plugin.VanillaSlots) return;
            if (!step.HasCrewAmounts && !step.ScalesWithCrew) return;
            // Ramp the multiplier in over players 5..8 so there is no jump between 4 and 5:
            // 5p -> 25%, 6p -> 50%, 7p -> 75%, 8p -> 100% of the extra.
            int extra = Mathf.Min(GameManager.instance.players.Count, Plugin.HardMax) - Plugin.VanillaSlots;
            float m = 1f + (Plugin.QuotaMultiplier.Value - 1f) * extra / (Plugin.HardMax - Plugin.VanillaSlots);
            if (Mathf.Approximately(m, 1f)) return;
            __result = Mathf.Max(1, Mathf.CeilToInt(__result * m));
        }
    }

    // ---- Shop unlocks follow the work order -----------------------------------
    //
    // Catalog unlocks are derived from DailyTaskManager._stepIndex (a SyncVar). ShopApp only
    // subscribes to the step/shop change events once, in Start(), and only if both managers
    // already exist at that moment. A client that joins mid-game (common with 5-8 players)
    // or a terminal whose app starts before the managers spawn never refreshes, so its
    // catalogs stay locked/unlocked out of sync with the quest. Fix: every ShopApp watches
    // the step index itself and refreshes when it changes (and the first time it runs).

    [HarmonyPatch(typeof(ShopApp), "Update")]
    static class ShopApp_Update
    {
        static readonly MethodInfo Refresh = AccessTools.Method(typeof(ShopApp), "OnShopStateChanged");
        static readonly Dictionary<int, int> LastStep = new Dictionary<int, int>();

        static void Postfix(ShopApp __instance)
        {
            var tasks = DailyTaskManager.instance;
            if (tasks == null || Refresh == null) return;
            int step = tasks.StepIndex;
            int id = __instance.GetInstanceID();
            if (LastStep.TryGetValue(id, out var seen) && seen == step) return;
            LastStep[id] = step;
            try { Refresh.Invoke(__instance, null); }
            catch (Exception e) { Plugin.Log.LogWarning("Shop refresh: " + (e.InnerException ?? e).Message); }
        }
    }
}
