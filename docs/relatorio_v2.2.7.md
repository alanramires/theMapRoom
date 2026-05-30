# v2.2.7 - AI Básica Pronta

## Contexto

Marco de consolidação da IA básica. Esta versão fecha os principais buracos de comportamento que impediam a AI de funcionar de forma coerente em cenários competitivos — compras reativas, transporte de evacuação, artilharia estacionária e pressão numérica. A AI agora toma decisões defensivas e ofensivas sem depender de reforços manuais de configuração por turno.

---

## Compras — Modo Defensivo (AIShoppingPlanner)

### Defensive Burst
Quando a postura é `Defensive` e operações do tipo `SectorDefense` não têm unidades atribuídas, o planejador agora abre slots de fogo de suporte além do cap normal. Antes, a AI acumulava dinheiro enquanto setores defendidos ficavam desguarnecidos de artilharia.

```
[AI Shopping] defensive_burst: stance=Defensive ops_sem_defesa=2 → fire_slots=2
```

### Produção Forçada
Quando inimigos estão a ≤7 hexes de um edifício, ou o exército tem 5 ou menos unidades em postura defensiva, todas as reservas são ignoradas e o orçamento restante é gasto imediatamente. Como fallback, compra a unidade terrestre mais barata disponível no edifício.

### Pressão Numérica — Capturadores
Quando o inimigo tem vantagem numérica significativa (`numericalPressure >= threshold`), o planejador adiciona capturadores extras independente dos objetivos do plano:
- 1 capturer extra por 2 unidades de déficit, máximo 3 por turno.

```
[AI Shopping] bulk_cap: pressaoNumerica=4.2 → cap_floor=3
```

### Bloqueio de Elite em Emergência Defensiva
Quando `Defensive` com ≥2 slots de fogo de suporte abertos, elite assault é suprimido e os slots de capturer são zerados. Se um elite de fogo de suporte estiver acessível, ele é comprado imediatamente.

```
[AI Shopping] elite_assault_bloqueado: stance=Defensive fire_slots=2 → elite assault suprimido, cap_slots zerados, elite_fire=true
```

### Acumulação Correta de Déficits
Corrigido: déficits de fogo de suporte de múltiplas operações agora são somados (`+=`) em vez de tomados pelo máximo (`Mathf.Max`). Isso impedia que a AI detectasse necessidade real quando dois setores estavam desguarnecidos simultaneamente.

### Floor de Pausa de Capturadores
A AI agora garante pelo menos 1 capturer por setor no mapa antes de pausar compras de captura — evita o cenário onde a AI para de comprar capturadores com menos unidades do que setores para disputar.

---

## Transporte de Evacuação (AIController.Transportador.Evac.cs)

### Park Adjacent ao Edifício de Reparo
Ao evacuar um passageiro ferido, o APC agora para em uma célula adjacente ao edifício de reparo em vez de na célula do próprio edifício. `FindEvacParkCell` busca um vizinho acessível de onde o sensor de desembarque permite largar o passageiro diretamente na célula do edifício, priorizando menor ameaça.

Sem essa correção, o APC ocupava o edifício e o passageiro não conseguia desembarcar nele.

---

## Artilharia Estacionária (AIController.FireSupport.Rogue.cs)

### Hold Position na Frente
Artilharia sem objetivo atribuído (`rogue`) agora segura posição quando a frente ativa está dentro do alcance máximo + 2 hexes. Antes, artilharia comprada em fábricas laterais tendia a recuar em direção ao HQ quando ociosa.

```
[AI FireSupport] 1042 rogue estacionario @ (3,7,0) — frente a ≤9h, segura
```

A verificação percorre unidades inimigas e edifícios inimigos no snapshot. Se qualquer um estiver dentro do `holdRange`, a artilharia não se move.

---

## Reparo — Acesso a Aeroportos (AIController.Repair.cs)

Removido filtro que impedia unidades terrestres de usar instalações de aeroportos para reparo. O filtro foi inserido para evitar conflito com aeronaves, mas na prática bloqueava reparos legítimos de unidades terrestres quando o aeroporto era a construção mais próxima disponível.

---

## HUD — Ícone de Manutenção (UnitManager.cs)

O ícone de manutenção agora só é exibido para unidades controladas pela AI (`IsPlayerAI(TeamId)`). Antes aparecia incorretamente para unidades do jogador humano que estavam em reparo.

---

## Resultado

A AI agora:
- Reage a pressão numérica comprando capturadores extras
- Produz unidades mesmo sem orçamento otimizado quando sob ameaça direta
- Defende setores com artilharia quando a postura é defensiva
- Evacua feridos sem bloquear o edifício de destino
- Mantém artilharia na linha de frente em vez de recuar para o HQ

Pipeline de compras, transporte, suporte de fogo e reparo estão funcionais para cenários competitivos básicos.
