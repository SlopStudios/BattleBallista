using System;
using System.Runtime.CompilerServices;
using TaleWorlds.InputSystem;

namespace TAOMBattleBallista
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

        // Current is polled EVERY mission tick (~60 Hz). Reading MCM each time
        // walks the settings-provider lookup and string-parses the key name —
        // wasted work 59 times out of 60+. Cache the resolved key and refresh
        // once per second: live-applies from the MCM screen still land within
        // a second, and the tick path becomes two field reads. (1.5.7 perf
        // review, 2026-11-11.)
        private const int RefreshIntervalMs = 1000;

        private static bool _mcmUnavailable;
        private static InputKey _cached = Default;
        private static int _nextRefreshTick = int.MinValue;

        public static InputKey Current
        {
            get
            {
                if (_mcmUnavailable) return Default;
                int now = Environment.TickCount;
                // Subtraction comparison survives TickCount wraparound.
                if (now - _nextRefreshTick < 0) return _cached;
                _nextRefreshTick = now + RefreshIntervalMs;
                try
                {
                    _cached = ReadFromSettings();
                }
                catch
                {
                    _mcmUnavailable = true;
                    _cached = Default;
                }
                return _cached;
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

    // Same soft-dependency pattern as DeployHotkey (see that class + the
    // 2026-07-17 MCM decision record): the ONLY route to MCM types is a
    // NoInlining method behind try/catch, so a no-MCM install falls back —
    // here to TRUE, the owner's chosen default (sally-outs allowed).
    internal static class SallyOutToggle
    {
        private const bool Default = true;

        private static bool _mcmUnavailable;

        public static bool AllowSallyOut
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
        private static bool ReadFromSettings()
        {
            var settings = BattleBallistaSettings.Instance;
            return settings != null ? settings.AllowSallyOutDeployment : Default;
        }
    }

    // Same soft-dependency pattern as DeployHotkey / SallyOutToggle: the ONLY route
    // to MCM types is a NoInlining method behind try/catch; a no-MCM install gets
    // the default cap. The value is clamped on read so a hand-edited settings json
    // (or a future MCM quirk) can never produce a zero or absurd cap.
    internal static class DeployCapSetting
    {
        public const int Default = 4;
        private const int Min = 1;
        private const int Max = 10;

        private static bool _mcmUnavailable;

        public static int MaxPerKind
        {
            get
            {
                if (_mcmUnavailable) return Default;
                try
                {
                    int value = ReadFromSettings();
                    if (value < Min) return Min;
                    if (value > Max) return Max;
                    return value;
                }
                catch
                {
                    _mcmUnavailable = true;
                    return Default;
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadFromSettings()
        {
            var settings = BattleBallistaSettings.Instance;
            return settings != null ? settings.MaxEnginesPerKind : Default;
        }
    }
}
