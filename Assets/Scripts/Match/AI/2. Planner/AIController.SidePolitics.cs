using System.Collections.Generic;
using UnityEngine;

// Strong/Weak Side Politic (PROTOTIPO — flag `strongWeakSidePolitic` no root, off por padrao).
//
// Doutrina de concentracao de forca ao estilo AWBW: massa/elite no lado FORTE, resistencia
// no lado FRACO ("segurar como der e aceitar que pode cair"). Esta primeira fase mexe SO na
// priorizacao do elite (fire support) ja existente — NAO infla a contagem de slots por setor,
// justamente para nao criar o laco de realimentacao "rico fica mais rico" via demanda.
//
// Duas salvaguardas contra o thrashing que a atribuicao (otimizador global sem histerese) teria:
//   1) EMA por eixo: a classificacao so migra apos evidencia sustentada, nao turno a turno.
//   2) Turno 0 e 1 sempre equilibrados: a AI ainda nao decidiu onde a guerra vai pesar.
//
// A contagem de slots (strong +1 / weak -1 com piso 1, jamais 0) fica para uma fase seguinte,
// deliberadamente separada e so depois de validar em jogo que este vies nao snowballa demais.
public partial class AIController
{
    public enum AxisSide { Strong, Balanced, Weak }

    // Peso composto de um eixo, mesma formula do HUD Shopping Pressure:
    // SCORE (criterio proprio da AI para importancia do eixo) domina, PROGRESSO (avanco da
    // guerra no corredor) reforca, PROFUNDIDADE (investimento acumulado) pesa de leve.
    // So serve para ranquear os eixos entre si — a escala absoluta e irrelevante.
    public static float AxisCompositeWeight(AIShoppingPlanner.AxisTransportPressureInspection axis)
    {
        return axis.Score + (axis.Progress * 50f) + (axis.Depth * 5f);
    }

    private const float SidePoliticEmaAlpha = 0.35f;      // suavizacao: 1 = sem historia, 0 = congelado.
    private const float SidePoliticStrongBand = 1.15f;    // peso >= media * banda -> forte.
    private const float SidePoliticWeakBand = 0.85f;      // peso <= media * banda -> fraco.
    private const float FireSupportStrongSideBias = 240f; // pull de elite pro lado forte.
    private const float FireSupportWeakSidePenalty = 180f; // alivio de elite no lado fraco (assimetrico: ainda segura).

    // Estado de runtime (nao vai pro save): a EMA reinicia numa carga de save, o que e aceitavel
    // num prototipo — apenas re-aquece ao longo de alguns turnos.
    private readonly Dictionary<int, float> smoothedAxisWeightByEixo = new Dictionary<int, float>();
    private readonly Dictionary<int, AxisSide> axisSideByEixo = new Dictionary<int, AxisSide>();
    private bool axisSidePoliticUndecided = true;

    // Chamado uma vez no topo do BuildObjectivePlan, ja com currentAxisMap construido.
    private void UpdateAxisSidePolitics(AIWorldSnapshot snapshot)
    {
        axisSideByEixo.Clear();
        axisSidePoliticUndecided = true;
        if (!strongWeakSidePolitic || snapshot == null)
            return;

        AIShoppingPlanner.OperationalPressureInspection pressure =
            AIShoppingPlanner.InspectOperationalPressure(snapshot);
        if (pressure == null || pressure.Axes.Count == 0)
            return;

        float sum = 0f;
        int count = 0;
        foreach (AIShoppingPlanner.AxisTransportPressureInspection axis in pressure.Axes)
        {
            float instant = AxisCompositeWeight(axis);
            float prev = smoothedAxisWeightByEixo.TryGetValue(axis.Eixo, out float s) ? s : instant;
            float ema = Mathf.Lerp(prev, instant, SidePoliticEmaAlpha);
            smoothedAxisWeightByEixo[axis.Eixo] = ema;
            sum += ema;
            count++;
        }

        float mean = count > 0 ? sum / count : 0f;
        // Turno 0 e 1: guerra indefinida. Media desprezivel: idem (nada a distinguir ainda).
        bool undecided = snapshot.TurnNumber <= 1 || mean < 0.01f;
        axisSidePoliticUndecided = undecided;
        if (undecided)
            return;

        foreach (AIShoppingPlanner.AxisTransportPressureInspection axis in pressure.Axes)
        {
            float ema = smoothedAxisWeightByEixo.TryGetValue(axis.Eixo, out float s) ? s : 0f;
            AxisSide side;
            if (ema >= mean * SidePoliticStrongBand)
                side = AxisSide.Strong;
            else if (ema <= mean * SidePoliticWeakBand)
                side = AxisSide.Weak;
            else
                side = AxisSide.Balanced;
            axisSideByEixo[axis.Eixo] = side;
        }
    }

    public AxisSide GetAxisSide(int eixo)
    {
        if (!strongWeakSidePolitic || axisSidePoliticUndecided)
            return AxisSide.Balanced;
        return axisSideByEixo.TryGetValue(eixo, out AxisSide side) ? side : AxisSide.Balanced;
    }

    public AxisSide GetSectorSide(ConstructionSector sector)
    {
        if (!strongWeakSidePolitic || currentAxisMap == null)
            return AxisSide.Balanced;
        return GetAxisSide(currentAxisMap.GetEixo(sector));
    }

    // Delta somado ao score de atribuicao do elite (fire support): puxa pro lado forte,
    // alivia o fraco. Zero quando a politica esta off ou o eixo esta equilibrado.
    private float ComputeFireSupportSideBias(ConstructionSector sector)
    {
        switch (GetSectorSide(sector))
        {
            case AxisSide.Strong: return FireSupportStrongSideBias;
            case AxisSide.Weak: return -FireSupportWeakSidePenalty;
            default: return 0f;
        }
    }
}
