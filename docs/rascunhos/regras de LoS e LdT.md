# Regras de LoS e LdT (estado atual)

## Escopo
Este documento descreve as regras atualmente usadas no `PodeMirarSensor` para validar tiro.

Arquivo-base: `Assets/Scripts/Sensors/PodeMirarSensor.cs`

---

## Pipeline de validacao de tiro
Para cada arma candidata e alvo:

1. Checa alcance (min/max da arma, considerando modo de movimento).
2. Checa municao (`squadAmmunition > 0`).
3. Checa dominio/altura do alvo (`weapon.SupportsOperationOn(targetDomain, targetHeight)`).
4. Checa LoS/LdT conforme tipo de trajetoria.
5. Checa detectabilidade do alvo (placeholder stealth).
6. Se passar, monta opcao valida de ataque.

---

## Trajetoria Straight
Para `WeaponTrajectoryType.Straight`:

1. Faz verificacao de linha entre hex origem e destino (hexes intermediarios).
2. Mantem a validacao de compatibilidade da arma com o terreno intermediario (`TerrainAllowsWeaponTrajectory`).
3. Aplica bloqueio por LoS com EV:
   - resolve `EV` e `blockLoS` por hex intermediario;
   - ignora hex se `blockLoS == false`;
   - ignora hex se `EV <= 0`;
   - excecao suprema: ignora hex se `EV_alvo - EV_hex >= 2`; (resolve um caso específico: todos os locais olhando para Air/High)
   - calcula altura da visada por ponto com `Lerp(EV_origem, EV_alvo, t)`;
   - bloqueia se `EV_hex > alturaDaVisadaNoPonto`.

Se bloquear, a opcao vira invalida com motivo:
`Linha de visada bloqueada.`

---

## Trajetoria Parabolic
Para `WeaponTrajectoryType.Parabolic`:

1. Nao percorre os hexes intermediarios (nao usa check de LdT por percurso).
2. Exige apenas compatibilidade da arma com o hex de destino (`CanWeaponHitVirtualCell` no destino).
3. Mantem checks de alcance, municao, dominio/altura e detectabilidade.

Observacao:
No estado atual, a verificacao de LoS por intermediarios (EV/block) esta aplicada ao fluxo `Straight`.

---

## Regra de visao por camada (EV / Block LoS)
A resolucao de EV/LoS usa:

1. Base do `TerrainTypeData` (`ev`, `blockLoS`).
2. Excecoes por terreno:
   - `constructionVisionOverrides`
   - `structureVisionOverrides`
3. Override de ar via `DPQAirHeightConfig` quando dominio ativo for `Air`:
   - `AirLow`: EV/BlockLoS de AirLow
   - `AirHigh`: EV/BlockLoS de AirHigh

Arquivo de suporte: `Assets/Scripts/Terrain/TerrainVisionResolver.cs`

---

## Placeholder stealth/deteccao
Existe hook para regra futura:

- Metodo: `IsTargetDetectableByAttacker(attacker, target)`
- Estado atual: retorna `true` (nao bloqueia ninguem).
- Objetivo futuro: permitir casos como alvo visivel por LoS, mas nao detectado (ex.: stealth).

Motivo de invalidez previsto:
`Alvo nao detectado (stealth placeholder).`

---

## LdT no sensor
O sensor ja guarda os hexes intermediarios (`lineOfFireIntermediateCells`) para:

1. debug de validacao;
2. desenho centro-a-centro no Scene View nas ferramentas de simulacao.

---

## Resumo rapido
- `Straight`: LoS + LdT por percurso, com EV e excecoes.
- `Parabolic`: ignora percurso de LdT, valida destino + checks basicos.
- Stealth: ainda placeholder.

---

## Configuracao EV e LoS Block (padrao adotado)
Valores de referencia para configurar os dados:

1. Terrenos de superficie (`TerrainTypeData`)
- Planicie: `EV = 0`, `blockLoS = true`
- Praia: `EV = 0`, `blockLoS = true`
- Mar: `EV = 0`, `blockLoS = true`
- Floresta: `EV = 1`, `blockLoS = true`
- Montanha: `EV = 2`, `blockLoS = true`

2. Camadas de ar (`DPQAirHeightConfig`, override por dominio ativo `Air`)
- AirLow: `EV = 3`, `blockLoS = true`
- AirHigh: `EV = 4`, `blockLoS = false`

3. Excecao suprema (air-high)
- Quando o alvo estiver alto o suficiente para cumprir `EV_alvo - EV_obstaculo >= 2`,
  o obstaculo intermediario e ignorado para bloqueio de LoS.
- Isso foi introduzido para preservar o comportamento esperado contra alvos `AirHigh`.


## Pendências 
as 2 pendências certas agora são:

1. LoS indireto
- definir regra separada por trajetória/arma (principalmente Parabolic);
- decidir se ignora intermediários totalmente ou usa uma versão “suave” de bloqueio.

2. Stealth mode
- separar visível de detectável;
- criar fonte de detecção (sensor/radar/unidade) e condição de reveal.
---

## Chave global: Fog of War
Controle no `MatchController`:
- Campo: `fogOfWar` (bool)
- Inspector: `Fog of War`

Comportamento:

1. `Fog of War = true`
- Aplica validacao completa de `LoS + LdT`.
- Para `Straight`, executa bloqueio por EV/`blockLoS` nos hexes intermediarios.

2. `Fog of War = false`
- Mantem apenas validacao de `LdT` (trajetoria/terreno no caminho).
- Ignora bloqueio por LoS baseado em EV/`blockLoS`.

Observacao:
- Checks de alcance, municao, dominio/altura e demais validacoes continuam ativos em ambos os modos.
