# v10.1.0 — Você não vai fazer dinheiro não

*2026-11-06. A força aérea deixa de ser cópia da terrestre.*

O fio do dia foi descobrir que a força aérea **nasceu como cópia da terrestre**. Cada marca do
AI Step revelava uma suposição de chão vestida de ar: todo mundo orbita o capitão, o valor do
alvo é o tipo dele, "ar primeiro" para qualquer aeronave, o Apache e o Bombardeiro como uma
linha só. Caso a caso — um commit por marca, como o autor fez questão de lembrar — apareceu a
regra do ar: **o capturador põe dinheiro no prédio, o Disruptor tira dinheiro do inimigo.**

## Por que Y

Uma parte do jogo e os filhos dela foram trabalhados: a força aérea (papel novo, magnético,
tiro, escolta) e o que a alimenta no shopping e na carona. Nenhuma regra de sensor validada
mudou; a cascata de dano aos embarcados ganhou fonte única sem mudar de comportamento.

---

## 1. O Disruptor de captura

O Apache rogue orbitava o capitão com 13 inimigos visíveis. O autor deu o lema dele: o do
capturador é "antes do tiro vem o dinheiro"; **o do Apache é "você não vai fazer dinheiro
não!"**. Ele caça os Chinooks que levam capturadores e, quando eles pousam, os capturadores no
chão. Escoltar Chinook aliado é missão secundária. AAA e SAM não impedem: "se der pra beliscar,
ótimo".

- **Papel `DisruptorDeCaptura`** (família Assalto, uma das seis; sub-rótulo de `AtaqueAereo`).
  O nome veio de uma IA externa. Nada no rótulo sabe que é helicóptero: um drone ou um sniper
  podem ser Disruptores. Para o sub-rótulo não tirar o Apache de ~20 testes estritos
  `roles[0] == AtaqueAereo`, nasceu um ponto único, `UnitRoleCompatibility.MatchesLabel`.
- **O capitão do Disruptor é o poder de captura combinado** (`PodeCapturarSensor.GetCombinedCapturePower`:
  o HP de quem tem chave de captura + o de cada embarcado capturador). "Não importa se é
  transportador, a pé, ou no navio de desembarque." Transporte vazio não atrai. Visto em Play:
  o Apache trocou sozinho do soldado capturando (10) para o Chinook com dois soldados (20).
- **O tiro pelo poder negado.** O valor do alvo para o Disruptor é o dinheiro que o tiro tira,
  com a cascata aos embarcados — que virou fonte única no `CombatFormula` (a execução e a IA
  chamam a mesma conta). A ordem do autor (Chinook cheio > capturador no chão > Chinook com um)
  sai da conta, sem lista. O autor, sobre isso: "eles caçam os cap powers somados, por isso o
  Chinook full carregado é apetitoso".
- **"Ar primeiro" é doutrina de caça.** O combate aéreo excluía alvo de chão se houvesse
  qualquer aeronave atacável: o Apache atirou num Chinook **vazio** com o soldado recém-desembarcado
  colado nele. Para o Disruptor, todo alvo legal entra. Visto em Play: o soldado foi de 10 a 3.

A calculadora confirmou o desenho do autor: um Apache leva o Chinook a 5 (e, pela propagação
percentual, a carga também — metade do poder de captura); dois abatem. **Concentrar, não
dividir.** Um Apache não abate de propósito ("não é tão cruel"): o dono ainda pode fugir.

## 2. O Bombardeiro nega a escolta

Primeiro eu tirei o Bombardeiro da linha do elite do Disruptor. O autor desfez: "bombardeiros
são excelentes disruptores, mas brilham contra MBT e artilharia", e é a progressão dele que
obriga o inimigo a comprar caça — "senão o jogo se resume a Apache vs Chinook".

- **Unit Secondary Role.** O editor da ficha cortava a lista `roles` em um elemento. Agora há
  o papel secundário (`roles[1]`): Apache `[Disruptor, AtaqueAereo]`, Bombardeiro
  `[AtaqueAereo, Disruptor]`. Tudo que usa `CanSatisfy` já enxerga o secundário.
- **Magnético do Bombardeiro:** a **escolta** do poder de captura (assalto e fogo de suporte
  inimigos que a arma dele acerta), sem escolta o capturador, depois o capitão. Palpite do
  autor: "o bombardeiro nega a escolta do cap power". Simulação: contra Soldado ele nega o mesmo
  que o Apache sem perder HP; Tanque Leve a 1; Obus Leve abatido. Não acerta o ar.

## 3. Shopping

- **Demanda de Disruptor pelo poder de captura em trânsito**, em pares, que **rompe a poupança
  do elite**. O counter enxergava classe (helicóptero pede antiaéreo) e não enxergava dinheiro:
  três Chinooks carregados passavam sem pedir quem os caça. Visto em Play: Apache + Chinook + 4
  Soldados com a poupança rompida; no turno seguinte, par completo, a poupança voltou.
- **A poupança aparece**, com o nome da unidade (o log dizia a demanda de counter em vez do
  Bombardeiro), e a outra sessão a destacou na DECISÃO do AI Step.
- **O imposto de conscrição cobrava duas vezes:** o corpo comprado pelo carrinho num produtor
  taxado pagava de novo. Agora a parcela volta (`TaxRefund`). O autor nem lembrava do imposto —
  é a Doutrina do Enxame do Difícil.

## 4. A carona: antes do tiro vem o dinheiro

