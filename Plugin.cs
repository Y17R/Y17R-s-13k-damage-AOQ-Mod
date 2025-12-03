using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Prefabs.WarhammerTitan.Scripts;

// Token: 0x02000002 RID: 2
[BepInPlugin("com.Y17R.13kDamageMod", "Y17R's 13k Damage Mod", "1.3.0")]
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

    // =======================================================================
    // WARHAMMER TITAN (OFFLINE & NETWORKED)
    // =======================================================================

    // WarhammerController handles both online and offline in the same script.
    [HarmonyPatch(typeof(WarhammerController), "DamageTaken")]
    internal class Patch_WarhammerDamageTaken
    {
        // Modifies 'damage' to 13000
        private static void Prefix(ref int damage)
        {
            damage = 13000;
        }
    }

    // =======================================================================
    // ARMOURED TITAN (OFFLINE)
    // =======================================================================

    [HarmonyPatch(typeof(ArmouredTitanController), "NapeHit")]
    internal class Patch_OfflineArmouredNape
    {
        // Modifies 'damage' to 13000. 
        private static void Prefix(ref int damage)
        {
            damage = 13000;
        }
    }

    [HarmonyPatch(typeof(ArmouredTitanController), "LegStrike")]
    internal class Patch_OfflineArmouredLegs
    {
        // Modifies 'damage' to 13000 before the game runs the leg break logic
        private static void Prefix(ref int damage)
        {
            damage = 13000;
        }
    }

    // =======================================================================
    // ARMOURED TITAN (NETWORKED)
    // =======================================================================

    [HarmonyPatch(typeof(NetworkArmouredTitanController), "NapeHit")]
    internal class Patch_NetworkArmouredNapeHitDamage
    {
        // Modifies 'damage' to 13000. 
        // The game then uses this new value to show the Popup AND send the RPC.
        private static void Prefix(ref int damage)
        {
            damage = 13000;
        }
    }

    [HarmonyPatch(typeof(NetworkArmouredTitanController), "LegStrike")]
    internal class Patch_NetworkArmouredLegHitDamage
    {
        // Modifies 'damage' to 13000.
        // We don't need to capture 'int leg' because we aren't changing it.
        private static void Prefix(ref int damage)
        {
            damage = 13000;
        }
    }

    // =======================================================================
    // FEMALE TITAN (FemaleControllerMulti)
    // =======================================================================

    // Unfortunately using the blocking method and replacing the entire function since the damage isn't just a parameter we can modify
    [HarmonyPatch(typeof(FemaleControllerMulti), "SwordStrike")]
    internal class Patch_FemaleSwordStrike
    {
        private static bool Prefix(FemaleControllerMulti __instance, int sword, int location)
        {
            // 1. ACCESS PRIVATE 'dead' VARIABLE
            // We use Traverse to look inside the instance for the field named "dead"
            bool isDead = (bool)Traverse.Create(__instance).Field("dead").GetValue();

            if (isDead)
            {
                return false; // Stop execution if already dead
            }

            // (Haptics removed to prevent OVR/AudioClip errors)

            // 2. RUN BLOOD SPRAY RPC
            // This is standard Unity/Photon, so it should be fine.
            __instance.photonView.RPC("BloodSpray_RPC", RpcTarget.All, new object[]
            {
                location
            });

            // 3. FORCE DAMAGE
            int num = 13000;

            // 4. HANDLE BLADE HEALTH (StaticVariables)
            // We use AccessTools to find the class "StaticVariables" by string name
            // This prevents the "name does not exist" error.
            Type staticVarsType = AccessTools.TypeByName("StaticVariables");
            if (staticVarsType != null)
            {
                if (sword == 0)
                {
                    // Set leftBladeHealth to 0
                    Traverse.Create(staticVarsType).Field("leftBladeHealth").SetValue(0);
                }
                else
                {
                    // Set rightBladeHealth to 0
                    Traverse.Create(staticVarsType).Field("rightBladeHealth").SetValue(0);
                }
            }

            // 5. SCORE POPUP
            // Access the private 'scorePopup' script and call DisplayScore
            object scorePopupObj = Traverse.Create(__instance).Field("scorePopup").GetValue();
            if (scorePopupObj != null)
            {
                Traverse.Create(scorePopupObj).Method("DisplayScore", new object[] { num }).GetValue();
            }

            // 6. SEND DAMAGE RPC
            __instance.photonView.RPC("SwordStrike_RPC", RpcTarget.MasterClient, new object[]
            {
                num,
                location
            });

            // Return false to skip the original game code
            return false;
        }
    }

    // =======================================================================
    // FEMALE TITAN (OFFLINE)
    // =======================================================================
    [HarmonyPatch(typeof(FemaleTitanController), "SwordStrike")]
    internal class Patch_FemaleSwordStrikeOffline
    {
        private static bool Prefix(FemaleTitanController __instance, int sword, int location)
        {
            var traverse = Traverse.Create(__instance);

            // 1. Check if Dead
            bool dead = traverse.Field("dead").GetValue<bool>();
            if (dead) return false;

            // 2. NAPE HIT (Location 2)
            if (location == 2)
            {
                bool vulnerable = traverse.Field("vulnerable").GetValue<bool>();

                // If not vulnerable, break blades
                if (!vulnerable)
                {
                    Type staticVars = AccessTools.TypeByName("StaticVariables");
                    if (staticVars != null)
                    {
                        string blade = (sword == 0) ? "leftBladeHealth" : "rightBladeHealth";
                        Traverse.Create(staticVars).Field(blade).SetValue(0);
                    }
                    return false;
                }
                // If vulnerable, KILL
                else if (!dead)
                {
                    traverse.Field("dead").SetValue(true);

                    // Play Sound (using Reflection to avoid AudioSource type error)
                    object deathClip = traverse.Field("femaleTitan_Death").GetValue();
                    object aS = traverse.Field("aS").GetValue();
                    if (aS != null && deathClip != null)
                        Traverse.Create(aS).Method("PlayOneShot", new object[] { deathClip }).GetValue();

                    // Fleeing Logic
                    traverse.Field("fleeing").SetValue(true);

                    // Animator (using Reflection)
                    object anim = traverse.Field("anim").GetValue();
                    if (anim != null)
                        Traverse.Create(anim).Method("SetBool", new object[] { "dead", true }).GetValue();

                    GameObject napeKilled = traverse.Field("napeKilled").GetValue<GameObject>();
                    if (napeKilled != null) napeKilled.SetActive(true);

                    // Disable HeadLook/Agent/Triggers (Using 'object' to avoid type errors)
                    object headLook = traverse.Field("headLook").GetValue();
                    if (headLook != null) ((MonoBehaviour)headLook).enabled = false;

                    object agent = traverse.Field("agent").GetValue();
                    if (agent != null) ((MonoBehaviour)agent).enabled = false;

                    // Disable Death Triggers (Iterate generic array)
                    Array deathTriggers = traverse.Field("deathTriggers").GetValue<Array>();
                    if (deathTriggers != null)
                    {
                        foreach (object trigger in deathTriggers)
                        {
                            // Assuming trigger is a Collider which inherits from Behaviour/Component
                            ((Behaviour)trigger).enabled = false;
                        }
                    }

                    // Disable Trigger Zones
                    GameObject[] triggerZones = traverse.Field("triggerZones").GetValue<GameObject[]>();
                    if (triggerZones != null) foreach (var g in triggerZones) g.SetActive(false);

                    // Stats
                    PlayerManager.instance.player.GetComponent<Timer>().OfflineBossKill(1);
                    PlayerManager.instance.player.GetComponent<Achievements>().KillFemale();

                    __instance.Invoke("DestroyParent", 15f);
                    return false;
                }
            }
            // 3. LEGS HIT (Location 3)
            else if (location == 3)
            {
                int fallsRemaining = traverse.Field("fallsRemaining").GetValue<int>();
                if (fallsRemaining <= 0)
                {
                    traverse.Field("vulnerable").SetValue(true);

                    // Anim
                    object anim = traverse.Field("anim").GetValue();
                    if (anim != null)
                        Traverse.Create(anim).Method("SetTrigger", new object[] { "isInjuredWalk" }).GetValue();

                    // Blood
                    Array bloodSprays = traverse.Field("bloodSprays").GetValue<Array>();
                    if (bloodSprays != null && bloodSprays.Length > location)
                    {
                        object specificSpray = bloodSprays.GetValue(location);
                        Traverse.Create(specificSpray).Method("Play").GetValue();
                    }

                    // Sound
                    object hitClip = traverse.Field("enemyHit").GetValue();
                    object aS = traverse.Field("aS").GetValue();
                    if (aS != null && hitClip != null)
                        Traverse.Create(aS).Method("PlayOneShot", new object[] { hitClip }).GetValue();

                    __instance.Invoke("ResetVulnerable", 8f);
                    return false;
                }
            }
            // 4. GENERAL HIT (13k Damage Logic)
            else
            {
                // FORCE THE DAMAGE VARIABLE HERE
                int damageToDeal = 13000;

                Type staticVars = AccessTools.TypeByName("StaticVariables");
                if (staticVars == null) return false;

                int currentBladeHealth = 0;
                string bladeName = (sword == 0) ? "leftBladeHealth" : "rightBladeHealth";

                currentBladeHealth = (int)Traverse.Create(staticVars).Field(bladeName).GetValue();

                if (currentBladeHealth > 0)
                {
                    // Break blade
                    Traverse.Create(staticVars).Field(bladeName).SetValue(0);

                    // Blood Effects
                    Array bloodSprays = traverse.Field("bloodSprays").GetValue<Array>();
                    if (bloodSprays != null && bloodSprays.Length > location)
                    {
                        object specificSpray = bloodSprays.GetValue(location);
                        Traverse.Create(specificSpray).Method("Play").GetValue();
                    }

                    // Sound
                    object hitClip = traverse.Field("enemyHit").GetValue();
                    object aS = traverse.Field("aS").GetValue();
                    if (aS != null && hitClip != null)
                        Traverse.Create(aS).Method("PlayOneShot", new object[] { hitClip }).GetValue();

                    // Apply Damage
                    float[] weakPointsHealth = traverse.Field("weakPointsHealth").GetValue<float[]>();
                    if (weakPointsHealth != null && weakPointsHealth.Length > location)
                    {
                        weakPointsHealth[location] -= (float)damageToDeal;
                    }

                    // Score Popup
                    object scorePopupObj = traverse.Field("scorePopup").GetValue();
                    if (scorePopupObj != null)
                        Traverse.Create(scorePopupObj).Method("DisplayScore", new object[] { damageToDeal }).GetValue();

                    // Flee Transition Check
                    bool fleeTransition = traverse.Field("fleeTransition").GetValue<bool>();
                    // Re-check health after damage
                    if (weakPointsHealth != null && weakPointsHealth[location] < 0f && !fleeTransition)
                    {
                        traverse.Field("fleeTransition").SetValue(true);
                        // Invoke coroutine by string name
                        __instance.StartCoroutine("FleeTransitionCR");
                    }
                }
            }
            return false;
        }
    }
}
