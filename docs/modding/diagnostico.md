# Diagnóstico no save — contrato

O primeiro ponto de extensão de **observação** do jogo (guia de modding, §6 e §12).
Fatos estruturados gravados onde acontecem, dentro do `.tmrsave`
(`diagnostico.json`), lidos em **Tools ▸ Auditoria ▸ Save Inspector**, seção
*Diagnóstico*.

Não é log de Console. As flags de log do Console controlam barulho; o diagnóstico
grava independente delas, porque o save de quem testa no celular é o que mais
precisa dele, e lá ninguém tem Console.

## A entrada

| campo | é |
|---|---|
| `rodada`, `slot` | quando e de quem. Sem valor, o jogo preenche com a rodada e o slot ativos |
| `severidade` | `Depuracao`, `Aviso`, `Erro` — separadas (§12) |
| `origem` | quem gravou: `core` hoje; `autor.mod` amanhã (§7) |
| `categoria` | o sistema: `combate`, `decisao` |
| `operacao` | o fato: `combate.resolvido`, `decisao.status`, `decisao.sem_opcao_sensor` |
| `uid`/`sigla`, `uid2`/`sigla2`, `cx`/`cy` | as entidades e o hex |
| `codigo` + `motivo` | o motivo em número estável e em texto |
| `agregada` + `contagem` | fatos repetidos somam numa linha em vez de repetir |
| `campos` | chave/valor livres. Quem lê **não pode assumir** que uma chave existe |

O log tem `formato` (versão do esquema, §11) e `descartadas` (quantas o teto jogou
fora, §15 — limite não fica escondido).

## O que o core grava hoje

| operação | quando | por quê |
|---|---|---|
| `decisao.status` | cada decisão de ataque da IA, agregada por rodada, slot e status | ver o funil: quanto foi permitido, bloqueado, sem opção |
| `decisao.sem_opcao_sensor` | a IA avaliou atacar de uma célula que o `PodeMirar` recusa; agregada por atacante, com o 1º exemplo | um papel que monta pares (alvo, célula) inválidos aparece aqui |
| `combate.resolvido` | cada combate executado, com a **previsão da IA** para o mesmo par e célula ao lado | previsto ≠ realizado vira `Aviso` com o que divergiu (hp, dpq, arma, revide) |

A previsão vive só em memória até a execução: decisão avaliada e não executada não
vira linha (seriam milhares por turno).

## Controle

`DebugManager` ▸ *Diagnóstico no Save*: liga/desliga e o teto (padrão 2000
entradas). Sem `DebugManager` na cena, fica **ligado**.

## Para gravar um fato novo

1. Escolha `categoria` e `operacao` (`sistema.fato`) e documente nesta tabela.
2. Fato que se repete muito: `DiagnosticoManager.TryGetAgregada` →
   `contagem++`, senão `RegistrarAgregada`. **Sem string no caminho quente.**
3. Fato raro: `DiagnosticoManager.Registrar(new DiagnosticoEntrada { … }.Campo(…))`.
4. Quem só quer observar assina `DiagnosticoManager.OnEntradaRegistrada` e
   **não altera** a entrada.

Exemplo de produtor: `Assets/Scripts/Combat/CombatDiagnostico.cs`.

## Ainda não grava

`[FSM]` (delta pendente na volta por `Retreat`, `Retreat` de estado reset-only) e
`[Watchdog]`. Entram no mesmo formato, como categoria `fsm` e `ia`.
