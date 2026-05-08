# Relatorio de Atualizacao - v2.0.20

## Em uma frase
AI Assault completada: escolta lê congestionamento de rota e escolhe caminhos laterais livres, com modo de avanço automático quando o capturador está próximo do objetivo.

## O que isso trouxe na pratica
- Tanques escolta param de ficar presos atrás de grupos de aliados — preferem rotas laterais desimpedidas.
- Quando o capturador já está perto do objetivo, a escolta entra em modo de avanço e para de rondar a zona de cobertura.
- Escoltas não são mais enviadas para defesa de base estável a distâncias absurdas (cap de 8 PM).
- Badges no HUD identificam corretamente ataque a HQ inimigo (`>>`) vs defesa do próprio HQ (`!`).
- Ferramenta de diagnóstico "Caminhos Válidos" ganha modo "Passando Por" (A→B→C) e filtra células de trânsito no mapa de progressão.

## Principais melhorias

1. **Forward Congestion Penalty**
   - `ComputeForwardCongestion`: mede a fração de vizinhos "à frente" (menor custo ao destino) que estão ocupados por aliados.
   - Penalidade de `700 × congestion` no score da célula candidata.
   - Resultado: centro da estrada com 5 aliados à frente (cong=1.0 → −700) perde para estrada lateral livre (cong=0.0 → sem penalidade).

2. **Advance Mode para escolta**
   - Quando o capturador mais próximo do objetivo tem `DistanceToObjective ≤ 6 PM`, a escolta entra em advance mode.
   - Efeitos: `scoutRingBonus = 0` (suprime penalidade de anel que chegava a −600), pesos de progressão dobrados (`routeProgressWeight` 450→900, `routeProgressBonus` 350→700, `routeProgressPenalty` 600→1200).
   - Resultado: escolta segue ativamente o capturador em vez de rondar uma zona de cobertura ampla.

3. **Cap de distância para defesa estável (Loop 3)**
   - `DefenseEscortMaxPM = 8f`: escoltas só são alocadas a planos de defesa de base se estiverem a ≤8 PM.
   - Impede que tanques do front sejam reatribuídos a bases distantes sem sentido tático.

4. **Badges de HQ corretas**
   - `ApplyPlanHUD` detecta se o setor é uma base (Base1–Base4) e, em caso positivo, consulta `ConstructionManager.AllActive` para identificar o dono do HQ no setor.
   - `>>` = atacar HQ inimigo; `!` = defender o próprio HQ.
   - Setor não-base continua usando a inicial da letra (`B` = Bravo, `C` = Charlie, etc.).

5. **Tool: Caminhos Válidos — "Passando Por" (A→B→C)**
   - Novo campo **B (waypoint)** no painel de progressão.
   - Calcula perna 1 (A→B via `CalculateMovementCostMap`) e perna 2 (B→C via cost map reverso da chegada).
   - Exibe custo total `leg1 + leg2 PM` com indicação se B é alcançável na perna 1.
   - Ponto A = amarelo, B = magenta com label `"3+8=11PM"`, C = ciano.

6. **Tool: filtragem de células de trânsito na progressão**
   - `CalcularCaminhosValidos` retorna apenas onde a unidade pode *parar*.
   - Círculos de progressão são suprimidos em células ocupadas por aliados (trânsito permitido pelo pathfinding, mas parada inválida).
   - Nota atualizada no painel: "respeita ocupação por aliados".

## Arquivos modificados

- `Assets/Scripts/Match/AI/AIController.Assault.cs` — constantes `AdvancedCapturerThreshold`, `ForwardCongestionWeight`; `GetBestCapturerDistanceToObjective`; passagem de `bestCapturerDist` para `FindAssaultEscortCoverCell`.
- `Assets/Scripts/Match/AI/AIController.Assault.Defender.cs` — `FindAssaultEscortCoverCell` e `ScoreAssaultEscortCover` com `bestCapturerDist`/`occupied`; `ComputeForwardCongestion`; struct `AssaultEscortCoverEvaluation` ampliado; log `cong=` e `[ADVANCE MODE]`.
- `Assets/Scripts/Match/AI/AIController.PlanEvaluator.cs` — `DefenseEscortMaxPM = 8f` no loop 3; `ApplyPlanHUD` reescrito com `FindHQTeamInSector`.
- `Assets/Editor/CaminhosValidosWindow.cs` — modo Passando Por completo; filtro de ocupação aliada; detecção de mudança em B.
