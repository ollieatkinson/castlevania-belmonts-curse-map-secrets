using MotherBase.RoomDesigner;
using HarmonyLib;
using UnityEngine;
using Il2CppInterop.Runtime.Attributes;
using Object = UnityEngine.Object;

namespace MapAssist;

public sealed class RoomObserver : MonoBehaviour
{
    internal static RoomObserver? Instance;
    private RoomInstance? room;
    private float nextPoll;
    private readonly List<SavableEntityBase> savables = new();
    private readonly List<SecretZone> secrets = new();
    private readonly FirstVisitTracker visits = new();
    private string? pendingRoom, pendingSave;
    private float pendingAt;
    private AudioSource? bell;
    private AudioClip? bellClip;
    private AudioListener? ownListener;
    private bool bellUnavailable;
    private float nextErrorLog;
    public RoomObserver(IntPtr pointer) : base(pointer) { }
    public void Awake() => Instance = this;

    [HideFromIl2Cpp]
    internal void Entering(ExplorerComponent explorer, MotherBase.RoomDesigner.WorldRoomData data)
    {
        SaveState.CheckSession();
        var logic = SaveLogic.Get();
        if (!logic || logic.CurrentSave?.playerSave == null) return;
        var id = logic.GetSaveId();
        if (string.IsNullOrEmpty(id)) return;
        if (visits.Enter(id, data.id, SaveState.IsRoomExplored(explorer, data.id)))
        {
            pendingRoom = data.id; pendingSave = id; pendingAt = Time.unscaledTime + 0.6f;
        }
        nextPoll = 0;
    }

    [HideFromIl2Cpp]
    internal void Reset()
    {
        room = null; pendingRoom = pendingSave = null; savables.Clear(); secrets.Clear(); nextPoll = 0;
    }

    public void Update()
    {
        if (Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.25f;
        try
        {
            if (!MainCharacterSingleton.TryGetEntity(out var entity) || !entity) { room = null; return; }
            SaveState.Snapshot();
            var current = entity.GetComponent<CurrentRoomInstance>();
            if (!current || !current.IsReady() || !current.currentRoom) { room = null; return; }
            if (room != current.currentRoom)
            {
                room = current.currentRoom;
                savables.Clear(); secrets.Clear();
                Plugin.Catalog.ByRoom.TryGetValue(room.worldRoomData.id, out var roomMarkers);
                foreach (var save in room.GetComponentsInChildren<SavableEntityBase>(true))
                {
                    var kind = NativeKind(save.gameObject.name);
                    if (Plugin.Catalog.SaveIds.Contains(save.guid) || kind.HasValue)
                    {
                        savables.Add(save);
                        if (kind.HasValue && roomMarkers != null)
                        {
                            var position = room.root.InverseTransformPoint(save.transform.position);
                            var marker = roomMarkers.Where(m => m.Source == "game" && m.Category == kind.Value && !string.IsNullOrEmpty(m.SaveId))
                                .OrderBy(m => Math.Pow(m.X - position.x, 2) + Math.Pow(m.Y - position.y, 2)).FirstOrDefault();
                            if (marker != null && Math.Pow(marker.X - position.x, 2) + Math.Pow(marker.Y - position.y, 2) < 1f)
                                SaveState.BindAlias(marker, save.guid);
                        }
                    }
                }
                foreach (var secret in room.GetComponentsInChildren<SecretZone>(true))
                    if (!secret.resetableSecretZone) secrets.Add(secret);
                Plugin.Logger.LogInfo($"Recalculating room markers: {room.worldRoomData.id}");
            }
            var roomId = room!.worldRoomData.id;
            foreach (var save in savables)
            {
                if (!save) continue;
                var encounter = save.GetComponent<GPE_EncounterGateComponent>();
                if (encounter) SaveState.Record(roomId, save.guid, encounter.IsDone);
                else
                {
                    var activable = save.GetComponent<ActivableEntity>();
                    if (activable) SaveState.Record(roomId, save.guid, activable.IsActivated());
                }
            }
            foreach (var secret in secrets)
                if (secret && secret.initialized && secret.revealed) SaveState.Record(roomId, "secret:" + secret.secretZoneIndex, true);
            SaveState.FlushObservations();
            MapOverlay.Instance?.RefreshNow();
            if (pendingRoom != null && Time.unscaledTime >= pendingAt)
            {
                var target = pendingRoom; pendingRoom = null;
                if (target == roomId && pendingSave == SaveLogic.Get()?.GetSaveId()
                    && Plugin.Enabled.Value && Plugin.RoomBell.Value && Plugin.Catalog.ByRoom.TryGetValue(target, out var candidates)
                    && candidates.Any(m => Categories.Enabled(m.Category) && (m.Verified || Plugin.Unverified.Value)
                        && SaveState.GetCollected(m) != CollectionState.Collected))
                {
                    try { if (!bellUnavailable) PlayBell(); }
                    catch (Exception e) { bellUnavailable = true; Plugin.Logger.LogWarning($"Room bell unavailable: {e.Message}"); }
                }
            }
        }
        catch (Exception e)
        {
            // Scene transitions can invalidate Unity wrappers temporarily. Rebuild the
            // current-room cache on the next poll instead of losing updates for the session.
            room = null; savables.Clear(); secrets.Clear();
            nextPoll = Time.unscaledTime + 1f;
            if (Time.unscaledTime >= nextErrorLog)
            {
                nextErrorLog = Time.unscaledTime + 10f;
                Plugin.Logger.LogWarning($"Room state will be retried after a transition error: {e}");
            }
        }
    }

    [HideFromIl2Cpp]
    private static MarkerKind? NativeKind(string name)
    {
        if (name.StartsWith("GPE_UpgradeCollectibleHealth", StringComparison.Ordinal)) return MarkerKind.Health;
        if (name.StartsWith("GPE_UpgradeCollectibleMana", StringComparison.Ordinal)) return MarkerKind.Mana;
        if (name.StartsWith("GPE_DestructibleSourceExperience", StringComparison.Ordinal)) return MarkerKind.Experience;
        if (name.StartsWith("GPE_ConsumableAtkUpBook", StringComparison.Ordinal)
            || name.StartsWith("GPE_ConsumableDefUpBook", StringComparison.Ordinal)) return MarkerKind.Upgrade;
        if (name.StartsWith("GPE_DestructibleWall", StringComparison.Ordinal)) return MarkerKind.Wall;
        return null;
    }

    private void PlayBell()
    {
        if (Plugin.BellVolume.Value <= 0) return;
        if (!bell)
        {
            bell = gameObject.AddComponent<AudioSource>();
            bell.playOnAwake = false; bell.spatialBlend = 0; bell.ignoreListenerPause = true;
            if (!Object.FindObjectOfType<AudioListener>()) ownListener = gameObject.AddComponent<AudioListener>();
        }
        if (ownListener)
        {
            var anotherListener = false;
            foreach (var listener in Object.FindObjectsOfType<AudioListener>())
                if (listener != ownListener && listener.enabled) anotherListener = true;
            ownListener!.enabled = !anotherListener;
        }
        if (!bellClip)
        {
            const int rate = 44100;
            var samples = new float[rate];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)rate;
                var attack = Mathf.Min(t / 0.008f, 1f);
                samples[i] = attack * (0.52f * Mathf.Sin(2 * Mathf.PI * 880 * t) * Mathf.Exp(-5.5f * t)
                    + 0.24f * Mathf.Sin(2 * Mathf.PI * 1765 * t) * Mathf.Exp(-8f * t)
                    + 0.12f * Mathf.Sin(2 * Mathf.PI * 2380 * t) * Mathf.Exp(-12f * t));
            }
            bellClip = AudioClip.Create("Map Secrets discovery bell", rate, 1, rate, false);
            bellClip.SetData(samples, 0);
        }
        bell!.PlayOneShot(bellClip, Plugin.BellVolume.Value);
    }

    public void OnDestroy()
    {
        SaveState.FlushObservations(true);
        if (bellClip) Object.Destroy(bellClip);
        if (ownListener) Object.Destroy(ownListener);
        Instance = null;
    }
}

