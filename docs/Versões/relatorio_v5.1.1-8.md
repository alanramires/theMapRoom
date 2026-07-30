# v5.1.1-8 — Refinamento: Vigilância Aérea 8/8

## Encerramento

Este checkpoint conclui o refactor do antigo papel operacional `Intel` para
`VigilanciaAerea`.

O papel agora representa somente a vigilância do espaço aéreo executada por:

- Radar Móvel, terrestre;
- EWACS, aéreo.

Inteligência estratégica, memória de contatos e análise de jogadas continuam
corretamente representadas por `AIIntelLedger`, `AIIntelReport`,
`AISectorIntel`, `AIShoppingPlanner.Intel.cs` e nomes equivalentes.

## Resultado das oito partes

### 1. Migração semântica

- `UnitRole.Intel` foi substituído por `UnitRole.VigilanciaAerea`.
- O valor serializado `6` foi preservado.
- Roteador, helpers, logs operacionais e referências executáveis usam o novo
  nome.
- A auditoria final não encontrou uso executável de `UnitRole.Intel`,
  `IsIntelUnit` ou `TryDecideIntelAction`.

O log `[AI Shopping][Intel]` permanece porque descreve inteligência estratégica
de compras, não o papel das unidades.

### 2. Política compartilhada

Radar Móvel e EWACS seguem a mesma ordem de autoridade:

1. emergência e reparo;
2. recuperação segura;
3. transporte ou plataforma;
4. saída de posição obstruída;
5. melhoria de cobertura aérea;
6. retaguarda conservadora;
7. permanência ou órbita.

Vigilância Aérea age cedo na iniciativa para revelar ameaças antes de combate,
fogo indireto e assalto.

### 3. Radar Móvel

O Radar Móvel:

- opera com `playConservative`;
- usa `longRangeStationary`;
- compara posição atual e destinos Tactical;
- valoriza cobertura AirLow e AirHigh;
- considera detecção stealth;
- mede cobertura marginal contra sensores aliados;
- permanece parado quando o ganho não paga a mudança;
- rejeita vanguarda, ameaça excessiva e isolamento.

### 4. Transporte terrestre

Quando marchar não resolve a necessidade operacional, o Radar Móvel pode
solicitar transporte.

- `QueroCarona` declara a necessidade.
- `MelhorEmbarque` encontra o encontro.
- `PodeEmbarcar` continua validando slot, classe, skills, camada e terreno.
- Passageiro e transportador compartilham o mesmo alvo materializável.
- Fragata, Trem de Carga ou outro transportador somente são usados quando a
  ficha autoriza.

### 5. EWACS e recuperação

O EWACS preserva combustível e recuperação acima da missão normal:

- combustível crítico vence cobertura;
- reparo vence reposicionamento;
- somente células dentro do envelope de recuperação permanecem candidatas;
- pista e plataforma compatíveis vêm de `PodePousar` e `MelhorPouso`;
- o EWACS não persegue cobertura até cair.

### 6. Plataforma aérea

`QueroCaronaAerea` foi integrado ao runtime para:

- Interceptador;
- Ataque Aéreo;
- Vigilância Aérea.

Fora de emergência, a plataforma precisa melhorar significativamente a missão
ou oferecer a única recuperação compatível sem regressão excessiva.

O transportador lê a intenção aérea do passageiro embarcado e não cai no
fallback de captura.

A futura unificação das intenções em `QueroCarona` permanece documentada em
`docs/ideias_futuras.md`; ela é um refactor separado.

### 7. Cobertura estrutural cacheada

`AirSurveillanceCoverageService` consulta a fonte oficial
`PodeDetectarSensor.CollectVisibleAirCellsAt` e armazena:

- cobertura AirLow;
- cobertura AirHigh;
- bloqueios geográficos e LoS;
- versão e fingerprint da topologia;
- mapa, célula e perfil de sensor.

Movimento de unidades não invalida geometria estática de terreno e montanhas.
Sobreposição aliada e ganho marginal permanecem dinâmicos e fora do cache.

O cache é puro: não pinta FOW, não publica contatos e não altera inteligência.

### 8. Ranking final do EWACS

O destino do EWACS deixou de usar somente:

- âncora;
- envelope aproximado de `airVis`;
- retaguarda;
- coesão;
- DPQ;
- ameaça;
- custo do caminho.

Agora cada destino seguro também recebe a cobertura exata:

