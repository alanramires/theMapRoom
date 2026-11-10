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
