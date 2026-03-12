# Relatorio de Visao e Spotting

## Escopo
- Sensores/LoS: `Assets/Scripts/Sensors/PodeMirarSensor.cs`
- Resolucao de visao por celula: `Assets/Scripts/Terrain/TerrainVisionResolver.cs`
- Atributo base de unidade: `UnitData.visao`
- Vision Specializations por alvo/camada: `UnitData.visionSpecializations` + `UnitData.ResolveVisionFor(...)`
- Persistencia/escopo de revelacao stealth: `UnitData.stealthRevealScope` + `UnitData.stealthVisibleIfDetectedForTurns`

## Alcance de visao padrao por unidade
- Campo de base: `UnitData.visao`.
- No banco atual de unidades, o valor esta praticamente padronizado em `3`.
- Isso indica que diferenciacao de observacao vem mais de contexto (LoS, EV, spotter, altitude) do que de um spread alto de `visao`.

## Excecoes de visao (por dominio/camada do alvo)
- O sistema agora suporta specialization por `Domain/HeightLevel` do alvo em `UnitData.visionSpecializations`.
- O sensor usa `ResolveVisionFor(targetDomain, targetHeightLevel)` para calcular alcance efetivo de observacao.
- Cada excecao possui `detectUnitsWithFollowingSkills` (lista): detecta alvos que tenham qualquer skill da lista.
- Uso pratico: unidade com visao base X pode ter alcance diferente contra `Land/Surface`, `Naval/Surface`, `Submarine/Submerged`, etc.
- Isso virou o mecanismo principal para experimentar deteccao especializada (ex.: anti-sub) sem quebrar o resto da regra.

## Como terreno afeta visao
`TerrainVisionResolver.Resolve(...)` compoe visao com:
- `terrain.ev` (elevacao)
- `terrain.blockLoS`
- overrides opcionais de construcao e estrutura
- overrides aereos via `DPQAirHeightConfig.TryGetVisionFor(...)`

## Montanha e floresta
- Floresta: EV=1, blockLoS=true
- Montanha: EV=2, blockLoS=true, com possibilidade de heranca de EV para atirador no terreno
- Na pratica, elevam chance de bloqueio/oclusao e exigem melhor posicionamento de observadores.

## Como spotters funcionam
No `PodeMirarSensor`:
- Flag de sistema: `enableSpotter` (vem do `MatchController`).
- Se alvo estiver fora da observacao direta do atacante (ou LoS direta falhar em contexto indireto), o sensor tenta `TryFindForwardObserverForIndirectFire(...)`.
- Observador avancado valida disparo quando confirma LoS/criterio de observacao ate o alvo.
- Com spotter desligado, parte da validacao indireta nao abre excecao por observador.

## Como artilharia verifica alvos visiveis
Fluxo de validacao em `PodeMirarSensor` combina:
1. alcance e municao da arma
2. compatibilidade de camada/dominio
3. validacao LDT (quando habilitada)
4. validacao LoS com EV/blockLoS
5. fallback por spotter (quando habilitado)
6. validacao stealth por camada/skill (`IsTargetDetectableByAttacker`)

## Stealth (estado atual)
- Status de desenvolvimento: **experimental / nao validado**. A direcao atual e promissora, mas ainda precisa de testes de equilibrio e casos limite em gameplay real.
- Ja existe gate de stealth no `PodeMirarSensor`:
  - alvo com skill id (`stealth`, `furtividade`, `submarine_stealth`, `submerged_stealth`) tambem entra como stealth.
- Para detectar alvo stealth, o atacante precisa de specialization em `visionSpecializations` para o `Domain/HeightLevel` do alvo e:
  - match de skill em `detectUnitsWithFollowingSkills`,
  - `detectUnitsWithFollowingSkills` contendo uma skill do alvo.
- Ao detectar, o alvo pode ficar revelado por N turnos (`stealthVisibleIfDetectedForTurns`, default 1) e com escopo configuravel (`stealthRevealScope`):
  - `AllTeams`: todos os times podem alvejar enquanto a janela estiver ativa;
  - `DetectorTeamOnly`: somente o time que detectou ganha a janela de disparo.
- Sem deteccao ativa e sem detector valido, mesmo com LoS e alcance validos, o disparo fica invalido com `aim.invalid.stealth`.

## Resumo pratico
- O jogo modela visao como sistema composto (unidade + terreno + camada + regras globais + observadores).
- Spotter e um habilitador de fogo indireto/fora da visao direta, nao apenas um bonus numerico.
- Com `visao` base quase uniforme, o meta de artilharia depende muito de relevo e cadeia de observacao.
- `visionSpecializations` agora controla duas dimensoes: alcance de observacao e permissao de detectar stealth por camada.
