using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[AddComponentMenu("AI/AI Intel Analyzer")]
public class AIIntelAnalyzer : MonoBehaviour
{
    [Header("Perspective")]
    public bool followActiveTeam = true;
    public TeamId aiTeam = TeamId.Green;
    [Min(1)] public int lookbackTurns = 4;
    [Min(0)] public int maxSectorSnapDistance = 5;

    [Header("Refresh")]
    public bool rebuildOnStart = true;
    public bool rebuildOnEnable = true;
    public bool rebuildOnActiveTeamChanged = true;
    public bool rebuildAfterLoad = true;
    [Min(0)] public int rebuildAfterLoadDelayFrames = 1;
    public bool rebuildEveryUpdate = false;

    [Header("Report")]
    [TextArea(5, 18)] public string resumo;
    public AIIntelReport report = new AIIntelReport();

    private void OnEnable()
    {
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
        SaveGameManager.OnAfterLoadSuccess += HandleAfterLoadSuccess;

        if (rebuildOnEnable)
            RebuildIntel();
    }

    private void OnDisable()
    {
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
        SaveGameManager.OnAfterLoadSuccess -= HandleAfterLoadSuccess;
    }

    private void Start()
    {
        if (rebuildOnStart)
            RebuildIntel();
    }

    private void Update()
    {
        if (rebuildEveryUpdate)
            RebuildIntel();
    }

    private void HandleActiveTeamChanged(int _)
    {
        if (rebuildOnActiveTeamChanged)
            RebuildIntel();
    }

    private void HandleAfterLoadSuccess()
    {
        if (!rebuildAfterLoad || !isActiveAndEnabled)
            return;

        StartCoroutine(RebuildAfterLoadRoutine());
    }

    private IEnumerator RebuildAfterLoadRoutine()
    {
        int frames = Mathf.Max(0, rebuildAfterLoadDelayFrames);
        for (int i = 0; i < frames; i++)
            yield return null;

        RebuildIntel();
    }

    [ContextMenu("Rebuild Intel Now")]
    public void RebuildIntel()
    {
        JogadasManager manager = JogadasManager.EnsureInstance();
        JogadasLog log = manager != null ? manager.log : null;
        MatchController match = FindFirstObjectByType<MatchController>();
        TeamId perspectiveTeam = ResolvePerspectiveTeam(match);
        int currentTurnOverride = match != null ? match.CurrentTurn : -1;

        aiTeam = perspectiveTeam;
        report = BuildReport(log, perspectiveTeam, lookbackTurns, maxSectorSnapDistance, currentTurnOverride);
        resumo = report != null ? report.BuildResumo() : string.Empty;
    }

    private TeamId ResolvePerspectiveTeam(MatchController match)
    {
        if (!followActiveTeam || match == null)
            return aiTeam;

        TeamId active = match.ActiveTeam;
        return active != TeamId.Neutral ? active : aiTeam;
    }

