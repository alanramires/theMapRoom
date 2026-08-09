# v8.2.0 — O plano dá o endereço; os rogues dividem o resto

Fechada em 2026-08-09. Antecessora: [`v8.1.2`](relatorio_v8.1.2.md).

---

## O fio do dia

O capturador tinha três respostas diferentes para uma pergunta que deveria ser
uma só:

```text
planner                 distribuía unidade + setor
Melhor Captura          escolhia uma construção
comportamento da unidade escolhia de novo quando chegava sua vez
```

Cada resposta era razoável isoladamente. Juntas, permitiam que carona,
transportador e movimento lessem destinos diferentes no mesmo turno. A correção
não foi dar mais autoridade a uma delas, e sim separar as populações:

```text
capturador COM plano    o planner publica a missão formal e seu endereço
capturador SEM plano    o Melhor Captura divide coletivamente o que sobrou
capturador SEM par      não ganha reserva, mas conserva um magnético
```

Essa separação vale para as duas formas de IA. Com HQ, `RogueUnitIds` entram no
matching residual. Sem HQ, não existe plano e todos são rogues. Não nasceu uma
segunda IA para facção rebelde.

O transporte encontrou a mesma forma um degrau adiante: promessa e escolha são
**faróis**, não propriedade. E quando um canal prova que o destino pertence a
outro componente de movimento, o prédio continua sendo o destino da missão, mas
deixa de ser uma âncora válida para o próximo passo. A âncora imediata passa a
ser o encontro terrestre da LZ.

> **O prédio responde onde quero chegar. A LZ responde onde preciso estar agora
> para ainda conseguir chegar lá.**

---

## Frente 1 — Praias ganharam identidade operacional

Uma praia não é um setor capturado e não pertence ao `SectorManager`. Ela é uma
linha natural costeira que precisa de semântica para transporte. Nasceu o
`BeachManager`, irmão natural do `RoadManager`: um interpreta uma linha
construída; o outro interpreta uma faixa de terreno.

As decisões que evitaram constantes escondidas foram:

- o terreno de praia vem do `TerrainTypeData` configurado no Inspector e chega
  ao tilemap pela `palette`; não existe nome hardcoded;
- componentes descontínuos nunca recebem a mesma identidade;
- a extensão é medida pela cadeia conectada, não por centroide;
- uma faixa recebe letras do alfabeto fonético militar e é repartida quando
  ultrapassa o comprimento máximo configurável, inicialmente seis hexes;
- `BeachRepCell` existe para log, Inspector e rótulo, mas não é âncora de
  encontro. Praia é banda; representante é uma célula;
- a visualização usa sobreposição e rótulo acima do desenho, com o mesmo
  espírito da bancada do Melhor Desembarque.

O `SectorManager` apenas consome o catálogo produzido pelo `BeachManager`; ele
não varre o tilemap de novo. Ambos passaram a ser resolvidos por cena e por
tilemap. Isso fecha o vazamento que apareceria numa campanha: avançar do mapa A
para o B não pode carregar praias, setores ou caches do mapa anterior.

Na mesma frente, o retorno para manutenção passou a preservar dois pontos de
movimento de unidades Land/Surface para embarque, salvo quando a reserva não é
necessária para alcançar a unidade ou construção supridora. A bancada do Melhor
Embarque também ganhou o botão de limpeza que faltava.

---

## Frente 2 — Melhor Embarque ganhou o eixo do transportador

A consulta antiga respondia somente ao passageiro: “onde devo esperar este
transportador, ou qualquer transportador compatível?”. Faltava a pergunta
reversa: “onde este transportador deve encontrar este conjunto de passageiros?”.

A ferramenta e o serviço agora possuem as duas perspectivas. No lado do
transportador, vários passageiros podem ser cadastrados para procurar um
manifesto conjunto. A agregação vale no Tactical, onde a interseção é uma ação
materializável agora. Pares Operational ou Strategic continuam 1:1: às vezes é
melhor buscar um passageiro, voar e buscar o outro do que congelar o veículo em
busca de uma interseção distante.

O manifesto respeita capacidade, slot e exclusividade. O passageiro continua
sendo a origem de seu destino; o transportador só recebe endereço de coleta e,
depois do embarque, herda a missão da carga.

---

## Frente 3 — Promessa virou coordenação sem lock

O primeiro desenho tentava impedir duplicação: se um transportador escolheu um
passageiro, os outros deveriam ficar longe. Isso resolvia três APCs indo ao mesmo
soldado, mas recriava a fome por outro caminho — passageiro parado esperando o
“dono” enquanto uma carona válida passava ao lado.

A regra final é deliberadamente distributiva e não impeditiva:

```text
há alternativas equivalentes   prefere passageiro ainda sem farol
só existe aquela demanda        vários transportadores podem convergir
um terceiro embarcou primeiro   promessa cumprida do mesmo jeito
```

Existem duas memórias do mesmo sinal: faróis provisórios da Fase 2 e a promessa
persistida no `Mission Intent` do casco. Transportadores agora consultam ambas.
A promessa vincula quem prometeu — ele deve continuar considerando a viagem —,
mas nunca transforma passageiro, vaga ou veículo em propriedade.

---

## Frente 4 — O Melhor Captura finalmente ganhou o eixo N

