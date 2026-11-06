# v10.2.0 — O dinheiro do elite para de vazar, e o jogo vai para outra pessoa

*2026-11-06 (tarde). A compra aérea aprende a esperar; o build ganha checklist, volume e estrada inteira.*

Dois fios no mesmo dia. O primeiro saiu das marcas do AI Step no Peixe Pequeno: **a compra
gastava o dinheiro do elite em qualquer coisa que pedisse com urgência** — três AAA, um Apache,
uma Metranca — e os caças nunca nasciam. O segundo veio de fora: uma pessoa vai jogar o jogo
"do jeito que está", e isso pediu tudo que um build entregue a outra pessoa precisa e que até
hoje só o autor sabia fazer de cabeça (desligar o debug, encolher textura para a Web, deixar o
som ajustável).

## Por que Y

Uma parte e os filhos dela: o shopping aéreo (AA, escolta, poupança, conscrição) e o que ele
alimenta no capturador. As ferramentas de publicação e o volume são novos, mas localizados.
Nenhuma regra de sensor validada mudou.

---

## 1. A compra aérea: uma ameaça, uma resposta

O autor resumiu o desconforto numa marca: *"estamos torrando tudo nelas :("* — três AAA por
$16,5k, contra um trem que "só tá rondando a ilha", com a promessa do Bombardeiro jogada fora.
Eram três causas, e nenhuma sozinha explicava os três AAA:

- **A demanda de Disruptor zerava a reserva inteira** do elite. Agora rompe **no tamanho da
  demanda** (`EstimateDemandCost`), nunca o caixa todo (e82675c).
- **Chinook contava no anel de AA.** Transporte inimigo é problema do Disruptor quando há
  Disruptor na oferta — uma ameaça, uma resposta (e82675c).
- **Sem escalada:** a AA urgente pedia tudo de uma vez. Agora é 1 urgente por turno; o resto vira
  demanda comum (prioridade 15), sem `ExactRole` para não se fundir de volta na urgente.

**A escolta (Caça B)** nasceu como o espelho do Disruptor (d2adad6): disruptores inimigos no ar e
Chinooks nossos avariados pedem caça — "a operação de captura está sendo atrasada pelos insetos".

