# Relatorio de Atualizacao - v2.0.5

## AI Basica

Esta versao introduz a primeira camada funcional de IA para partidas locais, capaz de assumir um time, aguardar a preparacao automatica do turno, executar acoes basicas de unidades, comprar reforcos e passar a vez usando o mesmo caminho de replay/execucao usado pelo jogador.

---

## Em uma frase

A IA agora consegue jogar um turno completo em modo basico: avalia o estado do mapa, move, captura, ataca, compra unidades terrestres e encerra o turno sem depender de input manual.

---

## Principais pontos revisados

### 1. Controlador de turno da IA

- Foi adicionado um `AIController` para orquestrar o turno em fases.
- A IA reage a troca de time ativo e inicia automaticamente quando o time atual esta marcado como IA.
- O fluxo espera o Servico do Comando automatico e a FSM voltar ao `Neutral` antes de tomar decisoes.
- As acoes sao executadas por batches vivos no `ReplayManager`, preservando o mesmo caminho de resolucao das acoes humanas.

### 2. Snapshot basico do mundo

- Foi criado um `AIWorldSnapshot` reconstruido no inicio do turno.
- O snapshot separa unidades aliadas, inimigas, construcoes aliadas, neutras e inimigas.
- O sistema registra celulas ocupadas, QGs, dinheiro disponivel e renda por turno.
- A IA calcula uma postura simples entre `Tactical`, `Offensive` e `Defensive` para orientar futuras heuristicas.

### 3. Decisao inicial de unidades

- Unidades disponiveis sao ordenadas por iniciativa de IA e vida atual.
- A avaliacao de hexes usa `HexEvaluator` para escolher destino, captura ou posicao de ataque.
- A IA consegue executar movimento simples, captura e ataque quando encontra alvo valido.
- Unidades sem decisao valida sao marcadas como ja acionadas para evitar travar o turno.

### 4. Compras automaticas

- Foi adicionado um `AIShoppingPlanner` basico.
- A IA compra em construcoes aliadas produtoras, desde que estejam desocupadas.
- Nesta primeira versao, a compra prioriza unidades terrestres acessiveis pelo saldo.
- O planner favorece capturadores quando o exercito da IA ainda tem poucos.
- O fluxo fecha o menu de compra se uma compra falhar e deixar a interface aberta.

### 5. Integracao com replay, menus e persistencia

- `PlayerAction` passa a identificar acoes geradas pela IA.
- O `ReplayManager` aceita batches vivos de IA para acoes de unidade, compra, Servico do Comando e fim de turno.
- O menu principal e os paineis de batalha passam a expor melhor a configuracao necessaria para partidas com IA.
- Ajustes de save/load e estado de turno acompanham os novos caminhos de execucao automatica.

### 6. Conteudo e cenas de suporte

- Foi adicionada a cena `Battle Map Ilhas` como mapa de suporte.
- Cenas e build settings foram atualizados para incluir os fluxos usados nos testes de IA.
- Dados de unidades aereas, navais e matriz RPS receberam ajustes para melhorar leitura e comportamento da IA.
- Assets de plugins, fontes e botoes foram adicionados para dar suporte visual aos novos menus.

---

## Resultado

O jogo passa a ter uma IA basica jogavel, ainda heuristica, mas integrada ao ciclo real de turno. A base agora permite evoluir comportamento tatico sem criar um caminho paralelo de regras: a IA decide, mas a execucao continua passando pelos mesmos sistemas de replay, FSM, sensores, compras e fim de turno.

---

## Validacao

Build C# executado:

```powershell
dotnet build Assembly-CSharp.csproj
```

Resultado: 0 erros. Permanecem warnings antigos de APIs obsoletas do Unity.
