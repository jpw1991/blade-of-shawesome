using System.Collections.Generic;
using BladeOfShawesome.Items;
using ChebsValheimLibrary.Common;
using HarmonyLib;

namespace BladeOfShawesome.Patches
{
    [HarmonyPatch(typeof(InventoryGui))]
    public class InventoryGuiPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(InventoryGui.UpdateRecipeList))]
        static void UpdateRecipeList(InventoryGui __instance, ref List<Recipe> recipes)
        {
            // Shawesome wants the blade only craftable during a Thunderstorm and only if the player has reached
            // 80 fist and 80 elemental magic skill. There's a couple of potential ways to go
            // about doing this (as far as my own understanding of things goes):
            //
            // a) List it as a recipe normally, then make a boolean patch on InventoryGui.OnCraftPressed and block
            //    and show a message like "Can only craft during thunderstorm"
            // b) Only add it as a recipe if it is currently a thunderstorm
            //
            // I think A is the nicer option, but will also conflict more with other mods. So for that reason, I
            // decided to go with option B and remove it if there's no thunderstorm currently active.

            var player = Player.m_localPlayer;
            if (player == null)
            {
                Jotunn.Logger.LogError("Player is null");
                return;
            }

            var fistSkill = player.GetSkillLevel(Skills.SkillType.Unarmed);
            if (fistSkill < 80) return;
            var elementalSkill = player.GetSkillLevel(Skills.SkillType.ElementalMagic);
            if (elementalSkill < 80) return;

            var currentEnvironment = EnvMan.instance.GetCurrentEnvironment();
            var thunderstormActive = BladeOfShawesomeItem.CraftingWeatherCondition.Value == Weather.Env.None
                                     || currentEnvironment.m_name ==
                                     InternalName.GetName(BladeOfShawesomeItem.CraftingWeatherCondition.Value);
            if (__instance.InCraftTab())
            {
                recipes.RemoveAll(recipe =>
                {
                    var recipeAsStr = recipe.ToString();
                    return !thunderstormActive &&
                               (recipeAsStr.Contains(BasePlugin.BladeOfShawesome.ItemName)
                               || recipeAsStr.Contains(BasePlugin.GreatswordOfShawesome.ItemName));
                });
            }
        }
    }
}