using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 统一处理"点击/触摸屏幕"以及"是否点在 UI 上"。
/// 同时兼容旧 Input Manager 与新 Input System（项目里两项都开启时走旧输入，避免版本差异）。
/// </summary>
public static class TapInput
{
    public static void EnableTouchSupport()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
#endif
    }

    /// <summary>本帧是否新产生一次点击，并返回屏幕坐标与 pointerId（鼠标为 -1）。</summary>
    public static bool TryGetTap(out Vector2 screenPosition, out int pointerId)
    {
        screenPosition = Vector2.zero;
        pointerId = -1;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began)
            {
                return false;
            }
            screenPosition = touch.position;
            pointerId = touch.fingerId;
            return true;
        }

        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            pointerId = -1;
            return true;
        }
#elif ENABLE_INPUT_SYSTEM
        var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        if (activeTouches.Count > 0)
        {
            var touch = activeTouches[0];
            if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began)
            {
                return false;
            }
            screenPosition = touch.screenPosition;
            pointerId = touch.touchId;
            return true;
        }

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            pointerId = -1;
            return true;
        }
#endif
        return false;
    }

    /// <summary>点击是否落在 uGUI 元素（按钮、输入框等）上，用于阻止误放标记。</summary>
    public static bool IsPointerOverUI(int pointerId)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }
        return eventSystem.IsPointerOverGameObject(pointerId);
    }
}
