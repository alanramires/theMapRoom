# DETECÇÃO VS CAÇADA — O JOGO DE GATO E RATO MILITAR
### *Manual de Doutrina Operacional — Versão Consolidada*

---

## 1. ESCALA E PRINCÍPIO FUNDAMENTAL

- **1 hex representa centenas de quilômetros.**
- O tabuleiro representa setores operacionais, não posições físicas exatas.
- Duas unidades no mesmo hex podem estar separadas por grande distância, altitude, direção ou assinatura.
- **Range 0 não possui combate.**
- O jogo é uma disputa de informação: mover-se sem saber quem observa o setor é uma das maiores apostas da partida.

A vitória não pertence apenas a quem possui a melhor arma, mas a quem administra melhor:

- observação;
- detecção;
- ocultação;
- alcance;
- momento do ataque;
- posição dos sensores.

---

## 2. ASSIMETRIA DE INFORMAÇÃO

Quando uma unidade comum é detectada, seu jogador **não recebe aviso automático**.

Isso vale para unidades como:

- Caça A;
- EWACS;
- Fragata;
- Destroyer;
- Radar Móvel;
- Super Tucano.

Essas unidades podem estar sendo rastreadas sem que seu dono saiba.

Unidades com skill de ocultação recebem tratamento diferente.

### O indicador de detecção — “Olho”

Quando uma unidade com Stealth é detectada por um inimigo, surge um **Olho** sobre seu token.

O jogador descobre apenas que foi visto.

Ele não descobre:

- qual unidade o detectou;
- de qual direção veio a detecção;
- se foi um EWACS, Radar Móvel ou outro sensor;
- quantas unidades inimigas agora conhecem sua posição.

O Olho representa informação parcial, não transparência total.

---

## 3. ORDEM DO TURNO — O XADREZ DAS TREVAS

O fluxo operacional segue uma ordem rígida:

1. **Movimento — comprometimento**  
   O jogador move a unidade dentro do Fog of War sem receber atualização contínua durante o trajeto.

2. **Confirmação da posição**  
   A unidade termina o deslocamento no hex escolhido.

3. **Recálculo de visão e detecção**  
   Somente depois do comprometimento o jogo atualiza LoS, alcance de sensores, Stealth e contatos revelados.

4. **Declaração de ação**  
   Com base no novo estado do tabuleiro, o jogador decide se ataca, captura, embarca, pousa, supre ou encerra a ação.

5. **Resolução do combate**  
   O ataque é resolvido como duelo entre atacante e defensor.

6. **Exposição pós-disparo**  
   Unidades furtivas podem perder temporariamente sua ocultação ou mudar de camada.

Essa ordem impede a exploração gratuita do mapa casa por casa.

> O jogador não move para descobrir e depois decide se aceita a posição.  
> Ele aceita a posição e só então descobre o que havia além dela.

---

## 4. DOMÍNIOS E CAMADAS OPERACIONAIS

O jogo usa quatro níveis operacionais principais.

### Domínio 1 — Air/High

Onde operam:

- EWACS;
- Caça A;
- Caça F;
- Bombardeiro;
- Bombardeiro Furtivo.

Características:

- EV 4;
- posição Melhorada;
- bônus de defesa de altitude;
- Stealth aéreo funciona nesta camada;
- **observação ar contra ar-alto é por alcance puro** — sem Linha de Visão.

Air/High oferece vantagem de observação, mas não onisciência.

A dispensa de LoS vale apenas para o par aeronave contra Air/High. A mesma aeronave, olhando para Air/Low, Land/Surface ou Naval/Surface, volta a depender de EV, relevo e angulação.

Ela enxerga o céu inteiro dentro do alcance, mas continua com sombras de terreno sobre o solo.

Sensores de superfície não recebem essa dispensa. O Radar Móvel, por ser terrestre, aplica LoS inclusive contra Air/High — e é isso que torna sua cobertura recortada enquanto a do EWACS é limpa.

### Domínio 2 — Air/Low

Onde operam:

- helicópteros;
- Super Tucano;
- aeronaves recém-decoladas;
- aeronaves em transição;
- aeronaves operando a partir de pistas improvisadas.

Características:

