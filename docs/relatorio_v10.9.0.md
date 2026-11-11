# v10.9.0 — A conta, a vaga e o gate falam a mesma língua

2026-11-11. Fecha D1, D2 e D3 do rally: a matriz de Forças, a prontidão sem artilharia
obrigatória e o catálogo de papéis como asset. Nada disto foi visto em Play além de duas
fotos de Golf no turno 11; tudo o mais é "só compilou + fixture".

## O fio do dia

A marca do AI Step de 2026-11-10 em Golf ("consegue ver onde estamos?") mostrou o rally
preso em `ASSEMBLING_WAIT_ART` com 33 mil parados. O autor tinha dito, mais de uma vez, que
"o rally pede 2 assaltos e 4 capturadores e artilharia é OPCIONAL", e a proposta de
`docs/rally/rally_proposta.md` registrava isso desde P1–P9. O código exigia 3 de artilharia
(`RallyMinimumArtillery`) e num mapa de ilhas sem artilharia esperava para sempre.

O erro se repetiu três vezes no mesmo dia, em roupas diferentes, e é ele que dá nome à versão:

| onde | a conta dizia | o gate ou a vaga dizia |
|---|---|---|
| prontidão | doutrina: artilharia opcional | `hasArtillery` obrigatória |
| vaga do rally | Caça B conta ruptura (Força) | vaga pede `UnitRole.Assalto`, caça não preenche |
| presença aérea | Apache no hangar "está no core" (rally no Tático) | o Caça colado em Golf está em Approach |

A regra que sai disto está nas Regras de trabalho do `resumo.md`: **quem gateia, quem conta e
quem abre vaga leem a mesma régua, no mesmo dono**. Quando divergem, a IA espera uma coisa que
a outra metade já entregou.

## Tela `ai show rally` (`437d946`, `4bac5b0`, `57dee04`, `00c221a`)

Antes de mexer na prontidão, o autor pediu para **ver**: Core (Tático do modelo), Approach
(Operacional), Vanguarda e Retaguarda pelo `AIBacklineAnalyzer`, participantes por
Core/Approach/Fora com destino previsto, demanda do objetivo por Força e barras de
prontidão lidas de `EvaluateRallyReadiness`. `QuadranteData.rallyModelLand/Naval` no Map
Helper. Refresh no `OnBoardSettledAtNeutral`, com force.