A bancada passou a aceitar várias unidades, adicionar a seleção somente quando
ela possui uma das skills exigidas por
`ConstructionData.requiredSkillsToCapture`, e cadastrar de uma vez todas as
unidades elegíveis do mesmo slot.

O solve coletivo recebe um ranking do Melhor Captura por sujeito e faz o
pareamento 1:1 uma vez. Ele usa fluxo máximo de custo mínimo:

1. maximiza quantos capturadores recebem construção;
2. nessa cardinalidade, minimiza o custo total;
3. cobra custo de troca 15 para preservar a missão anterior;
4. usa papel e identidade apenas como desempate estável.

Esse modelo resolve o caso que o guloso por iniciativa erra: dois capturadores
querem X, mas perder X custa muito mais para um deles. X vai para quem possui a
pior alternativa, não para quem teve a sorte de agir primeiro.

O solve mora no plano, não no laço da unidade. `BuildObjectivePlan` publica as
missões formais e depois resolve uma única fotografia dos rogues. Retomada de
plano salvo republica a mesma informação. `MissionIntent.Capture`, claim e
movimento passaram a ler o mesmo endereço.

### Sem par não significa sem comportamento

Com três capturadores e uma construção, um recebe o par. Os outros dois caem no
magnético da mesma construção, mesmo pertencendo ao claim alheio. Claim é
pareamento e endereço, não propriedade. Se a construção estiver além do
Operational para os três, os três pedem carona; somente um possui missão formal,
e o transportador continua livre para decidir quem atende.

Isso removeu o bloqueio incondicional de alvo “alheio” e também o segundo
mecanismo de reserva que procurava donos em designações individuais. O serviço
coletivo é o único escritor do claim residual.

---

## Frente 5 — A única exceção à marcha magnética burra

O log revelou uma contradição concreta: `QueroCarona=SIM`, “SEM ROTA PRÓPRIA”,
seguido por uma marcha cúbica em direção ao prédio do outro lado do canal. O
sistema provava que não havia caminho e, logo depois, fingia que aproximação
geométrica era progresso.

Quando o embarque não pode ser materializado no turno, o capturador agora chama
o Melhor Embarque na perspectiva do passageiro, sem impor transportador. Uma
promessa persistida favorece o casco prometido dentro da mesma banda, mas uma
solução Tactical de outro casco ainda vence uma promessa Operational ou
Strategic.

O destino de movimento é `passengerMeetingCell`, o lado terrestre do encontro,
nunca `lzCell`, que pode ser água. Se já estiver no encontro, espera. Se estiver
estruturalmente sem rota e nenhum encontro puder ser materializado, também
espera em vez de voltar à marcha impossível.

Essa é uma exceção estreita. Distância grande não suspende o magnético; ruptura
topológica suspende. O canal não é “muito longe”: é ausência de rota.

---

## Frente do autor — O cenário ficou pequeno o bastante para dizer a verdade

O Hot Seat 0 foi reduzido e remontado como bancada de captura e transporte:
quatro soldados, Chinook, navio de transporte, construções disputadas e o
`BeachManager` persistido sob o `SectorManager`. O catálogo acompanha as novas
identidades e posições. O preset Difícil passou a limitar logística e o Battle
Map 1 recebeu explicitamente seu `basePreset`.

Essa redução importa porque o comportamento coletivo agora aparece no log sem
ser encoberto por dezenas de unidades: pares de captura saem no Stage 1;
promessas nascem na vez dos transportadores; sobras magnéticas e pedidos de
carona ficam distinguíveis.

---

## O que não terminou

- **A aproximação do capturador à LZ e a leitura das promessas persistidas ainda
  não foram vistas em jogo depois da última alteração.** Runtime e Editor
  compilam com zero erros; falta repetir o caso do soldado diante do canal e
  conferir `encontroPax`, `LZTransport` e `promessa=sim/não` no log.
- **O critério completo do EVAC naval ainda não fechou.** As peças — praias,
  perspectivas do Melhor Embarque, manifestos Tactical, promessa e âncora de
  encontro — existem, mas a viagem de ida e volta com transporte aninhado ainda
  precisa ser exercitada como cenário único.
- **Melhor Embarque e Melhor Desembarque ainda não usam diretamente as praias
  nomeadas.** O `SectorManager` já expõe o catálogo por cena/tilemap; conectar o
  ranking de LZ a essas faixas é a próxima camada, não motivo para o
  `BeachManager` recalcular decisões.
- **O custo runtime da consulta passageiro-cêntrica com todos os transportadores
  e Strategic habilitado não foi medido.** O serviço é o mesmo da ferramenta e
  produz a resposta correta; falta conferir o frame do cenário maior.
- **O preset e o cenário do autor foram verificados no diff, não jogados por
  esta sessão.** `limitarLogistica=true` e o `basePreset` foram confirmados como
  intencionais pelo autor.

---

## Validação

```text
dotnet build Assembly-CSharp.csproj --no-restore         0 erros
dotnet build Assembly-CSharp-Editor.csproj --no-restore  0 erros
git diff --check nos arquivos da implementação           limpo
```

Os avisos são os já existentes de APIs obsoletas da Unity e do analisador de
serialização. A bancada do Melhor Desembarque continua com um aviso antigo de
código inalcançável; esta versão não o introduziu.
