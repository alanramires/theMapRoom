# v4.4.1 — Transport Tools

## Objetivo

Transformar a escolha de desembarque em uma ferramenta e serviço reutilizável,
fazendo transportadores terrestres, aéreos e navais consultarem as mesmas
regras de alcance, ocupação, terreno, estruturas e prioridade dos passageiros.

## Ferramenta Melhor Desembarque

- Criada a ferramenta `Tools > Transporte > Melhor Desembarque`.
- O cabeçalho segue o padrão das demais ferramentas, com seleção da unidade
  atual, escolha manual de célula e segundo local de objetivo.
- A visualização apresenta as LZs do transportador, os spots individuais dos
  passageiros, ranking, pontuação, quantidade entregue e rota restante.
- O cálculo utiliza `Caminhos Válidos`, custo real de movimento e progressão de
  uma passada.
- Células ocupadas, terrenos incompatíveis e estruturas não permitidas pelo
  `UnitData` são descartados.
- Passageiros recebem spots exclusivos; o transportador não precisa ocupar a
  construção para desembarcar uma unidade sobre ela quando sua configuração
  permite isso.

## Serviço compartilhado

- A avaliação foi extraída para `MelhorDesembarqueService`, consulta pura que
  não movimenta unidades nem altera ocupação, FOW, recursos ou revisões.
- O adaptador da IA traduz a intenção de cada passageiro em alvo e entrega ao
  serviço o envelope válido do transportador.
- Os serviços de retaguarda, hotzone e melhor desembarque foram organizados em
  `Assets/Scripts/Match/AI/Services`.
- O roteador universal prioriza o courier carregado antes dos papéis
  secundários do transportador.
- APC, Chinook, hidroavião, navio de transporte, trem de carga e demais
  unidades configuradas por dados podem compartilhar o mesmo avaliador.

## Passageiros, planos e prioridade

- A ordem de entrega continua FIFO por `embarkedOnTurn`, usando a vaga física
  como desempate quando os passageiros embarcaram no mesmo turno.
- Passageiros com plano mantêm o objetivo atribuído.
- O painel de ajuda, quando `Show AI HUD` está ativo, exibe o plano do
  passageiro; unidades sem slot formal aparecem como `ROGUE`.
- Capturadores e unidades de assalto verificam a possibilidade de embarque
  quando a rota terrestre é insuficiente.
- O teste “consegue chegar a pé” agora considera se o prédio de destino já está
  ocupado por um aliado; nesse caso outro objetivo livre é procurado.

## Rebeldes e rogues

- Rebeldes continuam sem plano ou eixo e procuram o capturável livre mais
  próximo.
- Rogues de uma IA com QG avançam pelo corredor em direção ao QG inimigo,
  capturando oportunidades próximas no caminho.
- Com dois objetivos úteis, dois passageiros são distribuídos entre destinos
  distintos.
- Com apenas um objetivo seguro, somente o passageiro prioritário desembarca e
  o outro permanece a bordo.
- Com apenas um objetivo sob pressão inimiga confirmada, o segundo rebelde ou
  rogue pode desembarcar como reforço.
- A decisão usa apenas inimigos presentes no snapshot confirmado da IA.

## Pickup e transporte terrestre

- APCs sem passageiro útil no objetivo antigo voltam a procurar outro
  passageiro potencial.
- A waiting zone do APC é de até dois hexes; transportadores aéreos usam até um
  hex.
- Distância geométrica não substitui alcançabilidade: o rendezvous é validado
  pelo sensor de embarque e pelos caminhos válidos.
- Um passageiro já posicionado no objetivo não prende o transportador, que pode
  retornar para buscar outra unidade necessitada.

## Segurança do AI Step

- O atalho F11 não força mais `Neutral` enquanto um batch, replay, movimento,
  scanner ou transição ainda está em execução.
- Steps repetidos durante a animação são ignorados com indicação do motivo.
- A guarda existe no `DebugManager` e também no `AIController`, evitando
  corrida entre frames e sprites de passageiros visíveis após o embarque.

## Arquitetura transacional

- Ferramentas e serviços apenas avaliam possibilidades antes do compromisso.
- Embarque, desembarque e movimento definitivo continuam ocorrendo no commit
  explícito do batch.
- O fluxo de replay retorna a `CursorState.Neutral` antes de aceitar o próximo
  AI Step.
- Nenhuma decisão de planejamento atualiza FOW, sensores ou memória confirmada.

## Conteúdo de calibração

- Atualizados assets de transportadores, estruturas, ícones e o mapa de
  calibração usados durante os testes de transporte.
- Incluída documentação auxiliar produzida durante esta etapa.

## Verificação

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- Resultado: build concluído com 0 erros.