- Stealth aéreo não funciona;
- relevo influencia fortemente a LoS;
- montanhas e florestas podem bloquear observação;
- menor vantagem defensiva que Air/High.

### Domínio 3 — Surface

Inclui:

- Land/Surface;
- Naval/Surface.

É o domínio de:

- tropas terrestres;
- blindados;
- artilharia;
- veículos;
- navios de superfície;
- construções e estruturas.

Terrestre e naval ocupam a mesma altura operacional, mas continuam separados por compatibilidade de terreno, arma e domínio.

### Domínio 4 — Submarine/Submerged

É a camada dos submarinos submersos.

Características:

- ocultação exclusiva de domínio;
- operação abaixo da superfície naval;
- sonar especializado;
- movimentação e perseguição mais lentas;
- exposição prolongada quando forçado a emergir.

---

## 5. SKILLS EXCLUSIVAS DE DOMÍNIO

Skills de ocultação só funcionam na camada para a qual foram configuradas.

### Stealth aéreo

Caça F e Bombardeiro Furtivo possuem Stealth apenas em **Air/High**.

Ao mover para:

- Air/Low;
- Land/Surface;
- qualquer outra camada não configurada;

a skill deixa de produzir ocultação.

A unidade continua tendo a skill em seus dados, mas ela não é aplicada fora do domínio válido.

### Submarine Operations

O submarino possui ocultação apenas em **Submarine/Submerged**.

Ao emergir para Naval/Surface:

- a skill deixa de funcionar;
- o submarino torna-se visível;
- pode ser alvejado por armas compatíveis com a nova camada.

A revelação não é uma exceção separada: ela ocorre porque a unidade abandonou o domínio onde sua skill funcionava.

---

## 6. LINHA DE VISÃO E ELEVAÇÃO

Todas as camadas usam a mesma lógica geométrica de Linha de Visão.

A altitude altera o EV da origem e do alvo, mas não elimina obstáculos.

### Princípio

- planícies e mar possuem EV baixo;
- florestas elevam e bloqueiam;
- montanhas e falésias criam sombras de visão;
- Air/Low e Air/High possuem EV elevado;
- o ângulo entre origem, obstáculo e alvo determina o bloqueio.

### Air/High

Air/High possui EV 4.

Esse EV 4 é usado normalmente quando a aeronave olha para baixo. A linha desce de 4 até o solo e **pode ser barrada** por uma montanha no meio do caminho — é por isso que os hexes atrás de uma serra continuam escuros no Fog of War de terreno, mesmo com o caça em alta altitude.

O que escapa da geometria é outro plano: **alvos que estão em Air/High**.

Para um observador aéreo olhando o próprio plano alto, a Linha de Visão é dispensada por completo. Vale o alcance, e nada mais. Um caça não perde contato aéreo por causa de montanha, floresta ou falésia.

### As duas leituras não coincidem

Na mesma linha, atrás da mesma montanha:

- o **solo** fica escuro — a LoS foi aplicada e barrada;
- um **caça inimigo em Air/High** é detectado — a LoS não foi consultada.

A serra sombreia o chão e não sombreia o céu. É uma assimetria deliberada do motor, não um efeito colateral: contra alvo na mesma altitude, o relevo não entra na conta.

O EV 4 volta a morder assim que a aeronave olha para baixo — Air/Low, Land/Surface, Naval/Surface.

### Radar Móvel

O Radar Móvel possui bom alcance nominal, mas sua cobertura real depende de:

- relevo;
- EV;
- angulação;
- montanhas;
- florestas;
- posição do próprio radar.

Por ser uma unidade terrestre, ele não recebe a dispensa de LoS do domínio aéreo: aplica geometria inclusive contra Air/High.

Por isso, seu raio de detecção não deve ser interpretado como um círculo perfeito.

### EWACS

A vantagem do EWACS sobre o Radar Móvel não é alcance. É a natureza da cobertura.

O Radar enxerga o céu recortado pelo terreno à sua volta. O EWACS, por ser aéreo, enxerga o céu inteiro dentro do alcance — sem sombra, sem ângulo morto, sem depender de onde foi posicionado.

Na prática:

