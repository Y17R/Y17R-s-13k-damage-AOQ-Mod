using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Prefabs.WarhammerTitan.Scripts;

// Token: 0x02000002 RID: 2
[BepInPlugin("Y17R's 13k damage Mod", "Y17R's 13k damage Mod", "1.2.0")]
public class TitanMod : BaseUnityPlugin
{
    // Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
    private void Awake()
    {
        TitanMod.logger = base.Logger;
        Harmony harmony = new Harmony("Y17R's 13k damagemod");
        harmony.PatchAll();
        TitanMod.logger.LogInfo("Y17R's 13k damage Mod Loaded!");
    }

    // Token: 0x04000001 RID: 1
    private static ManualLogSource logger;

    // Token: 0x02000004 RID: 4
    [HarmonyPatch(typeof(NetworkArmouredTitanController), "NapeHit_RPC")]
    internal class Patch_NapeHitDamage
    {
        // Token: 0x06000005 RID: 5 RVA: 0x000020EC File Offset: 0x000002EC
        private static void Prefix(NetworkArmouredTitanController __instance, ref int damage)
        {
            damage = 13000;
            FieldInfo field = typeof(NetworkArmouredTitanController).GetField("napeHealth", BindingFlags.Instance | BindingFlags.NonPublic);
            bool flag = field != null;
            if (flag)
            {
                field.SetValue(__instance, 0f);
            }
        }
    }

    // Token: 0x02000005 RID: 5
    [HarmonyPatch(typeof(WarhammerController), "DamageTaken")]
    internal class Patch_ModifyWarhammerTitanDamage
    {
        // Token: 0x06000007 RID: 7 RVA: 0x00002140 File Offset: 0x00000340
        private static void Prefix(ref int damage)
        {
            damage = 13000;
        }
    }

    // Token: 0x02000006 RID: 6
    [HarmonyPatch(typeof(FemaleControllerMulti), "SwordStrike_RPC")]
    internal class Patch_FemaleTitanDamage
    {
        // Token: 0x06000009 RID: 9 RVA: 0x00002153 File Offset: 0x00000353
        private static void Prefix(ref int damageAmount)
        {
            damageAmount = 13000;
        }
    }
}
