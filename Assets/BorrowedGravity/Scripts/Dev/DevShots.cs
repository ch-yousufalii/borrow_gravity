using System.IO;
using UnityEngine;

namespace BorrowGravity.Dev
{
    /// <summary>
    /// Test switches for standalone builds: -bgquality N picks a quality level (0 = Mobile, like the phones),
    /// -bgshots &lt;folder&gt; saves a screenshot every few seconds (for checking a build without a screen grabber).
    /// </summary>
    public class DevShots : MonoBehaviour
    {
        string folder;
        float interval = 4f, next;
        int count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (Application.isEditor) return;
            var args = System.Environment.GetCommandLineArgs();
            string shots = null;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-bgquality" && int.TryParse(args[i + 1], out int q)) QualitySettings.SetQualityLevel(q, true);
                if (args[i] == "-bgshots") shots = args[i + 1];
            }
            if (string.IsNullOrEmpty(shots)) return;
            var go = new GameObject("DevShots");
            DontDestroyOnLoad(go);
            var d = go.AddComponent<DevShots>();
            d.folder = shots;
            Directory.CreateDirectory(shots);
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + interval;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"shot_{count++:000}.png"));
        }
    }
}
