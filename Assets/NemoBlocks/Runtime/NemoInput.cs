using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Respect the project's selected input backend without changing Player settings.
public static class NemoInput
{
    public static bool Down(KeyCode code){
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current!=null&&Enum.TryParse<Key>(code.ToString(),out var key)&&Keyboard.current[key].wasPressedThisFrame;
#else
        return Input.GetKeyDown(code);
#endif
    }
    public static bool Held(KeyCode code){
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current!=null&&Enum.TryParse<Key>(code.ToString(),out var key)&&Keyboard.current[key].isPressed;
#else
        return Input.GetKey(code);
#endif
    }
}