[HarmonyPatch(typeof(ExplorerComponent), nameof(ExplorerComponent.SetNewChunkEntered))]
internal static class RoomEntered
{
    private static void Prefix(ExplorerComponent __instance, MotherBase.RoomDesigner.WorldRoomData roomData)
    {
        try { RoomObserver.Instance?.Entering(__instance, roomData); }
        catch (Exception e) { Plugin.Logger.LogWarning($"Could not check first room visit: {e.Message}"); }
    }
}

[HarmonyPatch(typeof(GPE_CloseUpDataHolder), nameof(GPE_CloseUpDataHolder.OpenCloseUp))]
internal static class LoreRead
{
    private static void Postfix(GPE_CloseUpDataHolder __instance)
    {
        try
        {
            // Only physical lore signs count here; NPC/clue close-ups are separate interactions.
            if (!__instance.gameObject.name.StartsWith("GPE_Sign_CloseUp", StringComparison.Ordinal)) return;
            var room = __instance.GetComponentInParent<RoomInstance>();
            if (room) SaveState.RecordLore(room.worldRoomData.id, room.root.InverseTransformPoint(__instance.transform.position));
        }
        catch (Exception e) { Plugin.Logger.LogWarning($"Could not refresh read lore: {e.Message}"); }
    }
}

[HarmonyPatch(typeof(ExplorerComponent), nameof(ExplorerComponent.SetNewExplorationZoneEntered), new[] { typeof(WorldRoomData), typeof(int) })]
internal static class ExplorationZoneEntered
{
    private static void Prefix(ExplorerComponent __instance, WorldRoomData roomData)
    {
        try { RoomObserver.Instance?.Entering(__instance, roomData); }
        catch (Exception e) { Plugin.Logger.LogWarning($"Could not check first zone visit: {e.Message}"); }
    }
}

[HarmonyPatch(typeof(ExplorerComponent), nameof(ExplorerComponent.SetDiscoveredSecretZone))]
internal static class SecretDiscovered
{
    private static void Postfix() => MapOverlay.Instance?.RefreshNow();
}
