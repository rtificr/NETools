using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NETools
{
    /// <summary>
    /// Observes the settings menu without detouring BasicMenu methods. Harmony detours crash on
    /// some supported game builds; polling the named menu object keeps this integration passive.
    /// </summary>
    internal sealed class GameHook : MonoBehaviour
    {
        internal static readonly GameObject_Local settingsMenu = new() { Value = null };
        internal static event Action OnSettingsMenuClosed;
        internal static event Action OnSettingsMenuOpened;
        private static GameHook driver;

        internal static void Initialize()
        {
            if (driver != null) return;
            var host = new GameObject("NETools Settings Menu Observer");
            Object.DontDestroyOnLoad(host);
            driver = host.AddComponent<GameHook>();
        }

        private void Update()
        {
            var activeMenu = GameObject.Find("Settings Menu");
            if (activeMenu != null)
            {
                if (settingsMenu.Value != activeMenu)
                {
                    settingsMenu.Value = activeMenu;
                    OnSettingsMenuOpened?.Invoke();
                }
                return;
            }

            if (settingsMenu.Value == null) return;
            settingsMenu.Value = null;
            OnSettingsMenuClosed?.Invoke();
        }

        private void OnDestroy()
        {
            if (driver != this) return;
            driver = null;
            if (settingsMenu.Value == null) return;
            settingsMenu.Value = null;
            OnSettingsMenuClosed?.Invoke();
        }
    }
}
