using System.Collections.Generic;
using ModLoader;
using SFS.IO;

namespace NETools
{
    /// <summary>
    ///     Main class of the mod.
    ///
    ///     Settings-menu integration observes the active settings window instead of detouring
    ///     BasicMenu methods, avoiding the Harmony/MonoMod crash seen on some game builds.
    ///
    ///     Auto-updates follow the original UITools opt-in IUpdatable flow and are only installed
    ///     after the user confirms the update prompt.
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Main : Mod, IUpdatable
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
        public override string ModVersion => "1.0.2";

        public Dictionary<string, FilePath> UpdatableFiles => new()
        {
            {
                "https://github.com/rtificr/NETools/releases/latest/download/NETools.dll",
                new FolderPath(ModFolder).ExtendToFile("NETools.dll")
            }
        };

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

        /// <summary>Load</summary>
        public override void Load()
        {
            ModsUpdater.StartUpdate();
        }
    }
}
