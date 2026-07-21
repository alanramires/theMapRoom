using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class HexCohabitationVisualManager
{
    // Offsets de fileira por andar de ocupacao. Cada combinacao de andares ocupados
    // tem o proprio conjunto: o arranjo que funciona com tres fileiras deixa buraco
    // ou aperta demais quando so duas estao em uso.
    public struct LayerOffsets
    {
        public Vector3 air;
        public Vector3 surface;
        public Vector3 submerged;
        // Deslocamento do coracao e do numero de HP no eixo Y, POR PARTICIPANTE.
        // Cada andar precisa achatar de um jeito: o de cima costuma descer o HUD
        // para nao invadir quem esta acima, o de baixo costuma sofrer o contrario.
        public float airHudY;
        public float surfaceHudY;
        public float submergedHudY;

        public LayerOffsets(
            Vector2 air, float airHudY,
            Vector2 surface, float surfaceHudY,
            Vector2 submerged, float submergedHudY)
        {
            this.air = new Vector3(air.x, air.y, 0f);
            this.surface = new Vector3(surface.x, surface.y, 0f);
            this.submerged = new Vector3(submerged.x, submerged.y, 0f);
            this.airHudY = airHudY;
            this.surfaceHudY = surfaceHudY;
            this.submergedHudY = submergedHudY;
        }
    }

    public static LayerOffsets AirSurface = new LayerOffsets(
        new Vector2(-0.1f, 0.2f), 0f, new Vector2(0f, -0.2f), 0f, Vector2.zero, 0f);
    public static LayerOffsets AirSubmerged = new LayerOffsets(
        new Vector2(-0.1f, 0.2f), 0f, Vector2.zero, 0f, new Vector2(0f, -0.2f), 0f);
    public static LayerOffsets SurfaceSubmerged = new LayerOffsets(
        Vector2.zero, 0f, new Vector2(0f, 0.15f), 0f, new Vector2(0f, -0.25f), 0f);
    public static LayerOffsets FullStack = new LayerOffsets(
        new Vector2(-0.1f, 0.3f), 0f, new Vector2(0f, 0f), 0f, new Vector2(0f, -0.32f), 0f);

    public static Vector3 SharedScale = new Vector3(0.6f, 0.6f, 1f);
    // Espalhamento horizontal entre unidades da MESMA banda dividindo o hex
    // (ex.: dois caças de times diferentes em hex contestado).
    public static float IntraLayerSpread = 0.18f;
    private static bool rescanWhenNeutralPending;
    private static TurnStateManager cachedTurnStateManager;
    private static MatchController cachedMatchController;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        UnitOccupancyRules.OnUnitOccupancyChanged -= OnOccupancyChanged;
        UnitOccupancyRules.OnUnitOccupancyChanged += OnOccupancyChanged;
        CursorController.OnCursorReturnedToNeutral -= OnCursorReturnedToNeutral;
        CursorController.OnCursorReturnedToNeutral += OnCursorReturnedToNeutral;
        MatchController.OnFogOfWarUpdated -= OnFogOfWarUpdated;
        MatchController.OnFogOfWarUpdated += OnFogOfWarUpdated;
        MatchController.OnActiveTeamChanged -= OnActiveTeamChanged;
        MatchController.OnActiveTeamChanged += OnActiveTeamChanged;

        // Scan no próximo frame para pegar unidades já posicionadas (load/start de cena)
        var bootstrap = new GameObject("[HexCohabitationBootstrap]");
        Object.DontDestroyOnLoad(bootstrap);
        bootstrap.AddComponent<CohabitationBootstrap>();
    }

    public static void ScanAllCells()
    {
        if (!IsConfirmedNeutralState())
        {
            rescanWhenNeutralPending = true;
            return;
        }

        var visited = new HashSet<(Tilemap, Vector3Int)>();
        var all = UnitManager.AllActive;

        for (int i = 0; i < all.Count; i++)
        {
            UnitManager u = all[i];
            if (u == null || u.IsEmbarked || u.IsDead || u.BoardTilemap == null)
                continue;

            Vector3Int cell = u.CurrentCellPosition;
            cell.z = 0;

            var key = (u.BoardTilemap, cell);
            if (visited.Contains(key))
                continue;

            visited.Add(key);
            EvaluateCell(u.BoardTilemap, cell);
        }
    }

    private static void OnOccupancyChanged(UnitManager unit, Vector3Int previousCell, Vector3Int currentCell)
    {
        if (unit == null)
            return;

        Tilemap tilemap = unit.BoardTilemap;
        if (tilemap == null)
            return;

        // A animacao atualiza CurrentCellPosition a cada hex. Enquanto a acao ainda
        // e provisoria, consultar os demais ocupantes faria o tamanho/offset da unidade
        // revelar algo escondido no FOW. A unidade movel pode usar seu visual normal;
        // a coabitacao confirmada so sera reconstruida depois do retorno a Neutral.
        if (!IsConfirmedNeutralState())
        {
            unit.ClearCohabitationVisual();
            rescanWhenNeutralPending = true;
            return;
        }

        previousCell.z = 0;
        currentCell.z = 0;

        if (previousCell != currentCell)
            EvaluateCell(tilemap, previousCell);

        EvaluateCell(tilemap, currentCell);
    }

    private static void EvaluateCell(Tilemap tilemap, Vector3Int cell)
    {
        List<UnitManager> units = UnitOccupancyRules.GetUnitsAtCell(tilemap, cell);

        List<UnitManager> airUnits = new List<UnitManager>();
        List<UnitManager> submergedUnits = new List<UnitManager>();
        List<UnitManager> surfaceUnits = new List<UnitManager>();

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager u = units[i];
            if (u == null || u.IsEmbarked || u.IsDead)
                continue;
            if (!IsVisibleForLocalObserver(u))
                continue;

            bool submerged = u.GetDomain() == Domain.Submarine
                && u.GetHeightLevel() == HeightLevel.Submerged;
            if (submerged)
            {
                submergedUnits.Add(u);
                continue;
            }

            HeightBand band = OccupancyResolver.GetHeightBand(u);
            if (band == HeightBand.Air)
                airUnits.Add(u);
            else if (band == HeightBand.Blocking)
                surfaceUnits.Add(u);
        }

        // Reset baseline: so re-aplicamos visual quando houver de fato divisao de hex.
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] != null)
                units[i].ClearCohabitationVisual();
        }

        bool hasAir = airUnits.Count > 0;
        bool hasSurface = surfaceUnits.Count > 0;
        bool hasSubmerged = submergedUnits.Count > 0;
        int occupiedLayers = (hasAir ? 1 : 0) + (hasSurface ? 1 : 0) + (hasSubmerged ? 1 : 0);

        // Dois ou mais andares ocupados: cada um ganha a propria fileira, com o
        // arranjo tunado para aquela combinacao especifica. A visibilidade vem do
        // FOW confirmado — ao voltar a ficar oculto, o proximo scan remove o offset.
        if (occupiedLayers >= 2)
        {
            LayerOffsets layout;
            if (hasAir && hasSurface && hasSubmerged)
                layout = FullStack;
            else if (hasAir && hasSurface)
                layout = AirSurface;
            else if (hasAir)
                layout = AirSubmerged;
            else
                layout = SurfaceSubmerged;

            ApplyLayerFan(airUnits, layout.air, layout.airHudY);
            ApplyLayerFan(surfaceUnits, layout.surface, layout.surfaceHudY);
            ApplyLayerFan(submergedUnits, layout.submerged, layout.submergedHudY);
            return;
        }

        // Um andar so: divide apenas o aereo contestado, que comporta mais de um
        // token na pratica. Superficie+superficie e submerso+submerso contestados
        // nao recebem efeito visual, como antes.
        if (hasAir && airUnits.Count >= 2)
            ApplyAirOnlyRows(airUnits);
    }

    private static void OnCursorReturnedToNeutral()
    {
        // O evento ocorre antes de o MatchController publicar o delta confirmado de
        // FOW. O bootstrap executa o scan no LateUpdate, depois desse processamento.
        rescanWhenNeutralPending = true;
    }

    private static void OnFogOfWarUpdated()
    {
        if (IsConfirmedNeutralState())
            rescanWhenNeutralPending = true;
    }

    private static void OnActiveTeamChanged(int teamId)
    {
        if (IsConfirmedNeutralState())
            rescanWhenNeutralPending = true;
    }

    private static bool IsConfirmedNeutralState()
    {
        if (cachedTurnStateManager == null)
            cachedTurnStateManager = Object.FindAnyObjectByType<TurnStateManager>();

        return cachedTurnStateManager == null ||
               cachedTurnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral;
    }

    private static bool IsVisibleForLocalObserver(UnitManager unit)
    {
        if (unit == null)
            return false;

        if (cachedMatchController == null)
            cachedMatchController = Object.FindAnyObjectByType<MatchController>();

        return cachedMatchController == null || cachedMatchController.IsUnitVisibleForActiveTeam(unit);
    }

    // Distribui as unidades de uma mesma banda em leque horizontal ao redor do offset base.
    // Com 1 unidade, mantem o offset base exatamente (sem regressao no caso ar+chao classico).
    private static void ApplyLayerFan(List<UnitManager> layerUnits, Vector3 baseOffset, float hudOffsetY)
    {
        int n = layerUnits.Count;
        if (n == 0)
            return;

        float center = (n - 1) * 0.5f;
        for (int i = 0; i < n; i++)
        {
            UnitManager u = layerUnits[i];
            if (u == null)
                continue;

            float dx = (i - center) * IntraLayerSpread;
            Vector3 offset = baseOffset + new Vector3(dx, 0f, 0f);
            u.ApplyCohabitationVisual(offset, SharedScale, hudOffsetY);
        }
    }

    // Sem ocupante de superficie, aeronaves coabitando usam duas linhas, como
    // no visual ar+terrestre. Para 3+ aeronaves, cada linha abre seu proprio leque.
    private static void ApplyAirOnlyRows(List<UnitManager> airUnits)
    {
        int count = airUnits.Count;
        if (count == 0)
            return;

        // Sem outro andar em jogo, o aereo contestado reaproveita as duas fileiras
        // do arranjo aereo+superficie.
        int upperCount = (count + 1) / 2;
        ApplyLayerFanRange(airUnits, 0, upperCount, AirSurface.air, AirSurface.airHudY);
        ApplyLayerFanRange(airUnits, upperCount, count - upperCount, AirSurface.surface, AirSurface.airHudY);
    }

    private static void ApplyLayerFanRange(
        List<UnitManager> units,
        int startIndex,
        int count,
        Vector3 baseOffset,
        float hudOffsetY)
    {
        if (units == null || count <= 0)
            return;

        float center = (count - 1) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            UnitManager unit = units[startIndex + i];
            if (unit == null)
                continue;

            float dx = (i - center) * IntraLayerSpread;
            Vector3 offset = baseOffset + new Vector3(dx, 0f, 0f);
            unit.ApplyCohabitationVisual(offset, SharedScale, hudOffsetY);
        }
    }

    private class CohabitationBootstrap : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null; // espera um frame para todos os OnEnable rodarem
            ScanAllCells();
        }

        private void LateUpdate()
        {
            if (!rescanWhenNeutralPending || !IsConfirmedNeutralState())
                return;

            rescanWhenNeutralPending = false;
            ScanAllCells();
        }
    }
}
