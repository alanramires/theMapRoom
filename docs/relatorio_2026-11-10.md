# Diário de implementação — 2026-11-10

## Caça A: controlador dos céus

O piso de um Caça A deixa de depender exclusivamente de reação a aeronave inimiga em ataque.
Com aviação adversária (produção em aeroporto ou aeronave visível), Caça B em campo abre a
progressão de elite Interceptador até o nível 1. Core, cadeia, horizonte e poupança conservam
suas fontes existentes. Não é autorização para escalar preventivamente ao Caça F.

A reserva disputa em paralelo: avistamento aéreo põe Interceptador antes de Assalto; sem
avistamento, Assalto mantém preferência. Compromissos existentes não são trocados arbitrariamente.
O fluxo em uso (carrinho por papéis) foi integrado além do caminho legado citado no pedido.

Sem presa, o caça protege o que está exposto: guarda-chuva AA e ameaça aérea vêm da mesma
consulta compartilhada com o shopping. Tenta cobrir EWACS e vanguarda juntos pela própria
banda; sem interseção, prioriza EWACS exposto, depois vanguarda. Um EWACS já coberto não é
uma âncora só por estar perto. Não foi criado raio fixo para essa escolha.

## Achados e decisões do autor

- A ficha Caça A era Defensive: a primeira fixture em Tactical não podia passar sem respeitar
  esse dado. O autor pediu mudar AR Caça A e AR Caça F para Either; feito pelo Editor, sem
  bypass especial de postura no código.
- A consulta antiga de guarda-chuva usava Terrestre para o SAM de alcance mínimo maior que 1;
  o contrato atual do envelope recusa esse ramo. A extração corrige a pergunta usando a
  validação existente de subetapa, compartilhada pelos dois consumidores.

## Validação e pendências

`bash tools/compilar.sh` passou nas duas assemblies. Fixtures de progressão/reserva e de posição
por exposição passaram fora do Play, sem compras
confirmadas nem alteração do ledger. A fixture de posição usa ameaça rápida sintética para
isolar o ramo de escolta sem presa operacional. Casos detalhados em `ai_test.md`.
A ação final de cobertura e a ordem de pré-compra também foram verificadas, sem executar.
Cinco regressões de interceptação e shopping passaram. Ainda falta observar prevenção, poupança
e postura durante a partida; não houve ensaio ponta a ponta dos três perfis nem migração de
compromissos antigos nesta frente. Nenhuma versão nova
foi escolhida ou tag publicada por este diário.

## Docs de doutrina

Desatualizados:
- `docs/AI Behavior/governanca_entre_papeis.md`, §2.3, linha Interceptador: substituir atração
  por proximidade por exposição, cobertura conjunta no Tático próprio, prioridade EWACS
  exposto/vanguarda e fallback sem atrair EWACS coberto. Regra implementada e testada em
  fixture; ainda sem validação em Play.
- `docs/rascunhos/AI Shopping.md`: apontar piso preventivo de Interceptador elite 1, gatilho
  de aviação, disputa paralela da reserva e limite que impede progressão preventiva ao F.

A doutrina não foi reescrita aqui. A skill `consolidar-doutrina` fará a alteração quando o
autor chamar. Herdar no próximo fechamento a lista de débitos do relatório v10.7.0.


## Revisão: ameaça e identificação estruturada

A cobertura conjunta deixou de aceitar apenas o centro geométrico: caminhos são filtrados
pela interseção e avaliados por `FindAirCombatAdvanceMove`, com a fórmula de movimento e o
peso de ameaça já existentes, sem cópia do score. Só nesse uso admite manter posição ou
deslocamento lateral; padrões dos demais chamadores continuam iguais.

A demanda carrega `IsPreventiveAirControl`, e a âncora devolve `out bool sharedExposedCover`.
O comportamento deixa de depender dos textos do motivo/tier, que permanecem nos logs.
Compilação das duas assemblies e as duas fixtures repetidas: PASS, fora do Play. A regressão
da perseguição operacional também passou. Validação em partida continua pendente.


