# v10.3.0 — Não precisa empatar, precisa não perder

*2026-11-06/07 · Peixe Pequeno (Médio e Difícil) · 10 commits desde a v10.2.0*

O fio do dia: a IA comprava por **igualdade** — a cobertura tinha de empatar a força
inimiga, o Médio planejava igual ao Fácil, e o "Interceptador" era um nome só para dois
trabalhos. Cada marca do autor trocou uma igualdade por uma **régua**: anel, tolerância,
presa em primário, capturador por prédio.

---

## 1. Capturadores por prédio — o Médio deixa de ser o Fácil

`f9c3222`. A demanda por setor era `ceil(prédios/2)` entre 1 e 4, e o Difícil dobrava
**depois** de arredondar: 2 prédios davam 2 no Hard, não 4. O campo
`capturerSlotsPerSectorMultiplier` do preset Médio valia 1 e **não tinha leitor** — o Médio
planejava exatamente como o Fácil. Era isso que o autor sentia como "IA média fraquinha".

A escala é do autor, em dois números do perfil (`AIPresetData.plano`):

```text
capturadoresPorPredio     Fácil 0.5   Médio 1   Difícil 1
reforcoPrimeirosPredios   +1 nos N primeiros (Difícil 2)
capturerSlotCap           teto por setor, agora lido (4; Difícil 6)

Fácil 1, 1, 2, 2      Médio 1, 2, 3, 4      Difícil 2, 4, 5, 6
```

Discussão que decidiu o desenho: "prédios por capturador" confundia — o autor pensa em
*capturadores por prédio*. E o autor reconheceu na escala uma tática do AWBW: lotar de
ocupação e dinheiro rápido e atrasar o elite. O "1º elite = MBT" não briga com isso: só
poupa depois da massa (`MinCapturerMassForSupport`) e nem liga em ilha sem rota terrestre.

Em `Defensive` o reforço desliga (o Difícil pede como o Médio): a massa do recrutamento
forçado fica para defender, não sai capturando retaguarda.

**Não mudou de propósito:** a invasão da base (eixo 4) tem regra própria — 1 por prédio, sem
teto, dobrada no Difícil. O autor temia que a escala quebrasse a invasão; não toca nela.

*Só compilou.*

## 2. O teto de conscrição contava menos gente que o HUD

`9870971`. **Achado:** o autor achou que o jogo ainda lia o teto antigo (55%). O valor 50% da
cena estava valendo — o erro era a **contagem**. A válvula usava `snapshot.MyUnits`, que deixa
os embarcados de fora; o limite do jogo e o HUD contam todos. Com 4 Chinooks lotados (a festa
da carona), a válvula via bem menos gente: HUD 32/60, imposto de $4000 ainda cobrado.

O autor também corrigiu o nome: **massacre é a vantagem de 2:1**; o teto é só o exército
cheio. Os dois desligam a conscrição, mas o log agora diz qual foi.

*Visto em Play:* `TETO DE CONSCRIÇÃO (32/60 >= 50%)`.

## 3. A escolta que cabe é comprada antes do carrinho

`d8b08ee`. Com o imposto zerado, o Caça B de $16000 cabia nos $18500 e mesmo assim o carrinho
levou Apache + Chinook + Suprimentos + 3 Soldados. O beam soma atendimentos e prefere
largura a profundidade. O precedente já existia (a pré-compra do 1º MBT); virou
`TryPreCommitDemand`, genérico, e a escolta/disruptor que cabe sai antes do carrinho.

*Visto em Play:* `PRÉ-COMPRA garantida (escolta) Caça B $16000`.

## 4. O contra-ataque saiu da corrida armamentista

`cb35d08`. O autor chamou de efeito **Pernalonga e Gaguinho**: faca, machete, espada,
machado… Qualquer saldo acima de 0,05 pedia compra, então a cobertura tinha de **igualar** a
força inimiga. No T7: 8,1 contra 4,9, com **23 de 27 inimigos em outra ilha**, virava 2
Soldados "urgentes" que não alcançam ninguém.

Três regras, uma pergunta só (`CounterCategoryWantsResponse`):

