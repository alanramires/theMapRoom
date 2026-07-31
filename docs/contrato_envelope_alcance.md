# Contrato do envelope de alcance

Contrato definido pelo autor. É a especificação de referência do
`UnitReachEnvelopeService`. Onde o código diverge, o código está errado.

## Cores

| cor | significado |
|-----|-------------|
| verde | Tactical — o que a unidade faz nesta rodada |
| azul | Operational — o turno seguinte (`+MP`) |
| vermelho | alcance de arma (inclui "armas" logísticas) |

`MP` é teto **por turno** e vem do `UnitData`. Não acumula: `+MP` é um segundo
turno, não orçamento dobrado. Um soldado de 3 MP faz 3+3, nunca 6 — por isso na
montanha de custo 2 ele entra em uma no verde e mais uma no azul.

## A Hotzone só devolve o que é materializável

O envelope responde seis coisas e nada além: **Tactical**, **Operational**,
**movimento**, **ação**, **custo**, **origem da ação**.

**Não existe banda estratégica.** Strategic não é um envelope finito — é a IA
dizendo *"meu objetivo está fora dessas bandas; qual direção sigo ou preciso de
transporte?"*. Essa pergunta é dela.

Pelo mesmo motivo, o envelope **não varre o tabuleiro** procurando objetivo,
não chama `PodeCapturar` e não conclui "dispensa carona". A inteligência de
objetivos já tem lugar próprio:

- alocação 1:1 — `CaptureOpportunityClaimService`
- objetivo persistido da unidade — `AIController.Rebel`
- validação final da construção — `PodeCapturarSensor`

`ClassifyMobility` fica como **consulta dirigida**: a IA entrega um alvo
específico e pergunta se é `OwnComponent`, `OtherComponent` ou `NotOccupiable`.

```text
IA escolhe objetivo
        ↓
pede envelope Tactical/Operational
        ↓
envelope responde alcance / custo / origem
        ↓
IA cruza objetivo × alcance
        ↓
IA decide andar, capturar ou pedir carona
```

### Divisão de responsabilidade

| camada | papel |
|---|---|
| Hotzone (ferramenta) | formula o pedido e pinta a resposta |
| Envelope (serviço) | calcula alcance materializável |
| IA | escolhe objetivos, interpreta Strategic e decide |

## Subetapas

Subetapa é **parâmetro de entrada**, como a intenção. O serviço não deduz nada:
quem classifica a unidade e escolhe é o chamador — a IA. O serviço é burro e
reativo.

Só existem três, e elas decidem **apenas a geometria e se a unidade se desloca**:

| subetapa | geometria | desloca |
|---|---|---|
| `Terrestre` | caminhos válidos | sim |
| `Aereo` | distância cúbica | sim |
| `Artilheiro` | mira em cúbica | não (MP=0) |

O alcance de arma (vermelho) é decidido pela **intenção Combate**, não pela
subetapa.

**Híbrido não é subetapa.** Tentar Artilheiro e cair para Terrestre/Aéreo é
comportamento da IA. **Ficção também não existe**: o jogo não tem zepelins.

### Árvore

```text
Mobilidade ── Terrestre, Aereo
Combate ───── Terrestre, Aereo, Artilheiro
Captura ───── (sem ramo: sempre terrestre)
Fusão ─────── Terrestre, Aereo
Suprir ────── Terrestre, Aereo
Estoque ───── Terrestre, Aereo
Embarque ──── Terrestre, Aereo
Desembarque ─ (sem ramo)
```

`UnitReachEnvelopeService.GetSubSteps(intent)` é a fonte dessa árvore.

### Validação pela unidade

`Aereo` exige `isAircraft` no `UnitData`. Pedir geometria cúbica para uma
unidade de superfície é **pedido inválido**, não envelope vazio: `Build`
devolve `null`. Isso é validação de entrada, não dedução.

`GetSubSteps(intent, unit)` devolve a árvore já filtrada pelo que a unidade
suporta; é o que a ferramenta usa para nem oferecer a opção.

## Intenções

### Mobilidade
Verde em MP, azul em `+MP`. Sem vermelho.

A IA decide o que fazer com isso: seguir o capitão, caças nos flancos e
vanguarda, bombardeiros na retaguarda.

### Combate

**No azul não entra a arma.** Não precisa.

