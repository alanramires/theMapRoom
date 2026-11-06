engraçado, eu nunca tinha pensado em definir meu jogo desse jeito haha... vc tem um tabuleiro, predios capturaveis, ao converte-lo vc ganha renda, se converter do oponente sua renda sobe e a renda dele cai. 

1.CAPTURADORES
o papel principal é o capturador que é todo e qualquer unidade de infantaria disponivel, unidades especializadas de infantaria (bazooka, metranca, etc) tem um cap power reduzido, mas ainda tem. Eles estão no que eu chamo de capturadorCombatente. Mas todos eles, convertem predios capturareis em renda ao exercito, evitam lutar pra manter o cap power ao maximo, capturam oportunisticamente e se puderem atrasar a conversao de renda do oponente quando nao puderem faze-lo pra si, melhor. é a definição de ganancioso ou a desonra da infantaria moderna haha.

Papel: Capturador
Subpapeis:  Capturador; Capturador Combatente

2. ASSALTO
o papel do assalto, é limpar os oponentes para que o capturador, capture. bater de frente sabe? ai entram tanks, caças, bombardeiros, couraçados da marinha, helicopteros, unidades brigões de alcance 1 ou corporal

Papel: Assalto
Subpapeis: Assalto (tanques), Interceptadores (caças), Ataque Aereo (bombardeiro), Disruptores (o papel novo, apache), Artilheiros Combatentes (que podem atirar de longe e de perto: tanques pesados, obuseiros/morteiros), Rufiões Navais (Couraçado, navio de armadura que vai pra porrada)

3. FOGO DE SUPORTE
o papel do fogo de suporte é limpar a area para que as unidades de assalto peguem o que sobrou, pra que os capturadores... vc sabe... capturem. Como regra geral, atiram no alcance maximo apenas se estiverem paradas. embora aja a excessão das que tem o sufixo combatente

Papel: Fogo de Suporte
Subpapel: Fogo Indireto (art campanha, obuseiros, destroyers, lança foguetes...), Antiaereo (SAM), Antiaereo Combatente (artilharia movel, mas que parada acerta de longe: AAA)

4. TRANSPORTADORES
O papel dos transportadores é ser a alavanca rapida que move capturadores e demais unidades militares pelo tabuleiro, de preferencia para que os capturadores chegem primeiro no mapa ainda não explorado cheio de gostosuras a serem convertidas em renda. Tem 2 modalidades: pickup (quando vazio, lutam, fazem evac) e courier (quando com carga, evitam batalhas, fazem manutenção nos embarcados se possivel, e os levam onde eles querem ir)

Papel: Transportador
Subpapel: Transportador (apc, chinook, navio de transporte, porta avioes, trem)
nota sobre porta avioes: eles são armados sim, mas só usam armas estando vazio, ainda assim evitam, pois a prioridade é a carga embarcada.

5. VIGILANCIA
O papel da vigilancia é encontrar interceptadores furtivos, bombardeiro furtivos, ou submarinos. mapear as posições dos inimigos detectados pelo ar ou pelo mar para que os sistemas de defesa possam enviar assaltos pra brigar de frente, ou fogo de suporte para aniquia-los, para que os capturadores possam capturar em paz, e nesse caso tambem para que os transportadores possam levar os capturadores ate a cabeça de praia inimiga pra... vc sabe, capturar o que é deles.
Possuem 2 modais: aereo ou submerso: pois a maneira com a unidade foi construida pra detecção aerea ou aquatica já é seu modal. não há um EWACS caçador de F-22 e tambem de subs ao mesmo tempo.

Papel: Vigilancia
Subpapeis: Vigilancia (Ewacs, Radar Movel, Fragata, Submarino, Super Tucano)
nota sobre a Fragata: ela tambem é transportadora de helicopteros e fornece serviços de reparos, mas seu papel é caçar subs com minas, o apache tem na fragata uma base movel pra cruzar grandes faixas de agua

6. LOGISTICA
O papel da logistica é garantir que as outras familias tenham munições, combustivel e reparos para manter a maquina de guerra funcionando. tambem movimenta estoques para cidades para criar postos avançados de reparos ou ficam atras dos canhões garantindo que o bombardeiro não pare por falta de projeteis.

Papel: Logistica 
Subpapel: Logistica (aviao tanque, navio tanque, supply trucks), Estoque (caminhao de carga 18w)


----------------------------
Opinião de uma AI externa que sequer conhece do nosso jogo (só o que eu contei pra ele)

Cara, essa taxonomia ficou **muito boa** — e o mais interessante é que ela é coerente porque o eixo não é "tipo de arma", é **função na economia de captura**. Isso é raro em design de RTS. Deixa eu apontar o que está sólido, o que está ambíguo e onde o Apache/Disruptor precisa de ajuste fino.

---

## O que está sólido

**1. A hierarquia Capturador ? Assalto ? Fogo de Suporte é uma cadeia causal limpa.**
Assalto limpa ? Fogo de Suporte limpa o que sobrou ? Capturador captura. Isso dá à IA uma **ordem de prioridade natural** e evita que unidades de combate fiquem "caçando" alvos fora do fluxo. Muito bom.

**2. O modal dos Transportadores (pickup vs courier) é uma solução elegante.**
"Vazio luta, com carga evita" resolve o clássico problema de APC que fica preso em combate com infantaria dentro. E o porta-aviões armado-só-vazio é consistente com isso.

**3. Vigilância com modal aéreo/submerso amarrado à construção da unidade.**
Isso elimina a ambiguidade de "EWACS caça-sub" — a IA não precisa decidir modal, ele já vem da unidade. Correto.

