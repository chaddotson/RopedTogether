using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using UnityEngine;

namespace RopedTogether;

internal sealed class RopeController : MonoBehaviourPunCallbacks
{
    private const float FallSpeedThreshold = -3f;
    private const float StableBelayerSpeedThreshold = -1.5f;
    private const float TargetAimConeDegrees = 42f;
    private const float MaxDisconnectAimDistance = 40f;

    private static readonly MethodInfo? CanDoInputMethod = typeof(Character).GetMethod(
        "CanDoInput",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
        null,
        Type.EmptyTypes,
        null);

    private Character _character = null!;
    private Transform? _hip;
    private Rigidbody? _hipBody;
    private RopeInput? _input;
    private readonly Dictionary<RopeLink, RopeVisual> _visuals = new();
    private Character? _currentTarget;
    private float _nextTargetRefresh;
    private GUIStyle? _statusStyle;

    private void Start()
    {
        _character = GetComponent<Character>();
        _hip = FindHip(transform);
        _hipBody = _hip != null ? _hip.GetComponent<Rigidbody>() : null;
    }

    private void Update()
    {
        if (_character == null || !_character.IsLocal)
            return;

        EnsureInput();
        UpdateTargetCache();
        HandleInput();
        UpdateVisuals();
    }

    private void FixedUpdate()
    {
        if (_character == null || !_character.IsLocal || _hip == null || _hipBody == null)
            return;

        if (!PhotonNetwork.InRoom || !IsEligibleCharacter(_character))
            return;

        ApplyRopePhysics();
    }

    private void OnGUI()
    {
        if (!Plugin.ShowStatus.Value || _character == null || !_character.IsLocal || !PhotonNetwork.InRoom)
            return;

        if (_statusStyle == null)
        {
            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft
            };
            _statusStyle.normal.textColor = Color.white;
        }

        int actor = GetActorNumber(_character);
        int count = RopeState.CountForActor(actor);
        string targetText = BuildTargetPrompt(actor);