| subetapa | retorno |
|---|---|
| `Terrestre` | verde em MP + vermelho no alcance da arma; azul em `+MP` |
| `Aereo` | verde em MP (cúbica) + vermelho no alcance da arma |
| `Artilheiro` | verde em MP=0, vermelho no alcance da arma; **sem azul** |

- Terrestre: a IA decide atacar ou avançar até o oponente no azul.
- Artilheiro: é comum atirar se estiver no tactical. No operational depende —
  na vanguarda recua, na retaguarda talvez avance. Decisão da IA.
- Dupla função (artilheiro combatente, antiaéreo combatente): a IA valida
  `Artilheiro`; se não der, pede `Terrestre` ou `Aereo`.

O tiro parado entra sempre: numa arma de alcance mínimo 1 e máximo 2, o tiro
pós-movimento colapsa para 1, então `MoveuParado` alcança alvos que
`MoveuAndando` não alcança.

### Captura / Reconquista

Verde `+MP`, azul `+MP`. **Só alcance.**

A IA cruza esse alcance com os objetivos que ela escolheu. Capturadores
geralmente recusam carona se conseguem chegar até o azul.

### Fusão

Verde `+MP`, **sem azul**.

Se não consegue fundir no verde, usa Mobilidade para recuar e tentar na próxima
rodada. Na invasão final, fundir na retaguarda é obrigatório.

### Suprir

Verde `+MP` com "arma logística" em vermelho (supply range); azul `+MP`.
Supply range 0 → não há vermelho.

Se não conseguir suprir, pode querer avançar para o operational — mas se isso o
puser na vanguarda, talvez não vá.

### Estoque

Verde `+MP` com "arma de caixa" em vermelho (operational range); azul `+MP`.
Range 0 → não há vermelho.

Se não conseguir estoque, pode querer avançar para o operational; checar
"play conservative" antes de decidir avançar.

### Embarque

Verde `+MP` −MPRequired (custo do terreno), azul `+MP`.

A área verde é menor porque a unidade precisa se posicionar E conservar MP para
entrar no hex do transportador. Montanha e floresta entram, porque pode haver um
APC ali. Se o transportador estiver no azul, vai ao encontro dele.

A subetapa `Aereo` serve para procurar porta-aviões ou fragatas.

Quando o transportador está fora do componente de movimento da unidade, quem
descobre isso é a IA, via `ClassifyMobility` dirigida ao alvo escolhido.

### Desembarque

Verde `+MP` `+1MP`, azul `+MP`.

Desembarque é adjacente e exige 1 MP válido, por isso o `+1MP` entra.
Embarcados `isAircraft` decolam, então qualquer hex é válido.

## Cobertura

Toda missão de unidade cai em alguma categoria acima — seguir o capitão,
abastecimento, e as demais.

---

## Estado do código

Verificado em `Assets/Scripts/Match/AI/Services/UnitReachEnvelopeService.cs`.

### Conforme

- as três cores e a ausência de banda estratégica;
- `+MP` como turno encadeado (`BuildTurnChainedReach`), não MP×2;
- bloqueio de terreno por teto de MP por turno (`BuildOwnMovementComponent`):
  hex acima do teto é intransponível e **bloqueia o corredor** atrás dele;
- ausência de arma no azul;
- subetapa como parâmetro de entrada, com árvore e validação por unidade;
- Captura devolve só alcance — sem `PodeCapturar` dentro do envelope;
- `ClassifyMobility` só como consulta dirigida.

### Falta

| # | contrato | código hoje |
|---|---|---|
| 1 | Fusão e Artilheiro não têm azul | `BuildProfile` sempre devolve Tactical + Operational |
| 2 | Suprir/Estoque expõem o range como vermelho | range de serviço entra em `ActionCells`, sem distinção de "arma" |
| 3 | Estoque é intenção própria ("arma de caixa") | não existe; só `Service` e `Transfer` |
| 4 | Desembarque é intenção própria (`+1MP`) | não existe |

### Dívida de migração

`UnitThreatEnvelopeService` continua como fachada de compatibilidade dos 4 call
sites antigos da IA. Ela é o único lugar que ainda deriva subetapa da unidade
(`ResolveLegacyCombatSubStep`), para não mudar o comportamento deles. Quando a
IA for migrada arquivo por arquivo, a fachada e `ResolveCombatProfile` somem
juntas.
