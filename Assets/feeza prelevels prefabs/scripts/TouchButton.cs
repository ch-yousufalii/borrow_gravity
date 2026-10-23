using UnityEngine;
using UnityEngine.EventSystems;

public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum Action { P1Left, P1Right, P1Jump, P1Power, P2Left, P2Right, P2Jump, P2Power }
    public Action action;

    static bool[] held = new bool[8];
    static bool[] pressed = new bool[8];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { held = new bool[8]; pressed = new bool[8]; }

    public static bool Held(Action a) { return held[(int)a]; }

    // Ek press ek hi baar milta hai
    public static bool Consume(Action a)
    {
        bool p = pressed[(int)a];
        pressed[(int)a] = false;
        return p;
    }

    public void OnPointerDown(PointerEventData e) { held[(int)action] = true; pressed[(int)action] = true; }
    public void OnPointerUp(PointerEventData e) { held[(int)action] = false; }
    public void OnPointerExit(PointerEventData e) { held[(int)action] = false; }
}