using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace TAOMBattleBallista
{
    // MCM is a SOFT dependency: this class is the ONLY type in the assembly
    // that references MCM types, and it is only ever touched from
    // DeployHotkey.ReadFromSettings() (a NoInlining method wrapped in
    // try/catch). If MCM isn't installed, the type never loads and the
    // hotkey falls back to the default B. Don't reference this class from
    // anywhere else without going through the same guard.
    public class BattleBallistaSettings : AttributeGlobalSettings<BattleBallistaSettings>
    {
        public override string Id => "TAOMBattleBallista_Settings";
        public override string DisplayName => "Battle Ballista (TAOM)";
        public override string FolderName => "TAOMBattleBallista";
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
    }
}
