# Relatorio de Atualizacao - v1.5.2

## Em uma frase
A versao v1.5.2 consolida a AI Automatation com suprir e capturar, alinhando o comportamento da IA ao fluxo oficial de sensores/replay e reforcando a tomada de decisao por simulacao de combate.

## O que isso trouxe na pratica
- Unidades de infantaria (soldado/bazooka) passaram a priorizar captura/recuperacao de construcoes quando houver objetivo valido.
- O caminhao de suprimentos (ST) ganhou comportamento hard-coded para suporte: prioriza aliados criticos e usa Pode Suprir no fluxo oficial.
- Quando o ST fica sem reservas, retorna para construcao aliada e usa Pode Transferir em modo Receber, tambem no fluxo oficial.
- A IA continua respeitando intel visivel (FoW) e sensores oficiais para evitar perseguir alvos invalidos.
- A execucao automatizada ficou mais estavel em runtime com sincronizacao em Neutral e delays herdados do replay.

## Principais entregas

### 1. Captura como prioridade tatica de infantaria
- Reuso do sensor oficial `PodeCapturarSensor` na Fase 2 da IA.
- Se houver captura imediata no hex atual, a unidade confirma captura antes de qualquer acao ofensiva.
- Se nao houver captura imediata, a IA calcula objetivo de captura e avanca para ele.

### 2. ST com protocolo de suporte
- Identificacao de ST por `isSupplier` + heuristica de id/nome (`st`, `supr`, `supply`).
- Escolha do aliado mais critico por necessidade combinada de HP e combustivel.
- Execucao de suprimento via pipeline de replay de supply (fila + execucao oficial).

### 3. ST com protocolo de refill
- Detector de falta de reservas core (peca, galao, caixa).
- Retorno para construcao aliada mais proxima quando faltar reserva.
- Execucao de transferencia em fluxo `Receber`, usando caminho oficial de replay/confirmacao.

### 4. Robustez de automacao e sincronizacao
- Guardas extras para estados inesperados e fallback seguro sem quebrar o turno.
- Validacao de destino por caminhos realmente alcancaveis (`movementPathsByCell`).
- Espera pos-acao ampliada para nao cortar animacoes de supply/transfer em execucoes mais longas.

## Bloco tecnico
- Scripts modificados (principais):
  - `Assets/Scripts/AI/AIPlayerController.cs`
  - `Assets/Scripts/Match/TurnState/TurnStateManager.Automation.cs`
  - `Assets/Scripts/Units/UnitManager.cs`
- Ferramentas e suporte de IA (janela/diagnostico e simulacao) foram mantidos/ajustados no pacote desta versao para depuracao de alvo e HP.

## Pendencias conhecidas (proxima versao)
- Evoluir heuristica do ST para considerar risco tatico local (nao apenas necessidade de recurso).
- Ajustar calibracao fina de score por tipo de unidade/custo para reduzir tiros de baixa eficiencia.
- Expandir prioridades de alvo configuraveis por perfil (alem do hard-code atual).

## Resultado
A v1.5.2 fecha um passo importante da IA jogavel: captura com prioridade para infantaria e cadeia completa de suporte do ST, sem romper a semantica de um player normal no replay/sensores.
