# Relatorio de Visao e Spotting

Data base: 2026-05-25 (revisado; base original: 2026-03-06)

## Escopo
- Motor principal de FoW/deteccao: `Assets/Scripts/Sensors/PodeDetectarSensor.cs`
- Resolucao de visao por celula: `Assets/Scripts/Terrain/TerrainVisionResolver.cs`
- Atributo base de unidade: `UnitData.visao`
- Vision Specializations por alvo/camada: `UnitData.visionSpecializations` + `UnitData.ResolveVisionFor(...)`
- Persistencia/escopo de revelacao stealth: `UnitData.stealthRevealScope` + `UnitData.stealthVisibleIfDetectedForTurns`

## Motor de FoW: PodeDetectarSensor
O `PodeDetectarSensor` (adicionado apos revisao anterior) e o nucleo do sistema de visao/FoW. Expoe quatro APIs principais:

| Metodo | Uso |
|--------|-----|
| `CollectVisibleCells(...)` | Calcula todos os hexes visiveis para um observador, com opcoes detalhadas |
| `CollectVisibleCellsForFogOfWar(...)` | Variante FoW: sem spotter, sem layer de ocupante, range-only para AirHigh |
| `CollectDetection(...)` | Detecta unidades inimigas e classifica em 4 buckets (ver abaixo) |
| `IsTargetObservedByTeam(...)` | Check booleano para FoW/UI: alguma unidade aliada ve o alvo? |
| `IsTargetObservedByTeamWithoutForwardObserver(...)` | Idem, sem spotter — usado para FoW de hexes |

### Os 4 buckets de CollectDetection
- `detectedStealthOutput`: unidades furtivas que foram detectadas com sucesso
- `undetectedStealthOutput`: furtivas no alcance mas sem deteccao (sem LOS ou sem especializacao)
- `spottedCandidatesOutput`: unidades normais avistadas
- `inRangeButLosBlockedOutput`: no alcance mas LOS bloqueada (furtivas ou nao)

## Alcance de visao padrao por unidade
- Campo de base: `UnitData.visao`.
- Apos rebalanceamento (ver doc 01), visao varia de 1 a 5 (nao mais uniforme em 3).
- `ResolveObserverMaxVisionRange` usa o maior valor entre `visao` base e todos os ranges das especializacoes para determinar o raio maximo de BFS.

## Excecoes de visao (por dominio/camada do alvo)
- O sistema suporta specialization por `Domain/HeightLevel` do alvo em `UnitData.visionSpecializations`.
- O sensor usa `ResolveVisionFor(targetDomain, targetHeightLevel)` para calcular alcance efetivo de observacao.
- Cada excecao possui `detectUnitsWithFollowingSkills` (lista): detecta alvos que tenham qualquer skill da lista.
- **Restricao de familia**: especializacoes aquaticas (Naval/Submarine) so podem revelar hexes dentro da familia aquatica. Impede que sonar revele terra ou ar.
- `ResolveLosValidationFor(...)` por especializacao permite desligar LoS para um dominio/altura especifico (ex.: sonar/radar detecta por alcance, sem LoS).

## Como terreno afeta visao
`TerrainVisionResolver.Resolve(...)` compoe visao com:
- `terrain.ev` (elevacao)
- `terrain.blockLoS`
- overrides opcionais de construcao e estrutura (ambos acumulam: `composedEv = Max(terrainEv, overrideEv)`)
- overrides aereos via `DPQAirHeightConfig.TryGetVisionFor(...)`

## Montanha e floresta
- Floresta: EV=1, blockLoS=true
- Montanha: EV=2, blockLoS=true, com possibilidade de heranca de EV para atirador no terreno
- Na pratica, elevam chance de bloqueio/oclusao e exigem melhor posicionamento de observadores.

