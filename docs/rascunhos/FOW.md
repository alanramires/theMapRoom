# Fog of War (Total War)

## Objetivo
Controlar informacao em campo: o jogador enxerga o terreno iluminado pelo seu time, mas so enxerga unidades inimigas que realmente foram observadas/detectadas.

## Chaves de configuracao
- `MatchController > Gameplay Setup > Total War`
  - `false`: FoW nao processa (sem custo de pipeline de FoW).
  - `true`: FoW ativo.
- `LdT`, `LoS`, `Spotter`, `Stealth`
  - influenciam regras de observacao/deteccao usadas pelo FoW de unidades.

## Escada de validadores (Game Setup)
As flags sao incrementais. Cada nivel novo adiciona uma validacao sobre o anterior, sem redefinir a base.

Nivel 0 (tudo desligado):
- Sem `LdT`: torpedos/projeteis podem atravessar peninsula/obstaculo de trajetoria.
- Sem `LoS`: tiro depende de alcance da arma (ignora atributo de visao para bloquear ataque).
- Sem `Spotter`: sem observador avancado.
- Sem `Stealth`: ignora skill stealth e especializacao de visao/deteccao.
- Sem `Total War`: sem coloracao de FoW e todas as unidades ficam sempre visiveis.

Camadas adicionais:
- `LdT = true`: valida dominio + trajetoria ate o alvo.
- `LoS = true`: valida elevacao/bloqueio de visada.
- `Spotter = true`: habilita observador avancado para apoiar ataque sem visao direta do atirador.
- `Stealth = true`: exige regra stealth skill vs vision specialization para detectar alvo furtivo.
- `Total War = true`: ativa ocultacao visual de unidades nao observadas + neblina de guerra no mapa.

## Fluxo geral
1. Inicio/troca de turno do time ativo:
   - FoW recarrega para o novo time.
2. Unidade entra em `HasAct = true`:
   - FoW recalcula incrementalmente (priorizando cache).
3. Terreno:
   - mapa inicia escurecido (overlay).
   - hexes visiveis do time ativo removem a nevoa.
4. Unidades:
   - aparecem/somem conforme observacao real.

## Regra central: terreno x unidade
- **Terreno iluminado**: diz por onde o jogador "tem leitura de mapa".
- **Unidade visivel**: exige observacao/deteccao valida.
- Logo: um hex pode estar iluminado e ainda assim nao mostrar a unidade inimiga que esta nele.

## 3 regras correlatas (sensores FoW)
### 1) Pode Enxergar (FoW de hex)
- Responsavel por liberar/iluminar hexes no FoW.
- Avalia visibilidade por alcance + LoS/EV para um alvo virtual por hex (camada virtual do terreno/estrutura/construcao, sem vazar a camada de ocupante oculto).
- Em resumo: determina **onde o time enxerga o mapa**, nao se uma unidade stealth foi detectada.

### 2) Pode Detectar (deteccao de unidades)
- Responsavel por tentar detectar unidades reais no mapa.
- Para alvo stealth: exige combinacao valida de especializacao de visao/deteccao vs skill stealth do alvo (quando `Stealth=true`).
- Para alvo nao stealth: usa alcance por camada + LoS/Spotter/policies aplicaveis.
- Em resumo: determina **quais unidades inimigas aparecem** (ou continuam ocultas).

### 3) Alguem me ve (visao reversa)
- E o fluxo inverso do Pode Detectar: dado um alvo, lista quem ao redor consegue observar/detectar esse alvo.
- Voltado principalmente para diagnostico de unidades furtivas (com skill stealth preenchida), mostrando quem realmente "te ve".
- Em resumo: responde **quem me detecta agora** e por qual motivo.

## Visibilidade de terreno
- Calculada por alcance de visao por camada (`ResolveVisionFor` + especializacoes).
- FoW de terreno usa camada do **terreno** para o calculo do hex.
- Nao usa camada do ocupante oculto (evita vazamento de informacao por "buraco").
- Nao usa `Spotter`: hex ilumina apenas com LoS direta valida (respeita EV/blockLoS de floresta/montanha).
- Construcoes aliadas iluminam o proprio hex (`visao 0`).

## Visibilidade de unidade inimiga
Uma unidade inimiga so aparece se todos os requisitos forem atendidos:
1. Hex da unidade esta iluminado pelo time ativo.
2. Alguma unidade aliada do time ativo consegue observar/detectar o alvo:
   - alcance por camada/domino adequado ao alvo,
   - LOS/Spotter conforme setup,
   - validacao de stealth quando habilitada.

Se falhar, a unidade fica oculta:
- sprite off,
- unit HUD off,
- sem selecao/inspecao,
- fora da lista de `Pode Mirar`.

