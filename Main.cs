using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.Switchers;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UnityEngine;
using UnityModManagerNet;

namespace MagicDeceiverCompat
{
public static class Main
{
    private const string HarmonyId = "MagicDeceiverCompat";
    private const string ArcanistClassGuid = "52dbfd8505e22f84fad8d702611f60b7";
    private const string LoremasterClassGuid = "4a7c05adfbaf05446a6bf664d28fb103";
    private const string MagicDeceiverArchetypeGuid = "5c77110cd0414e7eb4c2e485659c9a46";
    private const string MagicDeceiverSpellbookGuid = "587066af76a74f47a904bb017697ba08";
    private const string LoremasterMagicDeceiverSpellbookGuid = "d20206c5e91942399e76eb366c026ca9";
    private static readonly string[] LoremasterArcanistSpellSecretGuids =
    {
        "cd3058b460930a5418f3811ec9be9ebb",
        "084b3340727f0574a82239ecf74f8910",
        "17a4c17d48c295543b3c449f91de8076"
    };

    private static readonly Metamagic SupportedMetamagic =
        Metamagic.Empower
        | Metamagic.Maximize
        | Metamagic.Quicken
        | Metamagic.Extend
        | Metamagic.Heighten
        | Metamagic.Reach
        | Metamagic.CompletelyNormal
        | Metamagic.Persistent
        | Metamagic.Selective
        | Metamagic.Bolstered
        | Metamagic.Piercing
        | Metamagic.Intensified;

    internal static UnityModManager.ModEntry.ModLogger Logger;
    private static bool _initialized;
    private static readonly Dictionary<SpellbookPCView, ButtonLayoutSnapshot> ButtonLayouts =
        new Dictionary<SpellbookPCView, ButtonLayoutSnapshot>();
    private static readonly Dictionary<SpellbookPCView, SpellbookButtonMode> ButtonModes =
        new Dictionary<SpellbookPCView, SpellbookButtonMode>();

    public static bool Load(UnityModManager.ModEntry modEntry)
    {
        Logger = modEntry.Logger;
        var harmony = new Harmony(HarmonyId);
        harmony.PatchAll();
        Logger.Log("MagicDeceiverCompat loaded.");
        return true;
    }

    internal static void ApplyBlueprintPatches()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        PatchLoremasterPrerequisites();
        PatchLoremasterSpellSecrets();
        PatchMagicHackMetamagicAvailability();
    }

    private static void PatchLoremasterPrerequisites()
    {
        var loremasterClass = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>(LoremasterClassGuid);
        var arcanistClass = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>(ArcanistClassGuid);
        var magicDeceiverArchetype = ResourcesLibrary.TryGetBlueprint<BlueprintArchetype>(MagicDeceiverArchetypeGuid);
        if (loremasterClass == null || arcanistClass == null || magicDeceiverArchetype == null)
        {
            Logger.Error("Failed to locate Loremaster or Magic Deceiver blueprints.");
            return;
        }

        var originalCount = loremasterClass.ComponentsArray.Length;
        loremasterClass.ComponentsArray = loremasterClass.ComponentsArray
            .Where(component => !IsMagicDeceiverExclusion(component, arcanistClass, magicDeceiverArchetype))
            .ToArray();

        var removed = originalCount - loremasterClass.ComponentsArray.Length;
        Logger.Log(removed > 0
            ? string.Format("Removed {0} Loremaster exclusion component(s) for Magic Deceiver.", removed)
            : "No Loremaster exclusion component needed removal.");
    }

    private static void PatchLoremasterSpellSecrets()
    {
        var magicDeceiverSpellbookFeature = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>(LoremasterMagicDeceiverSpellbookGuid);
        if (magicDeceiverSpellbookFeature == null)
        {
            Logger.Error("Failed to locate Loremaster Magic Deceiver spellbook feature.");
            return;
        }

        var patched = 0;
        foreach (var guid in LoremasterArcanistSpellSecretGuids)
        {
            var feature = ResourcesLibrary.TryGetBlueprint<BlueprintParametrizedFeature>(guid);
            if (feature == null)
            {
                Logger.Error("Failed to locate a Loremaster Arcanist spell secret: " + guid);
                continue;
            }

            foreach (var prerequisite in feature.ComponentsArray.OfType<PrerequisiteFeaturesFromList>())
            {
                if (AddFeatureToPrerequisiteList(prerequisite, magicDeceiverSpellbookFeature))
                {
                    patched++;
                }
            }
        }

        Logger.Log(string.Format("Patched {0} Loremaster Arcanist spell-secret prerequisite list(s) for Magic Deceiver.", patched));
    }

