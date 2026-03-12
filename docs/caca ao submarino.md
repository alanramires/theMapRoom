# Caca ao Submarino

Este documento resume as regras atuais de stealth, deteccao e reveal no fluxo de "caca ao submarino" do projeto.
O texto mistura visao de design (como jogador percebe) com visao tecnica (como o sistema decide).

## 1) Conceito geral

- Um submarino pode estar em camadas diferentes (`Submarine/Submerged` ou `Naval/Surface`).
- Stealth agora e por camada: a skill furtiva pode valer em uma camada e nao valer em outra.
- Detectar nao depende so de alcance: tambem depende de LOS e, para alvo furtivo, da especializacao certa.

Narrativa curta:
"Submerso e fantasma. Na superficie, pode virar contato comum."

## 2) Stealth por camada (UnitData)

### Estrutura principal

- `stealthSkillRules` (lista por elemento):
  - `skill`
  - `domain`
  - `heightLevel`

Exemplo:
- `SUB Submerse Ops` em `Domain=Submarine` + `Height=Submerged`.

### Fallback legado

- `stealthSkills` (lista antiga global) continua como fallback.
- Regra atual:
  - Se `stealthSkillRules` existe, a furtividade vale somente quando casar com a camada atual.
  - Se nao houver regras por camada, usa `stealthSkills` legado.

Efeito pratico:
- Mesmo submarino pode ser furtivo submerso e visivel na superficie.

## 3) Alcance de visao e especializacao

### Base

- `visao` = alcance padrao da unidade.

### Especializacao por alvo

- `visionSpecializations` define alcance por `targetDomain/targetHeight`.
- O sensor usa `ResolveVisionFor(targetDomain, targetHeight)`.

Exemplo classico:
- Super Tucano com `visao=4` e specialization `Submarine/Submerged=6`:
  - contra submerso: alcance 6
  - contra naval/surface: alcance 4 (se nao existir specialization para essa camada)

## 4) Pode Detectar (ferramenta)

Menu:
- `Tools > Sensors > Pode Detectar`

Listas atuais:
1. `Unidades furtivas detectadas`
2. `Unidades furtivas nao detectadas`
3. `Candidatos avistados`
4. `Candidatos no alcance mas nao detectados por LOS`

Extras visuais:
- Clique no item: desenha linha no Scene (verde/vermelha).
- O indicador `detected` (olhinho no HUD) pode ser aceso na simulacao para contatos detectados.

## 5) LOS, observador e auto-observador

- Detectar usa LOS.
- Nao usamos LdT para detectar unidade (LdT e outra validacao, mais ligada a tiro).
- Regra de auto-observador no sensor de detectar:
  - Se alvo esta no alcance de visao daquele observador, ele pode "observar por si" (sem depender de outro aliado).

Resumo em linguagem de jogo:
"Nao basta estar perto. Precisa ter contato de observacao valido."

## 6) Reveal (ficar visivel por turnos)

### Onde mora o estado

- O estado de reveal agora mora na propria unidade (`UnitManager`), nao mais em cache estatico do sensor.
- Isso facilita FOW e consultas futuras de mapa.

### Duracao

- `stealthVisibleIfDetectedForTurns` controla por quantas rodadas a unidade fica revelada apos deteccao.

### Escopo de quem enxerga

`stealthRevealScope`:
- `AllTeams`
- `DetectorTeamOnly`
- `ConfiguredTeams`

Se `ConfiguredTeams`:
- usar `stealthRevealTeams` (lista de times permitidos).

Interpretacao:
- "Visivel para todos" e agora uma opcao entre outras.
- Em partida com varios times, voce pode fazer reveal parcial por coalizao/contexto.

## 7) Leitura de gameplay (estado mental do jogador)

- Submarino submerso com stealth ativo:
  - dificil de localizar sem plataforma/skill certa.
- Submarino na superficie sem stealth ativo:
  - pode virar contato "normal", detectavel por regras comuns de observacao.
- Unidade detectada:
  - pode ficar marcada por N rodadas conforme configuracao de reveal.

## 8) Checklist rapido de tuning

Quando algo parecer "estranho" no campo:
1. Verificar camada real do alvo (`Domain/Height`).
2. Verificar `visionSpecializations` do observador para aquela camada.
3. Verificar se alvo stealth esta ativo naquela camada (`stealthSkillRules`).
4. Verificar se observador possui especializacao de visao que case as stealth skills do alvo para aquela camada.
5. Verificar LOS.
6. Verificar escopo/duracao de reveal (`stealthRevealScope`, `stealthRevealTeams`, `stealthVisibleIfDetectedForTurns`).

---

Tom de design:
"Nao e radar magico; e guerra de informacao. Camada, sensor, linha de observacao e tempo de expose."
