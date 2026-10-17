using UnityEngine;
using UnityEngine.InputSystem;

public class RoomGravityPower : MonoBehaviour
{
    void Update()
    {
        var kb = Keyboard.current;
        bool pressed = (kb != null && kb.qKey.wasPressedThisFrame)
                       || TouchButton.Consume(TouchButton.Action.P1Power);
        if (pressed)
            GravityObject.RoomGravityOn = !GravityObject.RoomGravityOn;
    }
}