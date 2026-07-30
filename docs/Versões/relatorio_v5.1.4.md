# Refactor do Melhor LZ de Embarque e MP em Estruturas

## Versão

`v5.1.4`

## Objetivo

Consolidar duas fontes de verdade que passaram a trabalhar juntas:

1. `Melhor LZ de Embarque`, agora capaz de coordenar passageiro e
   transportador sem confundir os destinos de cada unidade;
2. `Caminhos Válidos`, agora responsável por interpretar custos e permissões
   de movimento em terrenos, estruturas, construções e redes de rota.

O princípio preservado é:

> O transportador recebe uma LZ válida para seu domínio; o passageiro recebe
> um hex de encontro válido para seu próprio domínio.

## Melhor LZ de Embarque centrado no passageiro

A consulta deixou de exigir que a unidade selecionada fosse o transportador.
Agora o passageiro é a origem da pergunta:

```text
Qual transportador compatível pode atender esta unidade,
em qual LZ ele deve ficar e onde o passageiro deve aguardar?
```

O transportador tornou-se um filtro opcional:

- informado: a consulta considera somente aquele transportador;
- ausente: compara todos os transportadores aliados compatíveis;
- incompatível: registra a razão do descarte.

Antes de varrer LZs, o serviço consulta as vagas do transportador pela fonte
oficial `PodeEmbarcarSensor.CanUseSlot`. O casamento passageiro–vaga verifica:

- domínio e altura aceitos;
- classe da unidade;
- skills obrigatórias e bloqueadas;
- capacidade restante;
- exclusividade entre slots.

Assim, um navio pode estar em Praia como `Naval/Surface` e oferecer uma vaga
`Land/Surface` ao Obus. A camada do transportador não precisa ser igual à
camada aceita pela vaga.

## Dois destinos, duas unidades

O resultado de `MelhorEmbarqueService` agora conserva explicitamente:

- `lzCell`: posição destinada ao transportador;
- `passengerMeetingCell`: parada válida destinada ao passageiro;
- `transporter`: transportador ao qual a opção pertence;
- estado da rota do passageiro:
  - `ReachableNow`;
  - `ReachableLater`;
  - `ReachableStrategic`;
  - `NoCurrentRoute`.

O cálculo já expandia cada parada do passageiro para o anel adjacente, mas
descartava qual parada havia produzido o encontro. O custo sobrevivia; o hex
real não. Isso fazia a IA enviar peças terrestres em direção ao próprio navio
ou a uma Praia que elas não podiam ocupar.

Agora o mapa de encontro preserva o par:

```text
LZ do transportador -> hex de parada do passageiro + custo
```

Quando o encontro está além do Operational, uma onda direcional de custo é
construída somente para a projeção passageiro–transportador que precisa dela.
O resultado usa o `MovementReachCache` compartilhado e é classificado como
`ReachableStrategic`.

## Materialização pela IA

Os papéis passageiros que usam o fluxo comum de transporte — Assault,
Fire Support e Vigilância Aérea — mantêm a divisão:

- o transportador progride para `lzCell`;
- o passageiro progride para `passengerMeetingCell`;
- `Tools > Transporte > Caminhos Válidos > Progressão` continua sendo a fonte
  oficial para escolher o passo alcançável da rodada;
- o embarque final continua sendo confirmado pelo `Pode Embarcar`;
- passageiro terrestre sem nenhum encontro transitável rejeita a opção.

O planejamento não força o passageiro a ocupar a célula do veículo. O encontro
pode ser adjacente e respeita o domínio real de cada unidade.

## Ferramenta de estudo

A janela `Tools > Transporte > Melhor LZ de Embarque` foi reorganizada:

- `Usar Selecionado`: preenche somente o passageiro;
- `Usar como Transportador`: preenche somente o filtro de transportador;
- clicar numa unidade da Scene ou Hierarchy não altera mais os campos;
- `Auto Detect` continua podendo capturar a unidade do batch preparado pelo
  F11 como passageiro;
- o mesmo objeto não pode ocupar os dois campos;
- uma unidade sem configuração de transportador é recusada no segundo botão.

