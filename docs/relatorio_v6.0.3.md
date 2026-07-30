# Vigilância Aérea

## Versão

`v6.0.3`

## Objetivo

Este ponto de verificação consolida o refinamento dos papéis de Vigilância Aérea e sua integração com a formação da IA.

O foco foi substituir varreduras caras do tabuleiro por uma política simples, legível e coerente: sensores aéreos acompanham a força aliada, sensores terrestres procuram uma posição estacionária útil e toda a família preserva separação, recuperação e segurança.

## Hierarquia magnética

A formação aérea passou a usar os seguintes ímãs:

- Interceptador: acompanha o mais próximo entre um EWACS e um capitão capturador;
- Ataque Aéreo e Raid Anti-Sub: acompanham um capitão capturador;
- EWACS: acompanha um capitão capturador;
- Radar Móvel terrestre: permanece estacionário e só se reposiciona quando existe ganho suficiente, posição obstruída ou necessidade de transporte.

Essa hierarquia faz os grupos se encontrarem naturalmente:

- capturadores materializam a direção da força;
- EWACS acompanha a cabeça de ponte;
- interceptadores protegem o EWACS ou o capitão mais próximo;
- aeronaves ofensivas deixam de vagar pelo mapa sem referência.

## Repulsão entre sensores

A repulsão continua compartilhada por toda a família de Vigilância Aérea:

- EWACS repele EWACS;
- Radar Móvel repele Radar Móvel;
- EWACS e Radar Móvel também se repelem.

A distância é uma preferência, não uma proibição rígida. Quando não existe alternativa segura, duas unidades ainda podem permanecer próximas.

O diagnóstico informa:

- distância até o sensor aliado mais próximo;
- distância de separação recomendada;
- penalidade de repulsão aplicada.

## EWACS orientado pelo capitão

O EWACS deixou de recalcular ganho de Fog of War para cada destino possível.

Sua decisão normal considera:

- distância cúbica até o capitão;
- distância de escolta compatível com seu alcance;
- custo cúbico do deslocamento;
- ameaça apenas nos candidatos finalistas;
- separação dos demais sensores;
- combustível, upkeep e possibilidade de retorno a uma LZ.

O log identifica a política com `policy=FollowMagnet`, o capitão com `CapturerMagnet=#...` e apresenta `magnetDist`, `escort`, `spacing`, `repel` e `recovery`.

## Hotzone cúbica para aeronaves

O planejamento normal do EWACS passou a consumir a fonte compartilhada:

`AIActionReachCoordinator.BuildSectorReachMap`

Para aeronaves, esse serviço materializa a Hotzone Tactical por distância cúbica e não abre uma onda de pathfinding terrestre.

Com isso:

- `CalcularCaminhosValidos` não é executado durante a decisão normal do EWACS;
- todos os candidatos Tactical são obtidos pela política centralizada de Hotzone;
- somente os 12 melhores candidatos recebem validações mais caras;
- o batch continua revalidando o destino pelas regras oficiais durante a execução;
- caminhos concretos permanecem reservados para emergência, reparo e aproximação de plataforma.

A previsão de combustível usa a distância cúbica quando a decisão não possui um caminho materializado.

## Segurança e recuperação

As simplificações não removeram as proteções do EWACS:

- combustível crítico continua tendo prioridade;
- reparo e emergência continuam procurando pouso ou plataforma;
- cada destino normal precisa preservar combustível para movimento, upkeep e retorno;
- unidades embarcadas ou em reparo não funcionam como ímãs;
- ocupação e destinos proibidos continuam sendo descartados.

## Radar Móvel

O Radar Móvel conserva sua identidade estacionária:

- procura cobertura Air High útil;
- evita a vanguarda;
- respeita a repulsão da rede;
- não permanece sobre construções neutras ou inimigas;
- ao liberar uma construção não controlada, não pode estacionar sobre outra construção igualmente não controlada;
- pode solicitar transporte quando uma posição terrestre melhor não é alcançável diretamente.

## Instrumentação

Foram adicionados ou preservados diagnósticos para separar os custos:

- `airSurveillanceMagnetRanking`;
- `AirSurveillanceHotzoneCandidates`;
- `AirSurveillanceMagnetCandidates`;
- `AirSurveillanceMagnetPreciseCandidates`;
- política e ímã escolhidos no `AI DecisionPreview`.

## Validação

O caso testado do EWACS `#113` apresentou:

- capitão `#13` escolhido como ímã;
- destino a exatamente sete hexes do capitão;
- ausência de ameaça e de penalidade de repulsão;
- retorno seguro à LZ;
- redução do preparo da decisão de aproximadamente `9,87 s` para `1,86 s`;
- atualização incremental do Fog of War concluída em aproximadamente `23 ms`.

A compilação de `Assembly-CSharp.csproj` foi concluída sem erros.

## Resultado

A Vigilância Aérea deixou de tentar resolver o tabuleiro inteiro a cada rodada.

Agora cada papel possui uma responsabilidade direta:

- o capitão fornece direção;
- o EWACS fornece vigilância móvel;
- o Radar Móvel fornece cobertura estacionária;
- o interceptador protege a rede;
- a repulsão distribui os sensores;
- a recuperação impede decisões suicidas.

O resultado é uma formação mais compreensível, previsível e significativamente mais rápida.
