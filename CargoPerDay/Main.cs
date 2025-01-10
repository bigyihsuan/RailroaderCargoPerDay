using HarmonyLib;
using Model.Definition.Data;
using Model.Ops;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Track;
using UnityEngine;
using UnityModManagerNet;
using static Model.Ops.IndustryComponent;

namespace CargoPerDay
{
    public static class Main
    {
        public static UnityModManager.ModEntry mod = null;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Harmony harmony = null;
            mod = modEntry;

            try
            {
                harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                modEntry.Logger.LogException($"Failed to load {modEntry.Info.DisplayName}:", ex);
                harmony?.UnpatchAll(modEntry.Info.Id);
                return false;
            }

            return true;
        }
    }


    [HarmonyPatch(typeof(IndustryLoader), nameof(IndustryLoader.PanelFields))]
    class IndustryLoaderPanelFieldsPatch
    {
        static IEnumerable<PanelField> Postfix(IEnumerable<PanelField> values, IndustryLoader __instance, IndustryContext ctx)
        {
            if (__instance.Industry.GetContractMultiplier() < 0.001f)
            {
                yield break;
            }
            if (!__instance.load.importable)
            {
                int totalTrackLength = __instance.TrackSpans.Sum((TrackSpan ts) => Mathf.FloorToInt(ts.Length * 3.28084f / 50f));
                var cargo = Mathf.FloorToInt(
                    Mathf.Min(
                        __instance.productionRate,
                        __instance.carLoadRate * (float)totalTrackLength
                ));
                var description = __instance.load.description;
                var format = "{0} @ {1}/day";
                yield return new PanelField("Production", string.Format(format, arg0: description, arg1: __instance.load.units.QuantityString(cargo)), "Cargo produced");
            }
            if (!__instance.orderEmpties)
            {
                yield return PanelField.InStorage(quantityInStorage: ctx.QuantityInStorage(__instance.load), load: __instance.load, effectiveStorage: __instance.maxStorage);
            }
        }
    }
    
    [HarmonyPatch(typeof(IndustryUnloader), nameof(IndustryUnloader.PanelFields))]
    class IndustryUnloaderPanelFieldsPatch
    {
        static IEnumerable<PanelField> Postfix(IEnumerable<PanelField> values, IndustryUnloader __instance, IndustryContext ctx)
        {
            float contractMultiplier = __instance.Industry.GetContractMultiplier();
            var cargo = Mathf.CeilToInt(new Traverse(__instance).Method("GetUnitsPerDay").GetValue<float>() * contractMultiplier);
            var description = __instance.load.description;
            var format = "{0} @ {1}/day";

            if (!(contractMultiplier < 0.001f))
            {
                if (!__instance.load.importable)
                {
                    yield return new PanelField("Consumes", string.Format(format, arg0: description, arg1: __instance.load.units.QuantityString(cargo)), "Cargo consumption rate");
                }
                if (!__instance.orderLoads)
                {
                    yield return PanelField.InStorage(effectiveStorage: contractMultiplier * __instance.maxStorage, quantityInStorage: ctx.QuantityInStorage(__instance.load), load: __instance.load);
                }
            }
        }
    }
}