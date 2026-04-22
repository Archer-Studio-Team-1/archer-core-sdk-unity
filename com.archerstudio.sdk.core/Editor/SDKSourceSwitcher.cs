using UnityEditor;

namespace ArcherStudio.SDK.Core.Editor {

    /// <summary>
    /// Redirects the old Source Switcher menu item to the Sources tab in Setup Wizard.
    /// </summary>
    public static class SDKSourceSwitcherRedirect {

        [MenuItem("ArcherStudio/SDK/Source Switcher", false, 10)]
        public static void ShowWindow() {
            SDKSetupWizard.ShowTab(3); // Sources tab
        }
    }
}
