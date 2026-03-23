# Antes do Planning

Versao: v1.4.5  
Status: checkpoint pre-planning

## Resumo
- Fechado checkpoint tecnico antes de iniciar o recurso de Planning / Rally Point.
- Fluxos de replay, save/load e neutral contract foram endurecidos para reduzir inconsistencias.
- Ajustes de UX feitos em mensagens de replay e comportamento de audio/feedback.

## Entregas principais

### 1) Save/Load e replay persistido
- Persistencia de dados de replay no save consolidada.
- Diagnosticos de save/load expandidos para facilitar triagem em slot.
- Painel de replay acessivel tambem quando os dados vieram de load.

### 2) Contrato de estado e consistencia
- Fluxo de `Destroy Unit` ajustado para respeitar sequencia de confirmacao:
  - entra em estado de remocao;
  - confirma;
  - executa animacao;
  - retorna para `Neutral`;
  - so entao registra/avanca.
- Ajustes no replay para reduzir deslocamentos residuais de cursor ("passinhos") e evitar transicoes indevidas.
- Navegacao por snapshot/replay com protecao contra autoplay indesejado.

### 3) Bloqueios de seguranca em runtime
- Save e Load bloqueados durante execucao da fila de queda de aeronaves no inicio do turno.
- Guardas adicionais para evitar persistencia em estado transitorio sensivel.

### 4) UX de replay e feedback
- Mensagem de carregamento de replay exibida imediatamente ao iniciar (`Start`), antes do trabalho pesado.
- Mensagem de encerramento de replay exibida imediatamente ao parar (`Stop`).
- Ajuste de SFX no fim de `Destroy Unit` para usar `load` no lugar de `done`.

### 5) Documentacao
- Atualizacao de `docs/turnState.md` com estados e fluxo operacional atuais.
- Inclusao de `docs/contract.md` com contrato de neutral e pontos de validacao.
- Atualizacao de roteiro em `docs/testes/Teste de Replay.md`.

## Estado antes do proximo passo
- Base estabilizada para iniciar o design/implementacao de Planning (Rally Point) sem carregar pendencias de replay.
- Proximo bloco planejado: arquitetura e execucao automatizada de ordens de planejamento no inicio do turno.
