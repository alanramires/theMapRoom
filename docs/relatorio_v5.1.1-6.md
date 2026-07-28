# v5.1.1-6 — Refinamento: Vigilância Aérea 6/8

## Objetivo

Integrar a necessidade de plataforma aérea ao runtime e transformar
`QueroCaronaAerea` em uma declaração de intenção consumida pelas aeronaves e
pelos transportadores, sem retirar de `MelhorPouso`, `PodePousar` e
`PodeEmbarcar` a autoridade mecânica.

## Papéis atendidos

A consulta passa a aceitar aeronaves nativas com os papéis:

- `Interceptador`;
- `AtaqueAereo`;
- `VigilanciaAerea`.

O resultado distingue combate aéreo, Vigilância Aérea e papel não suportado.
`RaidAntiSub` não foi incluído na política normal de rebasing.

## Critérios da intenção

Emergência continua vencendo qualquer comparação de missão. Fora de
emergência, uma plataforma somente é solicitada quando:

- aproxima o foco da missão pelo ganho mínimo configurado; ou
- é a única recuperação compatível no horizonte e não afasta a missão além da
  regressão permitida.

O resultado expõe:

- distância atual até a missão;
- distância da plataforma até a missão;
- ganho de distância;
- existência de recuperação exclusivamente em plataforma;
- plataforma escolhida e razão da decisão.

Uma melhora de apenas um hex não é mais automaticamente suficiente. O runtime
usa uma margem de dois hexes para EWACS e combate aéreo.

## Gate de desempenho

Antes de calcular `MelhorPouso`, o runtime verifica se existe uma plataforma
aliada com slot compatível.

Sem porta-aviões, fragata ou outro transportador autorizado:

- a consulta de pouso não é construída;
- não existe varredura de LZ;
- a aeronave segue diretamente para sua política normal.

Quando o EWACS já possui o snapshot de recuperação da Parte 5, o mesmo resultado
é reutilizado.

## Materialização compartilhada

Foi criado um materializador comum de plataforma aérea.

Em Tactical:

- tenta concluir mover e embarcar no mesmo batch;
- revalida slot, custo, ocupação e `PodeEmbarcar`;
- não altera o estado confirmado durante a decisão.

Em Operational:

- escolhe apenas uma célula alcançável nesta rodada;
- exige progresso real em direção à plataforma;
- considera ameaça e custo do caminho;
- preserva o envelope de recuperação do EWACS;
- reavalia a plataforma no turno seguinte.

## EWACS

O EWACS consulta plataforma depois da emergência e antes da postura normal.

Seu foco vem da mesma âncora de Vigilância Aérea usada para cobertura e
retaguarda. Ele aceita rebasing quando:

- a plataforma melhora significativamente a próxima zona; ou
- é a única recuperação compatível e preserva a missão dentro da tolerância.

Se a plataforma não vencer, o EWACS continua na política de cobertura e órbita
segura da Parte 5.

## Combate aéreo

Interceptadores e unidades de Ataque Aéreo consultam plataforma depois de não
encontrarem ataque legal.

Assim:

- um ataque disponível não é abandonado por rebasing normal;
- sem ataque, a plataforma pode vencer o avanço genérico;
- emergência já foi tratada anteriormente pelo roteador global.

## Intenção lida pelo transportador

Uma aeronave embarcada não pode cair no fallback de passageiro capturador.

O transportador agora resolve primeiro:

- EWACS embarcado: próxima âncora de Vigilância Aérea;
- aeronave de combate embarcada: vetor atual da missão aérea.

Somente passageiros sem uma intenção aérea seguem para os fallbacks de
captura, setor ou QG. Isso impede porta-aviões e fragatas de marcharem para uma
construção inimiga apenas porque transportam uma aeronave.

## Ferramenta

`Tools > Operações Aéreas > Quero Carona Aérea` foi atualizada para:

- aceitar e identificar Vigilância Aérea;
- configurar ganho mínimo;
- habilitar recuperação única;
- configurar regressão máxima;
- mostrar distância atual, distância da plataforma e ganho;
- mostrar se a plataforma é a única recuperação compatível.

## Autoridades preservadas

- `QueroCaronaAerea`: necessidade e intenção.
- `MelhorPouso`: ranking de LZs e plataformas.
- `PodePousar`: terreno, estrutura, classe, skill, camada, vaga e
  exclusividade.
- `PodeEmbarcar`: materialização do slot.
- caminhos Tactical: trecho executável desta rodada.
- política do passageiro: objetivo lido pelo transportador.

## Contrato transacional

- A consulta não move aeronave nem plataforma.
- Nenhum candidato altera FOW, contatos ou inteligência.
- Aproximação e embarque permanecem provisórios até o compromisso do batch.
- O transportador apenas lê a intenção; não altera ocupação durante o
  planejamento.
- Cancelamento restaura integralmente posição, camada e slot provisórios.

## Validação

- `git diff --check` concluído sem erros.
- Runtime e editor compilados com zero erros.
- Os 417 avisos pertencem ao baseline atual.
- O `.csproj` gerado foi usado somente para incluir o novo partial antes do
  refresh do Unity e não integra o checkpoint.

## Próxima etapa

A Parte 7 extrairá a cobertura de Vigilância Aérea para uma consulta pura e
cacheada:

- visão AirLow e AirHigh;
- detecção stealth;
- bloqueios geográficos;
- cobertura aliada e ganho marginal;
- chave por mapa, célula, perfil e versão da topologia;
- nenhuma reconstrução de FOW por candidato.
