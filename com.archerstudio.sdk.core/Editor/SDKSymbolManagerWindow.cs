using UnityEditor;

namespace ArcherStudio.SDK.Core.Editor {

    /// <summary>
    /// Redirects the old Symbol Manager menu item to the Symbols tab in Setup Wizard.
    /// </summary>
    public static class SDKSymbolManagerRedirect {

        [MenuItem("ArcherStudio/SDK/Symbol Manager", false, 20)]
        public static void ShowWindow() {
            SDKSetupWizard.ShowTab(3); // Symbols tab
        }
    }
}
