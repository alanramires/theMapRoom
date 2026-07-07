# v3.0.5 - AI Intel Aerea

Esta versao inaugura a primeira base estrutural de Intel para a AI e separa melhor as camadas de visao do Fog of War. O foco foi permitir que radar, EWACS e leitura aerea existam como informacao propria, sem depender da revelacao descendente do terreno.

## AI Intel

- Criado o comportamento `AIController.Intel`.
- Unidades de Intel passam a usar o mesmo principio de retaguarda do Fire Support: ficam atras da linha e evitam vanguarda.
- O roteador da AI passou a reconhecer unidades de Intel como papel proprio.
- A demanda de Intel aerea foi ligada a capacidade produtora aerea do oponente, nao apenas aos aeroportos do proprio slot.
- Se o inimigo tem aeroporto no mapa, a AI pode demandar visao aerea.
- Se a AI nao tem producao propria de EWACS, o Radar Movel vira fallback terrestre barato para ver o ceu.

## Shopping

- O shopping ganhou demanda especifica para Intel aerea.
- EWACS e Radar Movel passaram a competir por slots de Intel conforme disponibilidade de producao e budget.
- A leitura de ameaca aerea usa inimigos visiveis, inferencia por JogadasManager e capacidade produtora inimiga.
- Super Tucano foi removido da disputa direta de Intel para poder voltar ao papel de raid anti-sub/assalto leve.

## Unidades e dados

- EWACS foi reposicionado como Intel aerea dedicada.
- Radar Movel foi ajustado como Intel terrestre barata para detectar aeronaves.
- Super Tucano ficou fora do eixo de Intel principal, reduzindo competicao indevida com EWACS/Radar.
- Foi criado skill de stealth aereo para suportar deteccao especializada.

## Fog of War por camada

- Criado `FogOfWarVisionMode`.
- O atalho `L` alterna a exibicao visual do FoW:
  - `All`: Todas
  - `Air`: Aerea
  - `Surface`: Superficie
  - `Sub`: Submarina
- O painel `Panel_remaining` atualiza `text_camada` ao trocar de camada.
- O ciclo do `L` pula camadas que nao fazem sentido no mapa:
  - `Air` so aparece se existir `isAirport`.
  - `Sub` so aparece se existir `isHarbor`.
  - `Surface` so entra quando existe aeroporto ou porto para justificar comparacao de camadas.

## Pode Enxergar vs Pode Detectar

- Corrigida a mistura estrutural entre revelar tile e revelar unidade.
- `Pode Enxergar` continua sendo a fonte para revelar hexes/terreno: a linha descendente ate o chao.
- `Pode Detectar` passou a ser a fonte de verdade para revelar unidades/contatos.
- Uma unidade aerea detectada por EWACS pode aparecer mesmo quando o hex de superficie abaixo dela continua coberto.
- Isso corrige o caso do caca sobre o mar atras de floresta: o EWACS detecta o contato aereo, mas nao revela indevidamente o terreno.
- Uma AAA atras de montanha continua escondida, porque Intel aerea nao vira visao terrestre/satelite.

## Resultado esperado

A AI passa a ter uma base concreta para comprar e operar sensores de Intel, especialmente contra ameaca aerea. Para o jogador, o FoW fica mais legivel: a camada visual explica o que esta sendo revelado, enquanto contatos detectados aparecem conforme o sensor validado pelo `PodeDetectar`.

## Validacao

- `dotnet build Assembly-CSharp.csproj`
- Resultado: 0 erros.
- Avisos: warnings obsoletos de Unity ja existentes no projeto.
