using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SaveGameManager : MonoBehaviour
{
    [Serializable]
    private class SaveGameData
    {
        public int version = 2;
        public string sceneName;
        public int currentTurn;
        public int activeTeamId;
        public bool includeNeutralTeam;
        public bool economyEnabled = true;
        public List<MatchPlayerSaveData> players = new List<MatchPlayerSaveData>();
        public List<UnitSaveData> units = new List<UnitSaveData>();
        public List<ConstructionSaveData> constructions = new List<ConstructionSaveData>();
    }

    [Serializable]
    private class MatchPlayerSaveData
    {
        public int teamId;
        public bool flipX;
        public int startMoney;
        public int actualMoney;
        public int incomePerTurn;
        public bool startMoneyApplied;
    }

    [Serializable]
    private class UnitSaveData
    {
        public int instanceId;
        public string unitId;
        public int teamId;
        public int cellX;
        public int cellY;
        public float worldX;
        public float worldY;
        public int currentHP;
        public int currentAmmo;
        public int currentFuel;
        public int remainingMovementPoints;
        public bool hasActed;
        public bool receivedSuppliesThisTurn;
        public bool isEmbarked;
        public int transporterInstanceId;
        public int transporterSlotIndex;
        public int domain;
        public int heightLevel;
        public List<int> embarkedWeaponAmmo = new List<int>();
    }

    [Serializable]
    private class RuntimeSupplySaveData
    {
        public string supplyId;
        public int quantity;
    }

    [Serializable]
    private class ConstructionSiteRuntimeSaveData
    {
        public bool isPlayerHeadQuarter;
        public bool isCapturable;
        public int capturePointsMax;
        public int capturedIncoming;
        public int sellingRule;
        public bool canProvideSupplies;
        public List<string> offeredUnitIds = new List<string>();
        public List<string> offeredServiceIds = new List<string>();
        public List<RuntimeSupplySaveData> offeredSupplies = new List<RuntimeSupplySaveData>();
    }

    [Serializable]
    private class ConstructionSaveData
    {
        public int instanceId;
        public string constructionId;
        public int teamId;
        public int cellX;
        public int cellY;
        public float worldX;
        public float worldY;
        public int currentCapturePoints;
        public int originalOwnerTeamId;
        public bool hasOriginalOwner;
        public int firstOwnerTeamId;
        public bool hasFirstOwner;
        public bool hasInfiniteSuppliesOverride;
        public ConstructionSiteRuntimeSaveData siteRuntime;
    }

    [Header("References")]
    [SerializeField] private UnitSpawner unitSpawner;
    [SerializeField] private ConstructionSpawner constructionSpawner;
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private CursorController cursorController;

    [Header("Quick Save/Load")]
    [SerializeField] private bool enableHotkeys = true;
    [SerializeField] private KeyCode quickSaveKey = KeyCode.F8;
    [SerializeField] private KeyCode quickLoadKey = KeyCode.F9;
    [SerializeField] private string slotName = "quicksave";
    [SerializeField] private bool useSceneSpecificSlot = true;
    [SerializeField] private bool blockCrossSceneLoad = true;
    [SerializeField] private bool verboseLogs = true;
    [SerializeField] private bool forceLoadWhenBusy = true;

    private bool loadInProgress;
    private readonly Dictionary<string, ServiceData> cachedServicesById = new Dictionary<string, ServiceData>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SupplyData> cachedSuppliesById = new Dictionary<string, SupplyData>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        TryAutoAssignReferences();
    }

    private void Update()
    {
        if (!Application.isPlaying || !enableHotkeys || loadInProgress)
            return;

        if (UiInputBlocker.IsTextInputFocused())
            return;

        if (WasKeyPressedThisFrame(quickSaveKey))
            Save();

        if (WasKeyPressedThisFrame(quickLoadKey))
            Load();
    }

    [ContextMenu("Save Quick Slot")]
    public void Save()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SaveGame] Save funciona apenas em Play Mode.");
            return;
        }

        try
        {
            TryAutoAssignReferences();
            cursorController?.PlayConfirmSfx();

            SaveGameData data = BuildSaveData();
            string json = JsonUtility.ToJson(data, true);
            string path = GetSlotPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Application.persistentDataPath);
            File.WriteAllText(path, json);
            cursorController?.PlayLoadSfx();
            PanelDialogController.TrySetTransientText("Game saved", 2.2f);
            Debug.Log($"[SaveGame] jogo salvo em: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveGame] Falha ao salvar: {ex.Message}\n{ex.StackTrace}");
        }
    }

    [ContextMenu("Load Quick Slot")]
    public void Load()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SaveGame] Load funciona apenas em Play Mode.");
            return;
        }

        if (loadInProgress)
            return;

        cursorController?.PlayConfirmSfx();
        TryAutoAssignReferences();
        if (unitSpawner == null || constructionSpawner == null)
        {
            Debug.LogError("[SaveGame] UnitSpawner/ConstructionSpawner nao encontrados na cena.");
            return;
        }
        string path = GetSlotPath();
        if (!File.Exists(path))
        {
            cursorController?.PlayErrorSfx();
            Debug.LogWarning($"[SaveGame] Sem savegame para carregar: {path}");
            return;
        }

        if (!CanLoadNow(out string reason))
        {
            if (!forceLoadWhenBusy)
            {
                Debug.LogWarning($"[SaveGame] Load bloqueado: {reason}");
                return;
            }

            Debug.LogWarning($"[SaveGame] Load fora do estado ideal ({reason}). Forcando carregamento.");
        }

        string json = File.ReadAllText(path);
        SaveGameData data = JsonUtility.FromJson<SaveGameData>(json);
        if (data == null)
        {
            Debug.LogError("[SaveGame] Falha ao desserializar save.");
            return;
        }

        string currentScene = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrWhiteSpace(data.sceneName) && !string.Equals(data.sceneName, currentScene, StringComparison.Ordinal))
        {
            if (blockCrossSceneLoad)
            {
                cursorController?.PlayErrorSfx();
                Debug.LogWarning($"[SaveGame] Load bloqueado: save da cena '{data.sceneName}', cena atual '{currentScene}'.");
                return;
            }

            Debug.LogWarning($"[SaveGame] Save foi criado na cena '{data.sceneName}', cena atual: '{currentScene}'.");
        }

        PrepareRuntimeForLoad();
        StartCoroutine(LoadRoutine(data));
    }

    private void PrepareRuntimeForLoad()
    {
        // Load pode ser disparado no meio de subfluxos (embarque/suprir/fundir etc).
        // Antes de restaurar snapshot, limpa qualquer estado transiente pendente.
        animationManager?.StopCurrentMovement();
        if (turnStateManager != null)
        {
            turnStateManager.StopAllCoroutines();
            turnStateManager.ForceNeutral();
        }

        cursorController?.ClearRuntimeInputLocksAfterLoad();
    }

    private IEnumerator LoadRoutine(SaveGameData data)
    {
        loadInProgress = true;
        string stage = "init";
        bool coreLoadSucceeded = false;

        // Espera um frame apos destruir para evitar residuos de lookup no mesmo frame.
        stage = "clear-runtime";
        ClearCurrentRuntime();
        yield return null;

        try
        {
            Dictionary<int, UnitManager> unitsById = new Dictionary<int, UnitManager>();
            int maxUnitId = 0;
            int maxConstructionId = 0;

            stage = "spawn-constructions";
            if (data.constructions != null)
            {
                for (int i = 0; i < data.constructions.Count; i++)
                {
                    ConstructionSaveData saved = data.constructions[i];
                    if (saved == null || string.IsNullOrWhiteSpace(saved.constructionId))
                        continue;

                    if (!constructionSpawner.TryGetConstructionData(saved.constructionId, out ConstructionData constructionData) || constructionData == null)
                    {
                        Debug.LogWarning($"[SaveGame] Construcao nao encontrada no DB: {saved.constructionId}");
                        continue;
                    }

                    Vector3 world = new Vector3(saved.worldX, saved.worldY, 0f);
                    GameObject go = constructionSpawner.Spawn(constructionData, (TeamId)saved.teamId, world, Quaternion.identity);
                    if (go == null)
                        continue;

                    ConstructionManager manager = go.GetComponent<ConstructionManager>();
                    if (manager == null)
                        continue;

                    manager.AssignSpawnInstanceId(saved.instanceId);
                    manager.SetCurrentCellPosition(new Vector3Int(saved.cellX, saved.cellY, 0));
                    manager.ApplyOwnershipState(
                        (TeamId)saved.teamId,
                        (TeamId)saved.originalOwnerTeamId,
                        saved.hasOriginalOwner,
                        (TeamId)saved.firstOwnerTeamId,
                        saved.hasFirstOwner);
                    ConstructionSiteRuntime restoredRuntime = BuildSiteRuntimeFromSaveData(saved.siteRuntime);
                    if (restoredRuntime != null)
                        manager.ApplySiteRuntime(restoredRuntime);
                    manager.SetCurrentCapturePoints(saved.currentCapturePoints);
                    manager.SetInfiniteSuppliesOverride(saved.hasInfiniteSuppliesOverride);

                    if (saved.instanceId > maxConstructionId)
                        maxConstructionId = saved.instanceId;
                }
            }

            stage = "spawn-units";
            if (data.units != null)
            {
                for (int i = 0; i < data.units.Count; i++)
                {
                    UnitSaveData saved = data.units[i];
                    if (saved == null || string.IsNullOrWhiteSpace(saved.unitId))
                        continue;

                    if (!unitSpawner.TryGetUnitData(saved.unitId, out UnitData unitData) || unitData == null)
                    {
                        Debug.LogWarning($"[SaveGame] Unidade nao encontrada no DB: {saved.unitId}");
                        continue;
                    }

                    Vector3 world = new Vector3(saved.worldX, saved.worldY, 0f);
                    GameObject go = unitSpawner.Spawn(unitData, (TeamId)saved.teamId, world, Quaternion.identity);
                    if (go == null)
                        continue;

                    UnitManager manager = go.GetComponent<UnitManager>();
                    if (manager == null)
                        continue;

                    manager.AssignSpawnInstanceId(saved.instanceId);
                    manager.SetCurrentCellPosition(new Vector3Int(saved.cellX, saved.cellY, 0), enforceFinalOccupancyRule: false);
                    manager.SetCurrentHP(saved.currentHP);
                    manager.SetCurrentAmmo(saved.currentAmmo);
                    manager.SetCurrentFuel(saved.currentFuel);
                    manager.SetReceivedSuppliesThisTurn(saved.receivedSuppliesThisTurn);
                    if (saved.hasActed) manager.MarkAsActed();
                    else manager.ResetActed();
                    manager.SetRemainingMovementPoints(saved.remainingMovementPoints);

                    Domain domain = (Domain)saved.domain;
                    HeightLevel heightLevel = (HeightLevel)saved.heightLevel;
                    manager.TrySetCurrentLayerMode(domain, heightLevel);

                    IReadOnlyList<UnitEmbarkedWeapon> embarked = manager.GetEmbarkedWeapons();
                    if (embarked != null && saved.embarkedWeaponAmmo != null)
                    {
                        int count = Mathf.Min(embarked.Count, saved.embarkedWeaponAmmo.Count);
                        for (int weaponIndex = 0; weaponIndex < count; weaponIndex++)
                        {
                            if (embarked[weaponIndex] == null)
                                continue;
                            embarked[weaponIndex].squadAmmunition = Mathf.Max(0, saved.embarkedWeaponAmmo[weaponIndex]);
                        }
                    }

                    unitsById[saved.instanceId] = manager;
                    if (saved.instanceId > maxUnitId)
                        maxUnitId = saved.instanceId;
                }
            }

            // Religa passageiros embarcados apos todos os spawns.
            stage = "restore-embarked";
            if (data.units != null)
            {
                for (int i = 0; i < data.units.Count; i++)
                {
                    UnitSaveData saved = data.units[i];
                    if (saved == null || !saved.isEmbarked || saved.transporterInstanceId <= 0)
                        continue;

                    if (!unitsById.TryGetValue(saved.instanceId, out UnitManager passenger) || passenger == null)
                        continue;
                    if (!unitsById.TryGetValue(saved.transporterInstanceId, out UnitManager transporter) || transporter == null)
                        continue;

                    if (!transporter.TryEmbarkPassengerInSlot(passenger, saved.transporterSlotIndex, out string reason) && verboseLogs)
                        Debug.LogWarning($"[SaveGame] Falha embarque {saved.instanceId}->{saved.transporterInstanceId}: {reason}");
                }
            }

            stage = "sync-ids";
            unitSpawner.EnsureNextIdAbove(maxUnitId);
            constructionSpawner.EnsureNextIdAbove(maxConstructionId);

            stage = "restore-match";
            if (matchController != null)
            {
                RestoreMatchPlayers(data);
                matchController.SetEconomyEnabled(data.economyEnabled);
                matchController.SetCurrentTurn(data.currentTurn);
                matchController.SetActiveTeamId(data.activeTeamId);
                // Reaplica economia/flip apos SetActiveTeamId para evitar side effects
                // de credito no inicio do turno sobrescrever o snapshot salvo.
                RestoreMatchPlayers(data);
            }

            // Reaplica estado de acted apos MatchController liberar equipe ativa.
            stage = "restore-unit-flags";
            if (data.units != null)
            {
                for (int i = 0; i < data.units.Count; i++)
                {
                    UnitSaveData saved = data.units[i];
                    if (saved == null || !unitsById.TryGetValue(saved.instanceId, out UnitManager unit) || unit == null)
                        continue;

                    if (saved.hasActed) unit.MarkAsActed();
                    else unit.ResetActed();
                    unit.SetRemainingMovementPoints(saved.remainingMovementPoints);
                    unit.SetReceivedSuppliesThisTurn(saved.receivedSuppliesThisTurn);
                }
            }

            coreLoadSucceeded = true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveGame] Falha no load (etapa: {stage}): {ex.Message}\n{ex.StackTrace}");
        }

        if (coreLoadSucceeded)
        {
            stage = "warmup-hotzone-cache";
            if (turnStateManager != null)
            {
                yield return turnStateManager.WarmUpThreatCacheFromScene((processed, total) =>
                {
                    string progressText = total > 0
                        ? $"Loading hotzone cache {processed}/{total}"
                        : "Loading hotzone cache";
                    PanelDialogController.TrySetExternalText(progressText);

                    if (!verboseLogs)
                        return;
                    if (total <= 0 || processed == 0 || processed == total || processed % 10 == 0)
                        Debug.Log($"[SaveGame] Warm-up hotzone cache: {processed}/{total}");
                });
            }

            stage = "reset-runtime-input";
            turnStateManager?.ForceNeutral();
            cursorController?.ClearRuntimeInputLocksAfterLoad();
            cursorController?.SnapToCurrentCell();
            PanelDialogController.ClearExternalText();

            cursorController?.PlayBeepSfx();
            if (verboseLogs)
                Debug.Log($"[SaveGame] Load concluido: {data.units?.Count ?? 0} unidades, {data.constructions?.Count ?? 0} construcoes.");
            PanelDialogController.TrySetTransientText("Game loaded", 2.2f);
        }

        loadInProgress = false;
    }

    private SaveGameData BuildSaveData()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        SaveGameData data = new SaveGameData
        {
            sceneName = activeScene.name,
            currentTurn = matchController != null ? matchController.CurrentTurn : 0,
            activeTeamId = matchController != null ? matchController.ActiveTeamId : (int)TeamId.Green,
            includeNeutralTeam = matchController != null && matchController.IncludeNeutralTeam,
            economyEnabled = matchController == null || matchController.EconomyEnabled
        };

        if (matchController != null)
        {
            List<int> teamIds = new List<int>();
            List<bool> flipXs = new List<bool>();
            List<int> startMoneys = new List<int>();
            List<int> actualMoneys = new List<int>();
            List<int> incomePerTurns = new List<int>();
            List<bool> startMoneyAppliedFlags = new List<bool>();
            matchController.ExportPlayersState(teamIds, flipXs, startMoneys, actualMoneys, incomePerTurns, startMoneyAppliedFlags);

            int count = teamIds.Count;
            for (int i = 0; i < count; i++)
            {
                data.players.Add(new MatchPlayerSaveData
                {
                    teamId = teamIds[i],
                    flipX = i < flipXs.Count && flipXs[i],
                    startMoney = i < startMoneys.Count ? Mathf.Max(0, startMoneys[i]) : 0,
                    actualMoney = i < actualMoneys.Count ? Mathf.Max(0, actualMoneys[i]) : 0,
                    incomePerTurn = i < incomePerTurns.Count ? Mathf.Max(0, incomePerTurns[i]) : 0,
                    startMoneyApplied = i < startMoneyAppliedFlags.Count && startMoneyAppliedFlags[i]
                });
            }
        }

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;
            if (unit.gameObject.scene != activeScene)
                continue;

            UnitSaveData item = new UnitSaveData
            {
                instanceId = unit.InstanceId,
                unitId = unit.UnitId,
                teamId = (int)unit.TeamId,
                cellX = unit.CurrentCellPosition.x,
                cellY = unit.CurrentCellPosition.y,
                worldX = unit.transform.position.x,
                worldY = unit.transform.position.y,
                currentHP = unit.CurrentHP,
                currentAmmo = unit.CurrentAmmo,
                currentFuel = unit.CurrentFuel,
                remainingMovementPoints = unit.RemainingMovementPoints,
                hasActed = unit.HasActed,
                receivedSuppliesThisTurn = unit.ReceivedSuppliesThisTurn,
                isEmbarked = unit.IsEmbarked,
                transporterInstanceId = unit.EmbarkedTransporter != null ? unit.EmbarkedTransporter.InstanceId : 0,
                transporterSlotIndex = unit.IsEmbarked ? unit.EmbarkedTransporterSlotIndex : -1,
                domain = (int)unit.GetDomain(),
                heightLevel = (int)unit.GetHeightLevel()
            };

            IReadOnlyList<UnitEmbarkedWeapon> embarked = unit.GetEmbarkedWeapons();
            if (embarked != null)
            {
                for (int weaponIndex = 0; weaponIndex < embarked.Count; weaponIndex++)
                {
                    UnitEmbarkedWeapon weapon = embarked[weaponIndex];
                    item.embarkedWeaponAmmo.Add(weapon != null ? Mathf.Max(0, weapon.squadAmmunition) : 0);
                }
            }

            data.units.Add(item);
        }

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;
            if (construction.gameObject.scene != activeScene)
                continue;

            ConstructionSaveData item = new ConstructionSaveData
            {
                instanceId = construction.InstanceId,
                constructionId = construction.ConstructionId,
                teamId = (int)construction.TeamId,
                cellX = construction.CurrentCellPosition.x,
                cellY = construction.CurrentCellPosition.y,
                worldX = construction.transform.position.x,
                worldY = construction.transform.position.y,
                currentCapturePoints = construction.CurrentCapturePoints,
                originalOwnerTeamId = (int)construction.OriginalOwnerTeamId,
                hasOriginalOwner = construction.HasOriginalOwner,
                firstOwnerTeamId = (int)construction.FirstOwnerTeamId,
                hasFirstOwner = construction.HasFirstOwner,
                hasInfiniteSuppliesOverride = construction.HasInfiniteSuppliesOverride,
                siteRuntime = BuildSiteRuntimeSaveData(construction.GetSiteRuntimeSnapshot())
            };

            data.constructions.Add(item);
        }

        return data;
    }

    private void RestoreMatchPlayers(SaveGameData data)
    {
        if (matchController == null || data == null)
            return;
        if (data.players == null || data.players.Count == 0)
            return;

        List<int> teamIds = new List<int>(data.players.Count);
        List<bool> flipXs = new List<bool>(data.players.Count);
        List<int> startMoneys = new List<int>(data.players.Count);
        List<int> actualMoneys = new List<int>(data.players.Count);
        List<int> incomePerTurns = new List<int>(data.players.Count);
        List<bool> startMoneyAppliedFlags = new List<bool>(data.players.Count);

        for (int i = 0; i < data.players.Count; i++)
        {
            MatchPlayerSaveData player = data.players[i];
            if (player == null)
                continue;

            teamIds.Add(player.teamId);
            flipXs.Add(player.flipX);
            startMoneys.Add(Mathf.Max(0, player.startMoney));
            actualMoneys.Add(Mathf.Max(0, player.actualMoney));
            incomePerTurns.Add(Mathf.Max(0, player.incomePerTurn));
            startMoneyAppliedFlags.Add(player.startMoneyApplied);
        }

        matchController.ImportPlayersState(
            teamIds,
            flipXs,
            startMoneys,
            actualMoneys,
            incomePerTurns,
            startMoneyAppliedFlags,
            data.includeNeutralTeam);
    }

    private void ClearCurrentRuntime()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i] != null && units[i].gameObject.scene == activeScene)
                Destroy(units[i].gameObject);
        }

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            if (constructions[i] != null && constructions[i].gameObject.scene == activeScene)
                Destroy(constructions[i].gameObject);
        }
    }

    private bool CanLoadNow(out string reason)
    {
        reason = string.Empty;

        if (turnStateManager != null && turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
        {
            reason = $"cursor em {turnStateManager.CurrentCursorState}; volte ao estado Neutral.";
            return false;
        }

        if (animationManager != null && animationManager.IsAnimatingMovement)
        {
            reason = "animacao em progresso.";
            return false;
        }

        return true;
    }

    private string GetSlotPath()
    {
        string safeSlot = string.IsNullOrWhiteSpace(slotName) ? "quicksave" : slotName.Trim();
        if (useSceneSpecificSlot)
        {
            Scene scene = SceneManager.GetActiveScene();
            string sceneName = string.IsNullOrWhiteSpace(scene.name) ? "Scene" : scene.name.Trim();
            string sceneIdentity = !string.IsNullOrWhiteSpace(scene.path) ? scene.path : sceneName;
            string sceneHash = ComputeShortStableHash(sceneIdentity);
            safeSlot = $"{safeSlot}_{sceneName}_{sceneHash}";
        }

        return Path.Combine(Application.persistentDataPath, safeSlot + ".json");
    }

    private static string ComputeShortStableHash(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "00000000";

        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] hash = sha.ComputeHash(bytes);
            StringBuilder sb = new StringBuilder(8);
            for (int i = 0; i < 4; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }

    private void TryAutoAssignReferences()
    {
        if (unitSpawner == null)
            unitSpawner = FindInActiveScene<UnitSpawner>();
        if (constructionSpawner == null)
            constructionSpawner = FindInActiveScene<ConstructionSpawner>();
        if (matchController == null)
            matchController = FindInActiveScene<MatchController>();
        if (turnStateManager == null)
            turnStateManager = FindInActiveScene<TurnStateManager>();
        if (animationManager == null)
            animationManager = FindInActiveScene<AnimationManager>();
        if (cursorController == null)
            cursorController = FindInActiveScene<CursorController>();
    }

    private static T FindInActiveScene<T>() where T : Component
    {
        Scene activeScene = SceneManager.GetActiveScene();
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            T candidate = all[i];
            if (candidate == null)
                continue;
            if (candidate.gameObject.scene == activeScene)
                return candidate;
        }

        return null;
    }

    private bool WasKeyPressedThisFrame(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            return false;

        switch (key)
        {
            case KeyCode.A: return Keyboard.current.aKey.wasPressedThisFrame;
            case KeyCode.B: return Keyboard.current.bKey.wasPressedThisFrame;
            case KeyCode.C: return Keyboard.current.cKey.wasPressedThisFrame;
            case KeyCode.D: return Keyboard.current.dKey.wasPressedThisFrame;
            case KeyCode.E: return Keyboard.current.eKey.wasPressedThisFrame;
            case KeyCode.F: return Keyboard.current.fKey.wasPressedThisFrame;
            case KeyCode.G: return Keyboard.current.gKey.wasPressedThisFrame;
            case KeyCode.H: return Keyboard.current.hKey.wasPressedThisFrame;
            case KeyCode.I: return Keyboard.current.iKey.wasPressedThisFrame;
            case KeyCode.J: return Keyboard.current.jKey.wasPressedThisFrame;
            case KeyCode.K: return Keyboard.current.kKey.wasPressedThisFrame;
            case KeyCode.L: return Keyboard.current.lKey.wasPressedThisFrame;
            case KeyCode.M: return Keyboard.current.mKey.wasPressedThisFrame;
            case KeyCode.N: return Keyboard.current.nKey.wasPressedThisFrame;
            case KeyCode.O: return Keyboard.current.oKey.wasPressedThisFrame;
            case KeyCode.P: return Keyboard.current.pKey.wasPressedThisFrame;
            case KeyCode.Q: return Keyboard.current.qKey.wasPressedThisFrame;
            case KeyCode.R: return Keyboard.current.rKey.wasPressedThisFrame;
            case KeyCode.S: return Keyboard.current.sKey.wasPressedThisFrame;
            case KeyCode.T: return Keyboard.current.tKey.wasPressedThisFrame;
            case KeyCode.U: return Keyboard.current.uKey.wasPressedThisFrame;
            case KeyCode.V: return Keyboard.current.vKey.wasPressedThisFrame;
            case KeyCode.W: return Keyboard.current.wKey.wasPressedThisFrame;
            case KeyCode.X: return Keyboard.current.xKey.wasPressedThisFrame;
            case KeyCode.Y: return Keyboard.current.yKey.wasPressedThisFrame;
            case KeyCode.Z: return Keyboard.current.zKey.wasPressedThisFrame;
            case KeyCode.Alpha0: return Keyboard.current.digit0Key.wasPressedThisFrame;
            case KeyCode.Alpha1: return Keyboard.current.digit1Key.wasPressedThisFrame;
            case KeyCode.Alpha2: return Keyboard.current.digit2Key.wasPressedThisFrame;
            case KeyCode.Alpha3: return Keyboard.current.digit3Key.wasPressedThisFrame;
            case KeyCode.Alpha4: return Keyboard.current.digit4Key.wasPressedThisFrame;
            case KeyCode.Alpha5: return Keyboard.current.digit5Key.wasPressedThisFrame;
            case KeyCode.Alpha6: return Keyboard.current.digit6Key.wasPressedThisFrame;
            case KeyCode.Alpha7: return Keyboard.current.digit7Key.wasPressedThisFrame;
            case KeyCode.Alpha8: return Keyboard.current.digit8Key.wasPressedThisFrame;
            case KeyCode.Alpha9: return Keyboard.current.digit9Key.wasPressedThisFrame;
            case KeyCode.Keypad0: return Keyboard.current.numpad0Key.wasPressedThisFrame;
            case KeyCode.Keypad1: return Keyboard.current.numpad1Key.wasPressedThisFrame;
            case KeyCode.Keypad2: return Keyboard.current.numpad2Key.wasPressedThisFrame;
            case KeyCode.Keypad3: return Keyboard.current.numpad3Key.wasPressedThisFrame;
            case KeyCode.Keypad4: return Keyboard.current.numpad4Key.wasPressedThisFrame;
            case KeyCode.Keypad5: return Keyboard.current.numpad5Key.wasPressedThisFrame;
            case KeyCode.Keypad6: return Keyboard.current.numpad6Key.wasPressedThisFrame;
            case KeyCode.Keypad7: return Keyboard.current.numpad7Key.wasPressedThisFrame;
            case KeyCode.Keypad8: return Keyboard.current.numpad8Key.wasPressedThisFrame;
            case KeyCode.Keypad9: return Keyboard.current.numpad9Key.wasPressedThisFrame;
            case KeyCode.Space: return Keyboard.current.spaceKey.wasPressedThisFrame;
            case KeyCode.Return: return Keyboard.current.enterKey.wasPressedThisFrame;
            case KeyCode.KeypadEnter: return Keyboard.current.numpadEnterKey.wasPressedThisFrame;
            case KeyCode.Escape: return Keyboard.current.escapeKey.wasPressedThisFrame;
            case KeyCode.Tab: return Keyboard.current.tabKey.wasPressedThisFrame;
            case KeyCode.F1: return Keyboard.current.f1Key.wasPressedThisFrame;
            case KeyCode.F2: return Keyboard.current.f2Key.wasPressedThisFrame;
            case KeyCode.F3: return Keyboard.current.f3Key.wasPressedThisFrame;
            case KeyCode.F4: return Keyboard.current.f4Key.wasPressedThisFrame;
            case KeyCode.F5: return Keyboard.current.f5Key.wasPressedThisFrame;
            case KeyCode.F6: return Keyboard.current.f6Key.wasPressedThisFrame;
            case KeyCode.F7: return Keyboard.current.f7Key.wasPressedThisFrame;
            case KeyCode.F8: return Keyboard.current.f8Key.wasPressedThisFrame;
            case KeyCode.F9: return Keyboard.current.f9Key.wasPressedThisFrame;
            case KeyCode.F10: return Keyboard.current.f10Key.wasPressedThisFrame;
            case KeyCode.F11: return Keyboard.current.f11Key.wasPressedThisFrame;
            case KeyCode.F12: return Keyboard.current.f12Key.wasPressedThisFrame;
            default:
                return false;
        }
#else
        return Input.GetKeyDown(key);
#endif
    }

    private ConstructionSiteRuntimeSaveData BuildSiteRuntimeSaveData(ConstructionSiteRuntime runtime)
    {
        if (runtime == null)
            return null;

        runtime.Sanitize();
        ConstructionSiteRuntimeSaveData result = new ConstructionSiteRuntimeSaveData
        {
            isPlayerHeadQuarter = runtime.isPlayerHeadQuarter,
            isCapturable = runtime.isCapturable,
            capturePointsMax = Mathf.Max(0, runtime.capturePointsMax),
            capturedIncoming = Mathf.Max(0, runtime.capturedIncoming),
            sellingRule = (int)runtime.sellingRule,
            canProvideSupplies = runtime.canProvideSupplies
        };

        if (runtime.offeredUnits != null)
        {
            for (int i = 0; i < runtime.offeredUnits.Count; i++)
            {
                UnitData unit = runtime.offeredUnits[i];
                if (unit == null || string.IsNullOrWhiteSpace(unit.id))
                    continue;
                result.offeredUnitIds.Add(unit.id);
            }
        }

        if (runtime.offeredServices != null)
        {
            for (int i = 0; i < runtime.offeredServices.Count; i++)
            {
                ServiceData service = runtime.offeredServices[i];
                if (service == null || string.IsNullOrWhiteSpace(service.id))
                    continue;
                result.offeredServiceIds.Add(service.id);
            }
        }

        if (runtime.offeredSupplies != null)
        {
            for (int i = 0; i < runtime.offeredSupplies.Count; i++)
            {
                ConstructionSupplyOffer offer = runtime.offeredSupplies[i];
                if (offer == null || offer.supply == null || string.IsNullOrWhiteSpace(offer.supply.id))
                    continue;
                result.offeredSupplies.Add(new RuntimeSupplySaveData
                {
                    supplyId = offer.supply.id,
                    quantity = Mathf.Max(0, offer.quantity)
                });
            }
        }

        return result;
    }

    private ConstructionSiteRuntime BuildSiteRuntimeFromSaveData(ConstructionSiteRuntimeSaveData saved)
    {
        if (saved == null)
            return null;

        ConstructionSiteRuntime runtime = new ConstructionSiteRuntime
        {
            isPlayerHeadQuarter = saved.isPlayerHeadQuarter,
            isCapturable = saved.isCapturable,
            capturePointsMax = Mathf.Max(0, saved.capturePointsMax),
            capturedIncoming = Mathf.Max(0, saved.capturedIncoming),
            sellingRule = System.Enum.IsDefined(typeof(ConstructionUnitMarketRule), saved.sellingRule)
                ? (ConstructionUnitMarketRule)saved.sellingRule
                : ConstructionUnitMarketRule.FreeMarket,
            canProvideSupplies = saved.canProvideSupplies,
            offeredUnits = new List<UnitData>(),
            offeredServices = new List<ServiceData>(),
            offeredSupplies = new List<ConstructionSupplyOffer>()
        };

        if (saved.offeredUnitIds != null)
        {
            for (int i = 0; i < saved.offeredUnitIds.Count; i++)
            {
                string id = saved.offeredUnitIds[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                if (unitSpawner != null && unitSpawner.TryGetUnitData(id, out UnitData unit) && unit != null)
                    runtime.offeredUnits.Add(unit);
            }
        }

        if (saved.offeredServiceIds != null)
        {
            for (int i = 0; i < saved.offeredServiceIds.Count; i++)
            {
                string id = saved.offeredServiceIds[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                ServiceData service = ResolveServiceById(id);
                if (service != null)
                    runtime.offeredServices.Add(service);
            }
        }

        if (saved.offeredSupplies != null)
        {
            for (int i = 0; i < saved.offeredSupplies.Count; i++)
            {
                RuntimeSupplySaveData entry = saved.offeredSupplies[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.supplyId))
                    continue;
                SupplyData supply = ResolveSupplyById(entry.supplyId);
                if (supply == null)
                    continue;
                runtime.offeredSupplies.Add(new ConstructionSupplyOffer
                {
                    supply = supply,
                    quantity = Mathf.Max(0, entry.quantity)
                });
            }
        }

        runtime.Sanitize();
        return runtime;
    }

    private ServiceData ResolveServiceById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (cachedServicesById.TryGetValue(id, out ServiceData cached) && cached != null)
            return cached;

        ServiceData[] loaded = Resources.FindObjectsOfTypeAll<ServiceData>();
        for (int i = 0; i < loaded.Length; i++)
        {
            ServiceData service = loaded[i];
            if (service == null || string.IsNullOrWhiteSpace(service.id))
                continue;
            if (!cachedServicesById.ContainsKey(service.id))
                cachedServicesById[service.id] = service;
        }

        cachedServicesById.TryGetValue(id, out ServiceData resolved);
        return resolved;
    }

    private SupplyData ResolveSupplyById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (cachedSuppliesById.TryGetValue(id, out SupplyData cached) && cached != null)
            return cached;

        SupplyData[] loaded = Resources.FindObjectsOfTypeAll<SupplyData>();
        for (int i = 0; i < loaded.Length; i++)
        {
            SupplyData supply = loaded[i];
            if (supply == null || string.IsNullOrWhiteSpace(supply.id))
                continue;
            if (!cachedSuppliesById.ContainsKey(supply.id))
                cachedSuppliesById[supply.id] = supply;
        }

        cachedSuppliesById.TryGetValue(id, out SupplyData resolved);
        return resolved;
    }
}