    private static void PatchMagicHackMetamagicAvailability()
    {
        var spellbook = ResourcesLibrary.TryGetBlueprint<BlueprintSpellbook>(MagicDeceiverSpellbookGuid);
        if (spellbook == null)
        {
            Logger.Error("Failed to locate Magic Deceiver spellbook.");
            return;
        }

        var component = spellbook.GetComponent<MagicHackSpellbookComponent>();
        if (component == null)
        {
            Logger.Error("Magic Deceiver spellbook is missing MagicHackSpellbookComponent.");
            return;
        }

        var patched = 0;
        for (var i = 0; i < 10; i++)
        {
            patched += PatchMagicHackAbility(component.GetDefaultBlueprint(i));
            patched += PatchMagicHackAbility(component.GetTouchBlueprint(i));
        }

        Logger.Log(string.Format("Patched {0} Magic Hack slot blueprint(s) to allow standard metamagic.", patched));
    }

    private static int PatchMagicHackAbility(BlueprintAbility blueprint)
    {
        if (blueprint == null)
        {
            return 0;
        }

        var updated = blueprint.AvailableMetamagic | SupportedMetamagic;
        if (updated == blueprint.AvailableMetamagic)
        {
            return 0;
        }

        blueprint.AvailableMetamagic = updated;
        return 1;
    }

    private static bool IsMagicDeceiverExclusion(
        BlueprintComponent component,
        BlueprintCharacterClass arcanistClass,
        BlueprintArchetype magicDeceiverArchetype)
    {
        var exclusion = component as PrerequisiteNoArchetype;
        if (exclusion == null)
        {
            return false;
        }

        return exclusion.CharacterClass == arcanistClass && exclusion.Archetype == magicDeceiverArchetype;
    }

    private static bool AddFeatureToPrerequisiteList(
        PrerequisiteFeaturesFromList prerequisite,
        BlueprintFeature feature)
    {
        var field = typeof(PrerequisiteFeaturesFromList).GetField("m_Features", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            Logger.Error("Could not access PrerequisiteFeaturesFromList.m_Features.");
            return false;
        }

        var existing = (BlueprintFeatureReference[])field.GetValue(prerequisite) ?? new BlueprintFeatureReference[0];
        if (existing.Any(reference => reference != null && reference.Get() == feature))
        {
            return false;
        }

        var next = existing
            .Concat(new[] { BlueprintReference<BlueprintFeature>.CreateTyped<BlueprintFeatureReference>(feature) })
            .ToArray();
        field.SetValue(prerequisite, next);
        return true;
    }

    [HarmonyPatch(typeof(BlueprintsCache), "Init")]
    private static class BlueprintsCache_Init_Patch
    {
        private static void Postfix()
        {
            try
            {
                ApplyBlueprintPatches();
            }
            catch (Exception ex)
            {
                if (Logger != null)
                {
                    Logger.Error("MagicDeceiverCompat blueprint patch failed: " + ex);
                }
            }
        }
    }

    [HarmonyPatch(typeof(SpellbookSwitcherVM), "OnSpellbookChange")]
    private static class SpellbookSwitcherVM_OnSpellbookChange_Patch
    {
        private static void Postfix(SpellbookSwitcherVM __instance, Kingmaker.UnitLogic.Spellbook spellbook)
        {
            try
            {
                if (spellbook == null || spellbook.Blueprint == null)
                {
                    return;
                }

                if (spellbook.Blueprint.AssetGuidThreadSafe == MagicDeceiverSpellbookGuid)
                {
                    __instance.MetamagicAvailable.Value = true;
                }
            }
            catch (Exception ex)
            {
                if (Logger != null)
                {
                    Logger.Error("MagicDeceiverCompat spellbook switch patch failed: " + ex);
                }
            }
        }
    }