## Stealth e revelacao
- Unidade stealth depende de especializacao de deteccao (skill/domain/height) quando `Stealth = true`.
- Estados de "revealed for team/turns" continuam valendo e participam da visibilidade.
- Detected indicator (olhinho) segue ligado aos estados de deteccao/revelacao.

## Performance e cache
- FoW usa cache incremental por unidade (padrao inspirado na Hotzone `Z`).
- Em vez de recomputar tudo a cada evento, atualiza prioritariamente unidades afetadas.
- Troca de time invalida/reconstroi o contexto de visibilidade para o novo observador ativo.

## Comando de debug
No `panel_debug`:
- `FoW off`
  - desliga FoW em runtime, limpa overlay e revela todas as unidades.
- `FoW on`
  - reativa FoW com as regras normais.

## Estado atual e prox passos
Base de FoW tatico consolidada: terreno e unidade desacoplados, sem vazamento por empilhamento oculto.
Itens futuros podem incluir refinamentos de sensores especiais, marcadores temporais visuais e regras adicionais por demanda.

## Empilhamento de hexagono no Total War
- No `Total War`, o empilhamento usa bandas de ocupacao (`Air`, `Sub`, `Blocking`) no `OccupancyResolver`.
- Bandas:
  - `Blocking`: camadas de superficie/chao (ex.: `Land/Surface`, `Naval/Surface`).
  - `Air`: `AirLow` e `AirHigh`.
  - `Sub`: `Submarine/Submerged`.
- Regra de termino de movimento (`CanEndMove`):
  - se a unidade move em banda `Air` ou `Sub`, nao ha bloqueio de termino por empilhamento nessa regra;
  - se move em banda `Blocking`, nao pode terminar no mesmo hex de **aliado** na mesma banda;
  - em hex com **inimigo** na mesma banda, o estado vira `hex disputado` (restricoes de acoes no scanner), em vez de bloqueio simples de empilhamento.
- Regra de travessia (`CanPassThrough`):
  - aliados nao bloqueiam travessia;
  - inimigo na mesma banda `Blocking` bloqueia passagem pelo caminho.

## O que acontece ao "voar" para area desconhecida
- Enquanto a unidade esta em pre-visualizacao de movimento dentro da neblina (sem confirmar), o jogo nao vaza informacao de inimigos ocultos.
- Nesse estado, voce pode avancar/voltar quantas vezes quiser no trajeto; nenhum dado de unidade inimiga e revelado ate a confirmacao final do movimento.
- Se existir um inimigo vizinho na area desconhecida, voce nao consegue atacar apenas por "suspeita" durante a navegacao: primeiro precisa comprometer o movimento.
- O refresh incremental principal de FoW para a unidade ocorre quando ela entra em `HasActed` (`MarkAsActed` -> `NotifyUnitReachedHasAct`).
- Em outras palavras: mover e entrar em estado de sensores (`MoveuAndando/MoveuParado`) nao significa, por si so, abrir toda a intel final; a consolidacao forte acontece no compromisso da acao da unidade.
- Resultado pratico: a intel nova entra quando a jogada foi efetivamente assumida no fluxo de acao.
- Excecao operacional conhecida: se um desembarque for permitido porque o hex de destino aparenta vago no momento da acao, pode ocorrer um "vazamento sortudo" minimo ligado a essa validacao de ocupacao.

## Pode Mirar x FoW (por que entro na nevoa e nao consigo engajar)
- `PodeMirar` ja nasce filtrado por FoW em `IsEnemyTargetCandidate`.
- Com `Total War` ligado, se o alvo inimigo nao estiver visivel para o time ativo (`IsUnitVisibleForActiveTeam`), ele e descartado antes mesmo das validacoes de arma/LoS.
- Traducao pratica: o inimigo oculto nao entra na lista de alvos validos de mirar.

- Para uma unidade inimiga ficar visivel no FoW, nao basta "iluminar o terreno":
  1. o hex dela precisa estar visivel (`IsCellVisibleForActiveTeam`);
  2. e a unidade precisa estar observada/detectada (`PodeDetectarSensor.IsTargetObservedByTeam`), com alcance por camada + LoS + stealth conforme setup.
- Ou seja: voce pode entrar na nevoa, ver o terreno, e ainda assim nao poder engajar porque a unidade inimiga ainda nao foi detectada.

- No runtime, a atualizacao forte de FoW/deteccao acontece quando a unidade entra em `HasActed` (`MarkAsActed` -> `NotifyUnitReachedHasAct`).
- Durante navegacao/pre-commit, a intel pode ainda nao estar consolidada para liberar combate.

- Regra adicional de estado: em `Total War`, se o hex estiver disputado e a unidade estiver em `MoveuAndando`, o scanner remove a acao `A` (mirar) temporariamente nesse estado.