        GUILayout.BeginArea(new Rect(16f, 16f, 620f, 74f), GUI.skin.box);
        GUILayout.Label($"Ropes: {count}    {targetText}", _statusStyle);
        if (count > 0)
            GUILayout.Label("Shift+F: disconnect all", _statusStyle);
        GUILayout.EndArea();
    }

    [PunRPC]
    public void RpcSetRopeLink(int actorA, int actorB, bool enabled)
    {
        RopeState.Set(actorA, actorB, enabled);
        Plugin.Log.LogDebug($"Rope {actorA}-{actorB}: {(enabled ? "connected" : "disconnected")}");
    }

    private void EnsureInput()
    {
        if (_input != null)
            return;

        _input = new RopeInput(Plugin.Log, Plugin.KeyboardToggle.Value, Plugin.GamepadToggle.Value);
        Plugin.KeyboardToggle.SettingChanged += OnBindingChanged;
        Plugin.GamepadToggle.SettingChanged += OnBindingChanged;
    }

    private void OnBindingChanged(object sender, EventArgs e)
    {
        _input?.SetBindings(Plugin.KeyboardToggle.Value, Plugin.GamepadToggle.Value);
    }

    private void UpdateTargetCache()
    {
        if (Time.unscaledTime < _nextTargetRefresh)
            return;

        _nextTargetRefresh = Time.unscaledTime + 0.12f;
        _currentTarget = SelectTarget();
    }

    private void HandleInput()
    {
        if (_input == null || !PhotonNetwork.InRoom || !CanDoInput())
            return;

        RopeInputState input = _input.Read();
        if (!input.TogglePressed)
            return;

        int localActor = GetActorNumber(_character);
        if (localActor <= 0)
            return;

        if (input.DisconnectAllModifier)
        {
            DisconnectAll(localActor);
            return;
        }

        Character? target = _currentTarget ?? SelectTarget();
        if (target == null)
        {
            RopeLink[] existing = RopeState.ForActor(localActor).ToArray();
            if (existing.Length == 1)
                SendLink(existing[0].A, existing[0].B, false);
            return;
        }

        int targetActor = GetActorNumber(target);
        if (targetActor <= 0 || targetActor == localActor)
            return;

        bool currentlyLinked = RopeState.IsLinked(localActor, targetActor);
        if (!currentlyLinked)
        {
            float distance = HipDistance(_character, target);
            if (distance > Plugin.ConnectDistance.Value)
                return;
        }

        SendLink(localActor, targetActor, !currentlyLinked);
    }

    private void DisconnectAll(int localActor)
    {
        RopeLink[] links = RopeState.ForActor(localActor).ToArray();
        foreach (RopeLink link in links)
            SendLink(link.A, link.B, false);
    }

    private void SendLink(int actorA, int actorB, bool enabled)
    {
        if (photonView == null)
            return;

        photonView.RPC(nameof(RpcSetRopeLink), RpcTarget.AllBuffered, actorA, actorB, enabled);
    }

    private Character? SelectTarget()
    {
        if (_character == null || !PhotonNetwork.InRoom)
            return null;

        int localActor = GetActorNumber(_character);
        Camera? camera = Camera.main;
        Character? best = null;
        float bestScore = float.MaxValue;

        Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        foreach (Character candidate in characters)
        {
            if (!IsEligibleCharacter(candidate) || candidate == _character)
                continue;

            int actor = GetActorNumber(candidate);
            if (actor <= 0 || actor == localActor)
                continue;

            Transform? targetHip = FindHip(candidate.transform);
            if (targetHip == null || _hip == null)
                continue;

            float distance = Vector3.Distance(_hip.position, targetHip.position);
            bool linked = RopeState.IsLinked(localActor, actor);
            float allowedDistance = linked ? MaxDisconnectAimDistance : Plugin.ConnectDistance.Value;
            if (distance > allowedDistance)
                continue;

            float angle = 0f;
            if (camera != null)
            {
                Vector3 toTarget = targetHip.position - camera.transform.position;
                angle = Vector3.Angle(camera.transform.forward, toTarget);
                if (angle > TargetAimConeDegrees)
                    continue;
            }

            float score = distance + (angle * 0.12f);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private void ApplyRopePhysics()
    {
        int localActor = GetActorNumber(_character);
        if (localActor <= 0 || _hip == null || _hipBody == null)
            return;

        Dictionary<int, Character> byActor = BuildCharacterMap();
        Vector3 totalAcceleration = Vector3.zero;

        foreach (RopeLink link in RopeState.ForActor(localActor))
        {
            int otherActor = link.Other(localActor);
            if (!byActor.TryGetValue(otherActor, out Character? target) || target == null)
                continue;

            Transform? targetHip = FindHip(target.transform);
            if (targetHip == null)
                continue;

            Rigidbody? targetBody = targetHip.GetComponent<Rigidbody>();
            totalAcceleration += ComputeLinkAcceleration(targetHip, targetBody);
        }

        float maxTotal = Plugin.MaxTotalAcceleration.Value;
        if (totalAcceleration.sqrMagnitude > maxTotal * maxTotal)
            totalAcceleration = totalAcceleration.normalized * maxTotal;

        if (totalAcceleration.sqrMagnitude > 0.0001f)
            _hipBody.AddForce(totalAcceleration, ForceMode.Acceleration);
    }

    private Vector3 ComputeLinkAcceleration(Transform targetHip, Rigidbody? targetBody)
    {
        if (_hip == null || _hipBody == null)
            return Vector3.zero;

        Vector3 delta = targetHip.position - _hip.position;
        float distance = delta.magnitude;
        float stretch = distance - Plugin.RopeLength.Value;
        if (stretch <= 0f || distance < 0.001f)
            return Vector3.zero;

        Vector3 direction = delta / distance;
        Vector3 localVelocity = _hipBody.linearVelocity;
        Vector3 targetVelocity = targetBody != null ? targetBody.linearVelocity : Vector3.zero;

        float separatingSpeed = Mathf.Max(0f, -Vector3.Dot(localVelocity - targetVelocity, direction));
        float acceleration = (stretch * Plugin.SpringAcceleration.Value) + (separatingSpeed * Plugin.Damping.Value);

        bool targetIsAbove = targetHip.position.y > _hip.position.y + 0.5f;
        bool localIsFalling = localVelocity.y < FallSpeedThreshold;
        bool targetIsFalling = targetVelocity.y < FallSpeedThreshold;
        bool localIsStable = localVelocity.y > StableBelayerSpeedThreshold;
        bool targetIsBelow = targetHip.position.y < _hip.position.y - 0.75f;

        Vector3 result;
        if (localIsFalling && targetIsAbove)
        {
            acceleration *= Plugin.FallBoost.Value;
            result = direction * acceleration;

            float fallSeverity = Mathf.Clamp01((-localVelocity.y - 2.5f) / 8f);
            result += Vector3.up * (Plugin.RescueUpAcceleration.Value * fallSeverity);
        }
        else
        {
            if (localIsStable && targetIsFalling && targetIsBelow)
                acceleration *= Plugin.BelayerCounterPull.Value;

            result = direction * acceleration;
        }

        float maxPerLink = Plugin.MaxPerLinkAcceleration.Value;
        if (result.sqrMagnitude > maxPerLink * maxPerLink)
            result = result.normalized * maxPerLink;

        return result;
    }

    private void UpdateVisuals()
    {
        Dictionary<int, Character> byActor = BuildCharacterMap();
        HashSet<RopeLink> active = new();

        foreach (RopeLink link in RopeState.Snapshot())
        {
            if (!byActor.TryGetValue(link.A, out Character? a) || !byActor.TryGetValue(link.B, out Character? b))
                continue;

            Transform? hipA = FindHip(a.transform);
            Transform? hipB = FindHip(b.transform);
            if (hipA == null || hipB == null)
                continue;

            active.Add(link);
            if (!_visuals.TryGetValue(link, out RopeVisual? visual))
            {
                visual = new RopeVisual($"RopedTogether.{link}");
                _visuals.Add(link, visual);
            }

            visual.Update(hipA.position, hipB.position, Plugin.RopeLength.Value);
        }

        RopeLink[] stale = _visuals.Keys.Where(link => !active.Contains(link)).ToArray();
        foreach (RopeLink link in stale)
        {
            _visuals[link].Destroy();
            _visuals.Remove(link);
        }
    }

    private string BuildTargetPrompt(int localActor)
    {
        Character? target = _currentTarget;
        if (target == null)
            return "F: clip to nearby climber";

        int targetActor = GetActorNumber(target);
        bool linked = RopeState.IsLinked(localActor, targetActor);
        string name = string.IsNullOrWhiteSpace(target.characterName) ? $"player {targetActor}" : target.characterName;
        return linked ? $"F: unclip from {name}" : $"F: clip to {name}";
    }

    private bool CanDoInput()
    {
        if (CanDoInputMethod == null)
            return true;

        try
        {
            object? result = CanDoInputMethod.Invoke(_character, null);
            return result is bool allowed && allowed;
        }
        catch
        {
            return true;
        }
    }

    private static Dictionary<int, Character> BuildCharacterMap()
    {
        Dictionary<int, Character> map = new();
        Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        foreach (Character character in characters)
        {
            if (!IsEligibleCharacter(character))
                continue;

            int actor = GetActorNumber(character);
            if (actor > 0)
                map[actor] = character;
        }

        return map;
    }

    private static bool IsEligibleCharacter(Character character)
    {
        return character != null
            && character.isActiveAndEnabled
            && character.Ghost == null
            && character.IsRegisteredToPlayer
            && character.player != null
            && character.photonView != null
            && character.photonView.OwnerActorNr > 0
            && character.photonView.Owner != null
            && FindHip(character.transform) != null;
    }

    private static int GetActorNumber(Character character)
    {
        return character?.photonView?.OwnerActorNr ?? -1;
    }

    private static float HipDistance(Character a, Character b)
    {
        Transform? hipA = FindHip(a.transform);
        Transform? hipB = FindHip(b.transform);
        if (hipA == null || hipB == null)
            return float.MaxValue;
        return Vector3.Distance(hipA.position, hipB.position);
    }

    private static Transform? FindHip(Transform root)
    {
        if (root == null)
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>();
        foreach (Transform item in transforms)
        {
            if (item.name == "Hip")
                return item;
        }

        return null;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        DestroyVisuals();
    }

    private void OnDestroy()
    {
        Plugin.KeyboardToggle.SettingChanged -= OnBindingChanged;
        Plugin.GamepadToggle.SettingChanged -= OnBindingChanged;
        _input?.Dispose();
        DestroyVisuals();
    }

    private void DestroyVisuals()
    {
        foreach (RopeVisual visual in _visuals.Values)
            visual.Destroy();
        _visuals.Clear();
    }
}