- figura aérea contínua, não fragmentada;
- acompanha a ofensiva sem perder qualidade de cobertura;
- reposiciona a bolha de detecção rapidamente;
- não precisa de terreno alto para render o alcance nominal.

O Radar Móvel é um sensor de posição. O EWACS é um sensor de presença.

---

## 7. MATRIZ DE VISÃO E DETECÇÃO

| Unidade | Air/High | Air/Low | Naval/Surface | Land/Surface | Submerged | Detecta Stealth | Recebe Olho |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **EWACS** — Mv 7 | **7** | **7** | 3 | 3 | 3 | Aéreo | Não |
| **Radar Móvel** — Mv 4 | **6** | 4 | 3 | 3 | **0** | Aéreo | Não |
| **Caça A** — Mv 9 | 4 | 4 | 4 | 4 | 4 | Não | Não |
| **Caça F** — Mv 7 | 4 | 4 | 4 | 4 | 4 | Não | Sim |
| **Bombardeiro Furtivo** — Mv 6 | 3 | 3 | 3 | 3 | 3 | Não | Sim |
| **Super Tucano** — Mv 6 | 4 | 4 | 4 | 4 | **5** | Submerso | Não |
| **Fragata ASW** — Mv 5 | 3 | 3 | 3 | 3 | **6** | Submerso | Não |
| **Destroyer** — Mv 5 | 4 | 4 | 4 | 4 | 4 | Não | Não |
| **Submarino** — Mv 4 | 1 | 1 | **5** | 3 | **7** | Submerso | Sim |

### Como ler a matriz

**Não existe "não enxerga" implícito.** O motor resolve a visão por camada assim: se a unidade tem uma especialização para aquele par domínio/altura, usa o valor dela; senão, **cai na visão base** (mínimo 1). Cegueira só existe quando alguém escreveu `0` de propósito.

Mas a base **não vaza para Submerged na prática**, e vale entender por quê — são dois caminhos diferentes.

**Lendo hexes (Fog of War):** a camada-alvo vem do **terreno**, e mar resolve como `Naval/Surface` (Submerged é modo adicional do hex, não o principal). Uma unidade sem especialização submarina nunca chega a aplicar alcance submerso ao revelar terreno. Para o EWACS, os 3 são superfície naval e terrestre — ponto.

**Detectando unidades:** aí a camada vem do **alvo real**. Um submarino submerso resolve como `Submarine/Submerged`, e sem especialização o alcance cai na base.

Só que isso esbarra na skill. O submarino submerso é alvo furtivo, e `CanDetectStealthFor` exige que o observador tenha especialização **naquela camada** com a skill ASW listada. O EWACS não tem. Ele não vê.

Sobra exatamente um caso: submarino ainda em Submerged com o stealth suprimido — disparou neste turno, ou está com lock de camada pendente. Aí o alcance base alcança e o EWACS o registra. O que é justo: ele já se entregou.

**ASW é território de Fragata e Super Tucano por causa da skill, não do alcance.** Zerar Submerged em quem não é ASW não muda nada — inclusive o `0` do Radar Móvel é decorativo.

O terreno filtra o resto: um hex de terra não aceita camada Naval, então alcance naval sobre terra não produz nada. Mas isso é o terreno barrando, não o sensor.

### Observações

- EWACS e Radar Móvel são os detectores especializados de Stealth aéreo.
- Fragata e Super Tucano são os principais detectores ASW — e o próprio Submarino detecta outros submarinos.
- Caças comuns e furtivos não detectam Stealth automaticamente.
- Estar adjacente ou no mesmo hex não revela uma unidade furtiva.
- Range 0 não gera contato automático nem combate.
- A coluna "Recebe Olho" identifica quem **possui** skill de ocultação — não quem está com ela ativa. O Olho acende sempre que essa unidade for detectada, em qualquer camada.
- A camada é onde a skill foi feita para funcionar, não uma condição do aviso. Caça F em Air/High visto por um EWACS, em Air/Low pego por um SAM, ou pousado e avistado por um soldado: os três acendem o Olho.
- O que não acende é a ausência de detecção: Caça F em Air/High dentro do alcance de um SAM — que não detecta stealth aéreo — segue sem indicador. É essa diferença que o jogador lê.

---

## 8. COMBATE, RANGE E REVIDE