## Como spotters funcionam
No `PodeDetectarSensor`:
- Flag de sistema: `enableSpotter` (vem do `MatchController`).
- Spotter so e aplicado para alvos `Land/Surface` e `Naval/Surface`. Alvos aereos e submarinos nao admitem forward observer.
- Se alvo estiver fora da observacao direta do atacante (ou LoS direta falhar em contexto indireto), o sensor tenta `TryFindForwardObserverForVirtualCell(...)`.
- Observador avancado valida quando confirma LoS/criterio de observacao ate o alvo.
- Com spotter desligado (FoW), a validacao indireta nao abre excecao por observador.

## Deteccao de submarinos: distancia aquatica
Alvo `Submarine/Submerged` usa um distance map separado (`aquaticWorkspace`) que so atravessa celulas aquaticas (Naval/Surface ou Submarine/Submerged). Isso impede deteccao de submarino "atravessando" terra no pathfinding do BFS.

## Deteccao AirHigh por range (sem LoS)
Se `DPQAirHeightConfig` indicar `blockLoS = false` para `Air/AirHigh`, o sensor pula a validacao de LoS e usa apenas alcance. Modela deteccao radar de aeronaves em altitude.

## Como artilharia verifica alvos visiveis
Fluxo de validacao em `PodeMirarSensor` combina:
1. alcance e municao da arma
2. compatibilidade de camada/dominio
3. validacao LDT (quando habilitada)
4. validacao LoS com EV/blockLoS
5. fallback por spotter (quando habilitado e alvo e Land/Naval Surface)
6. validacao stealth por camada/skill (`IsTargetDetectableByAttacker`)

## Stealth (estado atual)
- Status de desenvolvimento: **experimental / nao validado**. A direcao atual e promissora, mas ainda precisa de testes de equilibrio e casos limite em gameplay real.
- Ja existe gate de stealth no `PodeDetectarSensor`:
  - alvo com skill id (`stealth`, `furtividade`, `submarine_stealth`, `submerged_stealth`) tambem entra como stealth.
- Para detectar alvo stealth, o atacante precisa de specialization em `visionSpecializations` para o `Domain/HeightLevel` do alvo e:
  - match de skill em `detectUnitsWithFollowingSkills`, via `SkillData.id` (comparacao case-insensitive).
- Ao detectar, o alvo pode ficar revelado por N turnos (`stealthVisibleIfDetectedForTurns`, default 1) e com escopo configuravel (`stealthRevealScope`):
  - `AllTeams`: todos os times podem alvejar enquanto a janela estiver ativa;
  - `DetectorTeamOnly`: somente o time que detectou ganha a janela de disparo.
- Sem deteccao ativa e sem detector valido, mesmo com LoS e alcance validos, o disparo fica invalido com `aim.invalid.stealth`.
- Se `enableStealthValidation = false` (Game Setup), alvo furtivo e automaticamente detectado.

## Cache do sistema de visao
O `PodeDetectarSensor` mantem tres caches para performance:
- `terrainCacheForRefresh`: terreno por celula (4096 entradas), limpo a cada refresh de FoW.
- `losCacheForRefresh`: resultado de LoS por par (observer, target) (8192 entradas), limpo a cada refresh.
- `collectVisibleCellsCache`: resultado de `CollectVisibleCells` por chave composta (128 entradas), invalidado por `ThreatRevisionTracker.GlobalBoardRevision` e `teamObserverRevision`.

## Resumo pratico
- O jogo modela visao como sistema composto (unidade + terreno + camada + regras globais + observadores).
- Spotter e um habilitador de fogo indireto/fora da visao direta, nao apenas um bonus numerico, e so funciona para alvos terrestres/navais de superficie.
- Deteccao de submarinos usa BFS aquatico — nao e possivel "pingar" atraves de terra.
- Deteccao de aeronaves em altitude alta pode ser por puro alcance (radar), sem LoS.
- `visionSpecializations` controla tres dimensoes: alcance de observacao, permissao de detectar stealth por camada, e se LoS e exigida.
