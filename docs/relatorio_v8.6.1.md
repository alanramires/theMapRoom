# v8.6.1 — o que está configurado não é o que está valendo

Um salvamento de fim de trabalho que acabou contando a mesma história cinco vezes.
Em cada uma, uma tela, um asset ou uma lembrança mostrava uma coisa, e o jogo
fazia outra:

```text
o Inspector         mostrava "Base Preset: Difícil"   e o jogo usava o catálogo
os perfis Fácil/Médio diziam "como a média"           e eram cópia do perfil Gulosa
o perfil Difícil    tinha números                     e ninguém lia número nenhum
a Retaguarda        mostrava "a massa inimiga"        e era a posição real, sob névoa
o resumo            mandava "limpar o progresso"      e o progresso nunca foi pro disco
```

Nenhum dos cinco dava erro. O dia terminou com o autor trazendo de fora a frase que
dá nome ao problema. Ela vem do guia que ele escreveu depois de fazer um mod para
Crusader Kings 3:

> *"Um bom suporte a mods permite ao autor entender o estado, alterar uma regra de
> forma localizada e descobrir rapidamente por que algo falhou."*

O jogo não vai ter mods tão cedo. Mas a primeira metade da frase, **entender o
estado**, é o que faltou nos cinco casos. A direção que sai daqui é deixar o jogo
**auditável**: cada valor sabe dizer de onde veio, e cada save pode ser lido sem
arqueologia.

---

## Frente 1 — nenhum portão pergunta mais "sou o difícil?"

A `v8.6.0` tirou quinze portões do `hardMode`. Sobravam seis lugares onde o toggle
existia no perfil e o jogo continuava lendo outra coisa:

| portão | lia | lê agora |
|---|---|---|
| armadura primeiro, metade do shopping ([Demand.cs:2771](../Assets/Scripts/Match/AI/3.%20Shopping/AIShoppingPlanner.Demand.cs)) | `HardMode` | `AbreComBlindado` |
| setor à frente depois de ceder ([Handoff.cs:170 e 210](../Assets/Scripts/Match/AI/2.%20Planner/AIController.PlanEvaluator.Handoff.cs)) | `hardMode` | `FazHandoffEmProfundidade` |
| conscrição sempre | campo da cena | `capacidades.conscricaoSempre` |
| conscrição quando perdendo | campo da cena | `capacidades.conscricaoQuandoPerdendo` |
| lado forte/fraco | o campo, lido direto em 3 lugares | `capacidades.politicaLadoForteFraco` |
| núcleo suave | campo da cena | `capacidades.gateNucleoSuave` |

O primeiro portão era um vazamento real. A outra metade da regra "abre com
blindado" (a poupança do 1º MBT) já perguntava `AbreComBlindado`, então desligar o
toggle no perfil apagava só metade do comportamento.

**O que não mudou, de propósito:** quem não tem catálogo continua idêntico. Na
reserva (baseline + overlay de código), os quatro toggles novos copiam os campos
da cena ([AIController.Preset.cs](../Assets/Scripts/Match/AI/Presets/AIController.Preset.cs)),
porque a overlay nunca soube deles. Sem a cópia, a reserva passaria a obedecer o
asset baseline em silêncio. O `hardMode` que sobrou no código é o `OnValidate` do
Inspector, que não decide comportamento.

---

## Frente 2 — os três perfis montados a partir das frases do autor

Os perfis em disco contradiziam a doutrina escrita neles. O Fácil dizia *"é como a
média, só que com menos dinheiro"* e tinha renda cheia. O relatório da `v8.6.0`
registrava *"a fácil fica agarrada até terminar a captura"*, e o Fácil tinha
blitzkrieg ligada.

