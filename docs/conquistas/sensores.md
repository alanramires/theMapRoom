# Sensores para as medalhas

12/10/2026. A parte técnica das medalhas: o que a FSM do tabuleiro precisa
registrar para que cada medalha do catálogo (`conquistas_ideias.md`) possa ser
verificada. O modelo vem do Zone 5 (`Zone 5/docs/achievments/sensores.md`).

## A decisão que vem antes da FSM

**Medalha nenhuma é verificada "durante o jogo".** Toda medalha é uma **consulta
sobre o log de jogadas** — da partida, e da carreira para as acumulativas.

A FSM não chama `ConcederMedalha()` em lugar nenhum. Ela só **registra fatos
completos**. As medalhas são avaliadas no fim da partida.

Por que assim:

- **Funciona igual contra a IA, em hot seat, em replay** e, no futuro, online: o log
  é o mesmo.
- **Medalha nova não mexe na FSM** — é uma consulta nova.
- **Dá para testar sem jogar:** um `jogadas.json` de exemplo e a consulta.
- **Dá para recalcular o passado:** o log vai no save (`*.jogadas.json`), então uma
  medalha criada depois pode ser concedida sobre partidas antigas — **mas só para os
  fatos que já estavam sendo gravados**. Por isso os eventos não podem esperar.
- **Só no fim da partida, por causa da névoa:** medalha no meio do turno pode
  entregar o que o jogador não devia saber ("Tiro no Escuro" acusa que havia alguém
  ali).

**A regra para o log:** registrar fatos, não conclusões. "O Obus #12 destruiu o
Soldado #4 sem ser visto" é fato; "o slot 0 ganhou Tiro no Escuro" é conclusão.

## O que já existe

`JogadasLog` (`Assets/Scripts/Shared/Jogadas/JogadasLog.cs`) já é esse log. Grava,
para humano e IA (`JogadasManager.RegistrarPlayerAction`, `ReplayManager`):

| ação | o que carrega |
|---|---|
| `Mover`, `Estacionario` | unidade, origem, destino |
| `Ataque` | atacante, alvo, HP antes/depois dos dois, `attackWeaponCategory`, `attackTrajectory`, `attackerVisibleToDefender`, custo e elite do defensor, `combatCargo` (com `cause`) |
| `Capturar` | unidade, prédio por coordenada, `obs` ("10/20", "capturado") |
| `Embarque`, `Desembarque`, `Desembarcando` | passageiro, transporte, células |
| `Fusao` | as duas unidades |
| `Suprir`, `Suprindo` | supridor/alvo, serviço, custo, ganho de HP/combustível/munição |
| `Compra`, `Destruir`, `ServicoComando` | unidade, célula |
| estado de reparo | `repairBefore` / `repairAfter` |

## Os fatos que faltam

**Feitos em 12/10/2026:**

| # | fato | como ficou |
|---|---|---|
| 1 | **slot** em toda jogada | **já era**: todo registrador passa `SlotIndex`/`ActingSlotIndex` em `team` — o nome é que engana (comentado no `Jogada`). ⚠️ **Achado:** o `AIIntelAnalyzer` comparava esse slot com `(int)aiTeam`, a COR — a memória da IA classificava amigo/inimigo errado sempre que a cor não coincidia com o número do slot. Corrigido: resolve o slot da IA uma vez e compara slot com slot |
| 2 | `PartidaIniciada` / `PartidaEncerrada` | `PartidaIniciada` (modo, dificuldade, regras, `slotsTime`, `slotsIA`) quando `MatchStartApplied` sobe — só partida nova. `PartidaEncerrada` (vencedor em `team`, `motivo`, `modo`) ouvindo `MatchController.OnMatchConcluded` |
| 3 | **início de turno** | `Renda` (`valor`) em `ApplyEconomyAtTurnStartForActiveTeam`; `Autonomia` (`fuelAntes/fuelDepois`, aeronaves) no upkeep; `PousoEmergencia` e `PaneSeca` na fila de combustível do `TurnStateManager` — fora do replay, que re-resolve a mesma fila |

| 4 | **prédio na captura** | `predioId`, `predioQG`, `donoAnterior` (slot), `capturaConcluida` — guardados na execução da captura **antes** de o dono trocar (`SetUltimaCapturaFato`) e consumidos no registro |
| 5 | **terreno do combate** | `dpqAtacante`, `dpqDefensor`, `posicaoAtacante`, `posicaoDefensor` — vêm da resolução do combate (`CombatResolutionResult`), que já calculava e só não devolvia |
| 6 | **causa da morte** | **já se deduz do log**: combate (`hp2Depois`/`hpDepois` = 0), `PaneSeca`, `Destruir`, carga (`combatCargo.cause`). Morte por outra via (camada forçada, fusão do doador) ainda não tem fato próprio |
| 8 | **custo da compra** | `valor` na `Compra` — o que saiu do caixa (0 com economia desligada) |
| 9 | **HP na fusão** | `hpAntes`/`hp2Antes` dos dois e `hpDepois` = resultado, guardados na execução da fusão (depois dela o doador já morreu) |

**Ainda falta:**

| # | fato | por que é maior |
|---|---|---|
| 7 | **detecção** (unidade X foi detectada pelo slot Y) | o Jornal registra contato por **nome** e só para o dono, fog-honesto. Medalha precisa do fato bruto — a transição "não detectado → detectado" no `PodeDetectar`, por unidade e observador —, que hoje não é um evento. Nasce no recálculo de detecção do `Neutral` assentado (`OnBoardSettledAtNeutral`) |
| 10 | **foto do exército no início de cada turno**, por slot: cada unidade com id, tipo, HP e em quem está embarcada | é outro tipo de fato: não um evento, um **estado**. Serve a toda medalha de composição ("tinha porta-aviões, destroyer, sub e fragata vivos ao mesmo tempo", "2 bombardeiros dentro do mesmo porta-aviões"), que não sai de somar compras e mortes — unidade assada não passa por `Compra`. Nasce no início do turno, junto da `Renda`. Serve também a replay e à futura tela de estatísticas. Custo a medir: uma foto por turno por slot cresce o `jogadas.json` |

Cada fato nasce **na confirmação** (Neutral), nunca no meio da ação: nada
provisório vai para o log. É o mesmo invariante transacional do resto do jogo.

## O que muda na FSM por causa disso

1. `Jogada` ganha `slot` (e `slot2` para o alvo).
2. Eventos de **partida** e de **início de turno** entram no mesmo log, não só no
   Jornal.
3. Captura, combate, morte, compra e fusão carregam os campos da tabela acima.

O avaliador (consultas, tela de medalhas, save de carreira) pode ficar para depois
do MVP. **O que não pode ficar para depois são os fatos.**

## Perguntas em aberto

1. **Carreira**: medalhas acumulativas exigem guardar perfil entre partidas. Mora no
   `CampaignProgressStore` ou num perfil próprio?
2. **Aula conta?** Medalha de combate obtida dentro de uma aula vale, ou aula só
   rende as medalhas da Academia?
3. **IA ganha medalha?** Útil para teste e hot seat contra IA; inútil para o jogador.
