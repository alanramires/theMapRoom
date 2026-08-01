# Unificação de AI Capturador: Constantes

## Versão

`v6.1.4`

## Objetivo

Versão sem código novo de comportamento. Ela conserta duas coisas que valem mais
do que parecem: uma regra que já existia e estava marcada como pendente, e o
arquivo que a IA lê primeiro em toda sessão dizendo números errados.

---

## 1. C5 — a regra já existia

**Regra do autor:** se o capturador agressivo for lutar mas o movimento o levar
para cima de um prédio capturável, ele tenta lutar em outro lugar.

Estava marcada ⚠️ "não verificado" no manual. Está implementada, e bem:
`IsReservedAssaultEscortCaptureCell` é a **primeira guarda** do laço de células
em `TryFindAssaultEscortAttack` — a função que o ramo agressivo usa nas duas
chamadas.

```csharp
return construction.SlotIndex != ResolveAISlotKey(aiTeam)                // não é meu
    || construction.CurrentCapturePoints < construction.CapturePointsMax; // meu, incompleto
```

A célula é descartada quando há capturável que **ainda importa**. Só libera
quando o prédio já é meu e está com captura cheia — aí não há o que atrapalhar.
E o `continue` é literalmente "tenta lutar em outro lugar": segue procurando
outra célula.

Não depende de plano, então vale igual para unidade sem plano.

Item fechado sem escrever uma linha de código. É o argumento a favor de
verificar antes de implementar.

---

## 2. D1 — o `CLAUDE.md` mentia sobre as constantes

Fui conferir uma divergência de número e encontrei duas de três afirmações
erradas no mesmo tópico:

| `CLAUDE.md` dizia | realidade |
|---|---|
| `TransportDropOffRange = 3` | **4** — o `3` é do `FireSupportDropOffRange`, sete linhas abaixo no mesmo arquivo |
| `MinDistanceForTransportSlot = 7`, constante de `Transportador.cs` | **não é constante nem é desse arquivo**: campo serializado em `AIController.cs`, e já migrado para `AIPresetData` |
| `ShuttlePickupRange = 2` | correto |

O segundo é o mais instrutivo: é exatamente o tipo de erro que a migração para
`AIPresetData` vai multiplicar. Cada tunable que sai da cena para o
ScriptableObject deixa para trás uma linha de documentação afirmando que aquilo
é uma constante.

---

## 3. Ranges deixaram de ser número no manual

> "Esses números eram fixos enquanto a programação era nova e eu estava
> aprendendo. As coisas têm que falar em Tactical em vez de drop-off range 3 ou
> 4." — o autor

O `CLAUDE.md` ganhou uma seção declarando a doutrina antes de qualquer número:

> Uma "range" da IA é uma **banda do envelope** — `Tactical` ou `Operational` —
> **da unidade avaliada**, resolvida pelo `UnitReachEnvelopeService`. Nunca é
> contagem fixa de hexes, porque obus de 2 MP e fuzileiro de 3 MP não
> compartilham alcance.

E as constantes passaram de "a regra" para **dívida listada**, cada uma nomeando
no que deve virar. Antes de escrever, verifiquei como cada uma é usada de fato:

| constante | uso real hoje | deve virar |
|---|---|---|
| `TransportDropOffRange` = 4 | fixo de verdade: `transportDistance <= 4 + 0.5f` | Tactical do **passageiro** a partir do objetivo |
| `FireSupportDropOffRange` = 3 | fixo **por papel**: `IsFireSupportUnit(pax) ? 3 : 4` | o mesmo — some junto com a de cima |
| `ShuttlePickupRange` = 2 | **quase lá**: nenhum chamador a usa sozinha, todos fazem `MP + range + margem` | já se comporta como "Tactical + folga" |
| `MinDistanceForTransportSlot` = 7 | teto sobre valor do mapa: `Min(7, Max(3, maiorSetor))` | `Operational` da unidade avaliada — hoje **nunca olha a unidade** |

Duas observações que saíram dessa leitura:

**A `FireSupportDropOffRange` é a prova do conceito, no pior sentido.** Ela existe
só porque a artilharia precisa de um número diferente da infantaria — ou seja, o
código já reconhece que a distância depende de quem é o passageiro, e resolve com
um `if` por papel em vez de perguntar o alcance daquela unidade. Quando a zona de
largada virar banda, as duas constantes e o `if` somem juntos.

**A `ShuttlePickupRange` é o caso mais barato para começar.** Ela nunca aparece
sozinha: é sempre somada ao movimento restante. Migrar ela primeiro prova o padrão
com risco quase nulo.

Os números foram removidos dos bullets de arquivo, onde davam a impressão de ser
a regra, e a seção aponta para os três documentos de doutrina.

---

## Verificação

Nada a rodar: não houve mudança de comportamento. O que mudou foi documentação e
um item de pendência que se provou já implementado.

---

## Pendências relacionadas

- **D2 — varredura de conferência do `CLAUDE.md` contra o código.** Duas de três
  afirmações erradas no único tópico verificado é taxa alta, e este é o arquivo
  que a IA lê primeiro em toda sessão: erro ali vira decisão errada depois.
- **C7 — zona de largada como banda.** Começar pela `ShuttlePickupRange`, pelo
  motivo acima.
- **C8 — destino de unidade sem plano.** Único item com sintoma observado em log,
  e cobre o cenário das unidades que começam embarcadas.