**A causa estava no `git show --stat` do `30fd327`:** o `AIPreset_Medio` é o
antigo `AIPreset_Gulosa` renomeado, e o Fácil nasceu como cópia dele. Os dois
herdaram os toggles de um perfil de **teste** guloso (conscrição sempre, nunca
poupa). Já o Difícil é do autor: a doutrina escrita à mão (*"spama soldados e usa o
caixa restante para unidades"*) casa com os toggles dele.

| | Fácil | Médio | Difícil |
|---|---|---|---|
| renda fora de cidades | **⅓** | cheia | cheia |
| blitzkrieg | não | **sim** | sim |
| slots de capturador dobrados | não | não | sim |
| lista banida, projeção, teto de logística | não | não | sim |
| conscrição sempre / perdendo | não / não | não / não | **sim** / sim |
| poupa pra elite | sim | sim | **não** |
| lado forte/fraco (protótipo) | não | não | sim |
| núcleo suave | sim | sim | sim |
| números | lado normal | lado normal | **lado hard** |

**Os números do Difícil estavam errados, e ninguém via.** O gerador
([AIPresetGeneratorWindow.cs:275](../Assets/Editor/AI/AIPresetGeneratorWindow.cs))
copia só o lado normal dos pares normal/hard da cena. Os três perfis tinham
**exatamente** os mesmos ~70 valores. Isso só não fez estrago porque nenhum número
é lido do perfil ainda (fase 2). Se a fase 2 tivesse religado os getters antes, o
Difícil teria perdido elite 0.60/0.80, poupança de 2 turnos, troco de 30%, núcleo
4/0/0 e slots ×2 até 6, sem nenhum aviso. Até a overlay de código estava furada:
ela sobe ratios e núcleo, mas esquece a poupança e o troco.

**A auditoria que achou isso** comparou, campo a campo, o mapa do gerador, a cena
Batalha e os três assets. Ela também me corrigiu: eu tinha dito que
`intel.lookbackTurns` não tinha leitor, porque procurei pelo nome. O leitor se
chama `IntelShoppingLookbackTurns`. **Busca por nome não prova ausência**, e o mapa
do gerador era a fonte certa desde o começo.

---

## Frente 3 — "Conquistar" ou "Reforçar controle" *(trabalho de outra IA)*

O sensor de captura tinha um rótulo só para duas operações diferentes. Agora o
painel de diálogo e o panel helper perguntam ao `CanUnitCaptureFromCurrentPosition`
qual é a operação. Com `RecoverAlly`, mostram **Reforçar controle**; no resto,
**Conquistar**. São duas mensagens novas nas bases de diálogo e de helper
(`panel_dialog.sensor.recover_control`, `helper.sensors.label.recover_control`).

Bate com a seção 2 do `Papeis.md`, que o autor escreveu no mesmo período: são dois
rótulos para a mesma ação, porque *reduzir os pontos de outro* e *recuperar os
seus* são fatos diferentes para quem joga.

---

## Frente 4 — mapa sem HQ deixa todo mundo rebelde *(trabalho de outra IA)*

Antes, um slot só era rebelde se **algum** HQ existisse na partida
(`anyOwnedHeadQuarter`). Num mapa sem HQ nenhum, ninguém era rebelde, e a IA
tentava montar plano num tabuleiro sem base. Agora a classificação depende só do
HQ do próprio slot ([MatchController.cs:4151](../Assets/Scripts/Match/MatchController.cs)).

A consequência, conferida no código: num mapa sem HQ, a IA limpa o plano e captura
pelo caminho rebelde ([PlanEvaluator.cs:85](../Assets/Scripts/Match/AI/2.%20Planner/AIController.PlanEvaluator.cs)).
É exatamente o cenário do novo `teste_blitzkrieg.md`: *"sem HQ, sem plano e sem
eixo"*. O comentário no código diz o limite: rebelde informa a disponibilidade de
base e plano, não é outro tipo de IA.

---

## Frente 5 — o rascunho do Quero Spotting

Pós-MVP, e o autor disse isso com todas as letras: *"a gente perdeu alguns créditos
brincando nele"*. Fica registrado porque a conversa produziu três decisões.

**A tela** (`Tools ▸ Hotzone ▸ Quero Spotting`,
[QueroSpottingWindow.cs](../Assets/Editor/QueroSpottingWindow.cs)) é o lado de
quem pede, par do Melhor Spotting (que ainda não existe), do mesmo jeito que o
QueroCarona é par do Melhor Embarque. Ela calcula assim:

```text
faixa     = Tactical da arma (UnitReachEnvelopeService, subetapa Artilheiro)
vanguarda = faixa num cone estreito apontado para a âncora   → pendura o papel
flancos   = entre o cone da vanguarda e 90°                  → peso menor
cega      = sem cobertura de sensor do slot
```

A cena `Em dev/Quadrado` virou o cenário do teste: artilharia de campanha, soldado
e serra.

**As três decisões:**

1. **Flanco entra no papel, mas sozinho não pendura.** Com o meio-plano, o spotter
   que acende seis células de grama no flanco "cumpria" o papel, e a massa atrás da
   serra continuava escura.
2. **A Retaguarda trapaceia de propósito, e isso vale para geografia, não para
   tropa.** O `SetAnchorFromEnemies` faz a média da posição **real** de todo
   inimigo. O autor aceita a trapaça de geografia (prédios e HQ, que o jogador viu
   antes de o mapa escurecer). Para pedir spotting, posição real de tropa é
   wallhack por procuração: o papel apontaria exatamente para onde a IA não pode
   saber. A tela põe as duas âncoras lado a lado.
3. **O `IsForwardObserverSpot` vai cair.** O autor não quer lugar autorado de
   spotting.

**A cobertura é uma aproximação:** a tela usa `SensorCoveredCells`, que é visão. O
tiro parabólico pede **detecção** (`ResolveDetectionContributors`). Para alvo comum
dá o mesmo resultado; contra furtivo, não. A cobertura de detecção por célula
continua faltando (degrau 1 da escada).

### Achado: o contrato de missões já existia

A conversa sobre "quadro de missões" (papel pendurado no Neutral, pedido, promessa,
ordem) tratou o QueroCarona como o embrião. Mas o
[`contrato_missoes.md`](AI%20Behavior/contrato_missoes.md) existe desde a `v7.2.1`
e já desenha `RevelacaoDeContato`, `RevelacaoTerritorial` e `SpottingDeCobertura`,
como brainstorming marcado. **Antes de seguir com missões, ler esse contrato.** A
conversa redescobriu partes dele sem saber.

---

## Frente 6 — a doutrina do capturador ganha documentos *(autor)*

Três documentos novos em `docs/AI Behavior/`:

- **`Papeis.md`**: as oito ações da unidade depois do movimento provisório, mais as
  faixas tática, operacional e estratégica.
- **`capturador_politicas.md`**: quatro políticas (persistência, blitzkrieg,
  substituição por eficiência, captura oportunista), cada uma em ficha.
- **`teste_blitzkrieg.md`**: a primeira política com matriz de casos de controle.

O teste foi confrontado com o código, e o código atual reprovaria em pelo menos
quatro casos:
- a troca mora no planner e exige plano, eixo e outro objetivo com vaga;
- um seguidor com slot no mesmo objetivo vira substituto sem alcançar o prédio
  ([Handoff.cs:97](../Assets/Scripts/Match/AI/2.%20Planner/AIController.PlanEvaluator.Handoff.cs)),
  o que fura o caso C;
- aceita o vizinho do prédio em vez do custo de entrar nele, o que fura o caso E;
- não confere se o substituto já agiu, o que fura o caso F.

O caso D, que tinha resto de dificuldade, foi fechado na Frente 1.

---

## Frente 7 — autoria de campanha e cenas *(autor)*

- **Catálogo de perfis ligado no `AIController` da Batalha**, com o GUID conferido.
  O catálogo trocou a entrada extinta `dificuldade: 4` por `2`.
- **Caixa inicial no Q3 e no Q4**: 2000 para um dos slots em cada.
- **Mixagem:** SFX da Batalha de 0.3 para 1, música para 0.8, música da Tela de
  Entrada para 0.8.
- **Rótulo "ALPHA BUILD v0.3"** na Tela de Entrada, na Campanha e na Batalha.
- **`beachManager` ligado no SectorManager da Batalha.**
- **Fixture reassada:** as 29 construções são as mesmas, só reordenadas. O diff da
  `Fixture.unity` é o cache `sectorInfos` do SectorManager se refazendo.

---

## Frente 8 — o guia de modding *(autor)*

[`docs/modding/guia de modding.md`](modding/guia%20de%20modding.md) é a experiência
do autor com o *Expanded Tournament Brackets* (CK3), transformada em guia de
práticas. A leitura contra o jogo mostrou que boa parte já é a doutrina da casa:

```text
§6 ponto de extensão pequeno     → a skill é uma chave; o alvo é dono da lista
§4 revalidar evento atrasado     → nada vale antes do Neutral
§2 id estável, não posição       → id técnico, dono por slot e não por cor
§5 o que ESTE observador sabe    → FOW honesto, Jornal do Comandante
```

O que vale pegar, em ordem de custo:

1. **Inspetor de save oficial (§11).** Para ler o save desta sessão foi preciso
   descobrir que o `.tmrsave` é um zip, extrair e escrever um script.
2. **Valor efetivo e quem o forneceu (§12).** É a Frente 2 inteira: a tela mostrava
   o configurado, não o que estava valendo.
3. **Resultado com motivo (§2).** O progresso grava dono e rodada, mas não *como*
   a partida acabou.
4. **Para missões, o "Confronto" do guia**: estado, revisão, idempotência e
   política de exceção antes de escolher substituto (§3, §4).

O que **não** pegar agora: migração de save e versionamento de API (§11, §14). O
CLAUDE.md é explícito: nada foi lançado, e não existe save de jogador para
proteger.

---

## Achados que contradizem o que se acreditava

- **O progresso da campanha nunca foi para o disco fora de um save.** O
  `CampaignProgressStore` vive em memória, é zerado a cada Play e a cada Novo Jogo,
  e só persiste dentro do `.tmrsave`. O `RODADAS: 3` "para limpar" existiu só
  naquela sessão. O item do resumo era um fantasma. Sobrou um arquivo órfão de
  versão antiga, `LocalLow/Sistemas Info/The Map Room/CampaignProgress/mundo
  fixture__A_IA.json`, que o código atual não lê.
- **O Q1 é jogável sem caixa inicial.** O save do autor mostra 6000 de renda por
  rodada (HQ 3000 + duas fábricas 1500) entrando antes da primeira compra. A
  "armadilha do turno 2" não vale para quadrante com HQ.
- **O Q3 e o Q4 não têm construção nenhuma**, nem no último commit. O caixa
  autorado neles não compra nada, porque não há fábrica onde comprar. E, sem HQ, os
  dois lados entram no modo rebelde da Frente 4.
- **O Play direto na Batalha roda sem perfil.** O perfil só é resolvido quando a
  dificuldade chega do menu
  ([AIController.Lifecycle.cs:167](../Assets/Scripts/Match/AI/AIController.Lifecycle.cs))
  ou quando um save é carregado. Abrindo a cena no editor, a IA usa os flags
  antigos, e o "médio" que roda é o antigo, sem a blitzkrieg do `AIPreset_Medio`.

---

## O que não terminou

- **Fase 2 dos valores:** os ~70 números do perfil ainda não são lidos. São ~20
  getters do `AIController` (fácil) e ~50 campos públicos do `AIShoppingPlanner`
  (precisam virar campo privado com `FormerlySerializedAs`, mais getter).
- **`poupaPraElite` × `eliteSaveTurns`:** o Difícil tem "poupa" desligado e
  poupança de 2 turnos. Na religação, alguém tem de mandar. A proposta é o toggle
  desligado zerar a poupança, como diz o tooltip.
- **"Perfil em uso" no Inspector** e **resolver o perfil no Play direto**: os dois
  propostos e nenhum feito.
- **Q3 e Q4** precisam de construções (no mínimo HQ e fábrica de cada lado) para
  serem jogáveis.
- **O teste de blitzkrieg** ainda reprova nos casos listados na Frente 6.
- **`Papeis.md` está gravado em Windows-1252**; os outros docs estão em UTF-8. Os
  acentos aparecem quebrados no git.
- **Erro de digitação:** o rótulo da Campanha e da Batalha diz **"Oububro/2026"**.
  A Tela de Entrada diz "Outubro".
- **O `0b`** (mapa A → menu → mapa B, plano nascendo vazio) segue sem ter sido
  exercitado.
- **Jogado:** o autor jogou o Q1 até a rodada 3 com o perfil Médio, pelo menu. O
  resto dos perfis (Fácil, Difícil) e os portões da Frente 1 ainda não foram
  vistos em partida.