    public static AIIntelReport BuildReport(
        JogadasLog log,
        TeamId aiTeam,
        int lookbackTurns,
        int maxSectorSnapDistance = 5,
        int currentTurnOverride = -1)
    {
        AIIntelReport result = new AIIntelReport
        {
            aiTeam = aiTeam,
            lookbackTurns = Mathf.Max(1, lookbackTurns)
        };

        if (log == null || log.jogadas == null || log.jogadas.Count == 0)
        {
            result.currentTurn = Mathf.Max(0, currentTurnOverride);
            result.observacao = "Sem jogadas registradas.";
            return result;
        }

        int currentTurn = 0;
        for (int i = 0; i < log.jogadas.Count; i++)
            currentTurn = Mathf.Max(currentTurn, log.jogadas[i].turno);

        if (currentTurnOverride >= 0)
            currentTurn = Mathf.Max(currentTurn, currentTurnOverride);

        result.currentTurn = currentTurn;
        int minTurn = Mathf.Max(0, currentTurn - result.lookbackTurns + 1);

        Dictionary<int, AIUnitIntel> unitsByUid = new Dictionary<int, AIUnitIntel>();
        Dictionary<string, AISectorIntel> sectorsByName = new Dictionary<string, AISectorIntel>();
        Dictionary<string, AIPurchaseIntel> purchasesBySigla = new Dictionary<string, AIPurchaseIntel>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, UnitData> siglaMap = BuildSiglaToUnitDataMap();

        for (int i = 0; i < log.jogadas.Count; i++)
        {
            Jogada jogada = log.jogadas[i];
            if (jogada == null)
                continue;

            bool recent = jogada.turno >= minTurn;
            bool friendly = jogada.team == (int)aiTeam;
            bool enemy = IsEnemyTeam(jogada.team, aiTeam);

            if (jogada.uid > 0)
            {
                AIUnitIntel unit = GetOrCreateUnit(unitsByUid, jogada.uid);
                unit.uid = jogada.uid;
                unit.team = jogada.team;
                unit.sigla = CleanSigla(jogada.unidadeSigla);
                unit.lastAction = jogada.acao;
                unit.lastSeenTurn = jogada.turno;
                unit.lastKnownCell = ResolveRelevantCell(jogada);
                unit.lastKnownSector = ResolveSectorName(unit.lastKnownCell, maxSectorSnapDistance);
                unit.destroyed = string.Equals(jogada.acao, "Destruir", StringComparison.OrdinalIgnoreCase);
                unit.confidence = recent ? 1f : 0.35f;
            }

            if (!recent)
                continue;

            float weight = RecencyWeight(currentTurn, jogada.turno);
            Vector3Int relevantCell = ResolveRelevantCell(jogada);
            string sectorName = ResolveSectorName(relevantCell, maxSectorSnapDistance);
            AISectorIntel sector = GetOrCreateSector(sectorsByName, sectorName);
            sector.sector = sectorName;
            sector.lastTurn = Mathf.Max(sector.lastTurn, jogada.turno);

            if (friendly)
            {
                result.friendlyActions++;
                sector.friendlyActivity += weight;
            }
            else if (enemy)
            {
                result.enemyActions++;
                sector.enemyActivity += weight;
                sector.enemyPresence += EnemyPresenceWeight(jogada.acao) * weight;
            }

            sector.totalActivity += weight;
            ApplyActionScores(result, sector, jogada, aiTeam, unitsByUid, purchasesBySigla, siglaMap, weight);
        }

        AppendLiveCounts(result, aiTeam);
        result.enemyMomentum = result.enemyActions - result.friendlyActions;
        result.numericalPressure = result.enemyKnownUnits - result.friendlyKnownUnits;

        result.sectors.AddRange(sectorsByName.Values);
        result.sectors.Sort((a, b) => b.hotScore.CompareTo(a.hotScore));

        foreach (AIUnitIntel unit in unitsByUid.Values)
        {
            if (IsEnemyTeam(unit.team, aiTeam))
                result.enemyLastKnownUnits.Add(unit);
        }
        result.enemyLastKnownUnits.Sort((a, b) => b.lastSeenTurn.CompareTo(a.lastSeenTurn));

        result.enemyPurchases.AddRange(purchasesBySigla.Values);
        result.enemyPurchases.Sort((a, b) => b.threatScore.CompareTo(a.threatScore));

        result.observacao = result.enemyActions == 0 && result.friendlyActions == 0
            ? "Sem jogadas recentes na janela configurada."
            : string.Empty;

        return result;
    }

