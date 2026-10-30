using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Editor;
using UnityEngine.InputSystem.LowLevel;

// Copied into a disposable Editor project by verify-inputs.py, never into the game.
public static class VerifyBindings
{
    private static readonly List<string> Passed = new List<string>();
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Passed.Add(description);
    }
    private static void Axis(float actual, float expected, string label)
    {
        Check(Mathf.Abs(actual - expected) < 0.001f, label + " = " + expected + " (actual " + actual + ")");
    }
    private static readonly HashSet<Key> PressedKeys = new HashSet<Key>();
    private static readonly Dictionary<Gamepad, GamepadState> PadStates = new Dictionary<Gamepad, GamepadState>();
    private static void SetKey(Keyboard keyboard, Key key, bool down)
    {
        if (down) PressedKeys.Add(key); else PressedKeys.Remove(key);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(PressedKeys.ToArray()));
        InputSystem.Update();
    }
    private static void Pad(Gamepad pad, string control, float value)
    {
        PadStates.TryGetValue(pad, out var state);
        if (control == "rightTrigger") state.rightTrigger = value;
        else if (control == "leftTrigger") state.leftTrigger = value;
        else
        {
            var button = new Dictionary<string, GamepadButton> {
                {"buttonSouth", GamepadButton.South}, {"buttonEast", GamepadButton.East},
                {"buttonWest", GamepadButton.West}, {"buttonNorth", GamepadButton.North},
                {"rightShoulder", GamepadButton.RightShoulder}, {"leftStickPress", GamepadButton.LeftStick},
                {"start", GamepadButton.Start}
            }[control];
            state = state.WithButton(button, value > 0);
        }
        PadStates[pad] = state;
        InputSystem.QueueStateEvent(pad, state);
        InputSystem.Update();
    }
    [InitializeOnLoadMethod]
    private static void ResumePendingRun()
    {
        if (SessionState.GetBool("VerifyBindings.pending", false))
            EditorApplication.playModeStateChanged += OnPlayMode;
    }
    public static void Run()
    {
        SessionState.SetBool("VerifyBindings.pending", true);
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.EnterPlaymode();
    }
    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("VerifyBindings.pending", false)) return;
        SessionState.SetBool("VerifyBindings.pending", false);
        EditorApplication.playModeStateChanged -= OnPlayMode;
        VerifyInPlayMode();
    }
    private static void VerifyInPlayMode()
    {
        int exit = 0;
        GameObject host = null;
        TumblerInput input = null;
        try
        {
            Application.runInBackground = true;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            foreach (var device in InputSystem.devices.ToArray()) InputSystem.RemoveDevice(device);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var xbox = (Gamepad)InputSystem.AddDevice("XInputController");
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Project/Input/TumblerInput.inputactions");
            Check(asset != null, "Input asset imports");
            Check(InputSystem.version.ToString() == "1.8.2", "Exact Input System 1.8.2");
            string code = InputActionCodeGenerator.GenerateWrapperCode(asset,
                new InputActionCodeGenerator.Options { className = "TumblerInput", sourceAssetPath = "Assets/_Project/Input/TumblerInput.inputactions" });
            Check(File.ReadAllText("Assets/_Project/Input/Input.cs").Replace("\r\n", "\n") == code.Replace("\r\n", "\n"), "Compiled wrapper matches official generator output");
            input = new TumblerInput();
            input.asset.name = asset.name;
            Check(input.asset.ToJson() == asset.ToJson(), "Generated wrapper and input asset agree");
            input.Enable();
            var actions = input.Gameplay;
            foreach (var key in new[] { Key.W, Key.UpArrow })
            {
                SetKey(keyboard, key, true); Axis(actions.Throttle.ReadValue<float>(), 1, key + " accelerates");
                SetKey(keyboard, key, false); Axis(actions.Throttle.ReadValue<float>(), 0, key + " releases throttle");
            }
            foreach (var key in new[] { Key.S, Key.DownArrow })
            {
                SetKey(keyboard, key, true); Axis(actions.Throttle.ReadValue<float>(), -1, key + " reverses");
                SetKey(keyboard, key, false); Axis(actions.Throttle.ReadValue<float>(), 0, key + " releases reverse");
            }
            SetKey(keyboard, Key.W, true); SetKey(keyboard, Key.DownArrow, true);
            Axis(actions.Throttle.ReadValue<float>(), 0, "Opposing throttle keys cancel");
            SetKey(keyboard, Key.W, false); SetKey(keyboard, Key.DownArrow, false);
            foreach (var key in new[] { Key.A, Key.LeftArrow, Key.D, Key.RightArrow })
            {
                SetKey(keyboard, key, true);
                var steer = actions.Steer.ReadValue<Vector2>();
                Axis(steer.x, key == Key.A || key == Key.LeftArrow ? -1 : 1, key + " steers");
                Axis(steer.y, 0, key + " has horizontal-only steering");
                SetKey(keyboard, key, false);
            }
            SetKey(keyboard, Key.A, true); SetKey(keyboard, Key.RightArrow, true);
            Axis(actions.Steer.ReadValue<Vector2>().x, 0, "Opposing steering keys cancel");
            SetKey(keyboard, Key.A, false); SetKey(keyboard, Key.RightArrow, false);
            SetKey(keyboard, Key.W, true); SetKey(keyboard, Key.D, true);
            Axis(actions.Throttle.ReadValue<float>(), 1, "Throttle while steering");
            Axis(actions.Steer.ReadValue<Vector2>().x, 1, "Steering while throttling");
            SetKey(keyboard, Key.W, false); SetKey(keyboard, Key.D, false);
            foreach (var pair in new[] { (Key.LeftShift, "Boost"), (Key.LeftCtrl, "Brake"), (Key.Space, "Handbrake") })
            {
                SetKey(keyboard, pair.Item1, true); Axis(input.asset.FindAction(pair.Item2).ReadValue<float>(), 1, pair.Item2 + " keyboard press");
                SetKey(keyboard, pair.Item1, false); Axis(input.asset.FindAction(pair.Item2).ReadValue<float>(), 0, pair.Item2 + " keyboard release");
            }
            Pad(xbox, "rightTrigger", 0.6f); Axis(actions.Throttle.ReadValue<float>(), 0.6f, "Xbox RT remains analog");
            Pad(xbox, "rightTrigger", 0); Pad(xbox, "leftTrigger", 0.4f); Axis(actions.Throttle.ReadValue<float>(), -0.4f, "Xbox LT remains analog");
            Pad(xbox, "leftTrigger", 0);
            InputSystem.QueueDeltaStateEvent(xbox.leftStick, new Vector2(0.5f, 0)); InputSystem.Update();
            Check(actions.Steer.ReadValue<Vector2>().x > 0 && actions.Steer.ReadValue<Vector2>().x < 1, "Xbox stick remains analog");
            InputSystem.QueueDeltaStateEvent(xbox.leftStick, Vector2.zero); InputSystem.Update();
            foreach (var pair in new[] { ("buttonSouth", "Boost"), ("buttonEast", "Brake"), ("buttonWest", "Handbrake") })
            {
                Pad(xbox, pair.Item1, 1); Axis(input.asset.FindAction(pair.Item2).ReadValue<float>(), 1, pair.Item2 + " Xbox press");
                Pad(xbox, pair.Item1, 0); Axis(input.asset.FindAction(pair.Item2).ReadValue<float>(), 0, pair.Item2 + " Xbox release");
            }
            // Exercise the single PlayerInput UnityEvent route used by the scene.
            host = new GameObject("Single player input verification"); host.SetActive(false);
            var player = host.AddComponent<PlayerInput>();
            player.actions = asset; player.defaultActionMap = "Gameplay";
            player.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
            var calls = new Dictionary<string, int>(); var events = new List<PlayerInput.ActionEvent>();
            foreach (var name in new[] { "FlipCar", "ResetCar", "SwitchDriveMode", "QuitGame" })
            {
                calls[name] = 0; string capture = name;
                var ev = new PlayerInput.ActionEvent(asset.FindAction(name));
                ev.AddListener(context => { if (context.started) calls[capture]++; }); events.Add(ev);
            }
            player.actionEvents = events.ToArray(); host.SetActive(true);
            Check(player.currentActionMap != null, "PlayerInput enables Gameplay map");
            foreach (var pair in new[] { (Key.F, "FlipCar"), (Key.R, "ResetCar"), (Key.E, "SwitchDriveMode"), (Key.Escape, "QuitGame") })
            {
                int before = calls[pair.Item2]; SetKey(keyboard, pair.Item1, true); SetKey(keyboard, pair.Item1, false);
                Check(calls[pair.Item2] == before + 1, pair.Item2 + " keyboard callback fires once");
            }
            foreach (var pair in new[] { ("buttonNorth", "FlipCar"), ("leftStickPress", "ResetCar"), ("rightShoulder", "SwitchDriveMode"), ("start", "QuitGame") })
            {
                int before = calls[pair.Item2]; Pad(xbox, pair.Item1, 1); Pad(xbox, pair.Item1, 0);
                Check(calls[pair.Item2] == before + 1, pair.Item2 + " Xbox callback fires once");
            }
            InputSystem.RemoveDevice(xbox);
            int resetBefore = calls["ResetCar"]; SetKey(keyboard, Key.R, true); SetKey(keyboard, Key.R, false);
            Check(calls["ResetCar"] == resetBefore + 1, "Keyboard works after Xbox disconnect");
            xbox = (Gamepad)InputSystem.AddDevice("XInputController"); InputSystem.Update();
            int modeBefore = calls["SwitchDriveMode"]; Pad(xbox, "rightShoulder", 1); Pad(xbox, "rightShoulder", 0);
            Check(calls["SwitchDriveMode"] == modeBefore + 1, "Xbox callback works after reconnect");
            Check(PlayerInput.all.Count == 1, "One PlayerInput, no competing device owners");
            input.Disable(); host.SetActive(false); var oldCount = calls["FlipCar"];
            SetKey(keyboard, Key.F, true); SetKey(keyboard, Key.F, false);
            Check(calls["FlipCar"] == oldCount, "Disabled input sends no callbacks");
            input.Enable(); host.SetActive(true); SetKey(keyboard, Key.F, true); SetKey(keyboard, Key.F, false);
            Check(calls["FlipCar"] == oldCount + 1, "Reenabled input sends one callback");
            var report = "{\n  \"unity_editor\": \"" + Application.unityVersion + "\",\n  \"input_system\": \"" + InputSystem.version + "\",\n  \"assertions_passed\": " + Passed.Count + ",\n  \"checks\": [\n" + string.Join(",\n", Passed.Select(x => "    \"" + x + "\"")) + "\n  ],\n  \"hardware_test\": \"NOT RUN: virtual keyboard and Xbox devices\",\n  \"full_game_test\": \"NOT RUN: isolated input harness only\"\n}\n";
            File.WriteAllText("input-results.json", report);
            Debug.Log("INPUT VERIFICATION PASS: " + Passed.Count + " assertions");
        }
        catch (Exception ex) { Debug.LogException(ex); exit = 1; }
        finally
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (input != null) { input.Disable(); UnityEngine.Object.DestroyImmediate(input.asset); }
            EditorApplication.Exit(exit);
        }
    }
}
