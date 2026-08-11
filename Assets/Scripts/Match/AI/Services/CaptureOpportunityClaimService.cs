using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Reivindicacao provisoria produzida pelo planejamento coletivo de captura.
/// Nao ocupa construcao nem altera a verdade confirmada do tabuleiro. Vive na
/// fotografia do plano e e substituida somente quando o plano e republicado.
/// </summary>
public readonly struct CaptureOpportunityClaim
{
    public readonly ConstructionManager Construction;
    public readonly UnitManager Capturer;
    public readonly int RouteCost;
    public readonly int AssignmentCost;
    public readonly int SwitchCost;
    public readonly bool FormalPlan;

    public CaptureOpportunityClaim(
        ConstructionManager construction,
        UnitManager capturer,
        int routeCost,
        int assignmentCost,
        int switchCost,
        bool formalPlan)
    {
        Construction = construction;
        Capturer = capturer;
        RouteCost = routeCost;
        AssignmentCost = assignmentCost;
        SwitchCost = switchCost;
        FormalPlan = formalPlan;
    }
}

public enum CaptureOpportunityUnmatchedReason
{
    NoReachableOpportunity,
    ReservedByOtherCapturer,
    BlockedByFormalPlan
}

public readonly struct CaptureOpportunityUnmatched
{
    public readonly UnitManager Capturer;
    public readonly CaptureOpportunityUnmatchedReason Reason;
    public readonly ConstructionManager MagneticTarget;

    public CaptureOpportunityUnmatched(
        UnitManager capturer,
        CaptureOpportunityUnmatchedReason reason,
        ConstructionManager magneticTarget)
    {
        Capturer = capturer;
        Reason = reason;
        MagneticTarget = magneticTarget;
    }
}

/// <summary>
/// Entrada ja avaliada pelo MelhorCaptura. O solve coletivo nao mede alcance:
/// recebe uma lista de preferencias por sujeito e apenas faz o pareamento 1:1.
/// </summary>
public sealed class CaptureOpportunityGroupCandidate
{
    public UnitManager Unit;
    public bool FormalPlan;
    public ConstructionSector FormalSector;
    public IReadOnlyList<MelhorCapturaAlvoScore> Ranking;
}

public sealed class CaptureOpportunityClaimSnapshot
{
    public readonly int StateHash;
    public readonly int ConfirmedRevision;

    private readonly Dictionary<int, CaptureOpportunityClaim>
        claimsByConstructionId;
    private readonly Dictionary<int, CaptureOpportunityClaim>
        claimsByCapturerId;
    private readonly List<CaptureOpportunityClaim> claims;
    private readonly Dictionary<int, CaptureOpportunityUnmatched>
        unmatchedByCapturerId;
    private readonly List<CaptureOpportunityUnmatched> unmatched;

    public IReadOnlyList<CaptureOpportunityClaim> Claims => claims;
    public IReadOnlyList<CaptureOpportunityUnmatched> Unmatched => unmatched;

    internal CaptureOpportunityClaimSnapshot(
        int stateHash,
        int confirmedRevision,
        Dictionary<int, CaptureOpportunityClaim>
            claimsByConstructionId,
        IEnumerable<CaptureOpportunityUnmatched> unmatched = null)
    {
        StateHash = stateHash;
        ConfirmedRevision = confirmedRevision;
        this.claimsByConstructionId =
            claimsByConstructionId
            ?? new Dictionary<int, CaptureOpportunityClaim>();

        // Indice reverso. O matching ja decidiu UM alvo por capturador; sem
        // este lado da tabela, quem quer saber "o que sobrou pra mim?" e
        // obrigado a re-derivar o alvo por conta propria — foi assim que
        // nasceram duas resolucoes independentes para a mesma unidade.
        claimsByCapturerId =
            new Dictionary<int, CaptureOpportunityClaim>(
                this.claimsByConstructionId.Count);
        foreach (CaptureOpportunityClaim claim
                 in this.claimsByConstructionId.Values)
        {
            if (claim.Capturer != null)
                claimsByCapturerId[claim.Capturer.InstanceId] = claim;
        }

        claims = new List<CaptureOpportunityClaim>(
            this.claimsByConstructionId.Values);
        claims.Sort((left, right) =>
        {
            int leftId = left.Capturer != null
                ? left.Capturer.InstanceId
                : int.MaxValue;
            int rightId = right.Capturer != null
                ? right.Capturer.InstanceId
                : int.MaxValue;
            return leftId.CompareTo(rightId);
        });

        this.unmatched = unmatched != null
            ? new List<CaptureOpportunityUnmatched>(unmatched)
            : new List<CaptureOpportunityUnmatched>();
        this.unmatched.Sort((left, right) =>
        {
            int leftId = left.Capturer != null
                ? left.Capturer.InstanceId
                : int.MaxValue;
            int rightId = right.Capturer != null
                ? right.Capturer.InstanceId
                : int.MaxValue;
            return leftId.CompareTo(rightId);
        });
        unmatchedByCapturerId =
            new Dictionary<int, CaptureOpportunityUnmatched>(
                this.unmatched.Count);
        for (int i = 0; i < this.unmatched.Count; i++)
        {
            CaptureOpportunityUnmatched item = this.unmatched[i];
            if (item.Capturer != null)
                unmatchedByCapturerId[item.Capturer.InstanceId] = item;
        }
    }

    /// <summary>
    /// O alvo que o matching reservou para ESTA unidade.
    ///
    /// E a alocacao, nao uma sugestao: quem pergunta nao precisa (nem deve)
    /// escolher de novo. Falso quando a unidade nao recebeu alvo — sem
    /// capturavel na banda, ou tudo o que alcancava ja tinha dono.
    /// </summary>
    public bool TryGetClaimForUnit(
        UnitManager unit,
        out CaptureOpportunityClaim claim)
    {
        claim = default;
        return unit != null
            && claimsByCapturerId.TryGetValue(
                unit.InstanceId, out claim);
    }