    private static void ApplyActionScores(
        AIIntelReport result,
        AISectorIntel sector,
        Jogada jogada,
        TeamId aiTeam,
        Dictionary<int, AIUnitIntel> unitsByUid,
        Dictionary<string, AIPurchaseIntel> purchasesBySigla,
        Dictionary<string, UnitData> siglaMap,
        float weight)
    {
        bool friendly = jogada.team == (int)aiTeam;
        bool enemy = IsEnemyTeam(jogada.team, aiTeam);
        string action = jogada.acao ?? string.Empty;

        if (string.Equals(action, "Ataque", StringComparison.OrdinalIgnoreCase))
        {
            AIUnitIntel target = null;
            bool targetKnown = jogada.uid2 > 0 && unitsByUid.TryGetValue(jogada.uid2, out target);
            bool hitFriendly = enemy && targetKnown && target.team == (int)aiTeam;

            if (friendly)
            {
                sector.damageDealt += 3f * weight;
                result.damageDealtScore += 3f * weight;
            }
            else if (hitFriendly || enemy)
            {
                sector.damageTaken += 3f * weight;
                result.damageTakenScore += 3f * weight;
            }
        }
        else if (string.Equals(action, "Capturar", StringComparison.OrdinalIgnoreCase))
        {
            if (enemy)
            {
                sector.capturePressure += 3f * weight;
                result.capturePressure += 3f * weight;
            }
        }
        else if (string.Equals(action, "Desembarque", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(action, "Desembarcando", StringComparison.OrdinalIgnoreCase))
        {
            if (enemy)
            {
                sector.landingPressure += 2.5f * weight;
                result.landingPressure += 2.5f * weight;
            }
        }
        else if (string.Equals(action, "Compra", StringComparison.OrdinalIgnoreCase))
        {
            if (friendly)
                result.friendlyPurchasesRecent++;
            else if (enemy)
            {
                result.enemyPurchasesRecent++;
                string sigla = CleanSigla(jogada.unidadeSigla);
                AIPurchaseIntel purchase = GetOrCreatePurchase(purchasesBySigla, sigla);
                purchase.sigla = sigla;
                purchase.count++;
                purchase.lastTurn = Mathf.Max(purchase.lastTurn, jogada.turno);

                float threat = ClassifyPurchaseThreat(sigla, siglaMap, out bool elite, out bool air, out bool armor, out bool artillery, out bool infantry);
                purchase.threatScore += threat * weight;
                purchase.elite = purchase.elite || elite;
                purchase.air = purchase.air || air;
                purchase.armor = purchase.armor || armor;
                purchase.artillery = purchase.artillery || artillery;
                purchase.infantry = purchase.infantry || infantry;

                result.enemyElitePurchaseScore += elite ? 3f * weight : 0f;
                result.enemyAirThreatScore += air ? 2f * weight : 0f;
                result.enemyArmorThreatScore += armor ? 2f * weight : 0f;
                result.enemyArtilleryThreatScore += artillery ? 2f * weight : 0f;
                result.enemyInfantryPressureScore += infantry ? 1f * weight : 0f;
            }
        }
        else if (string.Equals(action, "Destruir", StringComparison.OrdinalIgnoreCase))
        {
            if (friendly)
                result.friendlyDestroyedRecent++;
            else if (enemy)
                result.enemyDestroyedRecent++;
        }

        sector.hotScore = sector.enemyActivity + sector.damageTaken + sector.capturePressure + sector.landingPressure + sector.enemyPresence;
    }

    private static void AppendLiveCounts(AIIntelReport result, TeamId aiTeam)
    {
        List<UnitManager> units = UnitManager.AllActive;
        if (units == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;

            if (unit.TeamId == aiTeam)
            {
                result.friendlyKnownUnits++;
                if (unit.TryGetUnitData(out UnitData data) && data != null)
                {
                    if (data.domain == Domain.Air)
                        result.friendlyAirCount++;
                    if (data.roles != null)
                    {
                        if (data.roles.Contains(UnitRole.Capturador))  result.friendlyCapturerCount++;
                        if (data.roles.Count > 0 && data.roles[0] == UnitRole.Assalto) result.friendlyAssaultCount++;
                        if (data.roles.Contains(UnitRole.FogoIndireto)) result.friendlyFireSupportCount++;
                        if (data.roles.Contains(UnitRole.Transportador)) result.friendlyTransportCount++;
                    }
                }
            }
            else if (unit.TeamId != TeamId.Neutral)
                result.enemyKnownUnits++;
        }
    }

    private static AIUnitIntel GetOrCreateUnit(Dictionary<int, AIUnitIntel> map, int uid)
    {
        if (!map.TryGetValue(uid, out AIUnitIntel unit) || unit == null)
        {
            unit = new AIUnitIntel();
            map[uid] = unit;
        }
        return unit;
    }

    private static AISectorIntel GetOrCreateSector(Dictionary<string, AISectorIntel> map, string sectorName)
    {
        if (!map.TryGetValue(sectorName, out AISectorIntel sector) || sector == null)
        {
            sector = new AISectorIntel();
            map[sectorName] = sector;
        }
        return sector;
    }

    private static AIPurchaseIntel GetOrCreatePurchase(Dictionary<string, AIPurchaseIntel> map, string sigla)
    {
        if (!map.TryGetValue(sigla, out AIPurchaseIntel purchase) || purchase == null)
        {
            purchase = new AIPurchaseIntel();
            map[sigla] = purchase;
        }
        return purchase;
    }

    private static bool IsEnemyTeam(int team, TeamId aiTeam)
    {
        return team >= 0 && team != (int)aiTeam;
    }

    private static Vector3Int ResolveRelevantCell(Jogada jogada)
    {
        if (jogada == null)
            return Vector3Int.zero;

        string action = jogada.acao ?? string.Empty;
        if (string.Equals(action, "Mover", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "Capturar", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "Desembarque", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "Desembarcando", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "Suprir", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "Fusao", StringComparison.OrdinalIgnoreCase))
        {
            return new Vector3Int(jogada.dx, jogada.dy, 0);
        }

        if (string.Equals(action, "Ataque", StringComparison.OrdinalIgnoreCase))
            return new Vector3Int(jogada.dx, jogada.dy, 0);

        return new Vector3Int(jogada.cx, jogada.cy, 0);
    }

    private static string ResolveSectorName(Vector3Int cell, int maxSectorSnapDistance)
    {
        ConstructionManager best = null;
        float bestDist = float.PositiveInfinity;
        List<ConstructionManager> constructions = ConstructionManager.AllActive;

        if (constructions != null)
        {
            for (int i = 0; i < constructions.Count; i++)
            {
                ConstructionManager construction = constructions[i];
                if (construction == null)
                    continue;

                Vector3Int c = construction.CurrentCellPosition;
                c.z = 0;
                if (c == cell)
                    return construction.Sector.ToString();

                float dist = SectorManager.HexDistance(cell, c);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = construction;
                }
            }
        }

        if (best != null && bestDist <= Mathf.Max(0, maxSectorSnapDistance))
            return best.Sector.ToString();

        return "SemSetor";
    }

