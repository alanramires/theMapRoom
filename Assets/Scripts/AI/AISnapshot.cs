using System.Collections.Generic;
using UnityEngine.Tilemaps;
using UnityEngine;

// Foto do estado do jogo do ponto de vista da IA no inicio do turno.
// Construida uma vez por turno via Build() e passada para o perfil de IA.
public class AISnapshot
{
    public TeamId AiTeam;

    // Construcoes sao intel publica — visíveis independente da nevoa de guerra
    public bool HasHq;
    public Vector3Int HqCell;
    public List<AIConstructionInfo> KnownConstructions = new List<AIConstructionInfo>();
    // HQs dos times inimigos (atalho — subconjunto de KnownConstructions)
    public List<AIConstructionInfo> EnemyHqs = new List<AIConstructionInfo>();
    // Construcoes dentro do raio de defesa do proprio HQ
    public List<AIConstructionInfo> ConstructionsNearHq = new List<AIConstructionInfo>();
    public int HqDefendRadius;

    public Tilemap BoardTilemap;
    public List<UnitManager> FriendlyUnits = new List<UnitManager>();
    public List<UnitManager> VisibleEnemies = new List<UnitManager>();

    public const int DefaultDefendRadius = 5;

    public static AISnapshot Build(TeamId aiTeam, MatchController matchController, int defendRadius = DefaultDefendRadius)
    {
        AISnapshot snapshot = new AISnapshot { AiTeam = aiTeam, HqDefendRadius = defendRadius };

        // Todas as construcoes sao conhecidas (intel publica)
        for (int i = 0; i < ConstructionManager.AllActive.Count; i++)
        {
            ConstructionManager c = ConstructionManager.AllActive[i];
            if (c == null)
                continue;

            AIConstructionInfo info = new AIConstructionInfo
            {
                Cell = c.CurrentCellPosition,
                TeamId = c.TeamId,
                IsHq = c.IsPlayerHeadQuarter,
                CanProduceUnits = c.CanProduceUnits,
                DisplayName = c.ConstructionDisplayName,
                Source = c
            };
            snapshot.KnownConstructions.Add(info);

            if (c.IsPlayerHeadQuarter)
            {
                if (c.TeamId == aiTeam)
                {
                    snapshot.HasHq = true;
                    snapshot.HqCell = c.CurrentCellPosition;
                    snapshot.HqCell.z = 0;
                }
                else
                {
                    snapshot.EnemyHqs.Add(info);
                }
            }
        }

        // Tilemap (pego do primeiro unit ativo com referencia)
        for (int i = 0; i < UnitManager.AllActive.Count; i++)
        {
            UnitManager u = UnitManager.AllActive[i];
            if (u != null && u.BoardTilemap != null)
            {
                snapshot.BoardTilemap = u.BoardTilemap;
                break;
            }
        }

        // Tilemap fallback via construcoes
        if (snapshot.BoardTilemap == null)
        {
            for (int i = 0; i < ConstructionManager.AllActive.Count; i++)
            {
                ConstructionManager c = ConstructionManager.AllActive[i];
                if (c != null && c.BoardTilemap != null)
                {
                    snapshot.BoardTilemap = c.BoardTilemap;
                    break;
                }
            }
        }

        // Construcoes proximas do proprio HQ (raio hex)
        if (snapshot.HasHq && snapshot.BoardTilemap != null)
        {
            Vector3Int hqCell = snapshot.HqCell;
            for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
            {
                AIConstructionInfo info = snapshot.KnownConstructions[i];
                Vector3Int cell = info.Cell;
                cell.z = 0;
                if (HexCoordinates.IsWithinRange(snapshot.BoardTilemap, hqCell, cell, defendRadius))
                    snapshot.ConstructionsNearHq.Add(info);
            }
        }

        // Unidades amigas e inimigos visiveis
        for (int i = 0; i < UnitManager.AllActive.Count; i++)
        {
            UnitManager u = UnitManager.AllActive[i];
            if (u == null || u.IsDead || u.IsEmbarked)
                continue;

            if (u.TeamId == aiTeam)
            {
                snapshot.FriendlyUnits.Add(u);
            }
            else if (matchController == null)
            {
                snapshot.VisibleEnemies.Add(u);
            }
            else
            {
                // Usa o sensor diretamente (sem cache FOW) para garantir que
                // VisibleEnemies reflita o estado real da cena no momento do snapshot.
                // HasFiredThisTurn: unidade que atirou neste turno perde stealth.
                bool enforceStealthValidation = matchController.EnableStealthValidation
                    && !u.HasFiredThisTurn;
                bool visible = PodeDetectarSensor.IsTargetObservedByTeam(
                    u,
                    (int)aiTeam,
                    snapshot.BoardTilemap,
                    matchController.TerrainDatabaseRef,
                    null,
                    matchController.EnableLosValidation,
                    matchController.EnableSpotter,
                    enforceStealthValidation);
                if (visible)
                    snapshot.VisibleEnemies.Add(u);
            }
        }

        return snapshot;
    }
}

public class AIConstructionInfo
{
    public Vector3Int Cell;
    public TeamId TeamId;
    public bool IsHq;
    public bool CanProduceUnits;
    public string DisplayName;
    public ConstructionManager Source;
}
