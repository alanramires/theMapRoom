# v10.0.1 — A ilha sem ponte, porta por porta

*2026-11-05 e 2026-11-06. Salvamento de fim de trabalho.*

O fio foi o Peixe Pequeno: um mapa de ilhas sem ligação por terra entre os QGs. A v10.0.0 tinha
ensinado o pacote 2/2/1 a trocar tanque por ar quando a ilha está cortada. As marcas de hoje
mostraram que **a regra estava numa porta só**. O shopping tem várias portas (counter, elite,
déficit de operação, ameaça aérea, vigilância), e cada uma continuava comprando como se houvesse
estrada até o inimigo. Do lado do plano foi a mesma lição: o alocador e o blitz decidiam sem
perguntar se a unidade chega lá a pé.

---

## 1. Shopping: a ilha cortada em todas as portas

O autor descreveu o problema assim: "se o cara compra MBT, a gente compra o counter dele, que é o
bombardeiro ou o Apache; ele compra o counter nosso, que é o caça... e a gente compra outro tank
kkk". É um ping-pong de counters entre duas IAs que nunca se encontram por terra.

- **Uma fonte só para "esta célula é terra nossa"** (`OwnLandProbe`). Primeiro a ilha geográfica
  do QG; senão, o alcance terrestre da ficha de assalto mais barata. Sem dado, responde que sim,
  para não bloquear nada por falta de informação. A invasão e o counter leem dela.
- **Counter `not_in_reach`.** Inimigo de terra fora da nossa ilha mantém a pressão, mas uma
  resposta terrestre contra ele vale 10% (na compra e na cobertura). O log mostra `foraDaTerra=N`.
  A pressão não some: o inimigo existe. O que sai é a ideia de que um tanque responde a ele.
- **Linha do elite.** Baixar o score do MBT não bastaria. Pela composição antiga só tanque é
  "Assalto", e o MBT seria o único candidato, comprado ilhado do mesmo jeito. Com a ilha cortada,
  a linha troca de família e domínio: assalto vira a família Assalto **no ar** (Bombardeiro) e
  fogo vira a família Fogo de Suporte **antiaérea** (SAM). O caça fica de fora de propósito. Nas
  palavras do autor: "é muito raro comprar um caça sem ter o que abater". Caça vem da demanda de
  ameaça aérea.
- **Déficit de operação.** O Tanque Leve ainda entrava por uma terceira porta: a escolta que o
  analisador pede para captura terrestre "risky" (`Assaultx1`). Agora ela passa pela mesma linha.
- **Imposto de conscrição só conta corpo não-elite.** A fábrica pesada só-elite era taxada pelo
  preço do SAM ($12000) e zerava o carrinho do Difícil no T1, e por isso o Chinook não saía.
- **AA urgente só para aeronave dentro do anel.** O anel é o Tático da própria aeronave, com MP
  cheio (sem arma, cai no movimento). Ele precisa tocar a nossa ilha, só quando ela está cortada,
  ou um prédio ou unidade nossa. Um Chinook recém-comprado no canto oposto custava uma AAA de
  prioridade 8 antes de qualquer capturador. Na super-ilha única vale só o segundo "ou"; o autor
  pediu esse segundo "ou" justamente para ela.
- **Vigilância aérea e naval só quando há o que achar.** "Tem aeroporto" comprava Radar Móvel no
  T2 com o troco que o Apache não cabia. Com o tabuleiro revelado, ver o céu de longe acrescenta
  pouco, e furtivo aéreo (Caça F, Bombardeiro F) depende de um aeroporto especial que não está no
  jogo. A regra nova pede névoa total ou um inimigo capaz de pôr furtivo em campo, lido da ficha
  (`IsStealthUnit`) e das fábricas inimigas. A naval passou a pedir submarino ofertado, não "tem
  porto". Fica pronta para o Super Tucano e a fragata.

> **O que contradiz o que se acreditava:** "a ilha cortada já está resolvida no shopping" era
> verdade para o pacote e falsa para o resto. A regra precisa estar em **toda** porta que vira
> demanda de assalto ou fogo. Isso está anotado na memória do agente.

## 2. Alocador de capturadores: quem fecha agora ganha

A dúvida do autor: "me parece muito aleatório o alocador jogar letras... Bravo está exatamente
no Tático de uma fábrica leve". E era aleatório mesmo. A matriz media **passos**, e os termos de
continuidade (−8, −6, +12, histerese −15, eixo) engoliam 1 hex de diferença. "Fecha agora" e
"fecha no turno que vem" empatavam, e a ordem do laço decidia.