- células AirLow observáveis;
- células AirHigh observáveis;
- células AirLow novas em relação aos aliados;
- células AirHigh novas em relação aos aliados;
- capacidade de detectar stealth.

A cobertura entra com peso moderado. Ela diferencia destinos seguros, mas não
vence os gates de recuperação, combustível, retaguarda, ocupação ou ameaça.

O log mostra:

```text
low=<total>(new=<marginal>)
high=<total>(new=<marginal>)
coverage=<bruta>
covWeighted=<contribuição no ranking>
```

Isso torna possível estudar por que o EWACS escolheu um hex sem confundir
cobertura bruta com a pontuação final.

## Shopping

A auditoria final confirmou:

- demanda própria para Vigilância Aérea;
- limite separado de EWACS e Radar Móvel;
- EWACS limitado como peça estratégica;
- Radar Móvel como complemento terrestre;
- preferência por alcance aéreo nas fichas compatíveis;
- contagem de unidades existentes e em produção;
- logs `vigilancia_aerea_demand`, `vigilancia_aerea_slots` e
  `vigilancia_movel_slots`.

## Fichas validadas

### Radar Móvel

- papel serializado `6`;
- `playConservative = true`;
- `longRangeStationary = true`;
- visão especializada AirLow e AirHigh;
- detecção stealth configurada;
- gatilhos de manutenção preservados.

### EWACS

- papel serializado `6`;
- `playConservative = true`;
- aeronave não estacionária;
- visão aérea especializada;
- detecção stealth configurada;
- gatilho de autonomia para recuperação;
- pouso e mudança de camada preservados.

## Ferramentas

- `Tools > Utils > Retaguarda` reconhece Vigilância Aérea como suporte de
  retaguarda.
- `Tools > Operações Aéreas > Quero Carona Aérea` reconhece EWACS, missão,
  ganho, regressão e recuperação compatível.
- `Retaguarda` e cobertura aérea permanecem conceitos diferentes:
  formação de aliados versus alcance real do sensor.
- Logs e métricas expõem a decisão runtime sem publicar FOW.

## Política de alcance

- Tactical: avaliação exata das células alcançáveis nesta rodada.
- Operational: progressão ou transporte em direção à área escolhida.
- Strategic: escolha da direção/âncora por distância cúbica.
- Movimento: apenas destino materializável no turno atual.
- Próximo turno: toda intenção é reavaliada.

## Matriz de validação

| Cenário | Autoridade validada |
|---|---|
| Radar atrás de montanha procura cobertura melhor | `PodeDetectar` + cobertura estrutural |
| Radar bem posicionado permanece | ganho mínimo `Stationary` |
| Radar não atravessa a frente por ganho pequeno | gate de retaguarda e ameaça |
| Radar embarca somente quando permitido | `PodeEmbarcar` |
| Passageiro e transportador convergem | alvo e reserva compartilhados |
| EWACS crítico recupera antes de vigiar | roteador + envelope de recuperação |
| EWACS normal não pousa sem ganho | `QueroCaronaAerea` |
| EWACS usa plataforma que melhora a missão | missão + `MelhorPouso` |
| EWACS embarcado não fornece cobertura | filtro de aliados embarcados |
| Sensores evitam redundância | cobertura marginal |
| Vigilância age cedo | iniciativa |
| Cancelamento não publica visão | consulta pura e contrato transacional |

Essa matriz foi validada estruturalmente pelos gates, autoridades e integrações.
O balanceamento dos pesos continua observável em partidas reais pelos logs.

## Contrato transacional

- Células candidatas são virtuais.
- Nenhuma consulta move a unidade.
- Nenhuma avaliação publica FOW, stealth ou contatos.
- O cache contém somente geometria estrutural.
- Reservas de planejamento não alteram ocupação confirmada.
- Movimento, embarque e pouso continuam provisórios até o compromisso.
- Cancelamento não deixa visão ou inteligência residual.
- O tabuleiro definitivo continua recalculado após compromisso e retorno a
  `Neutral`.

## Validação técnica

- Auditoria de nomes legados concluída.
- Shopping, fichas, iniciativa, ferramentas e logs revisados.
- `git diff --check` concluído sem erros.
- Runtime e editor compilados com zero erros.
- Os 417 avisos pertencem ao baseline atual.

## Estado final

O refactor de Vigilância Aérea está encerrado em 8/8.

O próximo trabalho recomendado é o refactor independente de `QueroCarona`
orientado por intenção, já registrado no backlog.
