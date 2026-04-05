# Relatorio de Atualizacao - v1.6.2

## Em uma frase
A IA agora tem postura estratégica de equipe: em vez de reagir unidade por unidade, ela decide se está atacando, defendendo ou invadindo — e todas as unidades ajustam o comportamento de acordo.

## O que isso trouxe na prática
- A IA entra em modo de defesa quando inimigos se aproximam do QG, puxa as unidades dispersas de volta ao perímetro e para de capturar objetivos distantes.
- Quando domina o mapa, a IA escolhe uma base inimiga como alvo de invasão e manda força prioritária para lá.
- No ataque normal, nada muda — as unidades seguem seus planos de setor como antes.
- O designer configura quando cada postura ativa, qual ícone aparece no HUD e qual raio define o perímetro defensivo — tudo em assets, sem tocar código.

## Principais melhorias

1. **Battle Stance — postura estratégica de equipe**
   - A IA avalia uma vez por turno se está em Attack, Defend ou Invasion.
   - Essa decisão é data-driven: cada postura tem um `BattleStanceData` com tipo de ativação e limiar configurável.
   - Invasion ativa quando a IA controla mais de X% do mapa. Defend ativa quando há inimigos dentro do raio do QG. Attack é o fallback.

2. **Defesa com perímetro real**
   - `hqEngagementRadius`: unidades ignoram inimigos fora desse raio — não correm atrás de todo mundo.
   - `defenderPullRadius`: unidades que estão fora do perímetro são convocadas de volta ao QG. Unidades já dentro do raio continuam combatendo normalmente.
   - Captura e coesão de escolta são suspensas enquanto a postura de defesa estiver ativa.

3. **Invasão com alvo escolhido por inteligência**
   - Quando em Invasion, o planner seleciona a base inimiga com menor resistência e maior proximidade ao QG próprio.
   - Um plano de invasão com prioridade máxima é gerado dinamicamente a cada turno — não é fixo.
   - O badge `>>` aparece nas unidades designadas para a invasão.

4. **Battle Stance no HUD da unidade**
   - Badge combinado exibe postura + plano: `DEF`, `ATK`, `INV`, ou `INV/>>` quando há plano de invasão ativo.
   - Ícone configurável por postura: fortaleza para Defesa, raio para Invasão, vazio para Ataque.
   - Visibilidade controlada pela mesma flag que governa o badge de plano (`showPlanDebugAtUnit`).

5. **Bases numeradas para mapas de 4 jogadores**
   - `BaseTeam` substituído por `Base1`, `Base2`, `Base3`, `Base4`.
   - O designer atribui qual base pertence a qual posição no mapa — o código não presume nada.
   - `SectorManager` ganhou uma seção separada de `Base Infos` no inspector, paralela aos setores de batalha.

6. **Remoção dos Fixed Plans**
   - Os planos fixos `AIPlan_Attack` e `AIPlan_Defense` nunca eram ativados na prática — existiam como código morto.
   - Foram removidos completamente: assets, classes, lógica no evaluator e campos nos editors.
   - O comportamento que eles tentavam modelar agora é coberto pela camada de Battle Stance.

## Bloco técnico curto

- `BattleStanceData.cs` + `BattleStanceDatabase.cs`: novos ScriptableObjects que definem posturas.
- `AIPlayerController.cs`: avalia stance via `profile.EvaluateStance()`, aplica `defenderPullRadius` em Phase 2, passa `currentStance` para o planner.
- `AIPlanEvaluator.cs`: `TryActivateInvasionPlan()` gera plano dinâmico com badge `>>`; parâmetro `AIPlanDatabase` removido da assinatura.
- `BeginnerAIProfile.cs`: implementa `EvaluateStance()` com prioridade Invasion → Defend → Attack.
- `UnitManager.cs` + `UnitHudController.cs`: `SetAIStance()`, badge combinado, `SetStanceIcon()`.
- `AIPlanData.cs` + `AIPlanDatabase.cs`: deletados. `AIPlanIntent.Plan` removido.
- `ConstructionSector.cs`: `Base1–Base4` no lugar de `BaseTeam`; helper `IsBase()`.
- `SectorManager.cs` + `SectorManagerEditor.cs`: lista `baseInfos` separada dos setores de batalha.

## Resultado
A IA deixou de ser um conjunto de unidades reagindo individualmente e passou a ter uma leitura de situação coletiva. Defesa, ataque e invasão são posturas configuráveis em asset — o mesmo código, comportamentos completamente diferentes dependendo do perfil que o designer montar.
