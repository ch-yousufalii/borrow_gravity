using System.IO;
using System.Text;
using UnityEngine;

namespace BorrowGravity.Dev
{
    /// <summary>
    /// Test switches for standalone builds (ignored in the editor and when no switch is given):
    /// -bgquality N   pick a quality level (0 = Mobile, like the phones)
    /// -bgshots DIR   save a screenshot every few seconds (checking a build without a screen grabber)
    /// -bgstatus      log level, cameras and players every few seconds as "[BGSTATUS] ..." (headless checks)
    /// -bgadvance N   host: start each stage, finish it after N seconds and continue (walks through every level)
    /// </summary>
    public class DevShots : MonoBehaviour
    {
        string folder;
        bool status;
        float advanceAfter;
        float interval = 4f, next, nextStatus;
        int count;
        float stageSince = -1f, completedAt = -1f;
        StageManager seen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (Application.isEditor) return;
            var args = System.Environment.GetCommandLineArgs();
            string shots = null;
            bool status = false;
            float advance = 0f;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = i + 1 < args.Length ? args[i + 1] : "";
                if (args[i] == "-bgquality" && int.TryParse(arg, out int q)) QualitySettings.SetQualityLevel(q, true);
                if (args[i] == "-bgshots") shots = arg;
                if (args[i] == "-bgstatus") status = true;
                if (args[i] == "-bgadvance") float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out advance);
            }
            if (string.IsNullOrEmpty(shots) && !status && advance <= 0f) return;
            var go = new GameObject("DevShots");
            DontDestroyOnLoad(go);
            var d = go.AddComponent<DevShots>();
            d.folder = shots;
            d.status = status;
            d.advanceAfter = advance;
            if (!string.IsNullOrEmpty(shots)) Directory.CreateDirectory(shots);
        }

        void Update()
        {
            if (!string.IsNullOrEmpty(folder) && Time.unscaledTime >= next)
            {
                next = Time.unscaledTime + interval;
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"shot_{count++:000}.png"));
            }
            if (status && Time.unscaledTime >= nextStatus)
            {
                nextStatus = Time.unscaledTime + 2f;
                Debug.Log(Status());
            }
            if (advanceAfter > 0f && !NetSession.IsClient) Advance();
        }

        void Advance()
        {
            var sm = StageManager.Instance;
            if (sm == null || !sm.isActiveAndEnabled) { stageSince = -1f; return; }
            if (sm != seen) { seen = sm; stageSince = Time.unscaledTime; completedAt = -1f; }
            float t = Time.unscaledTime - stageSince;
            if (sm.State == StageState.Intro && t > 1.5f) sm.RemoteConfirm();
            else if (sm.State == StageState.Playing && t > advanceAfter) { sm.CompleteStage(); completedAt = Time.unscaledTime; }
            else if (sm.State == StageState.Complete && completedAt > 0f && Time.unscaledTime - completedAt > 2f) { completedAt = -1f; sm.Continue(); }
        }

        static string Status()
        {
            var sb = new StringBuilder("[BGSTATUS] ");
            var gs = GameStages.Instance;
            var sm = StageManager.Instance;
            sb.Append($"net={NetSession.Mode}/{NetSession.Status} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} ");
            sb.Append($"level={(gs != null ? gs.Current : -1)} stage={(sm != null && sm.isActiveAndEnabled ? sm.stageNumber + ":" + sm.State : "-")} ");
            sb.Append($"me={NetSession.LocalPlayer} of {GameFlow.PlayerCount} ");
            for (int p = 0; p < GameFlow.MaxPlayers; p++)
            {
                var pc = Players.Get(p);
                if (pc == null) { sb.Append($"P{p}=- "); continue; }
                var cam = pc.ViewCamera;
                string camInfo = cam == null ? "nocam" : $"{cam.name}:{(cam.isActiveAndEnabled ? "on" : "off")}:x{cam.rect.x:0.0}w{cam.rect.width:0.0}";
                sb.Append($"P{p}={pc.transform.position.x:0.0},{pc.transform.position.y:0.0} cam={camInfo} ");
            }
            sb.Append($"cams={Camera.allCamerasCount}");
            return sb.ToString();
        }
    }
}
