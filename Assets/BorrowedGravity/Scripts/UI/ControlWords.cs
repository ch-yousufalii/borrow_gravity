using System.Text.RegularExpressions;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// What to call the controls on this device: keys on a PC, the on-screen buttons on a phone. The levels' text is
    /// written with PC keys; phones translate it when it's shown, so one device's words never reach another's screen.
    /// </summary>
    public static class ControlWords
    {
        /// <summary>Show phone wording on another platform (for checking it in the editor).</summary>
        public static bool ForceTouch;

        public static bool Touch => Application.isMobilePlatform || ForceTouch;

        /// <summary>A line of a player's power panel (made on the host, which may be the other kind of device).</summary>
        public static string PanelLine(string line)
        {
            if (!Touch || string.IsNullOrEmpty(line)) return line;
            if (line.StartsWith("E  ")) return line.Substring(3);
            if (line.StartsWith("Q  ")) return "FREEZE  " + line.Substring(3);
            if (line.StartsWith("E · switch gravity")) return "GRAVITY button: on / off";
            if (line == "E · nullify") return "FREEZE button";
            return line.Replace("Hold POWER", "Hold FREEZE");
        }

        /// <summary>An instruction board's text, with the keys swapped for the on-screen buttons.</summary>
        public static string Board(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = Regex.Replace(text, @"\b(?:press|tap)\s+E\b", "tap GRAVITY");
            text = Regex.Replace(text, @"\bRight\s+Shift\b", "FREEZE");
            text = Regex.Replace(text, @"\bE\b", "GRAVITY");
            text = Regex.Replace(text, @"\bQ\b", "FREEZE");
            text = Regex.Replace(text, @"\bSpace\b", "JUMP");
            // solo boards put the button in front of the step: "[GRAVITY] tap GRAVITY to..." says it twice
            text = Regex.Replace(text, @"\[(GRAVITY|FREEZE)\]\s+(tap|hold|HOLD)\s+\1", "$2 $1");
            return text;
        }
    }
}
