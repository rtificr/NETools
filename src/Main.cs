using ModLoader;

namespace NETools
{
    /// <summary>
    ///     Main class of the mod.
    ///
    ///     LOCAL FORK NOTE: upstream (cucumber-sp/NETools 1.1.6) crashes on this game build with a
    ///     NullReferenceException in MonoMod.RuntimeDetour.ILHook (DetourHelper.GetIdentifiable),
    ///     thrown from Harmony.PatchAll() -&gt; the two [HarmonyPatch] hooks in Patches.cs that detour
    ///     BasicMenu.OnOpen/OnClose to support ConfigurationMenu's settings-page injection. Every
    ///     other NETools feature (ClosableWindow, NumberInput, PositionSaver, NEToolsBuilder) is
    ///     plain C# on top of the game's own SFS.UI.ModGUI and needs no Harmony patching at all, so
    ///     this fork simply never calls PatchAll(). ConfigurationMenu.Initialize() is still safe to
    ///     call - it just subscribes to GameHook events that will now never fire, so the settings-page
    ///     integration silently does nothing instead of crashing the whole mod.
    ///
    ///     The auto-updater is also disabled here (IUpdatable removed, ModsUpdater never invoked) so
    ///     it can't silently redownload the broken upstream DLL over this fix.
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Main : Mod
    {
        internal static Main main;

        /// <summary>Default constructor</summary>
        public Main()
        {
            main = this;
        }

        /// <summary>NameID</summary>
        public override string ModNameID => "NETools";

        /// <summary>DisplayName</summary>
        public override string DisplayName => "NETools";

        /// <summary>Author</summary>
        public override string Author => "Jay Churchill";

        /// <summary>MinimumGameVersionNecessary</summary>
        public override string MinimumGameVersionNecessary => "1.5.10.2";

        /// <summary>ModVersion</summary>
        public override string ModVersion => "1.0.0";

        /// <summary>Description</summary>
        public override string Description =>
            "Mod that provides advanced UI functionality and used by other mods as dependency. " +
            "Local fork: the Harmony/MonoMod settings-menu patch that crashes on this game build " +
            "has been removed; everything else works normally.";

        /// <summary>Early Load</summary>
        public override void Early_Load()
        {
            ConfigurationMenu.Initialize();
            PositionSaver.Initialize();
        }
    }
}