- **Trabalha enquanto espera:** capturador ilhado sem carona anda ao capturável livre mais
  perto **a pé** em vez de segurar. Visto em Play: o #19 rumo a Echo. Pula prédio com aliado em
  cima e o que a cessão do oportunista dá a outro.
- **Espera fora da produtora:** quem segura em cima da própria fábrica tranca a compra; agora sai
  para o lado.
- **Ganância no desembarque:** quem não tem rota para o próprio alvo, mas tem prédio livre no
  Tático a partir da LZ, desce junto como oportunista. O caso que puxou: o Chinook sobre o mar
  entre duas ilhas — "é como pegar um ônibus no Terminal Santo Antônio querendo ir pro shopping,
  mas ele passa antes pelo centro" (autor).
- **Corrida pelo prédio (registro de aproximações, ideia do autor):** sem reservar ninguém, o
  veículo leva quem está mais atrás na corrida — "quer saber, os caras vão lá pegar, minha carona
  chegou". Reservar por ordem de ação foi descartado de propósito: o do Estratégico tiraria quem
  chega no Tático.
- **Cabeça de praia:** sem nada a capturar a pé, avança até a célula do próprio componente mais
  perto do alvo e espera lá (otimiza a LZ futura).

> **Erros meus, registrados.** (1) Disse que o `PodePousar` não olhava ocupação — ele delega a
> `AircraftOperationRules`, onde ela mora; o furo era o gate de LZ tratar aeronave pousada como
> terrestre (v10.0.1). (2) A primeira ganância simulava o passageiro embarcado com o sensor
> normal, que **barra embarcado**: nunca dispararia. (3) Tirar o Bombardeiro da linha do elite
> foi corte demais; o autor tinha razão.

## 5. Ferramentas e processo

- **Tools ▸ AI Captain:** a Tabela Magnética organizada como a Role Families. O asset **não tem
  leitor** no jogo; decisão do autor: ele é o "guru" (a referência de quem segue quem), e a
  migração será um papel por vez, com o Disruptor como piloto.
- **Skills:** o fechamento passa a **apontar** docs de doutrina desatualizados e os prontos para
  consolidar, por três sinais objetivos (estável, visto em Play, sem marca aberta); a
  `consolidar-doutrina` escreve quando o autor quiser, de cabeça fresca.
- **Rascunho do autor** com uma IA externa (`docs/AI Behavior/rascunho/`): ela acertou que o
  subpapel manda no magnético — a Tabela Magnética é por rótulo, não por família.

---

## Docs de doutrina

Desatualizados
- `docs/AI Behavior/Assalto.md` — família Assalto sem o rótulo `DisruptorDeCaptura`; `AtaqueAereo` como uma linha só (Apache+Bombardeiro); magnético por capitão; "ar primeiro" para todo aéreo. CCP e tiro no soldado **vistos em Play**; negação de escolta **só compilou**.
- `docs/magnetic_tabela.md` (e `Assets/DB/AI/AICaptain.asset`) — linha de `AtaqueAereo` não tem interdição nem escolta; não há linha do Disruptor. O asset segue sem leitor.
- `docs/AI Behavior/Papeis.md`, `docs/AI Behavior/ficha_do_papel.md` — papel secundário (`roles[1]`) e o rótulo novo. **Visto em Play** (compromisso do Bombardeiro mantido pelo secundário).
- `docs/AI Behavior/Capturador.md` — blitz só com rota terrestre (v10.0.1, **visto**); trabalha enquanto espera carona (**visto**); espera fora da produtora e cabeça de praia (**só compilou**).
- `docs/AI Behavior/Transporte.md` — ganância no desembarque, corrida pesando no MelhorEmbarque (**só compilou**); LZ de Chinook pousado (v10.0.1, **só compilou**).
- `Assets/Scripts/Match/AI/3. Shopping/Shopping.md` §6.1 — o imposto de conscrição devolve a parcela do produtor usado pelo carrinho (**visto**); demanda de Disruptor rompe a poupança (**visto**).

Prontos para consolidar
- Nenhum. O que é da v10.0.1 tem um fechamento de estabilidade; o de hoje mudou hoje.

---

## O que não terminou

- **Só compilou:** a negação de escolta do Bombardeiro, a ganância no desembarque, a corrida no
  MelhorEmbarque, a espera fora da produtora, a cabeça de praia.
- **Foco no mesmo alvo** para dois Apaches ("concentrar, não dividir") não está no código; cada
  um escolhe o seu e podem espalhar.
- **Rotas de captura** no lugar do prédio contestado como âncora sem alvo visível.
- **Magnético do Bombardeiro** usa a família do alvo; a ideia "alvo Primary da ficha" segue aberta.
- **Disruptor não aéreo** (drone de chão, sniper) contaria como ataque aéreo no shopping: quando
  nascer, o "satisfaz" passa a depender do domínio; e o pipeline terrestre não tem a âncora CCP.
- **AICaptain.asset** sem leitor (decisão: migrar um papel por vez).
- **Corrida livre** em prédio de "trabalha enquanto espera" (3 em Charlie): decisão de deixar;
  se reservar um dia, por banda, nunca por ordem de ação.
- **Anel de AA em continente grande e cortado:** decisão de deixar (AAA cedo vira escolta).
- **Courier escolhe o rumo pelo primeiro passageiro**, não pelo destino mais perto nem pela missão.
- Herdados: planner mandando capturador para objetivo de carona com prédio livre a pé; prédio
  neutro na própria base; Feijão Torto depois da troca por família; medir
  `MelhorEmbarqueDestinationCostMaps`.
