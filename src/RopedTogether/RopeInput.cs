using System;
using BepInEx.Logging;
using UnityEngine.InputSystem;

namespace RopedTogether;

internal readonly struct RopeInputState
{
    public RopeInputState(bool togglePressed, bool disconnectAllModifier)
    {
        TogglePressed = togglePressed;
        DisconnectAllModifier = disconnectAllModifier;
    }

    public bool TogglePressed { get; }
    public bool DisconnectAllModifier { get; }
}

internal sealed class RopeInput : IDisposable
{
    private readonly ManualLogSource _logger;
    private InputAction _toggleAction;

    public RopeInput(ManualLogSource logger, string keyboardBinding, string gamepadBinding)
    {
        _logger = logger;
        _toggleAction = CreateAction(keyboardBinding, gamepadBinding);
    }

    public RopeInputState Read()
    {
        bool disconnectAll = false;

        Keyboard? keyboard = Keyboard.current;
        if (keyboard != null)
            disconnectAll = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

        Gamepad? gamepad = Gamepad.current;
        if (gamepad != null)
            disconnectAll |= gamepad.leftShoulder.isPressed;

        return new RopeInputState(_toggleAction.WasPressedThisFrame(), disconnectAll);
    }

    public void SetBindings(string keyboardBinding, string gamepadBinding)
    {
        InputAction oldAction = _toggleAction;
        _toggleAction = CreateAction(keyboardBinding, gamepadBinding);
        oldAction.Disable();
        oldAction.Dispose();
    }

    public void Dispose()
    {
        _toggleAction.Disable();
        _toggleAction.Dispose();
    }

    private InputAction CreateAction(string keyboardBinding, string gamepadBinding)
    {
        InputAction action = new("RopedTogether.Toggle", InputActionType.Button, expectedControlType: "Button");
        AddBinding(action, "keyboard/mouse", keyboardBinding);
        AddBinding(action, "gamepad", gamepadBinding);
        action.Enable();
        return action;
    }

    private void AddBinding(InputAction action, string deviceName, string binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
            return;

        try
        {
            action.AddBinding(binding);
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning($"Ignoring invalid {deviceName} rope binding '{binding}': {exception.Message}");
        }
    }
}