Todo combate é um duelo entre:

- um atacante;
- um defensor.

Não existem:

- ataques de oportunidade;
- revide coletivo;
- interceptação automática de unidades próximas;
- fogo defensivo em cadeia;
- múltiplos defensores reagindo ao mesmo ataque.

### Range 0

- Não há combate.
- Duas unidades inimigas podem ocupar o mesmo setor sem contato.
- Um caça comum pode estar no mesmo hex que um caça furtivo sem detectá-lo.
- O hex representa uma área ampla demais para equivaler a contato visual direto.

### Range 1

É a distância de combate próximo.

O revide ocorre se:

- o defensor possuir arma válida;
- a arma puder atingir o domínio do atacante;
- houver munição;
- a distância estiver dentro do alcance da arma de revide.

### Range 2 ou superior

Ataques à distância não geram revide imediato, salvo regra específica da arma.

O alvo sobrevivente poderá reagir em seu próprio turno se ainda possuir:

- alcance;
- detecção;
- linha de tiro;
- munição;
- posição válida.

---

## 9. INICIATIVA, NÃO “PRIMEIRO DISPARO”

Unidades furtivas não recebem um ataque gratuito.

Quando uma unidade Stealth escolhe iniciar o combate:

- ela se torna o atacante;
- recebe o benefício de iniciativa previsto pelo DPQ;
- puxa o arredondamento do confronto para o lado atacante;
- sofre revide normal se o defensor possuir arma válida.

A expressão “atirar primeiro” deve ser entendida como:

> escolher o momento do duelo e entrar como atacante.

Não significa eliminar o alvo antes que ele responda.

Entre caças, o combate continua sendo uma troca simultânea dentro da resolução.

---

## 10. STEALTH AÉREO PÓS-DISPARO

Caça F e Bombardeiro Furtivo permanecem em Air/High após atacar.

Eles não mudam de camada.

Sua ocultação é ignorada temporariamente porque a unidade disparou.

### Regra

- a unidade recebe a flag **fired this turn**;
- enquanto a flag estiver ativa, o Stealth é ignorado;
- a unidade fica detectável por **1 rodada**;
- terminado o período, o Stealth volta automaticamente se a unidade continuar em Air/High.

A exposição curta representa:

- alta velocidade;
- rápida mudança de setor;
- redução rápida de assinatura após o lançamento;
- capacidade de desaparecer antes que forças lentas cerquem a área.

### Consequência

O Caça F ou Bombardeiro Furtivo:

- pode iniciar o duelo;
- revela sua posição ao disparar;
- permanece vulnerável durante a janela de punição;
- volta a ficar oculto depois, se não estiver sob detecção ativa de EWACS ou Radar.

---

## 10.1 DESATIVAÇÃO DO ELITE POR DETECÇÃO — O CAÇA F

**Regra nova (2026-08-06).** A furtividade do Caça F deixa de ser propriedade e
passa a ser **estado**. Ela já tinha duas saídas — mudar de camada (§5) e disparar
(§10) — e ganha a terceira: **ser detectado**.

### Os três estados

| estado | condição | o que ele é |
|---|---|---|
| **OCULTO** | em Air/High, não detectado, não disparou | Caça F pleno: camuflagem + **Elite 2** |
| **COMUM** | **detectado** por sensor inimigo | vira um caça comum, do nível do Caça A. **Perde o Elite 2** |
| **EXPOSTO** | disparou neste turno | detectável por 1 rodada (§10) |

### A ordem é tudo

Se ele **ataca primeiro**, partindo de OCULTO, o disparo usa **a camuflagem e o
Elite 2** — e só depois ele vira EXPOSTO. Ser detectado **antes** de atacar tira o
bônus: ele entra no duelo como um caça qualquer.

```text
OCULTO -> ataca      usa camuflagem + Elite 2, depois EXPOSTO por 1 rodada
detectado -> ataca   ataca como Caça comum, sem o Elite 2
```

Isso transforma o Caça F num duelista de **uma pancada e saída**: o valor dele
não está em trocar tiros, está em **escolher o instante do primeiro tiro**. Um
EWACS ou Radar Móvel que o encontre antes não o mata — rebaixa.

### Por que o Bombardeiro Furtivo NÃO segue esta regra