    [HarmonyPatch(typeof(MetamagicBuilder), MethodType.Constructor, new[] { typeof(Kingmaker.UnitLogic.Spellbook), typeof(AbilityData) })]
    private static class MetamagicBuilder_Ctor_Patch
    {
        private static void Postfix(MetamagicBuilder __instance, AbilityData spell)
        {
            try
            {
                PreserveMagicHackData(__instance, spell);
            }
            catch (Exception ex)
            {
                if (Logger != null)
                {
                    Logger.Error("MagicDeceiverCompat metamagic preview patch failed: " + ex);
                }
            }
        }
    }

    [HarmonyPatch(typeof(MetamagicBuilder), "Apply")]
    private static class MetamagicBuilder_Apply_Patch
    {
        private static void Postfix(MetamagicBuilder __instance)
        {
            try
            {
                var resultAbility = __instance.ResultAbilityData;
                if (resultAbility == null || resultAbility.MagicHackData == null)
                {
                    return;
                }

                resultAbility.MagicHackData = resultAbility.MagicHackData.Clone();
                SyncMagicHackSpellLevel(resultAbility, __instance.ResultSpellLevel);
                EnsureMagicHackIconSource(resultAbility);
            }
            catch (Exception ex)
            {
                if (Logger != null)
                {
                    Logger.Error("MagicDeceiverCompat metamagic apply patch failed: " + ex);
                }
            }
        }
    }

    [HarmonyPatch(typeof(AbilityData), "get_Icon")]
    private static class AbilityData_Icon_Patch
    {
        private static void Postfix(AbilityData __instance, ref Sprite __result)
        {
            try
            {
                var magicHack = __instance == null ? null : __instance.MagicHackData;
                if (magicHack == null)
                {
                    return;
                }

                var iconSource = magicHack.Spell1 ?? magicHack.Spell2;
                if (iconSource != null && iconSource.Icon != null)
                {
                    __result = iconSource.Icon;
                }
            }
            catch (Exception ex)
            {
                if (Logger != null)
                {
                    Logger.Error("MagicDeceiverCompat Magic Hack icon patch failed: " + ex);
                }
            }
        }
    }

    [HarmonyPatch(typeof(AbilityData), "get_SpellLevel")]
    private static class AbilityData_SpellLevel_Patch
    {
        private static void Postfix(AbilityData __instance, ref int __result)
        {
            try
            {
                if (__instance == null || __instance.MagicHackData == null || __instance.MetamagicData == null)
                {
                    return;
                }

                __result = __instance.MagicHackData.SpellLevel + __instance.MetamagicData.SpellLevelCost;
            }
            catch (Exception ex)
            {
                if (Logger != null)
                {
                    Logger.Error("MagicDeceiverCompat Magic Hack AbilityData spell-level patch failed: " + ex);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.Metamagic.SpellbookMetamagicMixerVM), "TryWriteNewSpell")]
    private static class SpellbookMetamagicMixerVM_TryWriteNewSpell_Patch
    {
        private static void Prefix(object __instance)
        {
            try
            {
                var builder = GetReactiveValue<MetamagicBuilder>(__instance, "m_MetamagicBuilder");
                var spellVm = GetReactiveValue<object>(__instance, "m_CurrentTemporarySpell");
                var spell = spellVm == null
                    ? null
                    : Traverse.Create(spellVm).Field("SpellData").GetValue<AbilityData>();
                PrepareMagicHackSpellForWrite(spell, builder);
            }
            catch (Exception ex)
            {
                if (Logger != null)
                {
                    Logger.Error("MagicDeceiverCompat Magic Hack write-level patch failed: " + ex);
                }
            }
        }
    }

    [HarmonyPatch(typeof(SpellbookPCView), "BindViewImplementation")]
    private static class SpellbookPCView_BindViewImplementation_Patch
    {
        private static void Postfix(SpellbookPCView __instance)
        {
            TryShowBothMagicDeceiverButtons(__instance);
        }
    }

    [HarmonyPatch(typeof(SpellbookPCView), "OnActivateMetamagic")]
    private static class SpellbookPCView_OnActivateMetamagic_Patch
    {
        private static void Postfix(SpellbookPCView __instance)
        {
            SetButtonMode(__instance, SpellbookButtonMode.Metamagic);
            TryShowBothMagicDeceiverButtons(__instance);
        }
    }