- **outra ilha** — com ≥ 50% da pressão fora da nossa terra e resposta aérea/naval à venda,
  a terrestre sai da demanda. Sem ar no roster (Feijão Torto), a terrestre segue valendo.
  *Visto em Play.*
- **anel** — a mesma régua do antiaéreo: só é urgente quem alcança algo nosso. Para terra o
  anel é o **Operacional** do inimigo: no Feijão Torto (corredor sem ar) o Tático só virava
  urgência depois do primeiro choque, e a resposta nasce na base. *Só compilou.*
- **tolerância** — fora do anel, cobertura ≥ 60% não pede compra. *Só compilou.*

A artilharia da composição cedia a vaga ao counter com a régua velha; agora cede só quando
ele vai mesmo comprar. Sem isso, com 60% de cobertura, não saía nem counter nem artilharia.

## 5. A força aérea ganhou dois ramos

`c35dc13`, `1d94534`. Doutrina do autor:

```text
ramo 1  poder de captura (no chão ou embarcado) < Apache < Caça B < Caça A
ramo 2  bombardeiro < Caça B (alternativa barata) < Caça A
```

Primeiro proponho resolver só com prioridade de alvo, sem subpapel. O autor mostrou o
precedente que eu tinha esquecido: o Apache já saíra de Ataque Aéreo para virar
`DisruptorDeCaptura`, sub-rótulo com `MatchesLabel`. Pelo mesmo molde nasceu
`InterceptadorDeDisruptor` (17), sub-rótulo de `Interceptador`. **A lição do Apache foi
repetida de propósito:** os `roles[0] == Interceptador` estritos foram trocados por
`MatchesLabel`/`CanSatisfy` antes de o rótulo existir em ficha nenhuma.

- a escolta pede o Caça B pelo papel principal — Caça A de $26000 não caça Apache;
- o disruptor de linha exige papel principal Disruptor. **Achado:** com o Disruptor
  secundário nos caças, a demanda os aceitava e `CountActiveDisruptors` não os contava —
  loop de compra que nunca fecharia.

As fichas são autoria do autor e mudaram duas vezes no dia (o Disruptor secundário entrou nos
caças e saiu). Estado final: Caça B `[InterceptadorDeDisruptor, Interceptador]`, Caça A
`[Interceptador, InterceptadorDeDisruptor]`, Caça F `[Interceptador]`, Bombardeiro
`[AtaqueAereo, Disruptor]`, BF `[AtaqueAereo]`, Apache sem mudança. *Só compilou.*

## 6. O anel antiaéreo pede pela presa

`2e5b04b`. O anel pedia antiaéreo pela contagem e alternava AAA/SAM pela contagem nossa. Agora
conta o anel por classe e pede a peça que tem aquela presa, pela escada da ficha: primário à
venda (bombardeiro → SAM, Apache → AAA), senão secundário (só Fábrica Leve: AAA contra
bombardeiro — troca hit-kill que ela ganha no dinheiro), senão a regra antiga, agora só entre
o que o mapa vende. **Achado ao testar o caso do autor:** com 1 AAA e nenhum SAM à venda, a
regra antiga pedia SAM urgente — que nunca fecharia.

As fichas da AAA (Heli PRI) e do SAM (Plane PRI, a entrada Heli duplicada saiu) são do autor.
Um primário por classe é o que evita o empate decidido pelo preço — que nunca compraria o SAM,
como o autor lembrou.

*Só compilou.*

## 7. Furtivo não se revela por alvo pequeno — campo esperando

`336f57b`. `UnitData > Combat Behavior > aiAttackOnlyListedTargets`, ligado no Caça F e no BF,
**sem leitor**, a pedido do autor: as unidades nem foram testadas. Hoje classe fora da lista vale
Terciária, e o "ar primeiro" atira nela quando não há aeronave ao alcance. O tooltip responde
o "ué, revelou por quê?" quando vier. É campo da ficha, não derivado de furtividade: a skill
é chave, não poder.

## 8. Shopping Pressure parou de recalcular por segundo

`027fb33`. A janela refazia a demanda inteira a cada 1 s, e a linha "ameaça aérea" ignorava o
`log:false` — daí o loop no Console. Agora recalcula por mudança de turno, caixa ou unidades,
com 10 s de rede.

