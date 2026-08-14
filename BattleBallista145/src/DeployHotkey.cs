using System;
using System.Runtime.CompilerServices;
using TaleWorlds.InputSystem;

namespace BattleBallista145
{
    // Resolves the configured deploy hotkey without making MCM a hard
    // runtime requirement. ReadFromSettings() is the only path that touches
    // MCM types; NoInlining keeps the JIT from pulling the MCM type load
    // into the caller, so on a no-MCM install the TypeLoadException /
    // FileNotFoundException surfaces exactly here, gets caught once, and
    // every later read short-circuits to the default B.
    internal static class DeployHotkey
    {
        public const InputKey Default = InputKey.B;

        private static bool _mcmUnavailable;

        public static InputKey Current
        {
            get
            {
                if (_mcmUnavailable) return Default;
                try
                {
                    return ReadFromSettings();
                }
                catch
                {
                    _mcmUnavailable = true;
                    return Default;
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InputKey ReadFromSettings()
        {
            // Instance is null until MCM discovers the settings class (and
            // stays null forever if MCM is installed but failed) — fall back
            // rather than cache a failure, so the setting kicks in once MCM
            // finishes registration.
            var settings = BattleBallistaSettings.Instance;
            string name = settings?.DeployKey?.SelectedValue;
            if (string.IsNullOrEmpty(name)) return Default;

            return Enum.TryParse(name, out InputKey key) ? key : Default;
        }
    }
}
