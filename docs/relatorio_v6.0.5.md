# Ferramenta de Hotzone como Serviço: Logística Terrestre

## Versão

`v6.0.5`

## Objetivo

Este ponto de verificação transforma a Hotzone em serviço consultável e migra a
primeira família de jogadas — logística terrestre e fusão de reparo — para
consumi-lo.

O ponto central é a separação de responsabilidade. Cada papel da IA vinha
inventando o próprio Tactical e o próprio Operational, com modelos de orçamento
incompatíveis convivendo no mesmo projeto. A partir daqui existe uma única
fonte de alcance, e a jogada volta a cuidar só de política.

```text
política da jogada escolhe: intenção + subetapa
        ↓
UnitReachEnvelopeService responde: Tactical + Operational + custo + origem
        ↓
política interpreta: agir / aproximar / recuar / pedir carona
```

O contrato completo está em `docs/contrato_envelope_alcance.md`. Onde o código
divergir dele, o código está errado.

## Os dois eixos

O envelope é indexado por **intenção** e **banda**.

A intenção decide o que se materializa no destino, sempre delegando a legalidade
e o custo de entrada ao sensor correspondente:

| intenção | sensor |
|---|---|
| `Combat` | `PodeMirarSensor` |
| `Service` | `PodeSuprirSensor` |
| `Transfer` | `PodeSuprirSensor` (alcance de coleta) |
| `Fusion` | `PodeFundirSensor` |
| `Embark` | `PodeEmbarcarSensor` |
| `Capture` | só alcance |
| `Mobility` | só alcance |

A banda decide quão longe no tempo: `Tactical` é a rodada atual, `Operational`
é o turno seguinte.

**Não existe banda estratégica.** Objetivo fora dessas duas não é um envelope
finito — é a IA perguntando "qual direção sigo ou preciso de transporte?". Essa
pergunta é dela.

## Subetapa

Subetapa é parâmetro de entrada, como a intenção. O serviço não deduz nada:
quem classifica a unidade e escolhe é o chamador.

| subetapa | geometria | desloca |
|---|---|---|
| `Terrestre` | caminhos válidos | sim |
| `Aereo` | distância cúbica | sim |
| `Artilheiro` | mira em cúbica | não (MP=0) |

O alcance de arma é decidido pela intenção `Combat`, não pela subetapa.
"Híbrido" não é etapa do serviço: tentar `Artilheiro` e cair para
`Terrestre`/`Aereo` é comportamento da IA.

`Aereo` exige `isAircraft` no `UnitData`. Pedir geometria cúbica para uma
unidade de superfície é pedido inválido, não envelope vazio.

## Operational deixou de ser MP × N

O modelo anterior tratava o alcance de dois turnos como um orçamento único de
`MP × 2` numa busca só. Isso permitia rotas que o jogo não aceita: um soldado de
3 MP recebia 6 no bolso e atravessava três montanhas de custo 2, quando na
prática ele entra em uma por turno.

MP é teto **por turno** e não acumula. O alcance passou a ser somatório de
turnos: o turno atual vale o que ainda sobrou, os seguintes valem MP cheio.

Consequências:

- o Operational encolhe em terreno caro, que é o correto;
- o Operational encolhe conforme a unidade age dentro da rodada;
- um hex que custa mais do que o teto de um turno é intransponível para sempre,
  e **bloqueia o corredor atrás dele**.

`UnitMovementPathRules.CalculateTurnChainedCostMap` resolve isso numa única
passada, relaxando pelo par `(turno, MP gasto no turno)` minimizado
lexicograficamente. Ela reusa o mesmo `TryResolveTraversal` com `previousCell`
do mapa de custo de um turno, então regras de travessia e transição de camada
respondem idêntico.

## Origem da ação

O envelope passou a publicar, por célula de ação, de onde ela é materializável:

```csharp
public readonly struct ReachOrigin
{
    public readonly Vector3Int FromCell;         // onde parar
    public readonly int RemainingMovement;       // MP ao chegar
    public readonly int EnterCost;               // MP para entrar na célula de ação
}
```

`EnterCost` separa as duas contas de MP do jogo. Em combate o tiro não custa
movimento: anda 3 e atira no 4. Em fusão e embarque, entrar no hex custa: anda 2
na montanha, sobra 1, e só materializa no 3 se aquele hex custar 1.

Esse dado era reconstruído à mão pelos consumidores, com nova varredura de custo
sobre os mesmos caminhos.

## Jogadas migradas

**Logística de campo** — `AIController.Logistics.Supply.cs`. A escolha do alvo
parou de montar a própria onda de movimento e de chamar o adaptador legado.
Agora pede `Service` na banda `Operational` e lê `ActionCells` e
`OriginByActionCell`. A política ficou intocada: elegibilidade, pontuação,
cascata de decisão e validação por `PodeSuprir` são as mesmas.

**Fusão de reparo** — `AIController.Repair.cs`. As linhas que recalculavam custo
de caminho e MP restante deram lugar ao custo publicado pelo envelope.

O diagnóstico ganhou a origem:

```text
reason=service_hotzone_2t de=(-9, 11, 0) sobra=5
```

## Verificação

A mesma jogada foi executada antes e depois da migração, com ação, alvo,
pontuação, descartes por sensor e execução idênticos.

O `de=(-9, 11, 0)` publicado pelo envelope coincide com o `via=(-9, 11, 0)` que
o serviço de transporte calcula por outro caminho de código — duas rotas
independentes concordando sobre a célula de parada.

## Ferramenta

A janela `Tools/Utils/Hotzone` deixou de calcular alcance próprio e passou a ser
renderizador do serviço. Ela oferece intenção e subetapa como entradas, mostra o
funil de construção do envelope (orçamento → movimento → ação → primeira recusa
do sensor) e pinta verde para onde a unidade para, vermelho para o que ela só
alcança e azul para o turno seguinte.

Os filtros de sensor (LDT, linha de visada, exigência de observação) ficaram
desligados por padrão: a ferramenta passa tudo. Aplicar Fog of War e descartar o
que a unidade não deveria ver é função da IA e dos sensores `Pode*` na hora da
decisão, não da visualização.

O filtro de camada de operação, que vivia duplicado na janela de Editor e no
painel de inspeção, passou a existir uma vez só, dentro do serviço.

## Pendências conhecidas

- A fusão de reparo foi migrada mas ainda não foi observada em execução.
- O rótulo de banda no diagnóstico ainda pode informar `Operational` para uma
  ação que se completa na rodada atual, porque o envelope Operational contém o
  Tactical. `CalculateTurnChainedCostMap` já devolve o número de turnos por
  célula; falta consumir.
- A zona de serviço da logística ainda é montada uma segunda vez, por fora do
  envelope, para escolher a célula de parada.
- `UnitThreatEnvelopeService` permanece como adaptador de compatibilidade dos
  consumidores ainda não migrados.
- O contrato prevê banda ausente para Fusão e Artilheiro, alcance logístico em
  vermelho, e as intenções `Estoque` e `Desembarque`, que ainda não existem.