    public bool TryGetClaim(
        ConstructionManager construction,
        out CaptureOpportunityClaim claim)
    {
        claim = default;
        return construction != null
            && claimsByConstructionId.TryGetValue(
                construction.InstanceId,
                out claim);
    }

    public bool TryGetUnmatched(
        UnitManager unit,
        out CaptureOpportunityUnmatched item)
    {
        item = default;
        return unit != null
            && unmatchedByCapturerId.TryGetValue(
                unit.InstanceId, out item);
    }
}

/// <summary>
/// Distribui construcoes alcancaveis entre capturadores SEM PLANO do mesmo
/// slot. A IA com HQ publica primeiro as ordens formais e entrega a este
/// servico apenas os RogueUnitIds; a IA sem HQ nao produz plano, portanto todos
/// os seus capturadores entram pelo mesmo caminho rogue. Alvos formais ja
/// publicados saem do conjunto antes do matching, mas podem continuar servindo
/// como referencia magnetica para quem ficou sem par.
///
/// O matching deterministico maximiza o numero de rogues com alvo e, nessa
/// cardinalidade, minimiza o custo total. Permanecer no alvo anterior recebe
/// histerese; papel e identidade servem apenas como desempate estavel.
///
/// POLITICA, NAO ALCANCE. Elegibilidade rogue, matching 1:1 e desempate sao
/// deste servico. Quao longe cada capturador chega e de quanto custa a rota vem
/// do UnitReachEnvelopeService, na intencao Capture: o envelope devolve so
/// alcance, sem consultar PodeCapturar. Ver docs/contrato_envelope_alcance.md.
/// </summary>
public static class CaptureOpportunityClaimService
{
    private const int MaxCacheEntries = 8;
    private const int CaptureTargetSwitchCost = 15;
    private const long PrimaryCostScale = 1000000L;

    private sealed class PublishedClaims
    {
        public int MapObjectId;
        public int TerrainDatabaseObjectId;
        public int OperationalTurns;
        public TeamObjectivePlan Plan;
        public CaptureOpportunityClaimSnapshot Snapshot;
    }

