using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace BattleBallista145
{
    // MCM is a SOFT dependency: this class is the ONLY type in the assembly
    // that references MCM types, and it is only ever touched from
    // DeployHotkey.ReadFromSettings() (a NoInlining method wrapped in
    // try/catch). If MCM isn't installed, the type never loads and the
    // hotkey falls back to the default B. Don't reference this class from
    // anywhere else without going through the same guard.
    public class BattleBallistaSettings : AttributeGlobalSettings<BattleBallistaSettings>
    {
        public override string Id => "BattleBallista145_Settings";
        public override string DisplayName => "Battle Ballista (1.4.5)";
        public override string FolderName => "BattleBallista145";
        public override string FormatType => "json2";

        public static new BattleBallistaSettings Instance => GlobalSettings<BattleBallistaSettings>.Instance;

        // Every entry must be a TaleWorlds.InputSystem.InputKey member name —
        // DeployHotkey maps the selected string back with Enum.TryParse.
        // D1–D0 are the number-row keys (InputKey's names, not ours).
        // MCM persists the dropdown's SELECTED INDEX, not the string: never
        // reorder or remove entries (that silently rebinds users' saved
        // keys) — only append new keys at the end.
        internal static readonly string[] DeployKeyOptions =
        {
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9", "D0",
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
            "Insert", "Delete", "Home", "End", "PageUp", "PageDown",
            "Comma", "Period", "Slash", "SemiColon", "Apostrophe",
            "OpenBraces", "CloseBraces", "BackSlash", "Tilde", "Minus", "Equals",
            "Numpad0", "Numpad1", "Numpad2", "Numpad3", "Numpad4",
            "Numpad5", "Numpad6", "Numpad7", "Numpad8", "Numpad9",
            "NumpadSlash", "NumpadMultiply", "NumpadMinus", "NumpadPlus", "NumpadPeriod",
        };

        [SettingPropertyGroup("Hotkey", GroupOrder = 0)]
        [SettingPropertyDropdown("Deploy Key", Order = 0, RequireRestart = false,
            HintText = "Keyboard key that deploys the currently-selected siege engine in battle. " +
                "Applies immediately, no restart needed. Pick a key the game doesn't already use in " +
                "missions (the default B is safe; F/E/WASD and similar are already bound by vanilla). " +
                "D1-D0 are the number-row keys. The gamepad binding (D-pad Right) is fixed and " +
                "unaffected by this setting. If MCM is not installed, the mod simply uses B.")]
        public Dropdown<string> DeployKey { get; set; } = new Dropdown<string>(DeployKeyOptions, selectedIndex: 1); // "B"

        [SettingPropertyGroup("Deployment", GroupOrder = 1)]
        [SettingPropertyBool("Allow sally-out deployment", Order = 0, RequireRestart = false,
            HintText = "Allow deploying engines in sally-out battles (defenders sallying out of a " +
                "besieged settlement). These were blocked in v1.1.6 as a precaution after a crash " +
                "report that later proved unrelated; the vanilla engine AI explicitly supports " +
                "sally-outs, so they are enabled by default. Turn this OFF if you experience " +
                "crashes when deploying in battles outside a besieged settlement's walls. " +
                "Without MCM installed, sally-out deployment is allowed.")]
        public bool AllowSallyOutDeployment { get; set; } = true;

        [SettingPropertyGroup("Deployment", GroupOrder = 1)]
        [SettingPropertyInteger("Max deployments per engine type", 1, 10, Order = 1, RequireRestart = false,
            HintText = "How many of EACH engine type (Ballista, Catapult, Fire Catapult, Trebuchet) " +
                "you can deploy per battle. Applies immediately - even mid-battle: raising it " +
                "un-caps a type you had exhausted, lowering it stops further deploys of a type " +
                "already at or over the new cap (engines already placed stay). " +
                "Default: 4. Without MCM installed, the cap is 4.")]
        public int MaxEnginesPerKind { get; set; } = 4;
    }
}