O Bombardeiro Furtivo Elite 2 **mantém o nível mesmo detectado**. Não é exceção
arbitrária — as duas furtividades servem a coisas diferentes:

```text
Caça F        furtividade TATICA       ela existe para escolher o duelo.
                                       Visto, a emboscada acabou: nao ha o que
                                       o Elite 2 pague

Bombardeiro   furtividade ESTRATEGICA  ela existe para CHEGAR. Visto, a carga
                                       nao encolhe, o alvo nao se move e o
                                       trabalho continua o mesmo
```

Ser visto arruina uma emboscada; não arruína um bombardeio.

---

## 11. SUBMARINO PÓS-DISPARO

O submarino não apenas perde uma flag.

Ele muda de camada.

### Dois gatilhos, a mesma janela

A emersão forçada tem duas causas distintas, e ambas prendem o submarino na superfície por **2 rodadas**:

1. **Atacar.** O gatilho é a própria ação de disparar, não o resultado do combate. Acertando ou errando, ele sai de Submarine/Submerged e passa para Naval/Surface.
2. **Ser atingido** por arma configurada para forçar camada (Layer Force After Hit).

As duas janelas são iguais de propósito. Enquanto atacar custava 1 rodada e ser pego custava 2, **disparar era mais seguro do que ser caçado** — e a arma de ASW punia menos que o próprio ataque do submarino.

Ao emergir, por qualquer das causas:

- deixa de receber o benefício de Submarine Operations;
- torna-se visível pelas regras normais da superfície;
- só volta a mergulhar quando o lock expira e ele se move de novo.

Se a emersão for ilegal na célula — um navio já ocupando a superfície do mesmo hex — o ataque é barrado ainda na mira. Num revide, onde não há mira para barrar, o lock fica **pendente**: a unidade já conta como revelada, mas a camada só muda quando a superfície liberar.

### Exposição prolongada

Submarinos e navios são lentos.

Por isso, a janela de punição é maior:

- Stealth aéreo: 1 rodada;
- submarino: 2 rodadas.

A duração maior representa:

- ruído;
- cavitação;
- deslocamento lento;
- dificuldade de mergulhar novamente;
- perseguição ASW sustentada.

### O problema do alcance

A punição existe no papel. No tabuleiro, ela frequentemente não alcança.

- Torpedo do **Submarino**: alcance 1–3.
- Carga de Profundidade da **Fragata ASW**: alcance 1–2.
- Torpedo do **Super Tucano**: alcance 1.

O submarino dispara a 3 hexes, emerge, e passa suas 2 rodadas de exposição **fora do alcance das duas unidades construídas para caçá-lo**. E como o revide só ocorre a distância 1, o alvo original também não responde no momento do disparo.

O que fecha essa janela não é poder de fogo, é mobilidade: Fragata (Mv 5) e Super Tucano (Mv 6) gastam o intervalo fechando distância, não atirando. Contra um submarino que dispara e recua, a caçada depende de **já estar perto quando o torpedo sai**.

Uma escolta ASW que reage à emersão chega tarde. Uma que patrulha o corredor antes do disparo chega no tempo.

### Revide

Somente o defensor direto pode revidar durante a resolução do ataque.

Outras unidades próximas não atacam automaticamente.

Elas poderão atacar o submarino revelado em seus próprios turnos — se tiverem alcance para isso.

---

## 12. TRANSIÇÃO DE CAMADA E LANÇAMENTO

### Porta-aviões e pistas

Ao decolar:

- a aeronave entra primeiro em Air/Low;
- Stealth aéreo não funciona nessa camada;
- a unidade pode ficar exposta durante o lançamento.

### Movimento de decolagem

- aeronaves normais: deslocamento limitado no turno de decolagem;
- VTOL/SVTOL: podem usar movimento ampliado conforme suas regras;
- enquanto permanecer em Air/Low, o Stealth continua inativo.

A vulnerabilidade no lançamento é intencional.

O jogador deve proteger:

- porta-aviões;
- aeroportos;
- pistas improvisadas;
- corredores de subida.

---

## 13. PAPEL DE CADA UNIDADE NO META

### EWACS — O Olho do Rei