Revisão 4: EWACS coberto sem capitão não recebe retorno especial ao HQ. O interceptador
continua na cadeia antiga (inimigo mais próximo e demais fallbacks). Caso acrescentado à
fixture com HQ presente; a doutrina continua pendente de consolidação no documento de governança.


Revisão 5: a escolta do EWACS exposto ganha um claim local da passada da Fase 2, limpo junto
das intenções de destino. Outro interceptador desce ao capitão exposto; sem outro elo continua
aceitando o EWACS, sem sobrescrever o primeiro escolta. Reavaliação pelo próprio caça é idempotente.
Consultas auxiliares de decolagem/courier não registram claims. Não é uma atribuição 1:1 geral
nem estado persistido. Fixture sequencial, progressão preventiva e compilação: PASS fora do Play;
limpeza do início da Fase 2 conferida no código, com limpeza simulada na fixture.


### 2026-11-10 — fatia A: partida e propriedade sem excecao de faccao

- `verificar_regras_sem_qg.cs`: PASS no Editor, fora do Play, em cena temporaria. Tres slots: sem QG toma QG, elimina apenas o dono anterior e a partida continua; promocao dinamica preservada. Compra segue FreeMarket/OriginalOwner/FirstOwner/Disabled, sem flag de faccao; ocupacao sem QG nao grava FirstOwner nos setters de slot/team, e o primeiro captor com QG grava.
- Zero unidades elimina tambem o slot sem QG e neutraliza suas construcoes pelo `MarkSlotDefeated`. Fixture cobre primeiro turno, configuracao desativada e tutorial; captura de QG tambem nao elimina em tutorial. Ultimo sobrevivente recebe motivo `HeadQuarterCaptured`.
- `QuadranteData.allowDefeatForZeroUnits` e configuracao autoral serializada, conservada pelo bake, editavel no Map Helper e aplicada pelo QuadranteController ao pintar. Sem quadrante, permanece o valor serializado da cena. Fixture confere aplicacao true/false e fallback null; pintura completa ainda nao observada em Play.
- Removidos campo `allowRebelAIPurchase`, excecao de compra e secao correspondente do Inspector. Antes da remocao, flag ligada em Barracks, Docas, Garagem APC, Hidrobase e Terminal Rodoviario; autor avisado. Nenhuma sellingRule de instancia foi migrada automaticamente. Serializacao legada nos assets deixa de ser lida.
- Ambas assemblies compiladas por `bash tools/compilar.sh`. Teste usa a resolucao privada da captura apos a guarda de runtime; nao executa animacao nem captura em Play. Intercepta o evento final para nao registrar progresso nem iniciar apresentacao na cena do autor. Derivacao `IsHeadQuarterlessTeam` inalterada. B, C e renomeacoes nao iniciados.


### 2026-11-10 — A6: eliminacao por exercito tambem apenas elimina

- `HandleUnitDestroyed` e o inicio de `ReleaseUnitsForActiveTeam` usam `TryResolveArmyElimination`: marca/neutraliza pelo fluxo existente e consulta `TryDeclareLastStandingWinner(ArmyEliminated, defeatedTeam)`. Tres lados continuam com dois sobreviventes; 1v1 termina com o unico de pe. Guardas de runtime, tutorial, configuracao e turno preservadas.
- `DeclareEliminationVictory` ficou apenas para rendicao; removida a sobrecarga por TeamId sem chamadores e corrigido o comentario antigo sobre primeiro eliminador.
- `verificar_regras_sem_qg.cs`: PASS ampliado, fora do Play. O mesmo funil chamado no inicio do turno foi exercitado com slot sem QG/zero unidades: tres slots eliminam/neutralizam sem vencedor; dois slots coroam o sobrevivente com `ArmyEliminated`. Fixture nao executa o turno completo nem dispara destruicao em Play; ligacao dos dois pontos ao funil conferida no codigo. Demais casos da fatia A continuam passando. Ambas assemblies compilaram com `bash tools/compilar.sh`.
- B e C nao iniciados.
