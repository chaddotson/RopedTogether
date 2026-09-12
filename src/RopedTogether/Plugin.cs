using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace RopedTogether;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInIncompatibility("PeakRopes")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.openai.RopedTogether";
    public const string PluginName = "RopedTogether";
    public const string PluginVersion = "1.0.0";

    internal static ManualLogSource Log = null!;

    internal static ConfigEntry<string> KeyboardToggle = null!;
    internal static ConfigEntry<string> GamepadToggle = null!;
    internal static ConfigEntry<float> ConnectDistance = null!;
    internal static ConfigEntry<float> RopeLength = null!;
    internal static ConfigEntry<float> SpringAcceleration = null!;
    internal static ConfigEntry<float> Damping = null!;
    internal static ConfigEntry<float> MaxPerLinkAcceleration = null!;
    internal static ConfigEntry<float> MaxTotalAcceleration = null!;
    internal static ConfigEntry<float> FallBoost = null!;
    internal static ConfigEntry<float> RescueUpAcceleration = null!;
    internal static ConfigEntry<float> BelayerCounterPull = null!;
    internal static ConfigEntry<float> RopeWidth = null!;
    internal static ConfigEntry<bool> ShowStatus = null!;

    internal static Material RopeMaterial = null!;
    internal static Material ConnectorMaterial = null!;
    internal static readonly Color RopeColor = new(0.86f, 0.57f, 0.18f, 1f);

    private Harmony _harmony = null!;
    private bool _wasInRoom;

    private void Awake()
    {
        Log = Logger;

        KeyboardToggle = Config.Bind(
            "Controls",
            "Keyboard Toggle",
            "<Keyboard>/f",
            "Press to clip/unclip the climber you are looking at. Hold Shift while pressing to disconnect all your ropes.");

        GamepadToggle = Config.Bind(
            "Controls",
            "Gamepad Toggle",
            "<Gamepad>/rightShoulder",
            "Gamepad toggle. Hold left shoulder while pressing to disconnect all ropes.");

        ConnectDistance = Config.Bind(
            "Rope",
            "Connect Distance",
            4f,
            new ConfigDescription("Maximum distance for creating a new rope link.", new AcceptableValueRange<float>(1f, 10f)));

        RopeLength = Config.Bind(
            "Rope",
            "Length",
            6f,
            new ConfigDescription("Free rope length before tension begins.", new AcceptableValueRange<float>(2f, 15f)));

        SpringAcceleration = Config.Bind(
            "Rope Physics",
            "Spring Acceleration",
            18f,
            new ConfigDescription("Pull acceleration per meter of stretch.", new AcceptableValueRange<float>(1f, 60f)));

        Damping = Config.Bind(
            "Rope Physics",
            "Damping",
            7f,
            new ConfigDescription("Extra arrest force when climbers are separating quickly.", new AcceptableValueRange<float>(0f, 30f)));

        MaxPerLinkAcceleration = Config.Bind(
            "Rope Physics",
            "Max Per Link Acceleration",
            48f,
            new ConfigDescription("Maximum acceleration contributed by a single rope.", new AcceptableValueRange<float>(5f, 100f)));

        MaxTotalAcceleration = Config.Bind(
            "Rope Physics",
            "Max Total Acceleration",
            70f,
            new ConfigDescription("Maximum combined rope acceleration, preventing multi-rope launches.", new AcceptableValueRange<float>(10f, 140f)));

        FallBoost = Config.Bind(
            "Belay Assist",
            "Fall Boost",
            1.8f,
            new ConfigDescription("Multiplier when the local climber is falling and a rope partner is above them.", new AcceptableValueRange<float>(1f, 4f)));

        RescueUpAcceleration = Config.Bind(
            "Belay Assist",
            "Rescue Up Acceleration",
            16f,
            new ConfigDescription("Additional upward arrest acceleration during a fall with an uphill rope partner.", new AcceptableValueRange<float>(0f, 50f)));

        BelayerCounterPull = Config.Bind(
            "Belay Assist",
            "Belayer Counter Pull",
            0.3f,
            new ConfigDescription("How much downward counter-pull a stable belayer receives from a falling partner.", new AcceptableValueRange<float>(0f, 1f)));

        RopeWidth = Config.Bind(
            "Visuals",
            "Rope Width",
            0.045f,
            new ConfigDescription("Visual rope width only; it has no collider or stamina/weight effect.", new AcceptableValueRange<float>(0.015f, 0.12f)));

        ShowStatus = Config.Bind("Visuals", "Show Status", true, "Show a small connection hint and current rope count.");

        RopeMaterial = CreateMaterial(RopeColor);
        ConnectorMaterial = CreateMaterial(new Color(0.8f, 0.82f, 0.85f, 1f));

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        _wasInRoom = PhotonNetwork.InRoom;
        Logger.LogInfo("RopedTogether loaded. F toggles a rope; Shift+F disconnects all (defaults are configurable).");
    }

    private void Update()
    {
        bool inRoom = PhotonNetwork.InRoom;
        if (_wasInRoom && !inRoom)
            RopeState.Clear();
        _wasInRoom = inRoom;
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        RopeState.Clear();

        if (RopeMaterial != null)
            Destroy(RopeMaterial);
        if (ConnectorMaterial != null)
            Destroy(ConnectorMaterial);
    }

    private static Material CreateMaterial(Color color)
    {
        Shader? shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        Material material = new(shader);
        material.color = color;
        return material;
    }
}

[HarmonyPatch(typeof(Character), "Awake")]
internal static class CharacterAwakePatch
{
    [HarmonyPostfix]
    private static void Postfix(Character __instance)
    {
        if (__instance == null || __instance.gameObject.GetComponent<RopeController>() != null)
            return;

        __instance.gameObject.AddComponent<RopeController>();

        try
        {
            __instance.photonView?.RefreshRpcMonoBehaviourCache();
        }
        catch (Exception exception)
        {
            Plugin.Log.LogDebug($"RPC cache refresh skipped: {exception.Message}");
        }
    }
}