No jogo pausado, a ferramenta mostra o retrato atual. Com o jogo rodando,
compara:

- amarelo: ranking bruto da ferramenta;
- azul: escolha simulada pela política runtime.

A Scene View diferencia:

- `TRANSPORTADOR AGORA`;
- linha e seta de progressão;
- `LZ DESTINO`;
- `ENCONTRO PAX`.

Isso deixa claro que a LZ é uma referência futura e não a posição atual do
transportador.

## MP em estruturas e construções

O modelo de movimento passou a tratar explicitamente o par entre a superfície
visual do hex e a rede usada para atravessá-lo.

### Redes declaradas

`StructureData` agora identifica a rede:

- `None`;
- rodoviária;
- ferroviária.

Pontes, rodovias e trilhos declaram sua rede. Em cruzamentos, a aresta
percorrida escolhe qual representante conectado governa o movimento; a mera
presença de várias estruturas no mesmo nó não mistura permissões.

### Regras por terreno

Estruturas podem definir, por par `Estrutura + Terreno`:

- skills obrigatórias;
- skills bloqueadas;
- custo por skill;
- exigência de rota declarada;
- ativação, desativação ou herança do bônus rodoviário.

Construções também podem definir, por par `Construção + Terreno`:

- skills e bloqueios;
- overrides de custo;
- terrenos onde a estrutura conectada governa integralmente;
- terrenos onde o terreno base governa integralmente.

Isso permite que cidade, quartel, ponte, estrada e trilho mantenham sua função
de gameplay sem apagar o custo ou a restrição da superfície correta.

### Custo e bônus de estrada

`UnitMovementPathRules` resolve movimento e autonomia pela mesma decisão de
travessia. O bônus de estrada:

- é avaliado por aresta conectada;
- exige movimento terrestre em `Surface`;
- só concede o passo adicional quando o deslocamento-base foi inteiramente
  feito em rede rodoviária válida;
- não transforma trilho, ponte desconectada ou estrutura sobre outro terreno
  em estrada.

O fingerprint da topologia inclui rede, regras por terreno e configuração de
bônus. Mudanças nesses assets invalidam corretamente os caches derivados.

## Inspeção em Caminhos Válidos

`Tools > Transporte > Caminhos Válidos` ganhou inspeção do gasto de PM:

- clique numa célula alcançável;
- destaque do caminho escolhido;
- custo acumulado em cada passo;
- descrição da construção, estrutura e terreno;
- indicação explícita do bônus de estrada;
- resumo do total consumido contra o orçamento.

A ferramenta apenas apresenta os mesmos caminhos e mapas de custo produzidos
pelas regras runtime.

## Assets e conteúdo deste checkpoint

O snapshot também registra:

- nova `Ponte Rodoviária Baixa`;
- redes declaradas nas pontes, rodovias e trilhos;
- ajustes de custo e bloqueio em cidade, quartel e estruturas;
- substituição da skill legada `Motor Caminhão` por `Caminhoneiro`;
- atualização do Caminhão 18W;
- refinamentos de Stock, Air Combat, Assault e Progressão já presentes no
  workspace;
- atualização de `docs/quero_carona_refactor.md`;
- estado atual da cena de teste e dos assets de fonte serializados.

Esses arquivos entram no checkpoint porque a versão foi solicitada com
`git add .`.

## Contrato transacional

As consultas de LZ, encontro, custo e progressão são projeções do estado
confirmado:

- não movem unidades;
- não ocupam células;
- não embarcam passageiros;
- não alteram FOW, detecção ou inteligência;
- não consomem PM, combustível ou recursos;
- não modificam `HasActed`.

Somente o batch normal pode materializar movimento ou embarque, após a
confirmação explícita da ação e respeitando o retorno a `CursorState.Neutral`.

## Validação

- `Assembly-CSharp.csproj`: compilado com 0 erros;
- `Assembly-CSharp-Editor.csproj`: compilado com 0 erros;
- consulta passageiro–transportador: validada visualmente com Navio Transporte
  e Obus Leve;
- origem, LZ destino e encontro do passageiro: diferenciados na Scene View;
- validação final do diff executada antes do versionamento.