- **Desconto de −20** para o par cujo Tático de captura materializa o prédio. Ele é maior que a
  histerese de propósito: quando os dois chegam no mesmo turno, o desconto se anula e a histerese
  continua decidindo.
- **Troca pelo Tático entre quem já tem vaga.** O desconto sozinho não resolveu nem com
  `ai replan`: **o plano persiste entre turnos**, e quem já está numa vaga nunca volta para a
  matriz. O passo novo troca o par A↔B quando A não fecha agora, B fecha o objetivo de A agora,
  B não perde uma captura imediata, e A chega ao objetivo de B até 2 passos depois.

> **Erro meu:** a primeira versão (só o desconto na matriz) não podia funcionar, porque o #4 nunca
> entrava na matriz. Antes de mexer num custo de atribuição, conferir **quem entra** na atribuição.

## 3. Blitz: a ponta só reveza se tem para onde ir a pé

"O de trás paga" pressupõe que a ponta siga adiante. O #4 largou Bravo 10/20 rumo a Lima, do
outro lado da água. Agora a ponta **em cima do prédio** só reveza se o próximo alvo dela tem
rota terrestre a partir dela. Sem rota, o blitz cancela e ela captura normal. A checagem está
no plano (antes de escolher herdeiro) e na execução (revezamento lembrado em memória). Foi vista
em Play: "#5 cancela o blitz em Alpha: Echo sem rota terrestre" e captura antes de embarcar.

É a única exceção aceita ao "a ponta nunca para", e não contradiz a doutrina: ela não para para
fechar, fica porque não tem para onde seguir.

## 4. LZ do Chinook pousado

O Chinook #18 escolheu a LZ em cima do próprio passageiro #10. Não pousava, e ninguém embarcava.

> **Erro meu, registrado:** eu disse ao autor que o `PodePousar` não olhava ocupação. Ele olha,
> por delegação a `AircraftOperationRules`. Procurei a palavra no arquivo do sensor e não segui a
> chamada. A ocupação estava certa; o furo era o gate de LZ do MelhorEmbarque, que decidia por
> `GetDomain() != Air`. **Aeronave pousada é Land**, e o teste de pouso era pulado. Agora o gate
> decide pela ficha (`IsAircraft`) e repete a sequência de ocupação do pouso (camada de pouso,
> suporte, `CanEndLayerTransitionAtCell`), que vale no chão e no ar.

## 5. Trabalho de outros, junto nesta versão

- **BeachManager** (outra sessão): cache de ilhas por retângulo; sem quadrante selecionado, a
  autoria analisa cada quadrante separado e lista o quadrante de cada ilha. Teste de regressão em
  `tools/ai_test/verificar_ilhas_sem_recorte.cs`; nota em `docs/ilhas.md`.
- **AI Step:** a DECISÃO ganhou caixa própria, em destaque.
- **Mundo Fixture:** rebake das construções dos quadrantes (autor).

---

## O que não terminou

- **Nada disto foi validado em Play como fluxo**, exceto o cancelamento do blitz. Procurar nos
  logs: `foraDaTerra`, `(ilha sem ligacao por terra)`, `dentro do anel`, `[TrocaTatico]`,
  `vigilancia aerea: sem demanda` e a LZ do Chinook ao lado do passageiro.
- **Apaches demais?** A escolta "risky" agora pede Apache ($7500) por setor, e se junta com o
  pacote. Se exagerar, a pergunta é se setor "risky" numa ilha cortada merece escolta.
- **Compra de prioridade 20 no troco.** O carrinho desce para a demanda de baixo quando a de cima
  não cabe. Isso vale para toda demanda e não foi tocado.
- **Chinook pousado:** a ocupação da LZ agora é testada, mas o terreno de pouso não, porque o
  `PodePousar` só responde para aeronave no ar.
- **Desconto do Tático pode puxar capturador para outro eixo** por um prédio colado. Observar.
- **Posicionamento de SAM e AAA** (ideia do autor): SAM nos cantos expande a cobertura, um SAM
  cobre o ponto cego do outro, e AAA escolta o SAM. É posicionamento, não compra.
- **Super-ilha cortada e grande:** a aeronave sobre um canto distante ainda conta no anel.
- Herdados da v10.0.0 e ainda abertos: capturador com vaga que não reveza pode roubar o prédio do
  herdeiro; prédio neutro na própria base não vira objetivo; Feijão Torto depois da troca por
  família; medir `MelhorEmbarqueDestinationCostMaps`; escrever a doutrina do blitz em
  `docs/AI Behavior/Capturador.md`.
