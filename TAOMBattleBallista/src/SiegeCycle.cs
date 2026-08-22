namespace TAOMBattleBallista
{
    public enum SiegeKind
    {
        Ballista = 0,
        Catapult = 1,
        FireCatapult = 2,
        Trebuchet = 3,
    }

    // Session-level state for the deploy hotkey's spawn target. Not persisted
    // — resets to Ballista when the game restarts. Static so ROT_AI's MapBar
    // cycle button can flip it via reflection without a hard dependency on
    // this DLL.
    public static class SiegeCycle
    {
        private static SiegeKind _current = SiegeKind.Ballista;

        public static SiegeKind Current => _current;

        // ROT_AI's mixin invokes this via reflection. Returns the new label so
        // the caller can refresh button text without a separate read.
        public static string Next()
        {
            _current = (SiegeKind)(((int)_current + 1) % 4);
            return CurrentLabel;
        }

        public static string CurrentLabel
        {
            get
            {
                switch (_current)
                {
                    case SiegeKind.Ballista: return "Ballista";
                    case SiegeKind.Catapult: return "Catapult";
                    case SiegeKind.FireCatapult: return "Fire Catapult";
                    case SiegeKind.Trebuchet: return "Trebuchet";
                    default: return "Ballista";
                }
            }
        }

        // Each direct prefab is a composite that already bundles whatever ammo
        // the engine needs — confirmed in-game: mangonel_a / mangonel_a_fire /
        // trebuchet_a each instantiate WITH a paired usable pile (stone pile
        // for boulder engines, pot pile for fire mangonel). Ballista carries
        // its bolts on the engine itself. So one press = one full deployment;
        // no separate AmmoPrefab field, no alternation needed.
        public static string EnginePrefab
        {
            get
            {
                switch (_current)
                {
                    case SiegeKind.Ballista: return "ballista_a";
                    case SiegeKind.Catapult: return "mangonel_a";
                    case SiegeKind.FireCatapult: return "mangonel_a_fire";
                    case SiegeKind.Trebuchet: return "trebuchet_a";
                    default: return "ballista_a";
                }
            }
        }
    }
}
