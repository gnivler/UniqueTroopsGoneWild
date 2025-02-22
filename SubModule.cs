﻿using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SandBox.GauntletUI;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.LinQuick;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

// ReSharper disable ClassNeverInstantiated.Global  
// ReSharper disable InconsistentNaming

namespace GloriousTroops
{
    public class SubModule : MBSubModuleBase
    {
        internal static Harmony harmony;
        internal static readonly bool MEOWMEOW = Environment.MachineName == "MEOWMEOW";
        private static SkillPanel skillPanel;
        private bool panelShown;
        internal static readonly FieldInfo dataSource = AccessTools.Field(typeof(GauntletPartyScreen), "_dataSource");
        private static bool IsPatched;

        protected override void OnSubModuleLoad()
        {
            harmony = new Harmony("ca.gnivler.bannerlord.GloriousTroops");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            base.OnSubModuleLoad();
        }

        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            if (!IsPatched)
            {
                RunManualPatches();
                IsPatched = true;
                // original = AccessTools.Method("DefaultPartyWageModel:GetTotalWage");
                // var updateFinalizer = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateFinalizer));
                // harmony.Patch(original, finalizer: new HarmonyMethod(updateFinalizer));

                // original = AccessTools.Method("MBObjectManager:RegisterObject");
                // original = original.MakeGenericMethod(typeof(CharacterObject));
                // var postfix = AccessTools.Method(typeof(MiscPatches.MBObjectManagerRegisterObject), nameof(MiscPatches.MBObjectManagerRegisterObject.Finalizer));
                // harmony.Patch(original, finalizer: new HarmonyMethod(postfix));
                // if (MEOWMEOW || Globals.Settings.SaveRecovery)
                // {
                //     original = AccessTools.Method("SaveContext:CollectObjects", new Type[] { });
                //     var postfix = AccessTools.Method(typeof(MiscPatches.SaveContextCollectObjects), nameof(MiscPatches.SaveContextCollectObjects.Postfix));
                //     harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                // }  
            }

            EquipmentUpgrading.InitSkills();
            EquipmentUpgrading.SetName = AccessTools.Method(typeof(CharacterObject), "SetName");
            if (MEOWMEOW)
            {
                CampaignCheats.SetCampaignSpeed(new List<string> { "100" });
                CampaignCheats.SetMainPartyAttackable(new List<string> { "0" });
            }
        }

        private static void RunManualPatches()
        {
            var propertyBasedTooltipVMExtensions = AccessTools.TypeByName("PropertyBasedTooltipVMExtensions");
            var displayClass = AccessTools.Inner(propertyBasedTooltipVMExtensions, "<>c__DisplayClass16_0");
            var original = displayClass.GetMethod("<UpdateTooltip>b__0", AccessTools.all);
            var replacement = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateTooltipPartyMemberReplacement));
            harmony.Patch(original, prefix: new HarmonyMethod(replacement));

            original = displayClass.GetMethod("<UpdateTooltip>b__1", AccessTools.all);
            replacement = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateTooltipPartyPrisonerReplacement));
            harmony.Patch(original, prefix: new HarmonyMethod(replacement));

            displayClass = AccessTools.Inner(propertyBasedTooltipVMExtensions, "<>c__DisplayClass15_0");
            original = displayClass.GetMethod("<UpdateTooltip>b__0", AccessTools.all);
            replacement = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateTooltipSettlementMemberReplacement));
            harmony.Patch(original, prefix: new HarmonyMethod(replacement));

            original = displayClass.GetMethod("<UpdateTooltip>b__1", AccessTools.all);
            replacement = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateTooltipSettlementPrisonerReplacement));
            harmony.Patch(original, prefix: new HarmonyMethod(replacement));

            displayClass = AccessTools.Inner(propertyBasedTooltipVMExtensions, "<>c__DisplayClass17_0");
            original = displayClass.GetMethod("<UpdateTooltip>b__0", AccessTools.all);
            replacement = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateTooltipArmyMemberReplacement));
            harmony.Patch(original, prefix: new HarmonyMethod(replacement));

            original = displayClass.GetMethod("<UpdateTooltip>b__1", AccessTools.all);
            replacement = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.UpdateTooltipArmyPrisonerReplacement));
            harmony.Patch(original, prefix: new HarmonyMethod(replacement));

            original = AccessTools.Method("SPScoreboardSideVM:RemoveTroop");
            var prefix = AccessTools.Method(typeof(MiscPatches), nameof(MiscPatches.HideoutBossDuelPrefix));
            harmony.Patch(original, prefix: new HarmonyMethod(prefix));

            if (Globals.Settings.PartyScreenChanges)
            {
                var ctor = AccessTools.FirstConstructor(typeof(PartyCharacterVM), c => c.GetParameters().Length > 0);
                harmony.Patch(ctor, transpiler: new HarmonyMethod(typeof(MiscPatches.PartyCharacterVMConstructor), nameof(MiscPatches.PartyCharacterVMConstructor.Transpiler)));
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            Globals.Settings = Settings.Instance;
            if (Globals.Settings!.Debug)
                Globals.Log.Restart();
            Globals.Log.Debug?.Log($"{Globals.Settings?.DisplayName} starting up...");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (gameStarterObject is CampaignGameStarter gameStarter)
                gameStarter.AddBehavior(new GloriousTroopsBehavior());
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            if (Game.Current != null && Globals.Settings?.Hotkey is not null && ScreenManager.TopScreen is MapScreen mapScreen)
                if (Input.IsKeyPressed((InputKey)Globals.Settings.Hotkey.SelectedIndex + 1))
                {
                    if (panelShown)
                    {
                        panelShown = false;
                        mapScreen.RemoveLayer(skillPanel.layer);
                        skillPanel.layer.InputRestrictions.ResetInputRestrictions();
                    }
                    else
                    {
                        panelShown = true;
                        skillPanel = new();
                        mapScreen.AddLayer(skillPanel.layer);
                        skillPanel.layer.InputRestrictions.SetInputRestrictions();
                    }
                }

            if (MEOWMEOW && Input.IsKeyPressed(InputKey.F2))
            {
                var q = MobileParty.MainParty.MemberRoster.GetTroopRoster().WhereQ(c => c.Character.Name.ToString() == "Glorious Aserai Recruit").ToListQ();
                for (var index = 0; index < q.Count; index++)
                {
                    var recruit = q[index];
                    MobileParty.MainParty.MemberRoster.AddXpToTroop(20000, recruit.Character);
                }
            }

            var superKey = Campaign.Current != null
                           && (Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl))
                           && (Input.IsKeyDown(InputKey.LeftAlt) || Input.IsKeyDown(InputKey.RightAlt))
                           && (Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift));


            if (superKey && Input.IsKeyPressed(InputKey.T))
                Helper.Restore();

            if (MEOWMEOW && Input.IsKeyPressed(InputKey.F9))
                Helper.CheckTracking();
        }
    }
}
