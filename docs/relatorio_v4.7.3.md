# v4.7.3 — Refactor da AI Asssault and Fire Support 3/4

## Objetivo

Executar a terceira parte do refactor conjunto de Assault e FireSupport,
materializando o encontro entre passageiro combatente e transportador e
incorporando os papéis antiaéreos aos pipelines comuns.

## Progressão até o embarque

A decisão produzida por `QueroCaronaService` e `MelhorEmbarqueService` agora
pode ser materializada mesmo quando passageiro e transportador ainda não estão
reunidos.

O passageiro:

- embarca diretamente quando o transportador já está na LZ e
  `PodeEmbarcarSensor` confirma transportador e slot;
- move diretamente para a LZ quando ela está ao alcance no turno;
- aguarda quando já ocupa a LZ escolhida;
- progride em direção à LZ por `TransportRendezvous` quando ela ainda está
  distante;
- libera o papel para outra ação quando nenhuma progressão pode ser
  materializada.

O transportador continua podendo se aproximar independentemente pelo serviço
comum de operações de transporte.

## Candidatos sem rota atual

O estado `NoCurrentRoute` deixou de eliminar automaticamente um passageiro da
avaliação de melhor embarque.

O serviço decide qual é o melhor encontro. A forma de materializar a
aproximação continua sendo responsabilidade do controller, permitindo que o
transportador se aproxime de uma unidade que não consegue chegar sozinha à LZ.

## Reserva efêmera

A decisão escolhida registra uma claim apenas durante a passada corrente da
Phase 2.

Essa claim:

- evita que dois passageiros escolham simultaneamente o mesmo transportador;
- não altera posição, ocupação ou dados persistentes da unidade;
- não representa embarque confirmado;
- desaparece com o contexto efêmero da decisão.

## Antiaéreo

`Antiaereo` passa a usar integralmente o pipeline de FireSupport.

Sua única especialização permanece na seleção de alvo: somente unidades no
domínio aéreo são aceitas. Reposicionamento, segurança, transporte, embarque e
espera seguem as mesmas regras do restante do FireSupport.

## Antiaéreo Combatente

`AntiaereoCombatente` passa a usar integralmente o pipeline de
`ArtilheiroCombatente`, inclusive o roteamento híbrido Assault + FireSupport e
as mesmas decisões de transporte.

A distinção também fica restrita à seleção de alvo aéreo. O
`Shopping Pressure` não foi alterado nesta etapa.

## Filtros de alvo

O filtro por papel foi centralizado e aplicado nas avaliações de:

- ataque de FireSupport;
- alvos bloqueados;
- postura e reposicionamento em alcance máximo;
- progressão rogue;
- escolta e avanço de Assault;
- ruptura de QG;
- ataque para liberar objetivo de captura.

Papéis comuns continuam aceitando seus alvos normais. Apenas os dois papéis
antiaéreos restringem o alvo ao domínio aéreo.

## Ajustes adicionais incluídos no snapshot

O inspector de `ConstructionData` recebeu uma reorganização paralela presente
no worktree:

- seções recolhíveis para informações da unidade, produção, logística e
  comportamento da IA;
- agrupamento de Aircraft Ops e Naval Ops;
- exposição das opções de upkeep de aeronave pousada e detecção após emersão
  forçada;
- remoção de cabeçalhos duplicados desenhados pelos dados serializados.

## Arquitetura transacional

- As consultas de carona e embarque permanecem puras.
- A claim é apenas coordenação efêmera da fase de decisão.
- Nenhum scan altera FOW, detecção, recursos, ocupação ou estado confirmado.
- O controller apenas constrói batches existentes de movimento, espera ou
  embarque.
- O compromisso continua pertencendo ao fluxo explícito que retorna a
  `CursorState.Neutral`.

## Arquivos principais

- `Assets/Scripts/Match/AI/AIController.Router.cs`
- `Assets/Scripts/Match/AI/Units/Assault/AIController.Assault.cs`
- `Assets/Scripts/Match/AI/Units/Assault/AIController.Assault.Defender.cs`
- `Assets/Scripts/Match/AI/Units/Assault/AIController.Assault.HQBreaker.cs`
- `Assets/Scripts/Match/AI/Units/Fire Support/AIController.FireSupport.cs`
- `Assets/Scripts/Match/AI/Units/Fire Support/AIController.FireSupport.Attack.cs`
- `Assets/Scripts/Match/AI/Units/Fire Support/AIController.FireSupport.Reposition.cs`
- `Assets/Scripts/Match/AI/Units/Fire Support/AIController.FireSupport.Rogue.cs`
- `Assets/Scripts/Match/AI/Units/Fire Support/AIController.FireSupport.Antiaereo.cs`
- `Assets/Scripts/Match/AI/Units/Fire Support/AIController.FireSupport.Antiaereo.Combatant.cs`

## Próxima etapa

A parte 4/4 deve concluir a consolidação:

- auditar todos os pontos de entrada de Assault e FireSupport;
- remover caminhos e seletores de transporte que tenham ficado obsoletos;
- consolidar diagnóstico e logs da decisão passageiro–transportador;
- verificar regressões de roteamento dos papéis híbridos e antiaéreos;
- documentar o fluxo final e seus limites.

## Verificação

- `dotnet build Assembly-CSharp.csproj --no-restore`;
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`;
- `git diff --check`.
