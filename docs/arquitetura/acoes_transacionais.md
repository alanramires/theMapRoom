# Ações transacionais do tabuleiro

## Lei fundamental

> NADA NO JOGO É DEFINITIVO ATÉ O JOGADOR COMPROMETER A AÇÃO.

Esta é uma invariável arquitetural, não uma preferência de interface.

```text
NEUTRAL CONFIRMADO
    ↓
seleção e simulação provisória
    ↓
movimento/animação provisórios
    ↓
sensores e escolha da ação
    ├── cancelar → rollback completo → NEUTRAL CONFIRMADO
    └── confirmar/comprometer
            ↓
        aplicar mutações definitivas
            ↓
        voltar para NEUTRAL
            ↓
        recalcular o tabuleiro confirmado
```

## Estado provisório

Enquanto o fluxo não voltou a `CursorState.Neutral` por compromisso explícito:

- posição apresentada pode ser apenas uma prévia;
- unidade, HUD, cursor, alcance e rastro podem receber sorting temporário;
- menus e sensores podem mostrar possibilidades;
- custos podem ser preparados, mas não consumidos;
- qualquer alteração precisa ser integralmente reversível.

O estado provisório não pode revelar informação nova ao jogador nem alimentar sistemas que tratem essa informação como verdadeira.

## O que não pode acontecer antes do compromisso

- Recalcular ou pintar Fog of War a partir da posição provisória.
- Revelar terreno, submarinos, unidades stealth ou contatos.
- Atualizar `AIIntelLedger`, persistência de detecção ou memória equivalente.
- Incrementar revisões globais usadas como verdade confirmada.
- Invalidar ou substituir caches confirmados usando dados provisórios.
- Consumir combustível, movimento, munição, dinheiro ou suprimentos.
- Aplicar dano, captura, embarque, desembarque, fusão ou destruição definitiva.
- Marcar a unidade como `HasActed`.
- Usar o fim de uma animação como confirmação implícita.

## Ponto de compromisso

O compromisso deve ser explícito no fluxo da ação. `MarkAsActed()` e as rotinas finais específicas representam efeitos do compromisso; não devem ser antecipados para callbacks de animação ou estados intermediários.

Uma ação comprometida deve concluir suas mutações e retornar a `Neutral`. O refresh definitivo deve ocorrer na fronteira de retorno ao estado confirmado, nunca quando a unidade apenas alcança um destino provisório.

## Recalculo após Neutral

Após o compromisso e o retorno a `Neutral`, recalcular a partir do estado definitivo:

1. ocupação e revisões do tabuleiro;
2. caches de movimento e sensores;
3. visão por unidade e por camada;
4. Fog of War e detecção/stealth;
5. contatos e inteligência da IA;
6. HUD, minimapa e demais apresentações definitivas.

O objetivo é produzir um único snapshot confirmado. Nenhum sistema deve observar uma mistura de posição provisória com caches definitivos.

## Cancelamento e rollback

Cancelar deve restaurar exatamente o snapshot confirmado anterior:

- posição, domínio e altitude;
- custos preparados;
- sorting layers e visibilidade temporária;
- caminho, alcance e previews;
- seleção e pilha de estados.

Cancelamento não recalcula visão a partir do destino cancelado e não pode deixar informação revelada.

## Checklist obrigatório para alterações

Antes de concluir mudanças em TurnState, FOW, sensores ou ações, verificar:

- A rotina sabe distinguir prévia de compromisso?
- Algum callback de animação está alterando verdade definitiva?
- Cancelar restaura tudo sem deixar informação escapar?
- FOW/detecção só observam o snapshot confirmado?
- O fluxo começa e termina em `Neutral`?
- O recálculo definitivo acontece apenas após o compromisso e retorno a `Neutral`?
- O mesmo contrato vale para humano, IA e replay?

