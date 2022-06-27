using Assets.Code;
using Assets.Code.GameLogic.Things.SpecialFishes;
using Assets.Code.GameLogic.UI;
using Assets.Code.GameLogic.UI.Settings;
using Assets.Code.Libaries.Generic;
using Assets.Code.Maps;
using Assets.Code.UI.Extensions;
using AzzaMods;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace FeedAndGrowEssentials
{
    public class FeedAndGrowEssentials : MonoBehaviour
    {
        // If certain things are active
        private static bool active_cameraFix = false;
        private static bool active_unlockAllFish = false;
        private static bool active_infiniteSprint = false;
        private static bool active_unlimiedHealth = false;
        private static bool active_unlimitedBreath = false;
        private static bool hasAppliedInfiniteZoom = false;
        private static bool _active_unlimitedMoney = false;

        // The options we are going to register
        private static string optionUnlockAllFish = "Unlock All Fish";
        private static string optionUnlimitedCoins = "Unlimited Coins";
        private static string optionCoins = "Coins";
        private static string optionUnlimitedOxygen = "Unlimited Oxygen";
        private static string optionUnlimitedSprint = "Unlimited Sprint";
        private static string optionUnlimitedHealth = "Unlimited Health";
        private static string optionUnlimitedZoom = "Unlimited Zoom";
        private static string optionRemoveCameraClipping = "Remove Camera Water Clipping";
        private static string actionLevelUp = "Level Up";
        private const string optionLevelUpIncreaseCount = "Level Up Levels";
        private static string optionMaxFish = "Max Fish";
        private static string optionRemoveLevelCap = "Remove Level Cap";

        // Level cap
        private static bool overrideRemoveLevelCap = false;
        private static bool removeLevelCap = false;

        // Prevent spawning of non-default mobs
        private static Dictionary<string, bool> noDefaultSpawn = new Dictionary<string, bool>();

        private float cachedCoins = 0;

        // We use a seperate harmony instance for the unlimited zoom
        // That's so we can patch and unpatch easily
        private Harmony unlimitedZoomHarmony;

        void OnModLoaded()
        {
            // Camera Fix 1
            Patching.Prefix(
                typeof(PlayerController).GetMethod("RaycastCameraPosition", Patching.AnyMethod),
                this.GetType().GetMethod("Prefix_RaycastCameraPosition", Patching.AnyMethod)
            );

            // Camera Fix 2
            Patching.Prefix(
                typeof(PlayerController).GetMethod("FixWaterPosition", Patching.AnyMethod),
                this.GetType().GetMethod("Prefix_RaycastCameraPosition", Patching.AnyMethod)
            );


            // Unlock all fish fix
            Patching.Prefix(
                typeof(FishSelectFilterButton).GetMethod("CanBeSelected", Patching.AnyMethod),
                this.GetType().GetMethod("Prefix_CanBeSelected", Patching.AnyMethod)
            );

            // Unlock all fish survival mode
            Patching.Prefix(
                typeof(SurvivalStar).GetMethod("GetStarValue", Patching.AnyMethod),
                this.GetType().GetMethod("PrefixGetStarValue", Patching.AnyMethod)
            );

            // Infinite Sprint
            Patching.Postfix(
                typeof(LivingEntity).GetMethod("Update", Patching.AnyMethod),
                this.GetType().GetMethod("Postfix_update", Patching.AnyMethod)
            );

            // Unlimited Breathing Underwater
            Patching.Prefix(
                typeof(AirBreathing).GetMethod("Update", Patching.AnyMethod),
                this.GetType().GetMethod("Prefix_Update", Patching.AnyMethod)
            );

            // Unlock all fish
            Options.RegisterBool(optionUnlockAllFish, false);
            Options.SetDescription(optionUnlockAllFish, "Unlocks and allows you to play as any fish on any map.");
            Options.AddPersistence(optionUnlockAllFish);

            // Unlimited Coins
            //Options.RegisterBool(optionUnlimitedCoins, false);
            Options.RegisterFloat(optionCoins, PlayerCurrency.Amount);
            Options.SetDescription(optionCoins, "Change the number of coins you have between 0 and 1000. Enabling the lock will prevent your coins from ever going up or down.");
            Options.AddLock(optionCoins);
            Options.SetMinValue(optionCoins, 0);
            Options.SetMaxValue(optionCoins, 1000);
            Options.AddPersistence(optionCoins);

            // Unlimited Oxygen
            Options.RegisterBool(optionUnlimitedOxygen, false);
            Options.SetDescription(optionUnlimitedOxygen, "Prevents your oxygen from lowering allowing you to breath under water forever with fish / animals that have limited breath.");
            Options.AddPersistence(optionUnlimitedOxygen);

            // Unlimited Sprint
            Options.RegisterBool(optionUnlimitedSprint, false);
            Options.SetDescription(optionUnlimitedSprint, "Gives you unlimited sprint which allows you to sprint forever.");
            Options.AddPersistence(optionUnlimitedSprint);

            // Unlimited Health
            Options.RegisterBool(optionUnlimitedHealth, false);
            Options.SetDescription(optionUnlimitedHealth, "Gives you unlimited health to prevent other fish from killing you. This won't stop things from swallowing you if you're tiny.");
            Options.AddPersistence(optionUnlimitedHealth);

            // Unlimited Zoom
            unlimitedZoomHarmony = null;
            Options.RegisterBool(optionUnlimitedZoom, false);
            Options.SetDescription(optionUnlimitedZoom, "Allows you to zoom infinitely.");
            Options.AddPersistence(optionUnlimitedZoom);

            // Remove Camera Clipping
            Options.RegisterBool(optionRemoveCameraClipping, false);
            Options.SetDescription(optionRemoveCameraClipping, "Allows the camera to clip above the water when your fish is below the water.");
            Options.AddPersistence(optionRemoveCameraClipping);

            // Level up
            Options.RegisterAction(actionLevelUp, "Level Up");
            Options.SetDescription(actionLevelUp, "Level up your current fish.");
            Options.AddPersistence(actionLevelUp);

            // Level up - how many levels?
            Options.RegisterInt(optionLevelUpIncreaseCount, 1);
            Options.SetDescription(optionLevelUpIncreaseCount, "How many levels to add each time you level up.");
            Options.SetMinValue(optionLevelUpIncreaseCount, 1);
            Options.AddPersistence(optionLevelUpIncreaseCount);

            // Max fish
            Options.RegisterInt(optionMaxFish, 4);
            Options.SetDescription(optionMaxFish, "The max number of each type of fish that is allow to spawn.");
            Options.AddPersistence(optionMaxFish);

            // Remove level cap
            Options.RegisterBool(optionRemoveLevelCap, removeLevelCap);
            Options.SetDescription(optionRemoveLevelCap, "Remove the maximum level cap, allowing your fish to get past level 296.");
            Options.AddPersistence(optionRemoveLevelCap);
            Patching.Prefix(typeof(LivingEntity).GetMethod("CanLevelUp", Patching.AnyMethod), this.GetType().GetMethod("PrefixCanLevelUp", Patching.AnyMethod));

            // Ensure the spawns exist
            EnsureSpawnsExist();

            // Fix issues with spawning too many of an ent
            Patching.Prefix(typeof(NpcGenerator).GetMethod("ApplyPlayerSize", Patching.AnyMethod), this.GetType().GetMethod("PrefixApplyPlayerSize", Patching.AnyMethod));

            StartCoroutine(SlowUpdate());
        }

        void OnModUnloaded()
        {
            // Undo everything, your mod is being unloaded

            if(unlimitedZoomHarmony != null)
            {
                unlimitedZoomHarmony.UnpatchAll(unlimitedZoomHarmony.Id);
            }
        }

        void OnOptionChanged(string optionName)
        {
            // Unlock all fish
            if (optionName == optionUnlockAllFish)
            {
                active_unlockAllFish = Options.GetBool(optionName);
            }

            // Unlimited coins
            if (optionName == optionUnlimitedCoins)
            {
                _active_unlimitedMoney = Options.GetBool(optionName);
            }

            // Updating the amount of coins we have
            if (optionName == optionCoins)
            {
                cachedCoins = Options.GetFloat(optionName);
                PlayerCurrency.Amount = cachedCoins;
                _active_unlimitedMoney = Options.GetLockState(optionName);
            }

            // Unlimited Oxygen
            if (optionName == optionUnlimitedOxygen)
            {
                active_unlimitedBreath = Options.GetBool(optionName);
            }

            // Unlimited sprint
            if (optionName == optionUnlimitedSprint)
            {
                active_infiniteSprint = Options.GetBool(optionName);
            }

            // Unlimited Health
            if(optionName == optionUnlimitedHealth)
            {
                active_unlimiedHealth = Options.GetBool(optionName);
            }

            // Unlimited Zoom
            if(optionName == optionUnlimitedZoom)
            {
                // Is it on?
                if(Options.GetBool(optionName))
                {
                    // It's on, have we already done the patch?
                    if(unlimitedZoomHarmony == null)
                    {
                        // Configure it
                        unlimitedZoomHarmony = new Harmony("FeedAndGrowUnlimitedZoomCustomHarmony");
                        unlimitedZoomHarmony.Patch(typeof(PlayerController).GetMethod("Update", Patching.AnyMethod), null, null, new HarmonyMethod(this.GetType().GetMethod("TranspilePlayerControllerUpdate")));
                    }
                }
                else
                {
                    // It's off, is it currently configured?
                    if(unlimitedZoomHarmony != null)
                    {
                        // Unconfigure it
                        unlimitedZoomHarmony.UnpatchAll(unlimitedZoomHarmony.Id);
                        unlimitedZoomHarmony = null;
                    }
                }
            }

            if (optionName == optionRemoveCameraClipping)
            {
                active_cameraFix = Options.GetBool(optionName);
            }

            // Remove level cap
            if(optionName == optionRemoveLevelCap)
            {
                removeLevelCap = Options.GetBool(optionName);
            }
        }

        void OnAction(string actionName, string actionType)
        {
            if(actionName == actionLevelUp)
            {
                LevelUpFish(Options.GetInt(optionLevelUpIncreaseCount));
            }
        }

        void OnSceneChanged(string oldScene, string newScene)
        {
            // Reset the no spawn thing
            noDefaultSpawn = new Dictionary<string, bool>();

            // Ensure the spawns exist
            EnsureSpawnsExist();
        }

        void Update()
        {
            // Update coins in the mod manager
            if (cachedCoins != PlayerCurrency.Amount)
            {
                // Do we have unlimited money active?
                if (_active_unlimitedMoney)
                {
                    // Money can't decrease
                    PlayerCurrency.Amount = cachedCoins;
                }
                else
                {
                    // Update mod launcher
                    cachedCoins = PlayerCurrency.Amount;
                    Options.SetFloat(optionCoins, cachedCoins);
                }
            }
        }

        // Prefix for camera fixing
        static bool Prefix_RaycastCameraPosition(Vector3 desiredPos, ref Vector3 __result)
        {
            if (active_cameraFix)
            {
                // Just return the original position
                __result = desiredPos;

                // do not run original method
                return false;
            }
            else
            {
                // Run original method
                return true;
            }
        }

        // Prefix to unlock all fish, used in a patch
        static bool Prefix_CanBeSelected(LivingEntity livingEntity, ref bool __result)
        {
            if (active_unlockAllFish)
            {
                // Change the result
                __result = true;

                // do not run original method
                return false;
            }
            else
            {
                // Run the normal method
                return true;
            }
        }

        static void Postfix_update(LivingEntity __instance)
        {
            if (active_infiniteSprint)
            {
                // Stop if there is no player controller
                if (PlayerController.Instance == null) return;

                // Is this our fish?
                if (__instance == PlayerController.Instance.CurrentLivingEntity)
                {
                    // Ensure energy doesn't fall below 1
                    if (__instance.Energy < 1)
                    {
                        __instance.Energy = 1;
                    }
                }
            }

            if(active_unlimiedHealth)
            {
                // Stop if there is no player controller
                if (PlayerController.Instance == null) return;

                // Is this our fish?
                if (__instance == PlayerController.Instance.CurrentLivingEntity)
                {
                    // Ensure energy doesn't fall below 1
                    if (__instance.Health < __instance.MaxHealth)
                    {
                        __instance.Health = __instance.MaxHealth;
                    }
                }
            }
        }

        // Prefix to unlock all fish, used in a patch
        static bool Prefix_Update()
        {
            if (active_unlimitedBreath)
            {
                // do not run original method
                return false;
            }
            else
            {
                // Run the original method
                return true;
            }

        }

        public void EnsureSpawnsExist()
        {
            // Avoid this in certain scenes
            string currentScene = SceneHandler.GetCurrentScene();
            if (currentScene == "LoadingGame" || currentScene == "MainMenu") return;

            // Ensure we are on a map
            if (Map.Instance == null || Map.Instance.ActiveSpawns == null) return;

            // Load all fish
            List<LivingEntity> list = new List<LivingEntity>();
            list.AddRange(Resources.LoadAll<LivingEntity>("NPCS"));

            // Create a map of all existing keys
            LivingEntity[] keys = new LivingEntity[Map.Instance.ActiveSpawns.Keys.Count];
            Map.Instance.ActiveSpawns.Keys.CopyTo(keys, 0);

            // Picking a random spawn
            System.Random rnd = new System.Random();

            // Loop over each fish
            foreach (LivingEntity livingEntity in list)
            {
                // Does a spawn exist?
                if (!Map.Instance.ActiveSpawns.ContainsKey(livingEntity))
                {
                    // Nope! Assign a random one:
                    //List<NPCSpawn> ourSpawns = new List<NPCSpawn>();
                    //Map.Instance.ActiveSpawns[livingEntity] = ourSpawns;

                    Map.Instance.ActiveSpawns[livingEntity] = Map.Instance.ActiveSpawns[keys[rnd.Next(keys.Length)]];

                    // Prevent any spawns of this one
                    noDefaultSpawn[livingEntity.name] = true;

                    // This thing isn't allow to spawn anything
                    /*if (!LivingEntity.FishAmountList.ContainsKey(livingEntity.name))
                    {
                        LivingEntity.FishAmountList.Add(livingEntity.name, 1);
                    }

                    // Set it to max
                    LivingEntity.FishAmountList[livingEntity.name] = int.MaxValue - 1;

                    // Pick 5 random spawns for this
                    /*for (int i = 0; i < 5; ++i)
                    {
                        List<NPCSpawn> spawns = Map.Instance.ActiveSpawns[keys[rnd.Next(keys.Length)]];


                        //Map.Instance.ActiveSpawns[livingEntity] = spawns;

                        NPCSpawn referenceSpawn = spawns[rnd.Next(spawns.Count)];

                        NPCSpawn newSpawn = new NPCSpawn();
                        //newSpawn.enabled = false;

                        newSpawn.transform.position = referenceSpawn.transform.position;
                        ourSpawns.Add(newSpawn);
                    }*/
                }

                // Remove the requirnment for achievements
                foreach (RequiresAchievement ach in livingEntity.GetComponents<RequiresAchievement>())
                {
                    UnityEngine.Object.Destroy(ach);
                }
            }
        }

        void LevelUpFish(int totalLevels = 1)
        {
            // Ensure we have a fish to level up
            if (PlayerController.Instance == null || PlayerController.Instance.CurrentLivingEntity == null) return;

            // Allow infinite levels
            overrideRemoveLevelCap = true;

            // Grab our fish and set its experience to what is required to level up plus one
            LivingEntity ourFish = PlayerController.Instance.CurrentLivingEntity;

            //int currentLevel = ourFish.Level;

            // Manage our fish
            Traverse traverse = new Traverse(ourFish);
            traverse.Field<bool>("_canGainExperience").Value = true;

            if(totalLevels > 1)
            {
                ourFish.Level += totalLevels - 1;
            }

            float expRequired = ourFish.GetRequiredExp() + 0.000001f;
            ourFish.Experience = expRequired;

            // Did we fail to give it a new level?
            /*if(currentLevel == ourFish.Level)
            {
                // Add one anyways!
                ourFish.Level += 1;
            }*/

            // no longer allow infinite level
            overrideRemoveLevelCap = false;
        }

        // This is for unlimited zoom
        // It basically removes the scroll limits by changing the instructions from a set_Scroll to a double pop
        // We do a double pop to leave the stack in a good state
        public static IEnumerable<CodeInstruction> TranspilePlayerControllerUpdate(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int totalToRemove = 3;

            for (int i = 0; i < codes.Count; i++)
            {
                if(codes[i].opcode == OpCodes.Call && codes[i].operand != null)
                {
                    string theOpString = codes[i].operand.ToString();

                    if (theOpString.Contains("set_Scroll"))
                    {
                        // Replace the instruciton with POP, and add another pop to remove the reference to "this"
                        codes[i] = new CodeInstruction(OpCodes.Pop, null);
                        codes.Insert(i, new CodeInstruction(OpCodes.Pop, null));

                        // We done?
                        if (--totalToRemove == 0)
                        {
                            break;
                        }
                    }
                }
            }

            // Return it
            return codes.AsEnumerable();
        }

        // Fix for star values
        private static bool PrefixGetStarValue(ref int __result)
        {
            if(active_unlockAllFish)
            {
                // Return result is 3
                __result = 3;

                // Don't run original method
                return false;
            }

            // Run original method
            return true;
        }

        public static bool PrefixApplyPlayerSize(LivingEntity entity, ref int __result, NpcGenerator __instance)
        {
            // Do normal function
            if (JsonMonoSingleton<Gameplay, Gameplay.B, Gameplay.S, Gameplay.I, Gameplay.F>.Instance.GameMode == Gameplay.Mode.Survival && entity is Predator)
            {
                __result = Mathf.Clamp((int)(__instance.SurvivalPredatorRatio * entity.GetMaxCount()), 1, 9999);
            }
            else
            {
                __result = (int)entity.GetMaxCount();
            }

            // Limt max fish based on our option
            int theMax = Options.GetInt(optionMaxFish);
            if(__result > theMax)
            {
                __result = theMax;
            }

            if(noDefaultSpawn.ContainsKey(entity.name))
            {
                __result = 0;
            }

            // Don't run original method
            return false;
        }

        private static bool PrefixCanLevelUp(ref bool __result)
        {
            // Should we allow infinite levels?
            if(removeLevelCap || overrideRemoveLevelCap)
            {
                // Return result is true (allowed to level up)
                __result = true;

                // Don't run original method
                return false;
            }

            // Do run original method
            return true;
        }

        private IEnumerator SlowUpdate()
        {
            while(true)
            {
                // wait a second
                yield return new WaitForSeconds(1f);

                // If no level cap
                if(removeLevelCap)
                {
                    // Ensure we have a fish to level up
                    if (PlayerController.Instance != null && PlayerController.Instance.CurrentLivingEntity != null)
                    {
                        // Grab our fish
                        LivingEntity ourFish = PlayerController.Instance.CurrentLivingEntity;

                        // Ensure it is allowed to gain EXP
                        Traverse traverse = new Traverse(ourFish);
                        traverse.Field<bool>("_canGainExperience").Value = true;
                    }
                }
            }
        }
    }
}