    [HarmonyPatch(typeof(SpellbookPCView), "OnDeactivateMetamagic")]
    private static class SpellbookPCView_OnDeactivateMetamagic_Patch
    {
        private static void Postfix(SpellbookPCView __instance)
        {
            SetButtonMode(__instance, SpellbookButtonMode.Normal);
            TryShowBothMagicDeceiverButtons(__instance);
        }
    }

    [HarmonyPatch(typeof(SpellbookPCView), "OnActivateMagicHack")]
    private static class SpellbookPCView_OnActivateMagicHack_Patch
    {
        private static void Postfix(SpellbookPCView __instance)
        {
            SetButtonMode(__instance, SpellbookButtonMode.MagicHack);
            TryShowBothMagicDeceiverButtons(__instance);
        }
    }

    [HarmonyPatch(typeof(SpellbookPCView), "OnDeactivateMagicHack")]
    private static class SpellbookPCView_OnDeactivateMagicHack_Patch
    {
        private static void Postfix(SpellbookPCView __instance)
        {
            SetButtonMode(__instance, SpellbookButtonMode.Normal);
            TryShowBothMagicDeceiverButtons(__instance);
        }
    }

    private static void TryShowBothMagicDeceiverButtons(SpellbookPCView view)
    {
        try
        {
            var viewModel = Traverse.Create(view).Property("ViewModel").GetValue<object>();
            var currentSpellbook = Traverse.Create(viewModel).Field("CurrentSpellbook").GetValue<object>();
            var spellbook = currentSpellbook == null
                ? null
                : Traverse.Create(currentSpellbook).Property("Value").GetValue<Kingmaker.UnitLogic.Spellbook>();
            if (spellbook == null || spellbook.Blueprint == null || spellbook.Blueprint.AssetGuidThreadSafe != MagicDeceiverSpellbookGuid)
            {
                RestoreButtonLayout(view);
                return;
            }

            var metamagicButton = AccessTools.Field(typeof(SpellbookPCView), "m_MetamagicButton").GetValue(view) as OwlcatButton;
            var magicHackButton = AccessTools.Field(typeof(SpellbookPCView), "m_MagicHackButton").GetValue(view) as OwlcatButton;
            var metamagicLabel = AccessTools.Field(typeof(SpellbookPCView), "m_MetamagicLabel").GetValue(view) as TextMeshProUGUI;
            var magicHackLabel = AccessTools.Field(typeof(SpellbookPCView), "m_MagicHackLabel").GetValue(view) as TextMeshProUGUI;

            if (metamagicButton == null || magicHackButton == null)
            {
                return;
            }

            CaptureButtonLayout(view, metamagicButton, magicHackButton, metamagicLabel, magicHackLabel);
            metamagicButton.gameObject.SetActive(true);
            magicHackButton.gameObject.SetActive(true);
            if (metamagicLabel != null)
            {
                metamagicLabel.text = "超魔";
            }

            if (magicHackLabel != null)
            {
                magicHackLabel.text = "魔法融合";
            }

            if (metamagicLabel != null)
            {
                metamagicLabel.text = "\u8d85\u9b54";
                metamagicLabel.enableAutoSizing = true;
            }

            if (magicHackLabel != null)
            {
                magicHackLabel.text = "\u9b54\u6cd5\u878d\u5408";
            }

            LayoutMagicDeceiverModeButton(view, magicHackButton, metamagicButton);
        }
        catch (Exception ex)
        {
            if (Logger != null)
            {
                Logger.Error("MagicDeceiverCompat button layout patch failed: " + ex);
            }
        }
    }

    private static void PreserveMagicHackData(MetamagicBuilder builder, AbilityData source)
    {
        if (builder == null || source == null || source.MagicHackData == null || builder.ResultAbilityData == null)
        {
            return;
        }

        builder.ResultAbilityData.MagicHackData = source.MagicHackData.Clone();
        SyncMagicHackSpellLevel(builder.ResultAbilityData, builder.ResultSpellLevel);
        EnsureMagicHackIconSource(builder.ResultAbilityData);
    }

