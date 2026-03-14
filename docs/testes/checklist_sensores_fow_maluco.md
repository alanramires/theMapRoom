# Checklist Maluco - Sensores e FoW

Objetivo: validar que `Pode Mirar`, `Pode Detectar`, `Alguem me ve` e `Pode Enxergar` estao coerentes entre si.

## Setup base
- Cena: `Battle Map Test`.
- `Total War`: ON.
- `LdT`: ON.
- `LoS`: ON.
- `Spotter`: ON.
- `Stealth`: ON.
- Ferramentas abertas:
  - `Tools > Combat > Pode Mirar`
  - `Tools > Sensors > Pode Detectar`
  - `Tools > Sensor > Pode Enxergar`
  - Se a unidade tiver `visionSpecializations`, rodar os cenarios por especializacao no `Pode Enxergar`:
    - `Visao Geral (base, sem especializacao)` para cobrir camadas nao especializadas
    - cada especializacao gera seu bloco com:
      - `Desenhar todas as validas`
      - `Desenhar todas as invalidas`

## Teste 1 - Adjacente sem obstaculo
- Coloque soldado A e soldado B adjacentes em planicie.
- Esperado:
  - `Pode Enxergar`: hex do alvo visivel.
  - `Pode Detectar`: alvo avistado/detectado.
  - `Pode Mirar`: deve retornar alvo valido.
  - Em gameplay: unidade inimiga visivel.

## Teste 2 - Floresta no meio (bloqueio EV)
- A em planicie, B atras de floresta (1 hex de floresta entre eles).
- Esperado:
  - `Pode Enxergar`: hex atras da floresta nao deve abrir se LoS bloquear.
  - `Pode Detectar`: sem LoS direta (ou bloqueado).
  - `Pode Mirar`: invalido por LoS/LdT conforme arma.
  - Em gameplay: inimigo oculto se nao detectado.

## Teste 3 - Montanha no meio
- A em planicie, B atras de montanha.
- Esperado igual ao teste 2, com bloqueio mais forte de LoS.

## Teste 4 - Spotter nao abre mapa
- Monte cenario onde spotter aliado poderia ajudar ataque indireto.
- Esperado:
  - `Pode Enxergar`: nao deve iluminar hex so por spotter.
  - `Pode Detectar` (unidade): pode usar regra de observador conforme contexto.
  - FoW de terreno continua coerente com LoS direta de hex.

## Teste 5 - Visao dupla (nao eclipsar)
- Unidade com visao maior em dominio especifico (ex.: naval 6, geral 3).
- Esperado:
  - `Pode Enxergar`: alcance valido maior continua revelando hex (nao reduz para 3 quando 6 for valido).
  - `Pode Detectar`: detectar unidade ainda depende da regra do alvo.

## Teste 6 - Aereo vs terrestre
- Aviao com visao 7 no ar e 3 em terra.
- Esperado:
  - FoW de hex pode permanecer aberto ate 7 quando essa verdade for valida.
  - Unidade terrestre distante so aparece se `Pode Detectar` permitir.
  - Se `DPQ Air Height Config` estiver com `AirHigh blockLoS = false`, em `air/high` o teste de visao por hex considera alcance sem bloqueio de LoS.

## Teste 7 - Stealth real
- Alvo stealth sem skill de deteccao no observador.
- Esperado:
  - `Pode Detectar`: lista em furtivas nao detectadas.
  - FoW unidade: oculta.
  - `Pode Mirar`: invalido por stealth.

## Teste 8 - Stealth com especializacao correta
- Mesmo cenario, mas observador com skill/especializacao correta.
- Esperado:
  - `Pode Detectar`: stealth detectado.
  - FoW unidade: visivel.
  - `Pode Mirar`: alvo pode entrar como valido (se resto passar).

## Teste 9 - Total War OFF (controle)
- Desligue `Total War`.
- Esperado:
  - Todas unidades visiveis.
  - FoW de hex sem efeito pratico.
  - `Pode Mirar` e `Pode Detectar` ainda funcionam como sensores de regra.

## Teste 10 - Consistencia de tilemap
- Garanta unidade em `TileMap` (nao `quebraMar`/`BoardMap`).
- Esperado:
  - Resultado identico entre gameplay e tools.
  - Sem diferenca estranha de LoS por map errado.

## Teste 11 - Unidade com so visao (sem especializacao)
- Use unidade com apenas `visao` base (sem `visionSpecializations`).
- No `Tools > Sensors > Pode Enxergar`, rode com `Forcar camada virtual alvo` desligado e ligado.
- Esperado:
  - Alcance de hex visivel segue o `visao` base.
  - Alcance permanece coerente em todos os dominios/alturas quando nao houver especializacao por camada.

## Teste 11.1 - Unidade com especializacao + visao geral base
- Exemplo: `visao=3`, especializacoes `air/high=7`, `air/low=5`, `submarine/submerged=0`.
- Em `Pode Enxergar`, rode sem `Forcar camada virtual alvo`.
- Esperado:
  - Existe bloco `Visao Geral (base, sem especializacao)` cobrindo camadas nao especializadas (ex.: `land/surface`, `naval/surface`) com range base.
  - Existem blocos separados para cada especializacao com range proprio.

## Teste 12 - Camada virtual forcada air/low
- Em `Pode Enxergar`, ligue `Forcar camada virtual alvo`.
- Configure `Domain virtual = Air` e `Height virtual = AirLow`.
- Esperado:
  - Calculo considera alvo virtual em `air/low` para todos os hexes.
  - Alcance e LoS refletem a regra de visao da unidade para `air/low`.

## Teste 13 - Camada virtual forcada air/high
- Em `Pode Enxergar`, mantenha `Forcar camada virtual alvo` ligado.
- Configure `Domain virtual = Air` e `Height virtual = AirHigh`.
- Esperado:
  - Calculo considera alvo virtual em `air/high` para todos os hexes.
  - Alcance e LoS refletem a regra de visao da unidade para `air/high`.

## Teste 14 - Camada virtual forcada sub/submerge
- Em `Pode Enxergar`, mantenha `Forcar camada virtual alvo` ligado.
- Configure `Domain virtual = Submarine` e `Height virtual = Submerged`.
- Esperado:
  - Calculo considera alvo virtual em `submarine/submerged` para todos os hexes.
  - Alcance e LoS refletem a regra de visao da unidade para `submarine/submerged`.

## Registro sugerido por teste
- `PASS` / `FAIL`
- Coordenadas dos hexes
- Prints (tool + gameplay)
- Flag ativa no momento (`LdT/LoS/Spotter/Stealth/Total War`)
- Observacao curta do desvio
- No `Pode Enxergar`:
  - em validos: registrar `EV final (chegou)`
  - em invalidos: registrar `EV na parada` e `EV que passou` (hex bloqueador)