    private static float RecencyWeight(int currentTurn, int turn)
    {
        int age = Mathf.Max(0, currentTurn - turn);
        if (age == 0) return 1f;
        if (age == 1) return 0.75f;
        if (age == 2) return 0.5f;
        return 0.25f;
    }

    private static float EnemyPresenceWeight(string action)
    {
        if (string.Equals(action, "Ataque", StringComparison.OrdinalIgnoreCase)) return 2.5f;
        if (string.Equals(action, "Desembarque", StringComparison.OrdinalIgnoreCase)) return 2f;
        if (string.Equals(action, "Desembarcando", StringComparison.OrdinalIgnoreCase)) return 2f;
        if (string.Equals(action, "Capturar", StringComparison.OrdinalIgnoreCase)) return 2f;
        if (string.Equals(action, "Mover", StringComparison.OrdinalIgnoreCase)) return 1f;
        if (string.Equals(action, "Estacionario", StringComparison.OrdinalIgnoreCase)) return 0.75f;
        if (string.Equals(action, "Suprir", StringComparison.OrdinalIgnoreCase)) return 0.75f;
        return 0.25f;
    }

    private static string CleanSigla(string sigla)
    {
        return string.IsNullOrWhiteSpace(sigla) ? "-" : sigla.Trim();
    }

    private static Dictionary<string, UnitData> BuildSiglaToUnitDataMap()
    {
        var map = new Dictionary<string, UnitData>(StringComparer.OrdinalIgnoreCase);
        UnitData[] all = Resources.FindObjectsOfTypeAll<UnitData>();
        for (int i = 0; i < all.Length; i++)
        {
            UnitData ud = all[i];
            if (ud == null || string.IsNullOrWhiteSpace(ud.apelido)) continue;
            string key = ud.apelido.Trim();
            if (!map.ContainsKey(key))
                map[key] = ud;
        }
        return map;
    }