    private static void PrepareMagicHackSpellForWrite(AbilityData spell, MetamagicBuilder builder)
    {
        if (spell == null || builder == null || builder.ResultAbilityData == null)
        {
            return;
        }

        var result = builder.ResultAbilityData;
        if (spell.MagicHackData == null && result.MagicHackData != null)
        {
            spell.MagicHackData = result.MagicHackData.Clone();
        }

        if (spell.MagicHackData == null)
        {
            return;
        }

        if (result.MetamagicData != null)
        {
            spell.MetamagicData = result.MetamagicData.Clone();
        }

        SyncMagicHackSpellLevel(spell, builder.ResultSpellLevel);
        EnsureMagicHackIconSource(spell);
    }

    private static void SyncMagicHackSpellLevel(AbilityData ability, int spellLevel)
    {
        if (ability == null || ability.MagicHackData == null)
        {
            return;
        }

        var metamagicCost = ability.MetamagicData == null ? 0 : ability.MetamagicData.SpellLevelCost;
        var baseLevel = Math.Max(0, spellLevel - metamagicCost);
        ability.MagicHackData.SpellLevel = baseLevel;
        ability.OverrideSpellLevel = null;

        var field = AccessTools.Field(typeof(AbilityData), "<SpellLevelInSpellbook>k__BackingField");
        if (field != null)
        {
            field.SetValue(ability, null);
        }
    }

    private static void EnsureMagicHackIconSource(AbilityData ability)
    {
        if (ability == null || ability.MagicHackData == null)
        {
            return;
        }

        if (ability.SpellFoolIconOverrideSource == null)
        {
            var field = AccessTools.Field(typeof(AbilityData), "<SpellFoolIconOverrideSource>k__BackingField");
            if (field != null)
            {
                field.SetValue(ability, ability.MagicHackData.Spell1 ?? ability.MagicHackData.Spell2);
            }
        }
    }

    private static T GetReactiveValue<T>(object owner, string fieldName) where T : class
    {
        if (owner == null)
        {
            return null;
        }

        var reactive = Traverse.Create(owner).Field(fieldName).GetValue<object>();
        return reactive == null ? null : Traverse.Create(reactive).Property("Value").GetValue<T>();
    }

    private static void CaptureButtonLayout(
        SpellbookPCView view,
        OwlcatButton metamagicButton,
        OwlcatButton magicHackButton,
        TextMeshProUGUI metamagicLabel,
        TextMeshProUGUI magicHackLabel)
    {
        if (view == null || ButtonLayouts.ContainsKey(view))
        {
            return;
        }

        ButtonLayouts[view] = new ButtonLayoutSnapshot(
            GetButtonContainer(metamagicButton, magicHackButton),
            GetButtonContainer(magicHackButton, metamagicButton),
            metamagicButton,
            magicHackButton,
            metamagicLabel,
            magicHackLabel);
    }

    private static void RestoreButtonLayout(SpellbookPCView view)
    {
        ButtonLayoutSnapshot snapshot;
        if (view == null || !ButtonLayouts.TryGetValue(view, out snapshot))
        {
            return;
        }

        snapshot.Restore();
        ButtonLayouts.Remove(view);
    }

    private static void SetButtonMode(SpellbookPCView view, SpellbookButtonMode mode)
    {
        if (view != null)
        {
            ButtonModes[view] = mode;
        }
    }

    private static SpellbookButtonMode GetButtonMode(SpellbookPCView view)
    {
        SpellbookButtonMode mode;
        return view != null && ButtonModes.TryGetValue(view, out mode) ? mode : SpellbookButtonMode.Normal;
    }

    private static void LayoutMagicDeceiverModeButton(
        SpellbookPCView view,
        OwlcatButton magicHackButton,
        OwlcatButton metamagicButton)
    {
        var mode = GetButtonMode(view);
        var primaryButton = mode == SpellbookButtonMode.MagicHack ? metamagicButton : magicHackButton;
        var secondaryButton = primaryButton == magicHackButton ? metamagicButton : magicHackButton;
        var primaryTransform = GetButtonContainer(primaryButton, secondaryButton);
        var secondaryTransform = GetButtonContainer(secondaryButton, primaryButton);
        if (primaryTransform == null || secondaryTransform == null)
        {
            return;
        }

        var basePosition = primaryTransform.anchoredPosition.x <= secondaryTransform.anchoredPosition.x
            ? primaryTransform.anchoredPosition
            : secondaryTransform.anchoredPosition;

        primaryTransform.localScale = Vector3.one;
        secondaryTransform.localScale = Vector3.one;
        primaryTransform.anchoredPosition = basePosition;
        secondaryTransform.anchoredPosition = basePosition;
        primaryButton.gameObject.SetActive(true);
        secondaryButton.gameObject.SetActive(false);
        primaryTransform.SetAsLastSibling();
    }

