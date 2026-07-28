# v5.1.1-4 — Refinamento: Vigilância Aérea 4/8

## Objetivo

Integrar o Radar Móvel estacionário ao transporte terrestre sem transformar a
Vigilância Aérea em um planejador paralelo. O Radar passa a consultar
`QueroCarona` antes de marchar para uma zona operacional distante e continua
usando as ferramentas oficiais para compatibilidade, embarque, LZ e execução.

## Alvo explícito no Quero Carona

`QueroCaronaRequest` agora aceita um alvo operacional explícito:

```csharp
useExplicitTarget
explicitTarget
explicitTargetLabel
```

O serviço compara esse alvo com o alcance Tactical e Operational já calculado.
Quando o Radar consegue chegar por conta própria, ele não solicita transporte.
Quando o alvo está além desse alcance, o resultado registra a necessidade de
carona e conserva a razão legível para logs e ferramentas.

O alvo explícito também participa da chave de cache. Consultas para zonas de
vigilância diferentes não reutilizam indevidamente a mesma decisão.

## Escolha da zona de vigilância

Antes de solicitar transporte, o Radar procura uma zona terrestre segura ao
redor da âncora operacional.

A busca:

- usa vizinhança do `BoardTopologyIndex`, sem varrer o mapa inteiro;
- avalia uma região limitada ao redor da âncora;
- exige terreno em que o Radar possa terminar o movimento;
- descarta células reservadas para captura;
- preserva retaguarda e evita a vanguarda;
- considera cobertura AirLow e AirHigh;
- considera stealth, cobertura marginal e sobreposição aliada;
- pondera coesão, DPQ, ameaça e proximidade da missão.

O Radar só pede transporte quando a zona encontrada supera sua posição atual
por uma margem absoluta e proporcional. Uma posição excelente não é abandonada
por ganho pequeno.

## Compatibilidade e materialização

Antes da avaliação mais cara de cobertura, a IA verifica se existe algum
transportador aliado com slot compatível.

A autorização não depende do nome da unidade:

- `UnitData` define se a unidade é transportadora;
- `MelhorEmbarqueService.TryResolveCompatiblePassengerSlot` aplica as fichas e
  os slots;
- `PodeEmbarcar` permanece a autoridade da ação;
- `Melhor LZ de Embarque` resolve o encontro materializável;
- o batch normal executa movimento e embarque.

Assim, Fragata, Trem de Carga ou outro transportador somente aceitam o Radar
quando a configuração oficial permitir.

## Planejamento compartilhado

A necessidade específica de Vigilância Aérea é inserida no
`TransportPlanningSnapshot`.

Se o panorama do transportador foi produzido antes da solicitação explícita do
Radar, a projeção daquele passageiro é invalidada e refeita. O transportador
passa a enxergar o mesmo pedido, o mesmo alvo e a mesma disposição
`Requested`/`Emergency`.

As reservas existentes continuam evitando que dois transportadores assumam o
mesmo passageiro ou que passageiro e transportador escolham LZs divergentes.

## Destino após o embarque

O Radar embarcado não cai no fallback genérico de captura ou de QG inimigo.

O fluxo de entrega do transportador volta a resolver uma zona segura de
Vigilância Aérea. Se nenhuma zona compatível estiver disponível, mantém o Radar
na posição de espera e reavalia no turno seguinte.

A consulta virtual de cobertura aceita o Radar embarcado somente quando recebe
uma célula candidata explícita. A unidade embarcada continua sem fornecer visão
real ao FOW.

## Autoridades preservadas

- `QueroCaronaService`: necessidade de transporte.
- `MelhorEmbarqueService`: passageiro, slot e LZ de encontro.
- `PodeEmbarcar`: legalidade mecânica do embarque.
- `UnitMovementPathRules`: compatibilidade terrestre da zona de vigilância.
- `BoardTopologyIndex`: vizinhança estrutural do mapa.
- `PodeDetectarSensor`: cobertura virtual por camada.
- `TransportPlanningSnapshot`: compartilhamento e reservas do planejamento.
- batches da IA: materialização das ações.

## Contrato transacional

- A avaliação não move o Radar nem o transportador.
- A cobertura candidata não publica FOW ou contatos.
- O Radar embarcado não revela células.
- Reservas de planejamento não alteram a ocupação confirmada.
- Embarque, movimento e desembarque continuam provisórios até o compromisso do
  batch.
- Cancelamento não preserva visão, posição ou ocupação provisória.

## Validação

- `git diff --check` concluído sem erros.
- Compilação de runtime e editor concluída com zero avisos e zero erros.
- O checkpoint contém somente os arquivos esperados da Parte 4 e este
  relatório.

## Próxima etapa

A Parte 5 especializará o EWACS e sua recuperação:

1. emergência de combustível, HP ou reparo;
2. recuperação em pista ou plataforma compatível;
3. necessidade operacional de plataforma;
4. posição conservadora de vigilância;
5. permanência em órbita.

As regras de emergência usadas pelas aeronaves continuam acima da busca por
cobertura. O EWACS não deverá permanecer em missão quando isso impedir seu
retorno seguro.
