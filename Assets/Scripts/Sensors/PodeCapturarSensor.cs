using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeCapturarSensor
{
    public enum CaptureOperationType
    {
        None = 0,
        CaptureEnemy = 1,
        RecoverAlly = 2
    }

    /// <summary>
    /// Esta unidade tem a chave que ESTA construcao pede?
    ///
    /// A HABILIDADE E CHAVE, NAO PODER. Quem define o que a etiqueta abre e o
    /// alvo — aqui, a construcao, em `requiredSkillsToCapture` — e nunca a
    /// propria skill. E o mesmo desenho da montanha que pede alpino e da
    /// floresta que pede guerrilha: o lugar pendura a etiqueta e define ali o
    /// que ela significa.
    ///
    /// A consequencia pratica e que a etiqueta pode se chamar qualquer coisa. Se
    /// voce renomear "Captura Construcoes" para "sai que isso e meu", nada
    /// quebra: quem aponta para o asset e a construcao.
    ///
    /// Lista vazia = ninguem captura esta construcao por skill. Nao e o
    /// interruptor de "isto e capturavel" — esse continua sendo
    /// CapturePointsMax; esta lista diz POR QUEM.
    ///
    /// Ver docs/manual/01_principios_e_vocabulario.md.
    /// </summary>
    public static bool HasCaptureKeyFor(
        UnitManager unit,
        ConstructionData construction)
    {
        return ResolveCaptureEfficiency(unit, construction) > 0f;
    }

    public static bool HasCaptureKeyFor(
        UnitData unitData,
        ConstructionData construction)
    {
        return ResolveCaptureEfficiency(unitData, construction) > 0f;
    }

    /// <summary>
    /// Quanto a melhor chave desta unidade rende NESTA construcao.
    /// Zero quando ela nao tem chave nenhuma daqui — e o mesmo que "nao captura".
    ///
    /// A MAIOR vence, por decisao do autor: a unidade emprega a melhor
    /// ferramenta que carrega, e levar junto uma chave pior nunca e onus.
    /// </summary>
    public static float ResolveCaptureEfficiency(
        UnitManager unit,
        ConstructionData construction)
    {
        return unit != null
               && unit.TryGetUnitData(out UnitData unitData)
            ? ResolveCaptureEfficiency(unitData, construction)
            : 0f;
    }

    public static float ResolveCaptureEfficiency(
        UnitData unitData,
        ConstructionData construction)
    {
        if (unitData?.skills == null
            || construction?.requiredSkillsToCapture == null)
        {
            return 0f;
        }

        float best = 0f;
        for (int i = 0; i < construction.requiredSkillsToCapture.Count; i++)
        {
            CaptureSkillEfficiency entry =
                construction.requiredSkillsToCapture[i];
            if (entry?.skill == null || entry.efficiency <= 0f)
                continue;
            for (int j = 0; j < unitData.skills.Count; j++)
            {
                if (unitData.skills[j] == entry.skill
                    && entry.efficiency > best)
                {
                    best = entry.efficiency;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// A unidade carrega ALGUMA chave de captura?
    ///
    /// Pergunta de PLANEJAMENTO, nao de permissao: serve para o "vale a pena
    /// nem olhar para captura com esta unidade?" antes de haver um alvo. Quem
    /// autoriza de fato e HasCaptureKeyFor, contra a construcao concreta —
    /// uma unidade pode ter a chave do galpao e nao ter a do bunker.
    ///
    /// Varre as construcoes conhecidas procurando alguma que aceite alguma
    /// skill desta unidade.
    /// </summary>
    public static bool HasAnyCaptureKey(UnitData unitData)
    {
        if (unitData?.skills == null || unitData.skills.Count == 0)
            return false;

        IReadOnlyList<ConstructionManager> all = ConstructionManager.AllActive;
        if (all == null)
            return false;

        for (int i = 0; i < all.Count; i++)
        {
            ConstructionManager construction = all[i];
            if (construction == null)
                continue;
            if (!construction.TryResolveConstructionData(
                    out ConstructionData data))
                continue;
            if (HasCaptureKeyFor(unitData, data))
                return true;
        }
        return false;
    }

    public static bool HasAnyCaptureKey(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData unitData)
            && HasAnyCaptureKey(unitData);
    }

    /// <summary>
    /// Retorna quantos pontos esta unidade aplica ao capturar ou recuperar uma construcao.
    /// Capturadores agressivos trocam eficiencia de captura pelo alcance: 2 HP por ponto,
    /// arredondando para cima e com minimo de 1 enquanto estiverem vivos.
    /// </summary>
    public static int GetCapturePower(UnitManager unit)
    {
        if (unit == null || unit.IsDead)
            return 0;

        int hp = Mathf.Max(0, unit.CurrentHP);
        if (hp <= 0)
            return 0;

        if (unit.TryGetUnitData(out UnitData unitData)
            && unitData != null
            && unitData.roles != null
            && unitData.roles.Count > 0
            && unitData.roles[0] == UnitRole.CapturadorCombatente)
        {
            return Mathf.Max(1, Mathf.CeilToInt(hp / 2f));
        }

        return hp;
    }

    /// <summary>
    /// Retorna a forca efetiva contra uma construcao. Capturas inimigas sem o
    /// pre-requisito de progressao aplicam metade da forca base, arredondada
    /// para baixo e com minimo de 1. Recuperacao aliada nunca recebe essa penalidade.
    /// </summary>
    public static int GetCapturePower(
        UnitManager unit,
        ConstructionManager construction,
        CaptureOperationType operationType,
        MatchController matchController,
        out bool prerequisitePenaltyApplied)
    {
        prerequisitePenaltyApplied = false;
        int basePower = GetCapturePower(unit);
        if (basePower <= 0 || construction == null)
            return basePower;

        if (!construction.TryResolveConstructionData(out ConstructionData constructionData)
            || constructionData == null)
        {
            return basePower;
        }

        // A EFICIENCIA E DO PAR (chave x construcao), e vale para captura E
        // reconquista — quem e ruim de tomar um bunker tambem e ruim de
        // retomar. A penalidade de pre-requisito abaixo e outra conta, e as
        // duas se multiplicam: 0,8 aqui com pre-requisito faltando da 0,4.
        float efficiency =
            ResolveCaptureEfficiency(unit, constructionData);
        if (efficiency > 0f && !Mathf.Approximately(efficiency, 1f))
            basePower = Mathf.Max(1, Mathf.CeilToInt(basePower * efficiency));

        if (operationType != CaptureOperationType.CaptureEnemy)
            return basePower;

        if (matchController == null)
            matchController = Object.FindAnyObjectByType<MatchController>();
        if (matchController == null
            || !matchController.ShouldPenalizeCaptureForMissingPrerequisite(
                PlayerSlotId.FromIndex(unit.SlotIndex),
                constructionData,
                out _))
        {
            return basePower;
        }

        prerequisitePenaltyApplied = true;
        return Mathf.Max(1, basePower / 2);
    }

    public static int GetCapturePower(
        UnitManager unit,
        ConstructionManager construction,
        MatchController matchController = null)
    {
        CaptureOperationType operationType =
            unit != null
            && construction != null
            && PlayerSlotRelations.AreAllies(unit.SlotIndex, construction.SlotIndex)
                ? CaptureOperationType.RecoverAlly
                : CaptureOperationType.CaptureEnemy;
        return GetCapturePower(
            unit,
            construction,
            operationType,
            matchController,
            out _);
    }

    public static bool TryGetCaptureTarget(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        SensorMovementMode movementMode,
        out ConstructionManager targetConstruction,
        out string reason,
        MatchController matchController = null)
    {
        return TryGetCaptureTarget(
            selectedUnit,
            boardTilemap,
            movementMode,
            out targetConstruction,
            out _,
            out reason,
            matchController);
    }

    public static bool TryGetCaptureTarget(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        SensorMovementMode movementMode,
        out ConstructionManager targetConstruction,
        out CaptureOperationType operationType,
        out string reason,
        MatchController matchController = null)
    {
        Vector3Int evaluatedCell = selectedUnit != null
            ? selectedUnit.CurrentCellPosition
            : default;
        return TryGetCaptureTargetAtCell(
            selectedUnit,
            boardTilemap,
            evaluatedCell,
            movementMode,
            out targetConstruction,
            out operationType,
            out reason,
            matchController);
    }

    /// <summary>
    /// Consulta a mesma regra oficial de captura em uma célula projetada, sem
    /// alterar a posição ou qualquer estado confirmado da unidade. Usado por
    /// planejadores para avaliar o resultado de um movimento provisório.
    ///
    /// `applyFogOfWar` liga o descarte por terreno desconhecido. Ele é ON por
    /// padrão, que é a resposta da HORA DE AGIR: ali a névoa vale e a unidade
    /// não age no que não conhece.
    ///
    /// Planejamento pede OFF. A névoa não é regra de captura, é recorte do que
    /// o time enxerga — e cruzar alcance com o que se conhece é trabalho de
    /// quem organiza, não do sensor. Uma consulta que já chega recortada não
    /// consegue responder "vale a pena ir descobrir aquilo?", porque o alvo
    /// sumiu antes de ser pontuado. Mesmo padrão dos `enable*` do PodeMirar.
    ///
    /// `knownConstruction` é a construção que o chamador já tem na mão. Sem
    /// ela o sensor faz um `FindObjectsByType` da cena inteira para redescobrir
    /// o que quem chamou acabou de lhe entregar — barato uma vez, O(n²) dentro
    /// de um laço por candidata. A dica é validada (mesma célula, não é prédio
    /// falso) antes de ser aceita; errada, cai na busca normal.
    ///
    /// `applyEmbarkedGate` é o terceiro filtro de hora-de-agir. ON por padrão:
    /// embarcado não captura. Desligue ao perguntar por um passageiro que ainda
    /// está no veículo — "este prédio serve de destino para ele?" —, porque
    /// projetar a unidade numa célula já pressupõe que ela desembarcou lá. Com
    /// o portão ligado essa pergunta não tem resposta possível.
    /// </summary>
    public static bool TryGetCaptureTargetAtCell(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        Vector3Int evaluatedCell,
        SensorMovementMode movementMode,
        out ConstructionManager targetConstruction,
        out CaptureOperationType operationType,
        out string reason,
        MatchController matchController = null,
        bool applyFogOfWar = true,
        ConstructionManager knownConstruction = null,
        bool applyEmbarkedGate = true)
    {
        targetConstruction = null;
        operationType = CaptureOperationType.None;
        reason = string.Empty;
        bool sensorLogs = SensorLogGate.IsPodeCapturarEnabled();

        if (sensorLogs)
            SensorLogGate.Log("PodeCapturarSensor", $"collect unit={(selectedUnit != null ? selectedUnit.name : "(null)")} movement={movementMode}");

        if (selectedUnit == null)
        {
            reason = "Selecione uma unidade.";
            return false;
        }

        // Portao de HORA DE AGIR, como a nevoa. Embarcado nao captura, ponto —
        // mas quem PROJETA a unidade numa celula ja hipotetizou que ela
        // desembarcou la. E a pergunta do transporte: "onde eu largo este
        // passageiro?". Com o portao ligado ela nao tem resposta possivel, e o
        // transporte fica sem destino.
        if (applyEmbarkedGate && selectedUnit.IsEmbarked)
        {
            reason = "Unidade embarcada nao pode capturar.";
            return false;
        }

        if (movementMode != SensorMovementMode.MoveuParado && movementMode != SensorMovementMode.MoveuAndando)
        {
            reason = "Captura so pode ser avaliada em Moveu Parado ou Moveu Andando.";
            return false;
        }

        if (!selectedUnit.TryGetUnitData(out UnitData unitData) || unitData == null)
        {
            reason = "UnitData indisponivel.";
            return false;
        }

        // A CHECAGEM DE CHAVE MUDOU DE LUGAR, e nao por estilo: ela so existe
        // depois de saber QUAL construcao. Quem pergunta pela etiqueta e o
        // alvo, entao nao ha resposta antes de ter alvo. Ver mais abaixo,
        // depois de a construcao ser resolvida.

        Tilemap map = boardTilemap != null ? boardTilemap : selectedUnit.BoardTilemap;
        if (map == null)
        {
            reason = "Tilemap indisponivel para avaliar captura.";
            return false;
        }

        Vector3Int cell = evaluatedCell;
        cell.z = 0;
        // O MatchController so e necessario para a nevoa, e resolve-lo custa um
        // FindAnyObjectByType da cena. Fora do teste ele nao e usado em mais
        // nada aqui — e com a nevoa desligada era varredura de cena inteira,
        // por candidata, para nada.
        if (applyFogOfWar)
        {
            if (matchController == null)
                matchController = Object.FindAnyObjectByType<MatchController>();
            if (matchController != null && matchController.IsFogOfWarDebugEnabled &&
                !matchController.IsCellVisibleForActiveTeam(cell) &&
                !matchController.IsCellExploredBySlot(PlayerSlotId.FromIndex(selectedUnit.SlotIndex), cell))
            {
                reason = "Terreno ainda desconhecido.";
                return false;
            }
        }

        // A construcao pode vir do chamador. Quem varre candidatas JA a tem na
        // mao, e GetConstructionAtCell faz um FindObjectsByType da cena inteira
        // por chamada — num laco por candidata isso vira O(n²) varreduras de
        // cena, que e a armadilha que ja custou 43 s nesta base.
        //
        // Nao e voto de confianca: a dica so vale se estiver mesmo na celula
        // avaliada e nao for predio falso, exatamente os dois testes que a busca
        // faria. Errou a dica, cai na busca e ninguem quebra.
        ConstructionManager construction = null;
        if (knownConstruction != null && !knownConstruction.IsFakeBuilding)
        {
            Vector3Int knownCell = knownConstruction.CurrentCellPosition;
            knownCell.z = 0;
            if (knownCell == cell)
                construction = knownConstruction;
        }
        if (construction == null)
            construction = ConstructionOccupancyRules.GetConstructionAtCell(map, cell);
        if (construction == null)
        {
            reason = "Nao ha construcao no hex atual.";
            return false;
        }

        if (!construction.IsCapturable || construction.CapturePointsMax <= 0)
        {
            reason = "Construcao atual nao e capturavel.";
            return false;
        }

        // AGORA da para perguntar pela chave: existe alvo, e e o alvo que diz
        // qual etiqueta abre. Antes disso a pergunta nao tinha a quem ser feita.
        if (!construction.TryResolveConstructionData(
                out ConstructionData captureRules)
            || captureRules == null)
        {
            reason = "Construcao sem ficha para validar captura.";
            return false;
        }

        if (!HasCaptureKeyFor(unitData, captureRules))
        {
            reason = captureRules.requiredSkillsToCapture == null
                     || captureRules.requiredSkillsToCapture.Count == 0
                ? "Esta construcao nao aceita captura por skill nenhuma."
                : "A unidade nao possui a habilidade que esta construcao pede "
                  + "para ser capturada.";
            return false;
        }

        if (selectedUnit.SlotIndex < 0)
        {
            reason = "Unidade neutra nao captura.";
            return false;
        }

        if (PlayerSlotRelations.AreAllies(selectedUnit.SlotIndex, construction.SlotIndex))
        {
            if (construction.CurrentCapturePoints < construction.CapturePointsMax)
            {
                targetConstruction = construction;
                operationType = CaptureOperationType.RecoverAlly;
                if (sensorLogs)
                    SensorLogGate.Log("PodeCapturarSensor", $"result hasAny=true op={operationType} construction={construction.name}");
                return true;
            }

            reason = "Construcao aliada ja esta com captura maxima.";
            return false;
        }

        targetConstruction = construction;
        operationType = CaptureOperationType.CaptureEnemy;
        if (sensorLogs)
            SensorLogGate.Log("PodeCapturarSensor", $"result hasAny=true op={operationType} construction={construction.name}");
        return true;
    }
}