- maior cobertura aérea;
- detector móvel de Stealth;
- acompanha ofensivas;
- depende de proteção;
- caro e vulnerável se interceptado.

### Radar Móvel — A Sentinela

- detector terrestre de Stealth;
- mais barato;
- alcance nominal alto;
- cobertura recortada por LoS e relevo;
- extremamente dependente de posicionamento.

### Caça A — O Interceptador Supremo

- movimento 9;
- melhor perseguidor;
- mesma arma principal contra bombardeiros que o Caça F;
- não recebe Olho;
- pode estar sendo rastreado sem saber;
- depende de sensores para caçar Stealth.

### Caça F — O Predador Furtivo

- movimento 7;
- Elite 2;
- Stealth em Air/High;
- míssil de alcance 1–2;
- não substitui o Caça A em perseguição;
- escolhe quando iniciar o duelo;
- fica exposto por 1 rodada após disparar.

### Bombardeiro Furtivo — O Fantasma Estratégico

- movimento 6;
- Stealth em Air/High;
- três bombas;
- alcance 1–2;
- grande autonomia;
- infiltração e ataque profundo;
- vulnerável quando detectado;
- não deve enfrentar caças diretamente.

### Super Tucano — O Carrasco de Submarinos

- movimento 6;
- detecção submersa por sonobóia;
- torpedo leve;
- alcança e persegue submarinos mais lentos;
- não recebe Olho.

### Fragata ASW — O Escudo da Frota

- detecção submersa;
- protege navios de superfície;
- cobre corredores marítimos;
- transforma submarinos revelados em alvos perseguíveis.

### Destroyer — O Brigão

- movimento 5;
- Cruise 2–4 e Deck Gun 2–3;
- grande alcance;
- pressão contra navios e costa;
- depende de spotter;
- não possui ASW;
- **não possui nenhuma arma de alcance 1** — logo, nunca pode revidar;
- vulnerável à aproximação de submarinos não por tendência, mas por impossibilidade mecânica.

### Submarino — O Predador Silencioso

- ocultação em Submerged;
- alto alcance de detecção submarina;
- ataque poderoso;
- movimento lento;
- revela-se ao mudar para Surface;
- deve escolher cuidadosamente o momento do disparo.

---

## 14. RELAÇÕES ESTRATÉGICAS

O sistema cria relações de dependência:

- EWACS detecta para o Caça A;
- Radar protege aeroportos e centros logísticos;
- Caça F caça sensores e aeronaves isoladas;
- Bombardeiro Furtivo atravessa corredores sem cobertura;
- Fragata protege o Destroyer;
- Super Tucano persegue submarinos expostos;
- Submarino pune frotas sem ASW;
- relevo altera o valor real de cada sensor.

Uma unidade barata pode não destruir uma unidade cara, mas pode mudar completamente as condições em que ela opera.

Esse é o papel do Radar Móvel.

Ele não derrota o Caça F.

Ele permite que outra unidade o faça.

---

## 15. PRINCÍPIOS DE DOUTRINA

1. **Alta altitude não significa visão total.**
2. **Toda LoS continua sujeita a EV, relevo e angulação.**
3. **Skills de ocultação são exclusivas de domínio.**
4. **Range 0 não possui combate.**
5. **Todo combate é um duelo.**
6. **Não existe fogo de oportunidade coletivo.**
7. **Stealth concede iniciativa, não ataque gratuito.**
8. **Caças furtivos permanecem na camada e recebem exposição temporária.**
9. **Submarinos perdem ocultação ao mudar de camada.**
10. **Aeronaves rápidas desaparecem antes; navios lentos permanecem expostos por mais tempo.**
11. **Sensores produzem alvos; armas exploram a informação.**
12. **Mover no escuro é comprometer-se antes de saber.**

---

## 16. MÁXIMA DO JOGO

> *No ar, a velocidade é anulada pela ignorância.*  
> *No mar, o alcance é anulado pelo medo de emergir.*  
> *Quem move primeiro no escuro entrega sua posição; quem espera pode perder a janela.*  
> *A guerra não é sobre atirar. É sobre decidir se o alvo que aparece no seu tabuleiro está realmente vulnerável — ou se foi você quem entrou numa emboscada.*

---