**O achado que reorganizou a poupança (92e99ce):** a escolta **soltava** a reserva do Bombardeiro
mesmo quando o Caça B não cabia. Caça B de $16000 com $15000 livres não saía, e os $6750 do
Bombardeiro (prometido desde T3) viravam Apache + Chinook de outras demandas. A regra agora é a
da poupança do elite, que já existia: **se não cabe nem rompendo, segura o caixa e passa a vez**
— o imposto de conscrição continua (a massa sai), o resto espera, no horizonte `EliteSaveTurns`.
O autor pediu exatamente isso ("segura e passa a vez, tenta por 2 rodadas, não tinha um trem
assim?"); o mecanismo era o mesmo, só a escolta estava fora dele.

**A folga da conscrição (b7be08d):** o recrutamento forçado cessa ao chegar a 55% do teto de
unidades e só volta em defesa ou abaixo do piso de saída (45%). O autor: "isso daria chance pra
AI brilhar comprando elites". Os checkboxes `conscriptionDoctrine`/`conscriptionWhenLosing`
saíram do Inspector da cena (o preset decide); os números ficam.

**Visto em Play:** o Médio comprou Caça B e surpreendeu o autor ("me surpreendendo danada").
Não se sabe ainda por qual caminho saiu (segurar caixa ou folga): a próxima marca de compra de
caça responde.

APC para eixo sem ligação por terra também deixou de ser pedido (f190f04): a pressão operacional
agora confere a terra de cada eixo, e a Shopping Pressure mostra "SEM TERRA".

## 2. O capturador cede para quem fecha antes

- **Prédio de outro objetivo** (604d506): o ocupante cedia só ao parceiro do próprio objetivo;
  em cima do prédio de outro, trancava o dono do lado de fora.
- **O Tático da vizinhança** (cd9fd0b): na marca do T6 amarelo, o Soldado #8 (4 HP) capturava a
  cidade de Echo com um 10 HP rumo a Hotel logo abaixo, e o swap nem o via — só procurava nos
  slots do objetivo. Agora entra qualquer capturador cujo movimento deste turno cobre o hex; a
  régua continua `RodadasAteFechar`; os donos levam o empate; quem já captura outro prédio fica
  de fora. Custo aceito: o objetivo do substituto atrasa uma rodada.

## 3. A fila de carona, contada

O autor viu "mais passageiros que ônibus" e perguntou como ver a pressão. Não havia onde: a
Shopping Pressure conta ônibus por eixo (o lado da compra), o QueroCarona responde uma unidade
por vez. Nasceu o consumidor **`PressaoCaronaService`** (camada do meio: pergunta ao QueroCarona
uma vez por unidade, com o MESMO pedido do capturador, e cruza com bancos livres), a tela
**Tools ▸ Transporte ▸ Pressao Carona** e uma linha na Shopping Pressure.

**O achado:** os dois lados tinham ~4× mais pedidos que bancos (15–17 contra 4), e quase todos
"ilhados — só chegam de carona". Os corpos da conscrição **nascem ilhados**: a ilha de casa já
fechou e todo prédio que resta fica do outro lado da água.

A fila entrou na compra como **piso, não soma**: a demanda de Chinook vira o maior entre o que as
operações de airlift pedem e os ônibus que faltam, com teto de +2 acima das operações (para não
trocar o vício da AAA pelo do Chinook). Conta só capturador **do plano** ilhado contra banco
**aéreo** — emergência (hospital), rogue e AAA/SAM pedindo carona aparecem na tela mas não
compram ônibus.

**O freio da conscrição pela fila** (parar o imposto quando ilhados > bancos) ficou proposto e
**não entrou**: é decisão do autor.

## 4. Publicação: o que só o autor sabia fazer de cabeça

**O erro desta frente, registrado de propósito:** o autor pediu para trazer "a ideia da
Mi'Larhys". Eu trouxe só a casca da janela de checklist, e ele teve de lembrar: *"eu falei pra
copiar as melhores práticas da Mi'Larhys :("*. As práticas estavam nos menus `Tools/Mylarhis/*`
e no resumo dela. Virou memória (`feedback_copiar_praticas_mylarhis`).

**Tools ▸ Publicação ▸ Checklist** lê o valor DE VERDADE nas três cenas do build (Tela de
Entrada, Campanha, Batalha) — esquecer o debug ligado não deixa rastro em lugar nenhum:

- **Preparar para publicar** desliga Start On Pause, atalhos da IA e do Panel_Debug, HUD da IA e
  ~17 flags de log; mantém o Diagnóstico no save **ligado** (se ela mandar o save, dá para ler).
  Grava o valor de antes em `UserSettings/`.
- **Voltar ao debug** restaura. *Armadilha vista hoje:* cinco flags da Batalha (Start On Pause,
  atalhos, HUD e logs da IA) já estavam desligadas à mão antes do Preparar, então o backup não as
  tinha. A restauração deste fechamento foi pelo valor do HEAD, campo a campo.
- **Texturas Web**, da Mi'Larhys: 512 + ASTC 6x6 no que é do tamanho de um hex (peça, prédio,
  ícone, munição, cursor), 1024 no que ocupa a tela (retrato, logo, mapa, cena da rodada) e nas
  folhas de animação. Só na plataforma Web. Override manual não é tocado; áudio fica de fora. A
  convenção era do autor (97a05b7, no Inspector) e **vazava**: o `town.png` da metrópole entrou
  ontem sem override. O autor aplicou: 188 `.meta`, só blocos WebGL.
- Aviso de cena não salva (o build sai do disco) e dois itens manuais: Web sem LTO, jogar um
  turno no build gerado.

**Volume geral e da música**, também da Mi'Larhys: geral é o `AudioListener.volume` (abaixa tudo,
em toda cena, aplicado antes da primeira); música multiplica as trilhas. Padrão 100%, gravado em
`PlayerPrefs`. Entrou no `Panel_Config` que o autor montou à mão; as barras foram criadas pelo
editor (`PrefabUtility`), alinhadas pelos números do painel. Setas esquerda/direita mudam 10%,
Enter sobe e dá a volta, mouse/dedo arrastam. *Armadilha:* a raiz de um prefab guarda tamanho e
posição como override em cada cena — aumentar o prefab não chegava nas cenas; foi preciso ajustar
as três instâncias.

