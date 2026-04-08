# v1.6.5 - Refatorando Forcas Planejadas

## Contexto

Esta versao prepara a proxima etapa de refatoracao da IA ao consolidar correcoes de fluxo, visibilidade e suporte, antes da revisao estrutural do planner.

---

## Mudancas

### 1. FoW removido do caminho critico de snapshot da IA

**Problema:** `TakeSnapshot()` disparava `RefreshFogOfWarForActiveTeam()` antes de cada unidade agir. Isso gerava recalc global de FoW varias vezes por turno, com congelamentos perceptiveis na tela.

**Solucao:** o refresh global de FoW foi removido de `TakeSnapshot()`. O snapshot da IA continua sendo construido a partir do estado observado pelo sensor/time, sem recalculo visual/global no meio do bloco interno de acoes.

**Efeito esperado:** menos freeze durante o turno da IA e comportamento mais proximo ao do jogador, sem pre-revelacao de informacao antes do compromisso da acao.

---

### 2. Visibilidade residual da IA desacoplada do FoW global

**Problema:** partes da IA ainda consultavam o cache global de visibilidade do `MatchController`, o que podia entrar em conflito com o novo fluxo sem refresh por unidade.

**Solucao:** as checagens residuais de visibilidade da IA passaram a usar observacao direta por time/sensor, em vez de depender do estado global de FoW atualizado na cena.

---

### 3. Supply decision agora prioriza distancia

**Problema:** o supridor tendia a priorizar o aliado mais critico mesmo quando havia outro alvo valido muito mais proximo, o que criava rotas ruins e comportamento pouco natural.

**Solucao:** a selecao de alvos de suprimento passou a usar **distancia primeiro** e **criticidade como desempate**.

Fluxo atual do supridor:
- tenta suprir sem mover se houver alvo imediato valido
- se nao houver, escolhe o aliado valido mais proximo para navegar ate ele
- se houver empate de distancia, desempata por criticidade

---

### 4. Iniciativa temporaria `Retreat`

**Problema:** unidades em `Return to Base / Repair` ainda obedeciam a iniciativa normal do profile, podendo agir cedo demais no turno mesmo estando em retirada/manutencao.

**Solucao:** foi criada a iniciativa temporaria `Retreat`, sempre abaixo de `Low`.

Regras:
- `Retreat` nao aparece como opcao configuravel no `AIUnitProfile`
- quando a unidade entra em `Return to Base / Repair`, passa a usar `Retreat`
- ao sair desse modo, volta automaticamente para a iniciativa do profile

Ordem efetiva:
- `Priority > High > Medium > Low > Retreat`

---

### 5. Documentacao atualizada

O arquivo `docs/AI Unit Profile.md` foi atualizado para refletir:
- o novo fluxo de FoW durante o turno da IA
- a prioridade por distancia do sensor `Supply`
- a iniciativa temporaria `Retreat`
- o efeito do modo reparo sobre a ordem do turno

---

## Arquivos modificados nesta etapa

| Arquivo | Tipo de mudanca |
|---|---|
| `Assets/Scripts/AI/AIPlayerController.cs` | Remove refresh global de FoW do snapshot, troca visibilidade residual por sensor direto, muda prioridade do Supply para distancia, aplica initiative efetiva |
| `Assets/Scripts/AI/AIInitiative.cs` | Adiciona `Retreat` |
| `Assets/Editor/AIUnitProfileEditor.cs` | Oculta `Retreat` do inspector configuravel |
| `docs/AI Unit Profile.md` | Atualiza comportamento de FoW, Supply, Turn Order e Repair |
| `docs/relatorio_v1.6.5.md` | Novo relatorio da versao |

---

## Proximo passo planejado

A proxima refatoracao proposta e remover do planner a modelagem hardcoded por classes como `APC` e mover a composicao de forcas planejadas para capacidades orientadas por `AIUnitProfile`.