    private static float ClassifyPurchaseThreat(
        string sigla,
        Dictionary<string, UnitData> siglaMap,
        out bool elite,
        out bool air,
        out bool armor,
        out bool artillery,
        out bool infantry)
    {
        if (siglaMap != null && siglaMap.TryGetValue(sigla?.Trim() ?? string.Empty, out UnitData ud) && ud != null)
        {
            bool hasRoles = ud.roles != null && ud.roles.Count > 0;
            air       = ud.domain == Domain.Air;
            armor     = ud.unitClass == GameUnitClass.Armored;
            artillery = hasRoles && ud.roles.Contains(UnitRole.FogoIndireto);
            infantry  = hasRoles && ud.roles.Contains(UnitRole.Capturador);
            elite     = ud.eliteLevel > 0;

            float score = 1f;
            if (elite)     score += 3f;
            if (air)       score += 2f;
            if (armor)     score += 2f;
            if (artillery) score += 1.5f;
            if (infantry)  score += 0.5f;
            return score;
        }

        // Fallback: pattern matching for unknown siglas
        string s = (sigla ?? string.Empty).Trim().ToUpperInvariant();
        elite     = s.Contains("MBT") || s.Contains("TP") || s.Contains("PESADO") || s.Contains("APACHE") ||
                    s.Contains("BOMBAR") || s.Contains("CA") || s.Contains("SAM");
        air       = s.Contains("APACHE") || s.Contains("CA") || s.Contains("BOMBAR") || s.Contains("CHINOOK");
        armor     = s.Contains("MBT") || s.Contains("TQ") || s.Contains("TANQ") || s.Contains("TP") ||
                    s.Contains("APC") || s.Contains("IFV");
        artillery = s.Contains("OBUS") || s.Contains("ART") || s.Contains("SAM") || s.Contains("AAA");
        infantry  = s == "SD" || s.Contains("SOLD") || s.Contains("INF") || s.Contains("BZ");

        float fallback = 1f;
        if (elite)     fallback += 3f;
        if (air)       fallback += 2f;
        if (armor)     fallback += 2f;
        if (artillery) fallback += 1.5f;
        if (infantry)  fallback += 0.5f;
        return fallback;
    }
}

[Serializable]
public class AIIntelReport
{
    public TeamId aiTeam;
    public int currentTurn;
    public int lookbackTurns;
    public string observacao;

    [Header("Momentum")]
    public int friendlyActions;
    public int enemyActions;
    public float enemyMomentum;
    public float numericalPressure;

    [Header("Known Counts")]
    public int friendlyKnownUnits;
    public int enemyKnownUnits;
    public int friendlyPurchasesRecent;
    public int enemyPurchasesRecent;
    public int friendlyDestroyedRecent;
    public int enemyDestroyedRecent;

    [Header("Own Composition")]
    public int friendlyCapturerCount;
    public int friendlyAssaultCount;
    public int friendlyFireSupportCount;
    public int friendlyTransportCount;
    public int friendlyAirCount;

    [Header("Threat Scores")]
    public float damageTakenScore;
    public float damageDealtScore;
    public float capturePressure;
    public float landingPressure;
    public float enemyElitePurchaseScore;
    public float enemyAirThreatScore;
    public float enemyArmorThreatScore;
    public float enemyArtilleryThreatScore;
    public float enemyInfantryPressureScore;

    public List<AISectorIntel> sectors = new List<AISectorIntel>();
    public List<AIUnitIntel> enemyLastKnownUnits = new List<AIUnitIntel>();
    public List<AIPurchaseIntel> enemyPurchases = new List<AIPurchaseIntel>();

