// Config/Core/FrameDriver.cs
//
// PURPOSE: Runs the plugin's once-a-frame work (host config sync role check, ConfigSyncManager.Tick) from its own
//          GameObject, created in Plugin.Awake and kept across scene loads (DontDestroyOnLoad).
//
// WHY (2.11.7, GitLab #13, plan finding FWP-C-06): this used to run from Plugin.Update. APIShared's notes and
//   the Crusader DE Handicap's code report that SHCDE destroys plugin components at startup, so Plugin.Update may
//   never run; no Tweaker log showed either way. This object does not depend on it. Both log one line the first
//   time their Update runs ("[Frame] ..."), which answers the question in the next LogOutput.log.
//   Game-state work never runs here: UnitTransitionDispatcher drains on the game tick (SE OnTick).
//
using UnityEngine;

namespace CrusaderDETweaker.Config.Core
{
    internal sealed class FrameDriver : MonoBehaviour
    {
        private static bool _created;
        private bool _logged;

        /// <summary>Create the driver once (main thread, Plugin.Awake).</summary>
        internal static void Create()
        {
            if (_created) return;
            _created = true;
            var go = new GameObject("CrusaderDETweaker.FrameDriver");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<FrameDriver>();
        }

        private void Update()
        {
            if (!_logged)
            {
                _logged = true;
                Plugin.Logger.LogInfo("[Frame] Frame driver Update is running (host config sync checks run from here).");
            }
            Sync.ConfigSyncManager.Tick();
        }
    }
}