## 9. Entrega do helicóptero dentro da faixa do passageiro

`f694a24` (outra sessão; lido no diff). A LZ do courier aéreo fica no Tático do passageiro
principal a partir do alvo, e "bloqueado" deixou de alargar a zona de desembarque — antes,
travado, ele largava a carga em qualquer lugar. A célula de parada do helicóptero passa pela
mesma autoridade de camada da Fase 2. *Não verifiquei em Play.*

---

## Hipóteses minhas que caíram

- **"Teto 55% velho no save":** não era configuração, era contagem sem embarcados.
- **"Subpapel é poder no lugar errado":** defendi resolver Caça A × Caça B só com prioridade de
  alvo. Para *quem atirar primeiro*, vale; para *qual vaga a compra pede*, o autor estava certo —
  e o precedente do Disruptor já estava no código.
- **Antiaéreo zerado no Shopping Pressure não era bug:** é saldo (força − cobertura), e as
  Bazookas contam como cobertura antiaérea.

## O que não terminou

- **Anel Operacional do counter: custo não medido.** Um envelope encadeado por inimigo visível
  na compra, com cache por inimigo/célula/turno. Se a vez da IA ficar lenta no Feijão Torto,
  começar por aqui.
- **Freio de conscrição pela fila de carona** (passo 4 da v10.2.0) — continua fora.
- **Demanda "bombardeiro inimigo → Caça A"** — o ramo 2 só tem a resposta do chão (SAM/AAA);
  no ar, o Caça A ainda vem só pela progressão e pelas operações.
- **`aiAttackOnlyListedTargets` sem leitor**, de propósito.
- **SAM à venda mas bloqueado pela cadeia de elite:** a demanda urgente nasceria e não fecharia.
  Não verificado.
- **Secundário `InterceptadorDeDisruptor` do Caça A sem leitor** — só documenta a intenção.
- **Batalha** com `isAI` 1 nos dois slots e `hardMode` 1 (autoria do autor, commit próprio):
  lembrar ao publicar.

## Docs de doutrina

Desatualizados
- `Assets/Scripts/Match/AI/3. Shopping/Shopping.md` §6 — counter: anel Operacional, tolerância
  60% e outra ilha (**outra ilha vista**; anel e tolerância **só compilou**); anel antiaéreo por
  presa com escada primário/secundário (**só compilou**); pré-compra da escolta (**visto**);
  escolta por `InterceptadorDeDisruptor` e disruptor por papel principal (**só compilou**);
  teto conta embarcados (**visto**). Carregado da v10.2.0: escalada da AA, Chinook fora do anel,
  rompimento no tamanho da demanda, escolta que segura o caixa, folga 55/45, piso da fila de
  carona, imposto devolve a parcela.
- `docs/AI Behavior/Papeis.md`, `ficha_do_papel.md` (§ subpapéis de Assalto, linhas ~347 e
  ~478), `Assalto.md` (§ modalidade, ~594-611) e `Vigilancia.md` (~22, ~58) — Interceptador
  dividido em dois; Caça F e BF sem Disruptor (**só compilou**). Carregado: família Disruptor,
  negação de escolta.
- Escala de capturadores por prédio — **nenhum doc de doutrina descreve a fórmula de slots por
  setor** (procurei por "por setor", "slots de capturador", "dobra no Hard"). Lugar natural:
  `docs/AI Behavior/Capturador.md`. (**só compilou**)
- `docs/AI Behavior/Transporte.md` — entrega do helicóptero na faixa do passageiro (f694a24,
  não verificado). Carregado: fila de carona como piso de Chinook, ganância no desembarque,
  corrida no MelhorEmbarque, LZ de Chinook pousado.
- Carregados sem mudança: `docs/AI Behavior/Capturador.md` (swap pela régua `RodadasAteFechar`,
  blitz, espera fora da produtora), `docs/AI Behavior/FireSupport.md` (AA rogue olha alvo
  conhecido), `docs/magnetic_tabela.md`.

Prontos para consolidar
- Nenhum. O shopping e os papéis aéreos mexeram de novo hoje; o swap do Capturador tem um só
  fechamento de estabilidade e ainda não foi registrado como visto em Play.
