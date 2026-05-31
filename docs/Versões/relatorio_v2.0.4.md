# Relatorio de Atualizacao - v2.0.4

## Embarque e Desembarque Fix

Esta versao corrige a transicao de embarque e desembarque dentro da FSM em pilha, com foco em evitar estados fantasmas durante execucao, manter o cursor no fluxo correto e preservar a visibilidade/ordem visual das unidades envolvidas.

---

## Em uma frase

Embarque e desembarque agora entram em estados de execucao proprios, retornam pela pilha da FSM e deixam de depender de leituras diretas de estado que podiam ficar inconsistentes durante corrotinas.

---

## Principais pontos revisados

### 1. Estados de execucao explicitos

- Foram adicionados estados `EmbarcandoExecuting` e `DesembarcandoExecuting`.
- O embarque avanca para `EmbarcandoExecuting` antes da corrotina de execucao.
- O desembarque avanca para `DesembarcandoExecuting` antes de executar a fila de ordens.
- Abortos de pouso, troca de camada ou falha de acao retornam pela FSM com `Retreat`.

### 2. Leitura consistente do estado atual

- Fluxos de scanner, movimento, embarque, desembarque, pouso, ataque e debug passam a consultar `CurrentCursorState`.
- O alias interno antigo de estado foi removido para reduzir leituras ambiguas.
- Prompts e validacoes deixam de depender de uma leitura indireta que podia mascarar o topo real da pilha.

### 3. Desembarque com cursor mais previsivel

- As opcoes de desembarque passam a ser ordenadas em sentido circular ao redor do transportador.
- A navegacao do cursor durante a escolha de hex de desembarque cicla diretamente pela lista valida.
- O fluxo evita fallback direcional inconsistente quando os hexes validos nao formam uma vizinhanca simples.

### 4. Execucao mais segura de embarque e desembarque

- Flags de execucao sao limpas em `finally`, mesmo quando a corrotina sai por falha intermediaria.
- O desembarque restaura visibilidade de Fog of War para passageiros antes de posiciona-los no mapa.
- A ordem temporaria de sorting de transportador/passageiro continua sendo limpa ao final ou em abortos.

### 5. Ajustes colaterais de integracao

- Estados de execucao tambem foram preparados para captura, suprimento e fusao.
- Menus, prompts auxiliares e logs passam a reportar o estado real da FSM.
- Cenas de desenvolvimento/mapas foram reorganizadas no projeto e registradas no build settings.
- Excecoes de visao de unidade agora podem valer para todas as alturas de um dominio.

---

## Resultado

O fluxo de embarque/desembarque fica alinhado ao modelo de pilha usado no restante do turno: selecionar, confirmar, executar e retornar agora sao etapas separadas. Isso reduz entradas indevidas durante corrotinas, melhora a previsibilidade do cursor e evita que uma acao parcialmente abortada deixe a FSM presa em estado incorreto.

---

## Validacao

Build C# executado:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
```

Resultado: 0 erros. Permanecem warnings antigos de APIs obsoletas do Unity.
