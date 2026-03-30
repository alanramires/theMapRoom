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

    public Tilemap BoardTilemap;
    public List<UnitManager> FriendlyUnits = new List<UnitManager>();
    public List<UnitManager> VisibleEnemies = new List<UnitManager>();

    public static AISnapshot Build(TeamId aiTeam, MatchController matchController)
    {
        AISnapshot snapshot = new AISnapshot { AiTeam = aiTeam };

        // Todas as construcoes sao conhecidas (intel publica)
        for (int i = 0; i < ConstructionManager.AllActive.Count; i++)
        {
            ConstructionManager c = ConstructionManager.AllActive[i];
            if (c == null)
                continue;

            snapshot.KnownConstructions.Add(new AIConstructionInfo
            {
                Cell = c.CurrentCellPosition,
                TeamId = c.TeamId,
                IsHq = c.IsPlayerHeadQuarter,
                DisplayName = c.ConstructionDisplayName
            });

            if (c.IsPlayerHeadQuarter && c.TeamId == aiTeam)
            {
                snapshot.HasHq = true;
                snapshot.HqCell = c.CurrentCellPosition;
                snapshot.HqCell.z = 0;
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
            else if (matchController == null || matchController.IsUnitVisibleForActiveTeam(u))
            {
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
    public string DisplayName;
}
