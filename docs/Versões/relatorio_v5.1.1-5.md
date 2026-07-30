# v5.1.1-5 — Refinamento: Vigilância Aérea 5/8

## Objetivo

Especializar o comportamento do EWACS para que cobertura aérea nunca tenha
prioridade sobre sua recuperação. A aeronave reutiliza as regras globais de
emergência dos caças e passa a limitar seu reposicionamento normal por um
envelope explícito de retorno.

## Identificação do EWACS operacional

A política não depende do nome ou ID do asset. Ela é aplicada a uma unidade que:

- possui o papel `VigilanciaAerea`;
- tem domínio nativo `Air`;
- é reconhecida como aeronave pelas regras de operação aérea.

O Radar Móvel terrestre não cria snapshot de recuperação aérea.

## Prioridade de emergência

O roteador global já consulta reparo antes do papel de Vigilância Aérea.
Consequentemente, os gatilhos definidos no `UnitData` continuam sendo a primeira
autoridade:

- HP crítico;
- autonomia abaixo do percentual configurado;
- necessidade de reparo;
- estado runtime `IsUnderRepair`.

O fluxo de Vigilância Aérea possui ainda uma proteção local. Se for chamado
isoladamente por debug ou ferramenta, ele volta a consultar os mesmos gatilhos e
prioriza a recuperação antes de calcular cobertura.

## Recuperação oficial

O EWACS reutiliza o fluxo já validado para as demais aeronaves:

- `MelhorPousoService` organiza pistas, terrenos e plataformas;
- `PodePousarSensor` valida terreno, estrutura, classe, skills e camada;
- plataformas são aceitas somente quando possuem slot compatível;
- a aproximação usa somente um destino alcançável nesta rodada;
- o embarque em plataforma continua sendo materializado pelo batch oficial.

Quando nenhuma recuperação Tactical ou Operational pode ser materializada, a
regra de emergência procura uma LZ alcançável para preservar o pouso no upkeep.

## Snapshot de recuperação

Foi criado `EwacsRecoverySnapshot`, uma consulta pura que guarda:

- resultado atual do `MelhorPouso`;
- custo do próximo upkeep;
- movimento disponível;
- limite crítico de autonomia;
- estado de combustível crítico.

O snapshot nasce do estado confirmado da aeronave e da revisão confirmada de
ocupação usada pelo `MelhorPouso`. Ele não altera posição, combustível, camada,
ocupação ou FOW.

## Envelope de retorno

Cada célula Tactical candidata da postura de vigilância é validada antes de
participar do ranking.

O orçamento seguro é:

```text
combustível atual
- combustível do movimento desta rodada
- upkeep do próximo turno
```

A célula somente permanece elegível quando esse orçamento ainda alcança uma LZ
ou plataforma compatível do snapshot.

O custo do trecho desta rodada usa a rota válida real. O retorno projetado usa
distância cúbica até a LZ mais próxima, servindo como limite operacional sem
construir uma nova onda de caminhos para cada candidato.

## Órbita segura

Se nenhuma posição melhor preserva a recuperação, o EWACS:

- não persegue cobertura;
- permanece na célula atual;
- registra o estágio `Orbit`;
- reavalia no turno seguinte.

Os logs de postura incluem a LZ ou plataforma de referência, distância de
retorno, orçamento restante, combustível do movimento e upkeep.

## Desempenho

- O `MelhorPouso` é consultado uma vez por decisão do EWACS.
- Não existe uma varredura de pouso por célula candidata.
- O filtro do envelope reaproveita as opções já validadas.
- O movimento desta rodada reaproveita os caminhos Tactical já construídos.

A consulta estrutural e seu cache definitivo serão refinados na Parte 7.

## Contrato transacional

- O snapshot observa apenas o estado confirmado.
- A avaliação não pousa, embarca ou move a aeronave.
- Nenhum candidato publica FOW, detecção ou contatos.
- Combustível e upkeep são apenas projetados, nunca consumidos na consulta.
- Movimento e embarque permanecem provisórios até o compromisso normal do
  batch.
- Cancelamento não deixa camada, ocupação ou visão residual.

## Validação

- `git diff --check` concluído sem erros.
- Runtime e editor compilados com zero erros.
- Os avisos exibidos pertencem ao baseline atual do projeto.
- O `.csproj` gerado foi usado apenas temporariamente para incluir o novo
  partial antes do refresh do Unity e não faz parte do checkpoint.

## Próxima etapa

A Parte 6 integrará a necessidade de plataforma aérea ao runtime:

- `QueroCaronaAerea` aceitará `VigilanciaAerea`;
- o EWACS informará sua próxima zona de vigilância;
- emergência continuará acima do rebasing normal;
- plataforma somente vencerá quando melhorar significativamente a missão ou
  oferecer recuperação necessária;
- `MelhorPouso` e `PodePousar` continuarão sendo as autoridades mecânicas.
