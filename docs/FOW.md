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