    private static RectTransform GetButtonContainer(OwlcatButton button, OwlcatButton otherButton)
    {
        if (button == null)
        {
            return null;
        }

        var parent = button.transform.parent as RectTransform;
        if (parent != null && (otherButton == null || parent != otherButton.transform.parent))
        {
            return parent;
        }

        return button.transform as RectTransform;
    }

    private enum SpellbookButtonMode
    {
        Normal,
        MagicHack,
        Metamagic
    }

    private sealed class ButtonLayoutSnapshot
    {
        private readonly RectTransform _metamagicTransform;
        private readonly RectTransform _magicHackTransform;
        private readonly OwlcatButton _metamagicButton;
        private readonly OwlcatButton _magicHackButton;
        private readonly TextMeshProUGUI _metamagicLabel;
        private readonly TextMeshProUGUI _magicHackLabel;
        private readonly Vector2 _metamagicPosition;
        private readonly Vector2 _magicHackPosition;
        private readonly Vector3 _metamagicScale;
        private readonly Vector3 _magicHackScale;
        private readonly string _metamagicText;
        private readonly string _magicHackText;
        private readonly bool _metamagicAutoSizing;
        private readonly bool _magicHackAutoSizing;
        private readonly bool _metamagicActive;
        private readonly bool _magicHackActive;

        public ButtonLayoutSnapshot(
            RectTransform metamagicTransform,
            RectTransform magicHackTransform,
            OwlcatButton metamagicButton,
            OwlcatButton magicHackButton,
            TextMeshProUGUI metamagicLabel,
            TextMeshProUGUI magicHackLabel)
        {
            _metamagicTransform = metamagicTransform;
            _magicHackTransform = magicHackTransform;
            _metamagicButton = metamagicButton;
            _magicHackButton = magicHackButton;
            _metamagicLabel = metamagicLabel;
            _magicHackLabel = magicHackLabel;
            _metamagicPosition = metamagicTransform == null ? Vector2.zero : metamagicTransform.anchoredPosition;
            _magicHackPosition = magicHackTransform == null ? Vector2.zero : magicHackTransform.anchoredPosition;
            _metamagicScale = metamagicTransform == null ? Vector3.one : metamagicTransform.localScale;
            _magicHackScale = magicHackTransform == null ? Vector3.one : magicHackTransform.localScale;
            _metamagicText = metamagicLabel == null ? null : metamagicLabel.text;
            _magicHackText = magicHackLabel == null ? null : magicHackLabel.text;
            _metamagicAutoSizing = metamagicLabel != null && metamagicLabel.enableAutoSizing;
            _magicHackAutoSizing = magicHackLabel != null && magicHackLabel.enableAutoSizing;
            _metamagicActive = metamagicButton != null && metamagicButton.gameObject.activeSelf;
            _magicHackActive = magicHackButton != null && magicHackButton.gameObject.activeSelf;
        }

        public void Restore()
        {
            RestoreTransform(_metamagicTransform, _metamagicPosition, _metamagicScale);
            RestoreTransform(_magicHackTransform, _magicHackPosition, _magicHackScale);

            if (_metamagicButton != null)
            {
                _metamagicButton.gameObject.SetActive(_metamagicActive);
            }

            if (_magicHackButton != null)
            {
                _magicHackButton.gameObject.SetActive(_magicHackActive);
            }

            RestoreLabel(_metamagicLabel, _metamagicText, _metamagicAutoSizing);
            RestoreLabel(_magicHackLabel, _magicHackText, _magicHackAutoSizing);
        }

        private static void RestoreTransform(RectTransform transform, Vector2 position, Vector3 scale)
        {
            if (transform == null)
            {
                return;
            }

            transform.anchoredPosition = position;
            transform.localScale = scale;
        }

        private static void RestoreLabel(TextMeshProUGUI label, string text, bool autoSizing)
        {
            if (label == null)
            {
                return;
            }

            label.text = text;
            label.enableAutoSizing = autoSizing;
        }
    }

}
}
