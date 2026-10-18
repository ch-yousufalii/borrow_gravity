using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BorrowGravity.Dev
{
    /// <summary>
    /// Dev-only remote control for play-testing from the editor (e.g. via MCP execute_code): holds/taps keys on a
    /// virtual keyboard (real keyboards are ignored while it runs) and reports the game state.
    /// </summary>
    public class PlaytestBot : MonoBehaviour
    {
        static PlaytestBot instance;
        readonly HashSet<Key> held = new HashSet<Key>();
        readonly List<Keyboard> disabled = new List<Keyboard>();
        Keyboard kb;

        public static PlaytestBot Ensure()
        {
            if (instance != null) return instance;
            if (!Application.isPlaying) throw new System.InvalidOperationException("Not in Play mode");
            var go = new GameObject("PlaytestBot");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PlaytestBot>();
            // edit the live settings in place (never swap in a temporary copy: the Input System keeps a reference
            // across play-mode reloads and a destroyed copy breaks its initialisation) and restore them afterwards
            var settings = InputSystem.settings;
            instance.oldBackground = settings.backgroundBehavior;
            instance.oldEditorMode = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            instance.kb = InputSystem.AddDevice<Keyboard>("BotKeyboard");
            foreach (var d in InputSystem.devices)
                if (d is Keyboard other && other != instance.kb && other.enabled) { InputSystem.DisableDevice(other); instance.disabled.Add(other); }
            return instance;
        }

        InputSettings.BackgroundBehavior oldBackground;
        InputSettings.EditorInputBehaviorInPlayMode oldEditorMode;

        void OnDestroy()
        {
            var settings = InputSystem.settings;
            if (settings != null)
            {
                settings.backgroundBehavior = oldBackground;
                settings.editorInputBehaviorInPlayMode = oldEditorMode;
            }
            foreach (var d in disabled) if (d.added) InputSystem.EnableDevice(d);
            if (kb != null && kb.added) InputSystem.RemoveDevice(kb);
        }

        static Key Parse(string k) => (Key)System.Enum.Parse(typeof(Key), k.Trim(), true);

        void Apply() => InputSystem.QueueStateEvent(kb, new KeyboardState(held.ToArray()));

        /// <summary>Hold exactly these keys (comma separated, e.g. "D,RightArrow"); empty releases all.</summary>
        public static string Hold(string keys)
        {
            var b = Ensure();
            b.held.Clear();
            if (!string.IsNullOrWhiteSpace(keys)) foreach (var k in keys.Split(',')) b.held.Add(Parse(k));
            b.Apply();
            return "holding " + keys;
        }

        /// <summary>Press a key for a few frames on top of whatever is held.</summary>
        public static string Tap(string key, float seconds = 0.05f)
        {
            var b = Ensure();
            b.StartCoroutine(b.TapRoutine(Parse(key), seconds));
            return "tap " + key;
        }

        IEnumerator TapRoutine(Key k, float seconds)
        {
            held.Add(k);
            Apply();
            yield return null;
            yield return new WaitForSeconds(seconds);
            held.Remove(k);
            Apply();
        }

        /// <summary>Walk a player to x, jumping when passing each x in jumps ("12.3;15"). Replaces any walk already running for that player.</summary>
        public static string WalkTo(int player, float x, string jumps = "")
        {
            var b = Ensure();
            if (b.walks.TryGetValue(player, out var running) && running != null) b.StopCoroutine(running);
            var list = (jumps ?? "").Split(';').Where(j => j.Trim().Length > 0).Select(j => float.Parse(j, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            b.walks[player] = b.StartCoroutine(b.WalkRoutine(player, x, list));
            return $"walk P{player} -> {x} jumps [{jumps}]";
        }

        readonly Dictionary<int, Coroutine> walks = new Dictionary<int, Coroutine>();

        IEnumerator WalkRoutine(int index, float x, List<float> jumps)
        {
            var p = Players.Get(index);
            if (p == null) yield break;
            // third-person camera faces along the level (+X): forward/back walk it
            Key right = p.keys.up, left = p.keys.down, jump = p.keys.jump;
            float t = 0f;
            while (t < 15f && Mathf.Abs(p.transform.position.x - x) > 0.15f)
            {
                float px = p.transform.position.x;
                bool goRight = px < x;
                held.Remove(right); held.Remove(left);
                // let go a little early so momentum carries us to the target, not past it
                if (Mathf.Abs(px - x) > 0.3f) held.Add(goRight ? right : left);
                for (int i = 0; i < jumps.Count; i++)
                {
                    if (goRight ? px >= jumps[i] : px <= jumps[i])
                    {
                        jumps.RemoveAt(i);
                        held.Add(jump);
                        Apply();
                        yield return null;
                        yield return null;
                        held.Remove(jump);
                        break;
                    }
                }
                Apply();
                t += Time.deltaTime;
                yield return null;
            }
            held.Remove(right); held.Remove(left);
            Apply();
        }

        /// <summary>B: nullify the current target (if needed) and steer it until it reaches x (and z, if given), then let go of the key.</summary>
        public static string SteerTo(float x, float z = float.NaN)
        {
            var b = Ensure();
            b.StartCoroutine(b.SteerRoutine(x, z));
            return $"steer -> {x},{z}";
        }

        IEnumerator SteerRoutine(float x, float z)
        {
            var np = Object.FindFirstObjectByType<NullifyPower>();
            var p = np.GetComponent<PlayerController>();
            held.Add(p.keys.power);
            Apply();
            float t = 0f;
            while (np.Active == null && t < 1f) { t += Time.deltaTime; yield return null; }
            if (np.Active == null) { held.Remove(p.keys.power); Apply(); yield break; }
            var target = np.Active;
            t = 0f;
            while (t < 8f && np.Active == target)
            {
                Vector3 pos = target.transform.position;
                float dx = x - pos.x, dz = float.IsNaN(z) ? 0f : z - pos.z;
                held.Remove(p.keys.left); held.Remove(p.keys.right); held.Remove(p.keys.up); held.Remove(p.keys.down);
                if (Mathf.Abs(dx) < 0.08f && Mathf.Abs(dz) < 0.08f) break;
                if (Mathf.Abs(dx) >= 0.08f) held.Add(dx > 0 ? p.keys.right : p.keys.left);
                if (Mathf.Abs(dz) >= 0.08f) held.Add(dz > 0 ? p.keys.up : p.keys.down);
                Apply();
                t += Time.deltaTime;
                yield return null;
            }
            held.Remove(p.keys.left); held.Remove(p.keys.right); held.Remove(p.keys.up); held.Remove(p.keys.down);
            held.Remove(p.keys.power);
            Apply();
        }

        public static string SkipIntro()
        {
            Ensure();
            var sm = StageManager.Instance;
            if (sm != null && sm.State == StageState.Intro) Tap("Space");
            return sm != null ? sm.State.ToString() : "no stage";
        }

        public static string Teleport(int player, float x, float y, float z)
        {
            var p = Players.Get(player);
            if (p == null) return "no player";
            p.TeleportTo(new Vector3(x, y, z));
            return $"P{player} -> {x},{y},{z}";
        }

        static string V(Vector3 v) => $"({v.x:0.00},{v.y:0.00},{v.z:0.00})";

        public static string Status(string extraNames = "")
        {
            var sb = new StringBuilder();
            var sm = StageManager.Instance;
            sb.Append($"t={Time.time:0.0} state={(sm != null ? sm.State.ToString() : "-")} cp={(Checkpoint.Active != null ? Checkpoint.Active.label : "-")} deaths={(sm != null ? sm.Deaths : 0)}\n");
            foreach (var p in Players.All.OrderBy(p => p.playerIndex))
                sb.Append($"P{p.playerIndex} {V(p.transform.position)} up={V(p.Up)} grounded={p.IsGrounded} ground={(p.GroundCollider ? p.GroundCollider.name : "-")}\n");
            var gp = Object.FindFirstObjectByType<GravityPower>();
            if (gp != null) sb.Append($"A target zone={(gp.Target ? gp.Target.zoneName + ":" + gp.Target.Mode : "-")}\n");
            var np = Object.FindFirstObjectByType<NullifyPower>();
            if (np != null) sb.Append($"B target={(np.Target ? np.Target.displayName : "-")} active={(np.Active ? np.Active.displayName + " " + np.Active.Remaining.ToString("0.0") : "-")} steering={np.Steering} cd={np.CooldownRemaining:0.0}\n");
            if (EngineCarrier.Instance != null)
                sb.Append($"Carrier {V(EngineCarrier.Instance.transform.position)} stab={EngineCarrier.Instance.Stability:0} tilt={EngineCarrier.Instance.Tilt:0}\n");
            foreach (var n in (extraNames ?? "").Split(',').Where(s => s.Trim().Length > 0))
            {
                var go = GameObject.Find(n.Trim());
                sb.Append(go != null ? $"{n.Trim()} {V(go.transform.position)}\n" : $"{n.Trim()} missing\n");
            }
            return sb.ToString();
        }
    }
}
