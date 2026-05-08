# Relatorio de Atualizacao - v2.0.21

## Em uma frase
AI de Fogo Indireto documentada e validada: artilharia já opera com doutrina própria, forward observer obrigatório e dois perfis de posicionamento configuráveis por `UnitData`.

## O que isso trouxe na pratica
- Artilharia prioriza alvos próximos ao setor de interesse, com bônus para inimigos enfraquecidos e dentro de prédios.
- Unidades estacionárias (`longRangeStationary`) ficam na posição e nunca avançam às cegas.
- Unidades móveis reposicionam para trás (`preferRepositionToMaxRange`) ou avançam em direção ao anchor, dependendo do perfil.
- Forward observer já é exigência do sensor — a IA não consegue atirar além da sua visão sem aliado com LoS ao alvo.

## Doutrina implementada

**Anchor**: célula do prédio-alvo do objetivo atribuído (ou HQ inimigo / inimigo mais próximo quando rogue).

**Num turno típico:**

1. Varre células alcançáveis + posição atual, coleta alvos via `PodeMirarSensor`.
2. Escolhe melhor alvo por score:
   - Base 10 000
   - −500 × distância do alvo ao anchor (foca no setor de interesse)
   - +120 × HP faltando (prefere alvos enfraquecidos)
   - +1 500 se alvo está em prédio (interrompe captura)
   - ±score de alcance: `preferRepositionToMaxRange` → +30 × distância; caso contrário −5 × distância
3. Se sem alvo: reposiciona ou aguarda (estacionário fica parado).

**Forward observer** (regra do sensor `PodeMirarSensor`): se o atirador não enxerga o alvo diretamente, é exigido um aliado a ≤3h do alvo com LoS válida. Sem observador → disparo bloqueado.

## Estados de decisão

| Contexto | Handler |
|----------|---------|
| Sem objetivo no plano (rogue) | `DecideRogueFireSupportAction` — anchor = inimigo mais próximo ou HQ inimigo |
| Objetivo em `Defending` | `DecideFireSupportDefenderAction` — mesmo scoring, `defensiveContext = true` |
| Objetivo em andamento | `DecideAssignedFireSupportAction` — anchor = prédio capturável do setor |

## Arquivos

- `Assets/Scripts/Match/AI/AIController.FireSupport.cs` — entrada, roteamento por estado do objetivo
- `Assets/Scripts/Match/AI/AIController.FireSupport.Defender.cs` — contexto defensivo
- `Assets/Scripts/Match/AI/AIController.FireSupport.Rogue.cs` — sem plano atribuído
- `Assets/Scripts/Match/AI/AIController.FireSupport.Helpers.cs` — `TryBuildBestFireSupportAttack`, `ScoreFireSupportTarget`, `TryFindFireSupportRepositionCell`, `EnumerateFireSupportCandidateCells`
- `Assets/Scripts/Match/AI/AIController.Router.cs` — `TryDecideFireSupportAction` chamado antes do fallback `HexEvaluator`

## Campos relevantes em UnitData

- `longRangeStationary` — não move se não tiver alvo (ex: canhão fixo)
- `preferRepositionToMaxRange` — ao reposicionar, prefere célula mais distante do anchor (fica na retaguarda)