Duas correções de doutrina saíram da tela, pelo autor: a **vanguarda não é recortada pelo
approach do modelo terrestre** (caças sobre o mar estão certos, "as forças de ruptura têm que
estar na frente do rally point"), e a formação segue a analogia do WoW: **Ruptura na
vanguarda e flancos; Dissuasão, Ocupação e Apoio na retaguarda**. Eu tinha chamado a
vanguarda sobre o mar de bug; estava errado.

## D1 — Matriz de Forças (`e8b6676`)

Quatro Forças por cima das seis famílias, enum derivado, nunca serializado: Ruptura = Assalto;
Dissuasão = Fogo de Suporte; Ocupação = Capturador + Transportador; Apoio = Vigilância +
Logística. **Bombardeiro, Obus Leve e Tanque Pesado (o antigo Tanque Z) migram para Fogo de
Suporte**: "preferem atirar de longe primeiro, só partindo pra porrada depois". Consequência
aceita e medida: o pacote 2/2/1 passa a comprar Assalto de verdade (4 Soldados + Z pede
Assalto×2/Fogo×0; + Tanque B pede ×1/×1), a progressão de elite do Assalto para no MBT e o
Pesado divide a demanda de Fogo elite 2 com a Artilharia de Campanha via `eliteFrom`.

Apache **fica** Assalto: a essência dele é Disruptor de Captura, não Ataque ao Solo. Eu propus
um sub-rótulo e o autor derrubou: "a gente tirou o Apache do ataque aéreo, ele já é disruptor".

`Tools > Role Families` ganhou a visualização por grupo de combate e deixou de listar a ficha
debaixo dos papéis secundários: "só o papel principal determina em que nó da árvore ele
aparece".

## D2 — Prontidão por Forças (`43c0f51`, `53dce5f`, `0e1e1c6`)

Plano em `docs/rally/d2_prontidao_plano.md`, com a nota naval e a matriz por tipo de mapa.
Dono preservado: `EvaluateRallyReadiness`.

- **Piso fixo** 4 capturadores + ruptura ≥ 2, por peso (Tanque 1, MBT 2,5, Apache/caças 1).
  Suporte soma força e **nunca gateia**: `hasArtillery`, `RallyMinimumArtillery`,
  `overwhelmingBreakthrough` e `ASSEMBLING_WAIT_ART` morreram. `RequiredPackages =
  KnownEnemyForce/8` morreu com eles.
- **Inimigo local** pela névoa confirmada do slot, no sentido do eixo, com razão por perfil
  (`AIPresetData.rallyLocalRatio`: Fácil 1,2, Médio 1,5, Difícil 2,0). Sem inimigo local, o
  piso basta.
- **Presença pela banda da própria unidade**, não por raio fixo: os cinco raios
  (`RallyAssemblyForceRadius` e irmãos) morreram. **Embarcado conta como carga de Ocupação**,
  uma vez, pela banda do transporte; "ruptura sempre vai ter (blindados), porém quando eles
  embarcam, eles passam a ser a equipe de ocupação".
- **Saturação** no lugar do timeout: 2 turnos sem crescer de verdade (+1 ruptura ou +2 força)
  ou 4 desde o piso. Só com o piso cumprido **e** o rally held: a primeira versão começava o
  relógio com o prédio ainda inimigo e disparava GO_GREEN(saturado) no turno da captura.
- **Capitão** persistido no GoGreen (`RallyCaptainId`, `RallyAdvances` sobrevive ao save);
  fire support, magnético e monitor leem dele. **ESPERANDO**: o capitão para quando um
  participante não o tem no Tático; tenta tiro parado antes (helper do Hospital, renomeado
  `TryBuildStationaryDefensiveAttack`); participante sem o capitão no **Operacional** é solto
  do grupo em vez de congelar a invasão para sempre.
- **Tela aérea** na LZ: Ruptura aérea apta cujo Tático alcança a LZ conta como blindado.

### O que a foto de Golf ensinou (a terceira linha da tabela)

Com a D2 em Play, turno 11: Força 8/52 e Apache recém-comprado na base contado no core, Caça
B colado em Golf em Approach. "Rally no meu Tático" mede **alcance**, e o alcance de uma
aeronave cobre meio mapa; a união dos Operacionais dos participantes esticava o "inimigo
local" até a base inimiga. Decisão do autor: **presença aérea é pela célula na área do rally**
(Core ∪ Vanguarda no Tático, Approach no Operacional), terrestre segue pela banda; **inimigo
local é o Approach do modelo terrestre**. `IsRallyParticipantPresent` é o dono;
`IsRallyParticipantInBand` ficou só para o que é relativo ao capitão. A área é uma por rally e
revisão (`GetRallyArea`), e a tela lê o mesmo cache: tela e planner não podem mais discordar.

## D3 — Catálogo de papéis como asset (sem commit até este fechamento)

Decidido na `pergunte-me` da tarde. `CanSatisfy(Interceptador, Assalto)` era falso num
`switch`, então a vaga de ruptura não aceitava o caça que a conta já somava, e o Shopping
seguia comprando blindado. "Se um dia eu quiser que caças sejam dissuasão é só eu mesmo mudar
a flag."

- `UnitRoleCatalog` (`Assets/DB/Resources/UnitRoleCatalog.asset`), uma linha por `UnitRole`:
  família, Força, participação, "conta como", "satisfaz também". Os cinco `switch` de
  `UnitRole.cs` morreram **sem fallback**; ficaram em código a capacidade mecânica
  (`isTransporter`/`isSupplier`) e `MatchesLabel`, que é identidade de nome. A Força é por
  rótulo agora.
- Transcrição única por `tools/ai_test/criar_catalogo_papeis.cs`; o asset nasceu igual à tabela.
- **Vaga de ruptura por Força** em `EnsureRallyRoleSlots(byForce: true)`: Caça B recrutado
  como Interceptador cobre uma das duas vagas. Capturador segue por rótulo, senão Transportador
  (Ocupação) cobriria vaga de captura.
- `Tools > Role Families` virou editor do asset. Visto pelo autor: edita, Ctrl+Z volta.
- `TransportadorAereo` (15) caiu do enum: zero leitores, nenhuma ficha, G17 já o dava por
  incorporado. Valor reservado no comentário, como o 11.

Decidido junto, não codado: Caça A conta ruptura com peso 1 (é a superioridade aérea que a
invasão por ilha precisa); núcleo 2/2/1 continua por família, o caça entra pela flag; **D4 =
formação ativa** só depois do Play da D2.

## Outras

- `docs/aprenda unity/`: dois guias para terceiros, como montar a skill `pergunte-me` e como
  conectar ao CLI/MCP da Unity (winget, auth login, pipeline install, status; nunca recompile
  em Play; fixtures por `--file`; PowerShell para JSON porque o Python da máquina quebra em
  `import json`).
- Memórias: matriz de Forças, formação, doutrina naval do degrau 7.

## O que não terminou

1. **Play da D2** além das duas fotos: GO_GREEN em Golf, `ESPERANDO`, `solta retardatário`,
   capitão retomando. `ai show rally 1` no save do slot 3, turno 11.
2. **Caça como porradeiro** depende de o autor marcar `Assalto` em "satisfaz também" de
   `Interceptador`/`InterceptadorDeDisruptor` no asset; até lá a vaga de ruptura em Golf segue
   pedindo blindado.
3. **D4** (formação ativa por Força usando a `RallyArea`) e **degrau 7** (rally naval: core 4
   fogo de suporte naval + 4 embarcados ou 2 transportes carregados; navios bombardeiam e
   esperam; artilharia só depois da praia consolidada).
4. **Quem segura o capitão** no ESPERANDO: hoje todo participante, inclusive logística e
   vigilância; o montinho anda no passo do mais lento. Pergunta aberta.
5. Herdados da v10.8.0 e v10.7.0: nada foi visto em Play ainda.

## Docs de doutrina

Desatualizados (apontados, não reescritos):

- **Novo:** `docs/AI Behavior/Forcas.md` não existe; a matriz vive no canvas do autor, em
  `docs/rally/d2_prontidao_plano.md` e na memória. Alvo da `consolidar-doutrina`.
- `docs/rally/rally_proposta.md`: tabela "o que muda" marcada pela D2; o resto da proposta
  segue como proposta.
- `docs/AI Behavior/contrato_questionario.md` §7.1 e `ficha_do_papel.md` §7.7 e linha 376:
  família e composição vêm do `UnitRoleCatalog`, não de tabela em código;
  `TransportadorAereo` não existe mais. Só compilou.
- `docs/AI Behavior/governanca.md` §G17: o rótulo 15 caiu de vez.
- `docs/AI Behavior/Papeis.md`, `Assalto.md`, `FireSupport.md`: Bombardeiro, Obus Leve e
  Tanque Pesado são Fogo de Suporte; formação Ruptura vanguarda/flancos, resto retaguarda.
  Só compilou.
- `docs/AI Behavior/Capturador.md` e `Transporte.md`: presença no rally pela banda própria
  (terrestre) ou pela área (aéreo); embarcado é Ocupação; hold de embarque pelo Tático. Só compilou.
- Carregados da v10.8.0, sem mudança: `governanca_entre_papeis.md` §2.3, `Capturador.md` R1 e
  §10, `Assalto.md` S1/S9, `contrato_missao_captura.md`, `Transporte.md` (infiltração),
  `docs/rascunhos/AI Shopping.md`, `docs/Planos/Reformular Shopping.md`, `docs/manual/` (regras
  de partida). Carregados da v10.7.0: `Transporte.md`, `Assalto.md`/`Papeis.md`/
  `magnetic_tabela.md`, `Logistica.md`, `Capturador.md`, `Vigilancia.md`, `FireSupport.md`.

Prontos para consolidar:

- Nenhum pelos três sinais. A matriz de Forças é a candidata mais próxima: o autor a validou
  no canvas e na janela, mas a regra mudou neste fechamento e não foi vista em partida.