    private readonly struct CacheKey : IEquatable<CacheKey>
    {
        public readonly int MapObjectId;
        public readonly int TerrainDatabaseObjectId;
        public readonly int SlotIndex;
        public readonly int OperationalTurns;
        public readonly int ConfirmedRevision;
        public readonly int StateHash;

        public CacheKey(
            QueroCaronaRequest request,
            int confirmedRevision,
            int stateHash)
        {
            MapObjectId =
                request.map.GetEntityId().GetHashCode();
            TerrainDatabaseObjectId =
                request.terrainDatabase.GetEntityId().GetHashCode();
            SlotIndex = request.unit.SlotIndex;
            OperationalTurns = Mathf.Max(
                1, request.operationalTurns);
            ConfirmedRevision = confirmedRevision;
            StateHash = stateHash;
        }

        public bool Equals(CacheKey other)
        {
            return MapObjectId == other.MapObjectId
                && TerrainDatabaseObjectId
                    == other.TerrainDatabaseObjectId
                && SlotIndex == other.SlotIndex
                && OperationalTurns == other.OperationalTurns
                && ConfirmedRevision == other.ConfirmedRevision
                && StateHash == other.StateHash;
        }

        public override bool Equals(object obj)
        {
            return obj is CacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MapObjectId;
                hash = (hash * 397) ^ TerrainDatabaseObjectId;
                hash = (hash * 397) ^ SlotIndex;
                hash = (hash * 397) ^ OperationalTurns;
                hash = (hash * 397) ^ ConfirmedRevision;
                hash = (hash * 397) ^ StateHash;
                return hash;
            }
        }
    }

    private sealed class Edge
    {
        public ConstructionManager Construction;
        public int RouteCost;
        public int AssignmentCost;
        public int SwitchCost;
    }

    private sealed class Candidate
    {
        public UnitManager Unit;
        public bool FormalPlan;
        public ConstructionSector FormalSector;
        public ConstructionManager MagneticTarget;
        public readonly List<Edge> Edges = new List<Edge>();
    }

    private static readonly Dictionary<
        CacheKey,
        CaptureOpportunityClaimSnapshot> Cache =
            new Dictionary<
                CacheKey,
                CaptureOpportunityClaimSnapshot>();
    private static readonly Dictionary<int, PublishedClaims>
        PublishedBySlot = new Dictionary<int, PublishedClaims>();

    /// <summary>
    /// <paramref name="requestReach"/> e o envelope de Captura que o chamador
    /// ja construiu para <c>request.unit</c>. O reuso e por IDENTIDADE de
    /// envelope — mesma unidade, mesma intencao, mesma banda, mesmos turnos —
    /// e nao mais por igualdade de orcamento inteiro, que deixou de existir.
    /// </summary>
    public static CaptureOpportunityClaimSnapshot GetOrBuild(
        QueroCaronaRequest request,
        UnitReachProfile requestReach = null)
    {
        if (request?.unit == null
            || request.map == null
            || request.terrainDatabase == null)
        {
            return new CaptureOpportunityClaimSnapshot(
                0,
                -1,
                null);
        }

        int confirmedRevision =
            ResolveConfirmedRevision(request.map);
        TeamObjectivePlan plan =
            ObjectiveManager.GetPlanForSlot(
                PlayerSlotId.FromIndex(request.unit.SlotIndex));
        if (TryGetPublished(
                request.unit.SlotIndex,
                request.map,
                request.terrainDatabase,
                request.operationalTurns,
                plan,
                out CaptureOpportunityClaimSnapshot published))
        {
            AIDecisionPerf.AddCount("CaptureClaimPublishedHits");
            return published;
        }
        int stateHash = BuildStateHash(
            request.unit.SlotIndex,
            plan);
        var key = new CacheKey(
            request,
            confirmedRevision,
            stateHash);
        if (Cache.TryGetValue(
                key,
                out CaptureOpportunityClaimSnapshot cached))
        {
            AIDecisionPerf.AddCount(
                "CaptureClaimSnapshotHits");
            return cached;
        }

        AIDecisionPerf.AddCount(
            "CaptureClaimSnapshotBuilds");
        CaptureOpportunityClaimSnapshot built = Build(
            request,
            plan,
            confirmedRevision,
            stateHash,
            requestReach,
            previousSnapshot: null);
        if (Cache.Count >= MaxCacheEntries)
            Cache.Clear();
        Cache[key] = built;
        return built;
    }

    /// <summary>
    /// Resolve e publica UMA tabela para o plano terminado. Consumidores da
    /// fase leem esta fotografia mesmo que unidades ajam depois: movimento e
    /// HasActed nao podem permutar as N reservas no meio da execucao.
    /// </summary>
    public static CaptureOpportunityClaimSnapshot PublishForPlan(
        PlayerSlotId slot,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        int operationalTurns,
        TeamObjectivePlan plan)
    {
        if (map == null || terrainDatabase == null)
        {
            ClearPublishedForSlot(slot);
            return new CaptureOpportunityClaimSnapshot(0, -1, null);
        }

        UnitManager subject = null;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (IsEligibleCapturer(unit, slot.Value))
            {
                subject = unit;
                break;
            }
        }

        int stateHash = BuildStateHash(slot.Value, plan);
        int confirmedRevision = ResolveConfirmedRevision(map);
        PublishedBySlot.TryGetValue(
            slot.Value, out PublishedClaims previousPublication);
        int mapObjectId = map.GetEntityId().GetHashCode();
        int terrainDatabaseObjectId =
            terrainDatabase.GetEntityId().GetHashCode();
        CaptureOpportunityClaimSnapshot previous =
            previousPublication != null
            && previousPublication.MapObjectId == mapObjectId
            && previousPublication.TerrainDatabaseObjectId
                == terrainDatabaseObjectId
                ? previousPublication.Snapshot
                : null;

        CaptureOpportunityClaimSnapshot snapshot;
        if (subject == null)
        {
            snapshot = new CaptureOpportunityClaimSnapshot(
                stateHash, confirmedRevision, null);
        }
        else
        {
            var request = new QueroCaronaRequest
            {
                unit = subject,
                map = map,
                terrainDatabase = terrainDatabase,
                operationalTurns = Mathf.Max(1, operationalTurns)
            };
            snapshot = Build(
                request,
                plan,
                confirmedRevision,
                stateHash,
                requestReach: null,
                previousSnapshot: previous);
        }

        PublishedBySlot[slot.Value] = new PublishedClaims
        {
            MapObjectId = mapObjectId,
            TerrainDatabaseObjectId = terrainDatabaseObjectId,
            OperationalTurns = Mathf.Max(1, operationalTurns),
            Plan = plan,
            Snapshot = snapshot
        };
        PublishCaptureMissionIntents(slot.Value, snapshot);
        return snapshot;
    }

    /// <summary>
    /// Materializa a fotografia coletiva na ficha de cada capturador. Claim e
    /// missao sao a mesma decisao vista por dois consumidores: o matching usa
    /// a tabela 1:1; transporte, save e iniciativa leem Mission Intent.
    ///
    /// Isto roda no planejamento do turno, sobre estado confirmado, antes de
    /// qualquer preview de acao. Nao ocupa construcao, nao move unidade e nao
    /// altera a verdade do tabuleiro.
    ///
    /// Quem ficou sem par perde SOMENTE uma antiga missao Capture. Outros
    /// verbos pertencem a outros fluxos e nunca sao apagados aqui. Unidade
    /// embarcada/repair nao participa do novo solve e conserva sua missao para
    /// que a entrega possa continuar apontando ao destino original.
    /// </summary>
    private static void PublishCaptureMissionIntents(
        int slotIndex,
        CaptureOpportunityClaimSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        IReadOnlyList<CaptureOpportunityClaim> claims =
            snapshot.Claims;
        for (int i = 0; i < claims.Count; i++)
        {
            CaptureOpportunityClaim claim = claims[i];
            UnitManager unit = claim.Capturer;
            ConstructionManager construction = claim.Construction;
            if (unit == null
                || construction == null
                || unit.SlotIndex != slotIndex
                || unit.IsDead)
            {
                continue;
            }

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            bool kept = unit.AIHasDesignatedCaptureTarget
                && unit.AIDesignatedCaptureTargetInstanceId
                    == construction.InstanceId
                && unit.AIDesignatedCaptureTargetCell == cell;
            if (!unit.SetAIDesignatedCaptureTarget(
                    construction.InstanceId,
                    cell))
            {
                // IsEligibleCapturer exclui outro verbo antes do matching. A
                // guarda fica para proteger contra mudanca de estado entre o
                // solve e a publicacao.
                Debug.LogWarning(
                    $"[CaptureClaim] unidade #{unit.InstanceId} recebeu " +
                    $"claim de {construction.ConstructionDisplayName}@{cell}, " +
                    $"mas preservou missao {unit.AIDesignatedMissionIntent}.");
                continue;
            }

            if (!kept)
            {
                Debug.Log(
                    $"[Missao] #{unit.InstanceId} Capture -> {cell} " +
                    $"predio=#{construction.InstanceId} " +
                    $"({(claim.FormalPlan ? "plano" : "oportunidade")}, " +
                    $"matcher 1:1, custo={claim.RouteCost}).");
            }
        }

        IReadOnlyList<CaptureOpportunityUnmatched> unmatched =
            snapshot.Unmatched;
        for (int i = 0; i < unmatched.Count; i++)
        {
            CaptureOpportunityUnmatched item = unmatched[i];
            UnitManager unit = item.Capturer;
            if (unit == null
                || unit.SlotIndex != slotIndex
                || !unit.AIHasDesignatedCaptureTarget)
            {
                continue;
            }

            int previousTarget =
                unit.AIDesignatedCaptureTargetInstanceId;
            unit.ClearAIDesignatedCaptureTarget();
            string magnetic = item.MagneticTarget != null
                ? $" magnetico=#{item.MagneticTarget.InstanceId}"
                : string.Empty;
            Debug.Log(
                $"[Missao] #{unit.InstanceId} Capture predio=" +
                $"#{previousTarget} BAIXA: sem par ({item.Reason});" +
                magnetic);
        }
    }

    public static bool TryGetPublishedForSlot(
        PlayerSlotId slot,
        out CaptureOpportunityClaimSnapshot snapshot)
    {
        snapshot = null;
        if (!PublishedBySlot.TryGetValue(
                slot.Value, out PublishedClaims published)
            || published?.Snapshot == null)
        {
            return false;
        }
        snapshot = published.Snapshot;
        return true;
    }

    public static void ClearPublishedForSlot(PlayerSlotId slot)
    {
        PublishedBySlot.Remove(slot.Value);
    }

    private static bool TryGetPublished(
        int slotIndex,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        int operationalTurns,
        TeamObjectivePlan plan,
        out CaptureOpportunityClaimSnapshot snapshot)
    {
        snapshot = null;
        if (!PublishedBySlot.TryGetValue(
                slotIndex, out PublishedClaims published)
            || published == null
            || published.Snapshot == null
            || published.Plan != plan
            || published.MapObjectId
                != map.GetEntityId().GetHashCode()
            || published.TerrainDatabaseObjectId
                != terrainDatabase.GetEntityId().GetHashCode()
            || published.OperationalTurns
                != Mathf.Max(1, operationalTurns))
        {
            return false;
        }
        snapshot = published.Snapshot;
        return true;
    }

    public static int ResolveStateHash(
        QueroCaronaRequest request)
    {
        if (request?.unit == null)
            return 0;
        TeamObjectivePlan plan =
            ObjectiveManager.GetPlanForSlot(
                PlayerSlotId.FromIndex(request.unit.SlotIndex));
        return BuildStateHash(
            request.unit.SlotIndex,
            plan);
    }

    private static CaptureOpportunityClaimSnapshot Build(
        QueroCaronaRequest request,
        TeamObjectivePlan plan,
        int confirmedRevision,
        int stateHash,
        UnitReachProfile requestReach,
        CaptureOpportunityClaimSnapshot previousSnapshot)
    {
        var rogueCandidates = new List<Candidate>();
        var formallyReservedConstructionIds = new HashSet<int>();

        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null
                || unit.SlotIndex != request.unit.SlotIndex
                || unit.IsDead)
            {
                continue;
            }

            // Capturador formal ja recebeu unidade + setor do planner e teve o
            // endereco materializado em Mission Intent. Ele NAO participa do
            // MelhorCaptura residual; apenas retira sua refeicao da mesa antes
            // de os rogues dividirem o que sobrou.
            bool formal = TryResolveFormalCaptureSector(
                unit,
                plan,
                out _);
            if (formal)
            {
                if (unit.AIHasDesignatedCaptureTarget)
                {
                    formallyReservedConstructionIds.Add(
                        unit.AIDesignatedCaptureTargetInstanceId);
                }
                continue;
            }

            if (!IsEligibleCapturer(
                    unit,
                    request.unit.SlotIndex))
            {
                continue;
            }

            // IA com HQ declara explicitamente quem sobrou como rogue. Unidade
            // sem slot de Capturador mas alocada a outro papel nao pode entrar
            // escondida no leilao. Sem plano (IA sem HQ), todos sao rogues.
            if (plan != null
                && (plan.RogueUnitIds == null
                    || !plan.RogueUnitIds.Contains(unit.InstanceId)))
            {
                continue;
            }

            // O teste de ocupacao aliada e a mesma pergunta de sempre — "eu
            // consigo parar ali?" — e continua sendo politica de quem organiza,
            // nao regra de captura.
            Tilemap map = request.map;
            UnitManager evaluated = unit;
            Func<ConstructionManager, bool> gate = construction =>
            {
                if (construction == null)
                    return false;
                Vector3Int cell =
                    construction.CurrentCellPosition;
                cell.z = 0;
                bool blockedByAlly = UnitOccupancyRules
                    .HasBlockingOccupantForUnitAtCell(
                        map,
                        cell,
                        evaluated,
                        alliedOnly: true);
                if (!blockedByAlly)
                    return true;

                // Um taxi vazio sobre o endereco que o proprio passageiro ja
                // carregava nao invalida a refeicao. Ele e um bloqueador
                // temporario que a iniciativa manda sair antes da captura.
                // Sem esta excecao, o solve seguinte apaga o farol exatamente
                // porque o transporte terminou a entrega no porto.
                return IsExistingCaptureTargetBlockedOnlyByEmptyAlliedTransport(
                    evaluated,
                    construction,
                    map);
            };

            // Reuso por identidade: mesma unidade, mesma intencao, mesmas
            // bandas. Sem isso a travessia encadeada roda de novo por
            // capturador, e era exatamente o que o codigo anterior evitava.
            MelhorCapturaResult captura =
                MelhorCapturaService.Evaluate(new MelhorCapturaRequest
                {
                    unit = unit,
                    map = request.map,
                    terrainDatabase = request.terrainDatabase,
                    operationalTurns = Mathf.Max(
                        1, request.operationalTurns),
                    reachProfile = unit == request.unit
                        ? requestReach
                        : null,
                    includeConstruction = gate,
                    matchController = null,
                    // A nevoa nao entra na reivindicacao, como nunca entrou: o
                    // recorte do que o time conhece e cruzado depois, por quem
                    // decide agir. Ver o cabecalho do MelhorCapturaService.
                    applyFogOfWar = false,
                    // Aqui so importam banda e custo — as arestas sao
                    // reordenadas por CompareEdges e a nota nunca e lida.
                    // Calcular esforco de captura por candidata era pagar a
                    // ficha da construcao e a penalidade de pre-requisito de
                    // todo predio do mapa para jogar fora.
                    includeCaptureEffort = false,
                    // Beyond tambem participa: no endgame um predio distante
                    // ainda recebe UM rogue; os demais conservam a mesma
                    // referencia apenas como magnetico.
                    includeBeyondOperational = true
                });

            var candidate = new Candidate
            {
                Unit = unit,
                FormalPlan = false,
                FormalSector = ConstructionSector.None,
                MagneticTarget = captura.best?.construction
            };

            for (int i = 0; i < captura.ranking.Count; i++)
            {
                MelhorCapturaAlvoScore alvo = captura.ranking[i];
                candidate.Edges.Add(
                    new Edge
                    {
                        Construction = alvo.construction,
                        RouteCost = alvo.effectiveCost,
                        AssignmentCost = alvo.effectiveCost
                    });
            }

            // Ordena por CUSTO DE ROTA, e nao pela nota da consulta.
            //
            // De proposito: esta troca muda a FONTE da lista, nao a ORDEM dela.
            // A nota do MelhorCaptura pesa banda e turnos de captura, o que
            // provavelmente e melhor — mas mexer nas duas coisas no mesmo passo
            // deixaria qualquer diferenca em jogo sem causa identificavel.
            // Trocar o criterio e passo proprio.
            candidate.Edges.Sort(CompareEdges);
            rogueCandidates.Add(candidate);
        }

        SortCandidates(rogueCandidates);

        ApplySwitchCosts(
            rogueCandidates,
            previousSnapshot,
            rogueCandidates.Count > 1);

        var claims =
            new Dictionary<int, CaptureOpportunityClaim>();
        var unmatched = new List<CaptureOpportunityUnmatched>();
        MatchCandidates(
            rogueCandidates,
            claims,
            unmatched,
            formallyReservedConstructionIds,
            blockedMeansFormal: true);

        AIDecisionPerf.AddCount(
            "CaptureClaimAssignments",
            claims.Count);
        return new CaptureOpportunityClaimSnapshot(
            stateHash,
            confirmedRevision,
            claims,
            unmatched);
    }

    private static bool IsExistingCaptureTargetBlockedOnlyByEmptyAlliedTransport(
        UnitManager capturer,
        ConstructionManager construction,
        Tilemap map)
    {
        if (capturer == null
            || construction == null
            || map == null
            || !capturer.AIHasDesignatedCaptureTarget
            || capturer.AIDesignatedCaptureTargetInstanceId
                != construction.InstanceId)
        {
            return false;
        }

        Vector3Int cell = construction.CurrentCellPosition;
        cell.z = 0;
        List<UnitManager> occupants =
            UnitOccupancyRules.GetUnitsAtCell(map, cell, capturer);
        bool foundBlockingTransport = false;
        for (int i = 0; i < occupants.Count; i++)
        {
            UnitManager occupant = occupants[i];
            if (occupant == null
                || occupant.IsDead
                || occupant.IsEmbarked
                || !PlayerSlotRelations.AreAllies(capturer, occupant))
            {
                continue;
            }

            var singleOccupant = new List<UnitManager>(1) { occupant };
            if (OccupancyResolver.CanEndMove(
                    capturer, cell, singleOccupant))
            {
                continue;
            }

            if (!occupant.TryGetUnitData(out UnitData occupantData)
                || occupantData == null
                || !occupantData.isTransporter
                || !UnitRoleCompatibility.CanSatisfy(
                    occupantData, UnitRole.Transportador)
                || UnitRoleCompatibility.CanSatisfy(
                    occupantData, UnitRole.Capturador)
                || HasAnyEmbarkedPassenger(occupant))
            {
                return false;
            }

            foundBlockingTransport = true;
        }

        return foundBlockingTransport;
    }

    private static bool HasAnyEmbarkedPassenger(UnitManager transporter)
    {
        if (transporter?.TransportedUnitSlots == null)
            return false;

        IReadOnlyList<UnitTransportSeatRuntime> seats =
            transporter.TransportedUnitSlots;
        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            if (seat?.embarkedUnit != null
                && seat.embarkedUnit.IsEmbarked)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class FlowArc
    {
        public int To;
        public int Reverse;
        public int Capacity;
        public long Cost;
        public Candidate Candidate;
        public Edge Assignment;
    }

    /// <summary>
    /// Max-flow de custo minimo: primeiro acha o maior numero de pares; entre
    /// todos os pareamentos dessa cardinalidade escolhe o menor custo global.
    /// Assim uma unidade so fica com o alvo disputado quando perder esse alvo
    /// custa mais ao conjunto do que entrega-lo a outra unidade.
    /// </summary>
    private static void MatchCandidates(
        List<Candidate> candidates,
        Dictionary<int, CaptureOpportunityClaim> claims,
        List<CaptureOpportunityUnmatched> unmatched,
        HashSet<int> blockedConstructionIds,
        bool blockedMeansFormal)
    {
        if (candidates == null || candidates.Count == 0)
            return;

        var constructions = new List<ConstructionManager>();
        var seenConstructionIds = new HashSet<int>();
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            for (int e = 0; e < candidate.Edges.Count; e++)
            {
                ConstructionManager construction =
                    candidate.Edges[e]?.Construction;
                if (construction == null)
                    continue;
                int id = construction.InstanceId;
                if (blockedConstructionIds != null
                    && blockedConstructionIds.Contains(id))
                {
                    continue;
                }
                if (seenConstructionIds.Add(id))
                    constructions.Add(construction);
            }
        }
        constructions.Sort((left, right) =>
            left.InstanceId.CompareTo(right.InstanceId));

        int source = 0;
        int candidateStart = 1;
        int constructionStart = candidateStart + candidates.Count;
        int sink = constructionStart + constructions.Count;
        var graph = new List<FlowArc>[sink + 1];
        for (int i = 0; i < graph.Length; i++)
            graph[i] = new List<FlowArc>();

        var constructionNodeById = new Dictionary<int, int>();
        for (int i = 0; i < constructions.Count; i++)
        {
            constructionNodeById[constructions[i].InstanceId] =
                constructionStart + i;
            AddFlowArc(graph, constructionStart + i, sink, 1, 0L);
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            int candidateNode = candidateStart + i;
            AddFlowArc(graph, source, candidateNode, 1, 0L);
            for (int e = 0; e < candidate.Edges.Count; e++)
            {
                Edge edge = candidate.Edges[e];
                if (edge?.Construction == null
                    || !constructionNodeById.TryGetValue(
                        edge.Construction.InstanceId,
                        out int constructionNode))
                {
                    continue;
                }

                // Tudo abaixo de PrimaryCostScale e apenas desempate. O custo
                // de rota + troca sempre decide antes de papel/ordem estavel.
                long tieCost =
                    ResolveCapturerRolePrecedence(candidate.Unit) * 10000L
                    + e;
                AddFlowArc(
                    graph,
                    candidateNode,
                    constructionNode,
                    1,
                    (long)Mathf.Max(0, edge.AssignmentCost)
                        * PrimaryCostScale
                        + tieCost,
                    candidate,
                    edge);
            }
        }

        RunMinCostMaxFlow(graph, source, sink);

        var matchedUnitIds = new HashSet<int>();
        for (int i = 0; i < candidates.Count; i++)
        {
            int candidateNode = candidateStart + i;
            foreach (FlowArc arc in graph[candidateNode])
            {
                if (arc.Assignment == null
                    || arc.Candidate == null
                    || arc.Capacity != 0)
                {
                    continue;
                }
                Candidate owner = arc.Candidate;
                Edge edge = arc.Assignment;
                matchedUnitIds.Add(owner.Unit.InstanceId);
                claims[edge.Construction.InstanceId] =
                    new CaptureOpportunityClaim(
                        edge.Construction,
                        owner.Unit,
                        edge.RouteCost,
                        edge.AssignmentCost,
                        edge.SwitchCost,
                        owner.FormalPlan);
                break;
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            if (matchedUnitIds.Contains(candidate.Unit.InstanceId))
                continue;
            bool hasUnblocked = false;
            bool hasBlocked = false;
            for (int e = 0; e < candidate.Edges.Count; e++)
            {
                Edge edge = candidate.Edges[e];
                if (edge?.Construction == null)
                    continue;
                if (blockedConstructionIds != null
                    && blockedConstructionIds.Contains(
                        edge.Construction.InstanceId))
                    hasBlocked = true;
                else
                    hasUnblocked = true;
            }
            CaptureOpportunityUnmatchedReason reason =
                hasUnblocked
                    ? CaptureOpportunityUnmatchedReason
                        .ReservedByOtherCapturer
                    : hasBlocked && blockedMeansFormal
                        ? CaptureOpportunityUnmatchedReason
                            .BlockedByFormalPlan
                        : CaptureOpportunityUnmatchedReason
                            .NoReachableOpportunity;
            unmatched.Add(new CaptureOpportunityUnmatched(
                candidate.Unit,
                reason,
                candidate.MagneticTarget));
        }
    }

    private static void AddFlowArc(
        List<FlowArc>[] graph,
        int from,
        int to,
        int capacity,
        long cost,
        Candidate candidate = null,
        Edge assignment = null)
    {
        var forward = new FlowArc
        {
            To = to,
            Reverse = graph[to].Count,
            Capacity = capacity,
            Cost = cost,
            Candidate = candidate,
            Assignment = assignment
        };
        var reverse = new FlowArc
        {
            To = from,
            Reverse = graph[from].Count,
            Capacity = 0,
            Cost = -cost
        };
        graph[from].Add(forward);
        graph[to].Add(reverse);
    }

    private static void RunMinCostMaxFlow(
        List<FlowArc>[] graph,
        int source,
        int sink)
    {
        int nodeCount = graph.Length;
        var distance = new long[nodeCount];
        var previousNode = new int[nodeCount];
        var previousArc = new int[nodeCount];
        var queued = new bool[nodeCount];
        var queue = new Queue<int>();
        const long infinity = long.MaxValue / 4L;

        while (true)
        {
            for (int i = 0; i < nodeCount; i++)
            {
                distance[i] = infinity;
                previousNode[i] = -1;
                previousArc[i] = -1;
                queued[i] = false;
            }
            distance[source] = 0L;
            queue.Clear();
            queue.Enqueue(source);
            queued[source] = true;

            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                queued[node] = false;
                for (int i = 0; i < graph[node].Count; i++)
                {
                    FlowArc arc = graph[node][i];
                    if (arc.Capacity <= 0
                        || distance[node] + arc.Cost
                            >= distance[arc.To])
                    {
                        continue;
                    }
                    distance[arc.To] = distance[node] + arc.Cost;
                    previousNode[arc.To] = node;
                    previousArc[arc.To] = i;
                    if (!queued[arc.To])
                    {
                        queued[arc.To] = true;
                        queue.Enqueue(arc.To);
                    }
                }
            }

            if (previousNode[sink] < 0)
                return;
            for (int node = sink; node != source;
                 node = previousNode[node])
            {
                FlowArc arc =
                    graph[previousNode[node]][previousArc[node]];
                arc.Capacity--;
                graph[node][arc.Reverse].Capacity++;
            }
        }
    }

    /// <summary>
    /// Solve puro usado por ferramentas e testes. Cada ranking continua sendo
    /// produzido uma vez por sujeito pelo MelhorCaptura; este metodo somente
    /// agrega. Com um sujeito nao existe custo de troca, preservando a escolha
    /// unitária anterior exatamente.
    /// </summary>
    public static CaptureOpportunityClaimSnapshot SolveGroup(
        IReadOnlyList<CaptureOpportunityGroupCandidate> inputs,
        CaptureOpportunityClaimSnapshot previousSnapshot = null)
    {
        var formalCandidates = new List<Candidate>();
        var rogueCandidates = new List<Candidate>();
        if (inputs != null)
        {
            var seenUnits = new HashSet<int>();
            for (int i = 0; i < inputs.Count; i++)
            {
                CaptureOpportunityGroupCandidate input = inputs[i];
                if (input?.Unit == null
                    || !seenUnits.Add(input.Unit.InstanceId))
                {
                    continue;
                }
                var candidate = new Candidate
                {
                    Unit = input.Unit,
                    FormalPlan = input.FormalPlan,
                    FormalSector = input.FormalSector,
                    MagneticTarget = input.Ranking != null
                        && input.Ranking.Count > 0
                            ? input.Ranking[0]?.construction
                            : null
                };
                if (input.Ranking != null)
                {
                    for (int r = 0; r < input.Ranking.Count; r++)
                    {
                        MelhorCapturaAlvoScore target = input.Ranking[r];
                        if (target?.construction == null)
                        {
                            continue;
                        }
                        candidate.Edges.Add(new Edge
                        {
                            Construction = target.construction,
                            RouteCost = target.effectiveCost,
                            AssignmentCost = target.effectiveCost
                        });
                    }
                }
                candidate.Edges.Sort(CompareEdges);
                if (candidate.FormalPlan)
                    formalCandidates.Add(candidate);
                else
                    rogueCandidates.Add(candidate);
            }
        }

        SortCandidates(formalCandidates);
        SortCandidates(rogueCandidates);
        int totalCandidates =
            formalCandidates.Count + rogueCandidates.Count;
        ApplySwitchCosts(
            formalCandidates, previousSnapshot, totalCandidates > 1);
        ApplySwitchCosts(
            rogueCandidates, previousSnapshot, totalCandidates > 1);

        var claims = new Dictionary<int, CaptureOpportunityClaim>();
        var unmatched = new List<CaptureOpportunityUnmatched>();
        var formalIds = new HashSet<int>();
        MatchCandidates(
            formalCandidates,
            claims,
            unmatched,
            blockedConstructionIds: null,
            blockedMeansFormal: false);
        foreach (int id in claims.Keys)
            formalIds.Add(id);
        MatchCandidates(
            rogueCandidates,
            claims,
            unmatched,
            formalIds,
            blockedMeansFormal: true);
        return new CaptureOpportunityClaimSnapshot(
            0, -1, claims, unmatched);
    }

    private static void ApplySwitchCosts(
        List<Candidate> candidates,
        CaptureOpportunityClaimSnapshot previousSnapshot,
        bool enabled)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            int previousConstructionId = -1;
            if (enabled
                && previousSnapshot != null
                && previousSnapshot.TryGetClaimForUnit(
                    candidate.Unit,
                    out CaptureOpportunityClaim previousClaim)
                && previousClaim.Construction != null)
            {
                previousConstructionId =
                    previousClaim.Construction.InstanceId;
            }
            else if (enabled
                     && candidate.Unit.AIHasDesignatedCaptureTarget)
            {
                previousConstructionId = candidate.Unit
                    .AIDesignatedCaptureTargetInstanceId;
            }

            for (int e = 0; e < candidate.Edges.Count; e++)
            {
                Edge edge = candidate.Edges[e];
                edge.SwitchCost = previousConstructionId >= 0
                    && edge.Construction != null
                    && edge.Construction.InstanceId
                        != previousConstructionId
                        ? CaptureTargetSwitchCost
                        : 0;
                edge.AssignmentCost = edge.RouteCost
                    >= int.MaxValue - edge.SwitchCost
                        ? int.MaxValue
                        : Mathf.Max(
                            0, edge.RouteCost + edge.SwitchCost);
            }
        }
    }

    private static bool IsEligibleCapturer(
        UnitManager unit,
        int slotIndex)
    {
        return unit != null
            && unit.SlotIndex == slotIndex
            && !unit.IsDead
            && !unit.IsEmbarked
            && !unit.IsUnderRepair
            // Missao de outro verbo manda ate o dono dela dar baixa. Capture
            // continua sendo farol distributivo e pode participar do novo
            // matching coletivo.
            && (!unit.AIHasDesignatedMission
                || unit.AIDesignatedMissionIntent
                    == AIPlanRuntimeIntent.Capture)
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && UnitRoleCompatibility.CanSatisfy(
                data,
                UnitRole.Capturador);
    }

    private static bool TryResolveFormalCaptureSector(
        UnitManager unit,
        TeamObjectivePlan plan,
        out ConstructionSector sector)
    {
        sector = ConstructionSector.None;
        if (unit == null
            || plan?.Objectives == null)
        {
            return false;
        }

        foreach (SectorObjective objective in plan.Objectives)
        {
            if (objective == null
                || objective.Status == ObjectiveStatus.Defending
                || objective.Status == ObjectiveStatus.Complete
                || objective.Status == ObjectiveStatus.Abandoned
                || objective.Slots == null)
            {
                continue;
            }
            foreach (SlotNeed slot in objective.Slots)
            {
                if (slot != null
                    && slot.Filled
                    && slot.Role == UnitRole.Capturador
                    && slot.AssignedUnitId == unit.InstanceId)
                {
                    sector = objective.Sector;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Capturador puro escolhe antes do Capturador Agressivo.
    ///
    /// O agressivo e BACKUP de captura: ele satisfaz o papel (CanSatisfy) e por
    /// isso disputa, mas a vocacao dele e abrir caminho. Quando os dois querem o
    /// MESMO predio, o puro leva e o agressivo cai para o proximo — que e a
    /// regra "no empate, o agressivo cede a vez".
    ///
    /// Isso e ordem de ESCOLHA, nao de distancia: cada candidato ja tem as
    /// arestas ordenadas por custo de rota (CompareEdges), entao um capturador
    /// puro distante nao rouba predio que ele nao quer — ele pega o dele. A
    /// precedencia so decide o desempate quando ha disputa pelo mesmo alvo.
    ///
    /// No runtime esta precedencia atua somente entre rogues. O SolveGroup
    /// puro conserva o campo FormalPlan para ferramentas/testes que queiram
    /// comparar grupos explicitamente montados, sem mudar a doutrina runtime.
    /// </summary>
    private static int ResolveCapturerRolePrecedence(UnitManager unit)
    {
        if (unit == null
            || !unit.TryGetUnitData(out UnitData data)
            || data == null
            || data.roles == null
            || data.roles.Count == 0)
        {
            return 0;
        }

        return data.roles[0] == UnitRole.CapturadorCombatente ? 1 : 0;
    }

    private static void SortCandidates(
        List<Candidate> candidates)
    {
        candidates.Sort((left, right) =>
        {
            int compare =
                ResolveCapturerRolePrecedence(left.Unit)
                    .CompareTo(
                        ResolveCapturerRolePrecedence(right.Unit));
            if (compare != 0)
                return compare;
            compare =
                left.Unit.InstanceId.CompareTo(
                    right.Unit.InstanceId);
            if (compare != 0)
                return compare;
            compare =
                left.Edges.Count.CompareTo(
                    right.Edges.Count);
            if (compare != 0)
                return compare;
            if (left.Edges.Count == 0)
                return 0;
            compare =
                left.Edges[0].RouteCost.CompareTo(
                    right.Edges[0].RouteCost);
            return compare;
        });
    }

    private static int CompareEdges(
        Edge left,
        Edge right)
    {
        int compare =
            left.RouteCost.CompareTo(right.RouteCost);
        return compare != 0
            ? compare
            : left.Construction.InstanceId.CompareTo(
                right.Construction.InstanceId);
    }

    private static int ResolveConfirmedRevision(
        Tilemap map)
    {
        if (map != null
            && ConfirmedOccupancyIndex.TryGetFor(
                map,
                out ConfirmedOccupancyIndex occupancy)
            && occupancy != null
            && occupancy.CanServeLiveQueries)
        {
            return occupancy.ConfirmedRevision;
        }

        return ThreatRevisionTracker.GlobalBoardRevision;
    }

    /// <summary>
    /// Assinatura do estado que decide a alocacao de oportunidades de captura.
    ///
    /// A populacao aqui e a MESMA que o <see cref="Build"/> usa para montar as
    /// candidaturas: capturadores elegiveis. Antes esta varredura dobrava todas
    /// as unidades do slot — as 66 —, inclusive o <c>HasActed</c> de cada uma, e
    /// isso fazia o hash mudar a cada acao de QUALQUER peca.
    ///
    /// O efeito nao era no hash: era no cache do QueroCarona, que carrega este
    /// valor na chave. Um Chinook esperando parado derrubava a resposta de todo
    /// capturador do time, e cada transportador seguinte recalculava os ~820 ms
    /// de necessidade de carona do zero. A prova estava no log: o segundo
    /// Chinook acertou o cache UMA vez em 17 perguntas, e a unica foi a do APC —
    /// justamente quem nao satisfaz Capturador e por isso tem hash 0 aqui.
    ///
    /// Ocupacao continua invalidando por outro canal: a chave do QueroCarona ja
    /// carrega <c>occupancyRevision</c> em campo separado. Um transporte parado
    /// em cima de um predio segue sendo visto — so nao passa mais por aqui.
    /// </summary>
    private static int BuildStateHash(
        int slotIndex,
        TeamObjectivePlan plan)
    {
        unchecked
        {
            int hash = 17;
            foreach (UnitManager unit
                     in UnitManager.AllActive)
            {
                if (!IsEligibleCapturer(unit, slotIndex))
                    continue;
                Vector3Int cell =
                    unit.CurrentCellPosition;
                cell.z = 0;
                hash = (hash * 31) + unit.InstanceId;
                hash = (hash * 31) + cell.GetHashCode();
                hash = (hash * 31)
                    + unit.RemainingMovementPoints;
                hash = (hash * 31)
                    + unit.MaxMovementPoints;
                // HasActed fica: um capturador que agiu pode ter tomado o
                // predio, e isso remaneja as oportunidades de verdade.
                hash = (hash * 31)
                    + (unit.HasActed ? 1 : 0);
                // IsDead / IsEmbarked / IsUnderRepair sairam: o filtro acima ja
                // exclui os tres, entao aqui eles eram constante. A transicao
                // continua sendo detectada — quem morre ou embarca DESAPARECE
                // do laco, e o hash muda por perder os termos dele.
                hash = (hash * 31)
                    + (unit.AIHasDesignatedCaptureTarget
                        ? 1
                        : 0);
                hash = (hash * 31)
                    + unit
                        .AIDesignatedCaptureTargetInstanceId;
                hash = (hash * 31)
                    + unit
                        .AIDesignatedCaptureTargetCell
                        .GetHashCode();
            }

            foreach (ConstructionManager construction
                     in ConstructionManager.AllActive)
            {
                if (construction == null)
                    continue;
                Vector3Int cell =
                    construction.CurrentCellPosition;
                cell.z = 0;
                hash = (hash * 31)
                    + construction.InstanceId;
                hash = (hash * 31)
                    + cell.GetHashCode();
                hash = (hash * 31)
                    + (int)construction.TeamId;
                hash = (hash * 31)
                    + (int)construction.Sector;
                hash = (hash * 31)
                    + (construction.IsCapturable ? 1 : 0);
            }

            if (plan?.Objectives != null)
            {
                foreach (SectorObjective objective
                         in plan.Objectives)
                {
                    if (objective == null)
                        continue;
                    hash = (hash * 31)
                        + (int)objective.Sector;
                    hash = (hash * 31)
                        + (int)objective.Status;
                    foreach (SlotNeed slot
                             in objective.Slots)
                    {
                        if (slot == null)
                            continue;
                        hash = (hash * 31)
                            + (int)slot.Role;
                        hash = (hash * 31)
                            + (slot.Filled ? 1 : 0);
                        hash = (hash * 31)
                            + slot.AssignedUnitId;
                    }
                }
            }

            return hash;
        }
    }
}