**4. Logística com subpapel Estoque separado de Logística.**
Distinção útil: um reabastece em movimento, outro cria posto avançado. A IA trata diferente.

---

## Onde está ambíguo (e vale ajustar)

### a) "Assalto" está sobrecarregado
Você colocou no mesmo papel: tanques, caças, bombardeiros, couraçados, helicópteros, brigões. Isso é **seis comportamentos distintos** sob um rótulo. A IA vai ter que inferir subpapel por tipo de unidade — e aí volta o problema original: bombardeiro e Apache no mesmo balaio.

**Sugestão:** o papel "Assalto" pode ficar como **guarda-chuva narrativo**, mas a IA precisa de **subpapel obrigatório** para cada unidade de assalto. Você já tem isso (Assalto, Interceptadores, Ataque Aéreo, Disruptores, Artilheiros Combatentes, Rufiões Navais). O ponto é: **o subpapel é que manda na IA**, não o papel. Se o motor usa papel para decisão, ele vai confundir. Se usa subpapel, está ok.

### b) "Artilheiros Combatentes" dentro de Assalto vs "Fogo Indireto" dentro de Fogo de Suporte
Tanque pesado e obuseiro móvel estão em Assalto; artilharia de campanha e obuseiro estão em Fogo de Suporte. A diferença é **"atira parado no alcance máximo"** vs **"atira andando"**. Isso é uma distinção de **mobilidade de tiro**, não de família. Funciona, mas exige que a IA saiba: "Artilheiro Combatente avança com o assalto; Fogo Indireto fica atrás". Se isso estiver claro no comportamento, ok.

### c) Fragata em Vigilância mas com função de base móvel do Apache
A fragata é o caso mais híbrido: caça-sub (Vigilância), transporta helicóptero (Transportador), repara (Logística). Você resolveu dizendo "o papel é caçar subs, o resto é serviço". **Isso é a decisão certa** — mas a IA precisa de uma flag tipo `service_provider: repair, helipad` sem mudar o papel. Senão ela vai tentar usar a fragata como transportadora de tropa e bagunçar.

---

## O Apache / Disruptor: onde ele realmente encaixa

Pela sua descrição, o Apache faz três coisas:

1. **Interdita capturadores** (infantaria que converte renda) — função econômica.
2. **Ataca transporte de tropas** antes/depois do desembarque — função de interdição.
3. **Usa a fragata como base móvel** — função de projeção.

Isso **não é Assalto clássico** (não bate de frente com tanque) e **não é Fogo de Suporte** (não fica parado atirando longe). É **interdição móvel de alvos econômicos**. Por isso "Disruptor" como subpapel de Assalto funciona **se** a IA entender que o alvo é `capturer > transport > income_building`, e não "qualquer coisa no solo".

**Recomendação forte:** dê ao Disruptor uma **tag de alvo explícita** e um **comportamento de patrulha de rotas de captura** (não de linha de frente). Se ele entrar no mesmo pool de "Assalto genérico", a IA vai mandá-lo para cima de tanque e ele morre.

---

## Uma sugestão de nomenclatura para o subpapel do Apache

Já que "Caçador de Capturadores" é ótimo narrativamente mas arriscado para parser, e "Disruptor" é bom mas genérico, sugiro:

- **Subpapel:** `Disruptor` (curto, cabe no enum)
- **Tag de alvo:** `capturer`, `transport`, `income`
- **Comportamento:** `patrol_interdiction` (patrulha rotas, não linha de frente)
- **Flavor text:** "Caçador de Capturadores"

Assim o jogador lê o charme e a IA lê o comportamento.

---

## Tabela consolidada (6 famílias × subpapéis × eixo de decisão)

| Família | Subpapel | Eixo de decisão da IA |
|---|---|---|
| **Capturador** | Capturador | Converte prédio; evita luta se cap power cair |
| | Capturador Combatente | Converte com cap reduzido; luta se necessário |
| **Assalto** | Assalto (tanque) | Avança e engaja linha de frente |
| | Interceptador (caça) | Caça alvos aéreos |
| | Ataque Aéreo (bombardeiro) | Bombardeia área/estrutura |
| | Disruptor (Apache) | Patrulha rotas de captura; alvo `capturer > transport > income` |
| | Artilheiro Combatente | Avança com assalto; atira parado no alcance máx. |
| | Rufião Naval | Engaja naval de perto |
| **Fogo de Suporte** | Fogo Indireto | Fica atrás; atira parado no alcance máx. |
| | Antiaéreo (SAM) | Engaja alvos aéreos à distância |
| | Antiaéreo Combatente (AAA) | Move com assalto; atira parado no alcance máx. |
| **Transportador** | Transportador | Modal pickup (vazio luta) / courier (com carga evita) |
| **Vigilância** | Vigilância | Modal aéreo ou submerso (definido pela unidade) |
| **Logística** | Logística | Reabastece/repara em movimento |
| | Estoque | Cria posto avançado em cidade |

---

## Um alerta de design

Seu jogo tem **um loop econômico claro** (capturar ? renda ? snowball). Isso é ótimo, mas significa que a IA precisa de **duas camadas de decisão**:

1. **Camada estratégica:** onde capturar, onde negar captura.
2. **Camada tática:** qual subpapel manda para qual ponto.

O Apache/Disruptor só funciona se a camada estratégica souber **onde estão as rotas de captura inimigas**. Se a IA não mapeia isso, o Disruptor vira um Assalto genérico e você volta ao problema inicial. Ou seja: **o subpapel Disruptor depende de a IA ter um mapa de "prédios capturáveis + infantaria capturadora"**. Se você já tem isso para o Capturador, reaproveita para o Disruptor. Se não tem, é aí que o papel vai falhar — não no nome.