**Estrada inteira no mapa da Campanha:** cada quadrante assa só o trecho dentro do próprio
retângulo, então a aresta que cruza a divisa entre dois quadrantes não era de ninguém e a rodovia
aparecia partida. O "Assar fundo" agora guarda também as rotas, recortadas pela **campanha**
(`CampanhaData.bakedFundoRotas`), e o "Assar TODOS" assa quadrantes + fundos. Na Batalha a
estrada "comida" na borda fica: o autor descartou levar o entorno para o tabuleiro ("estamos em
MVP ainda").

**Panel_helper:** `OnValidate` no prefab asset durante o Play/build tentava pendurar a barra de
tempo e a alça no asset; o Unity recusava e o objeto ficava órfão. Guarda de cena válida.

## 5. Fora do jogo: backup e arte

- Skill de usuário **`faca-um-backup`**: zip datado (nunca espelho) de The Map Room e Mylarhis no
  NordLocker, sem `Library`/`Temp`, com `.git`, retenção de 8.
- **`Unity Drafts\<projeto>`** no cofre é a área de trabalho de arte do autor: não entra no
  backup. 8 PSDs saíram do The Map Room e 2 da Mylarhis — todos provados sem referência pelo guid.

## 6. Da outra sessão

- `aa065ec` (cobertura da AAA, entregas úteis do transporte) e `f541939` (ocupação aérea, comandos
  de dono histórico no debug) já estavam commitados.
- **Abate oportunista enquanto espera carona** (`AIController.Capturer.Embark.cs`, com
  `verificar_abate_espera_carona.cs`): commit próprio neste fechamento.

---

## Docs de doutrina

Desatualizados
- `docs/AI Behavior/Capturador.md` — o swap (linhas 173, 324 e 511 ainda dizem "mesmo objetivo" e "HP cru"): agora é a régua `RodadasAteFechar`, prédio de outro objetivo e o Tático da vizinhança (**só compilou**). Carregado da v10.1.0: blitz só com rota terrestre, trabalha enquanto espera carona (**visto**); espera fora da produtora e cabeça de praia (**só compilou**).
- `Assets/Scripts/Match/AI/3. Shopping/Shopping.md` §6 — escalada da AA (1 urgente/turno), Chinook fora do anel com Disruptor na oferta, rompimento no tamanho da demanda, escolta que segura o caixa, folga da conscrição 55/45 (**visto em Play**: Caça B comprado) e o piso da fila de carona (**só compilou**). Carregado: imposto devolve a parcela do produtor usado (**visto**).
- `docs/AI Behavior/Transporte.md` — a fila de carona como piso de Chinook (**só compilou**). Carregado: ganância no desembarque, corrida no MelhorEmbarque, LZ de Chinook pousado (**só compilou**).
- `docs/AI Behavior/FireSupport.md` — antiaéreo rogue olha alvo conhecido antes do rendezvous (8693d68, **visto em Play** pelo autor: o SAM escolheu o spot de alcance máximo).
- Carregados da v10.1.0 sem mudança: `docs/AI Behavior/Assalto.md` (família Disruptor, negação de escolta), `docs/magnetic_tabela.md`, `docs/AI Behavior/Papeis.md` e `ficha_do_papel.md`.

Prontos para consolidar
- Nenhum. O que é da v10.1.0 ganhou mudança hoje (a escolta e o Disruptor mexeram de novo no shopping); o restante ainda não tem dois fechamentos de estabilidade.

---

## O que não terminou

- **Só compilou:** swap pelo Tático, fila de carona na compra, estrada do fundo (o autor ia
  reassar e não confirmou o mapa), a guarda do Panel_helper. O checklist e os volumes foram usados
  no editor; no build Web, o volume persistir no navegador ainda não foi visto.
- **Freio da conscrição pela fila de carona** — proposto, decisão do autor.
- **Metranca contra inimigo fora da ilha:** a pressão de counter desconta inimigo do outro lado
  só ao escolher quem responde, não na pressão; com o Aeroporto ocupado, a demanda urgente cai na
  Fábrica Leve e compra Metranca. Proposta (urgência ou só aéreo quando a pressão é quase toda
  fora da terra) **não implementada**.
- **Dinheiro liberado não é carimbado:** quando a escolta cabe rompendo, uma demanda urgente que
  entre antes ainda pode consumi-lo.
- **Apaches seguem contando no anel de AA.**
- **Por que o claim do slot 0 estava vazio** no meio do turno (o "alvo de outro" do capturador lê
  o claim publicado) — não investigado.
- **Ajustes do autor na Batalha** que entraram como autoria: os dois slots `isAI` 1→0, `aiStep` e
  `hardMode` desligados, `massacreUnitCapFillRatio` 0.5 (não 0.55 como conversado). Não verifiquei
  se o `isAI` é sobrescrito pela Nova Partida em runtime.
- Primeiro backup de verdade da skill nunca rodou antes deste fechamento.