    public string BuildResumo()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"AI Intel: {aiTeam} | T{currentTurn} | janela={lookbackTurns}");
        if (!string.IsNullOrWhiteSpace(observacao))
            sb.AppendLine(observacao);

        sb.AppendLine($"Atividade recente: propria={friendlyActions} eventos inimiga={enemyActions} eventos iniciativaInimiga={enemyMomentum:0.0}");
        sb.AppendLine($"Forca conhecida: aliado={friendlyKnownUnits} inimigo={enemyKnownUnits} pressaoNumerica={numericalPressure:0.0}");
        sb.AppendLine($"Reforcos recentes: aliado={friendlyPurchasesRecent} inimigo={enemyPurchasesRecent}");
        sb.AppendLine($"Pressao operacional: danoTomado={damageTakenScore:0.0} danoCausado={damageDealtScore:0.0} capturaInimiga={capturePressure:0.0} desembarqueInimigo={landingPressure:0.0}");
        sb.AppendLine($"Compras inimigas: elite={enemyElitePurchaseScore:0.0} ar={enemyAirThreatScore:0.0} blindado={enemyArmorThreatScore:0.0} artilharia={enemyArtilleryThreatScore:0.0} infantaria={enemyInfantryPressureScore:0.0}");

        if (sectors != null && sectors.Count > 0)
        {
            sb.AppendLine("Setores quentes:");
            int count = Mathf.Min(5, sectors.Count);
            for (int i = 0; i < count; i++)
            {
                AISectorIntel s = sectors[i];
                string zone = ResolveSectorZoneLabel(aiTeam, s.sector);
                sb.AppendLine($"- {s.sector}: zone={zone} hot={s.hotScore:0.0} atividadeInimiga={s.enemyActivity:0.0} presencaInferida={s.enemyPresence:0.0} dano={s.damageTaken:0.0} captura={s.capturePressure:0.0} landing={s.landingPressure:0.0}");
            }
        }

        if (enemyPurchases != null && enemyPurchases.Count > 0)
        {
            sb.AppendLine("Compras inimigas:");
            int count = Mathf.Min(5, enemyPurchases.Count);
            for (int i = 0; i < count; i++)
            {
                AIPurchaseIntel p = enemyPurchases[i];
                sb.AppendLine($"- {p.sigla} x{p.count} threat={p.threatScore:0.0} T{p.lastTurn}");
            }
        }

        sb.AppendLine($"Composicao propria: cap={friendlyCapturerCount} ass={friendlyAssaultCount} fogo={friendlyFireSupportCount} trans={friendlyTransportCount} ar={friendlyAirCount}");
        var lacunas = new System.Collections.Generic.List<string>();
        if (friendlyFireSupportCount == 0) lacunas.Add(enemyArtilleryThreatScore > 0 ? "!sem_fogo_indireto(inimigo_tem)" : "sem_fogo_indireto");
        if (friendlyAssaultCount == 0)     lacunas.Add("!sem_assalto");
        if (friendlyAirCount == 0 && enemyAirThreatScore > 0) lacunas.Add("!sem_ar(inimigo_tem)");
        if (friendlyTransportCount == 0 && friendlyCapturerCount >= 3) lacunas.Add("sem_transporte");
        if (lacunas.Count > 0)
            sb.AppendLine($"Lacunas: {string.Join("  ", lacunas)}");

        return sb.ToString();
    }

    private static string ResolveSectorZoneLabel(TeamId team, string sectorName)
    {
        if (string.IsNullOrWhiteSpace(sectorName)
            || !Enum.TryParse(sectorName, out ConstructionSector sector))
            return "-";

        SectorManager.SectorInfo info = null;
        if (!SectorManager.TryGetSectorInfo(sector, out info)
            && !SectorManager.TryGetBaseInfo(sector, out info))
            return "-";

        return AISectorIntentAnalyzer.ClassifyRelation(team, info).ToString();
    }
}

[Serializable]
public class AISectorIntel
{
    public string sector;
    public int lastTurn;
    public float hotScore;
    public float totalActivity;
    public float friendlyActivity;
    public float enemyActivity;
    public float enemyPresence;
    public float damageTaken;
    public float damageDealt;
    public float capturePressure;
    public float landingPressure;
}

[Serializable]
public class AIUnitIntel
{
    public int uid;
    public int team;
    public string sigla;
    public string lastAction;
    public int lastSeenTurn;
    public Vector3Int lastKnownCell;
    public string lastKnownSector;
    public float confidence;
    public bool destroyed;
}

[Serializable]
public class AIPurchaseIntel
{
    public string sigla;
    public int count;
    public int lastTurn;
    public float threatScore;
    public bool elite;
    public bool air;
    public bool armor;
    public bool artillery;
    public bool infantry;
}
