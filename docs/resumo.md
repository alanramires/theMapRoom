# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-03, logo depois de fechar a `v7.0.2`.
Leia isto primeiro; ele diz o que ler depois.

---

## Estado

**`v7.0.2` tagueada e no ar.** Cinco frentes num dia só, três delas amarradas
pelo mesmo fio.

A descoberta que organiza o que vem, e que custou três propostas erradas antes
de alguém mandar ler o manual:

> **Uma habilidade não é um poder. É uma chave.** O nome na ficha não faz nada
> sozinho; quem define o que a etiqueta abre é o alvo. A montanha diz "só entra
> quem for alpino"; a construção diz quem a captura.

Três regras do projeto estavam do lado errado dessa frase. Todas voltaram na
v7.0.2. **O teste, antes de acrescentar qualquer campo:** o designer consegue
renomear a etiqueta para qualquer coisa e tudo continua funcionando?

---

## A arquitetura, em cinco linhas

```text
0. sensores PodeX              → a resposta legal            ✅ prontos
1. serviços de área (Hotzone)  → devolvem ÁREA               ✅ prontos
2. consumidores Melhor*        → cruzam, ranqueiam, decidem  ⚠️ 10 existem, 3 faltam
3. papéis                      → só POLÍTICA                 encolhem junto do 2
4. variações de papel          → sem plano, agressivo, jipe  vira PARÂMETRO
```

Faltam **Combate**, **Fusão** e — descoberto na v7.0.2 — **Visão**, que é o
"para onde revelar" hoje escrito três vezes.

---

## O que ler, e nesta ordem

| # | documento | por quê |
|---|---|---|
| 1 | `docs/manual/01_principios_e_vocabulario.md` | **as regras do JOGO**, que a IA só consome. Decide *onde uma regra pode morar*, e isso não se recupera lendo código |
| 2 | `docs/relatorio_v7.0.2.md` | o que acabou de acontecer, incluindo o que não terminou |
| 3 | `docs/refactor/plano_de_trabalho.md` | a fila, ordenada por dependência |
| 4 | `docs/AI Behavior/contrato_envelope_alcance.md` | **norma** das bandas. Inclui a inversão do artilheiro |
| 5 | `docs/magnetic_tabela.md` | quem cada papel acompanha, e o que virou asset |
| 6 | o contrato do papel em que for mexer | `Capturador.md`, `Assalto.md`, `FireSupport.md`, `Transporte.md` |

---

## Onde eu parei

### Melhor Captura — degrau 2.1, quase inteiro

Consumido pelo `CaptureOpportunityClaimService` e pelo `QueroCaronaService`. A
ordem foi invertida: o matching **aloca**, e carona e âncora **leem** a alocação
por `TryGetClaimForUnit`.

**Falta:** 7 varreduras de tabuleiro no `Capturer/`, o `QueroCaronaContext`, e o
`Rebel.cs`.

### Melhor Capitão — nasceu, ninguém consome

Serviço + janela + `AICaptainData` (asset em `DB/AI/AICaptain.asset`). Os quatro
resolvedores antigos continuam mandando.

**Falta:** o tradutor `AICaptainData → List<MelhorCapitaoAttraction>` e os
predicados que ainda não existem (`AliadoFerido`, `AeronaveInimigaDetectada`,
`PontoDeObservacao`…). `ConstrucaoCapturavel` já existe via Melhor Captura.

### O achado que reordena a fila

**O `Rebel.cs` vazou para fora do capturador.**
`FindNearestPlanlessCaptureTarget` é chamado por Transporte (2 sítios), Assalto
(`HQBreaker`) e o rogue do capturador. `IsRebelCapturable` já foi consertado por
dentro na v7.0.2 — delega ao sensor —, mas o nome e os chamadores continuam.

**Ele não é "o passo depois do capturador" — é a ponte para os degraus 4 e 5.**

**Cuidado com o nome:** há duas coisas chamadas "rebelde". A *facção sem QG* é
conceito de jogo e **fica**. O `AIController.Rebel.cs` é controlador paralelo e
**evapora** — e já é só um roteador que chama
`TryDecideCapturerAction(plan: null)`.

### Critério de aceite, inalterado

> Um `UnitData` novo com a skill de captura — o "jipe capturador" — passa a
> capturar **sem uma linha de IA escrita para ele**. Há `jeep.png` e
> `soldado_jetpack.png` no repo esperando o teste.

Depois da v7.0.2 o teste tem um segundo passo: dar a skill à unidade **e**
listar a skill em `Required Skills To Capture` da construção.

---

## Regras de trabalho (não são sugestão)

- **Uma classe por vez.** Você mexe, o autor compila e roda no jogo, e comita
  antes da próxima. **Não emenda fases.**
- **Verificar antes de documentar.** E **busca vazia não prova ausência**.
- **Ler o manual antes de decidir onde uma regra mora.** Custou três propostas
  erradas na v7.0.2.
- **Tem relatório, tem tag. Não tem relatório, é só commit.**
- **Medir antes de otimizar.** Ler código não acha gargalo.
- **Não editar `.asset` no disco com o inspector aberto.**

### O ritual de encerrar o dia

Ordem fixa, do autor:

1. escrever `docs/relatorio_vX.Y.Z.md` e a linha no `CHANGELOG.md`
2. **commits separados por frente de trabalho** — não um commit único do lote
3. `git add .` no que sobrou (churn do Editor), com mensagem dizendo que é churn
4. criar a tag `vX.Y.Z` e `push` do commit e da tag
5. atualizar este arquivo

O passo 2 é o mais novo. O ganho é reverter uma frente sem tocar nas outras: na
v7.0.2 foram seis commits (3a, Melhor Capitão, chave de captura, desembarque,
replay, relatório), e qualquer um deles volta sozinho.

---

## Armadilhas que já custaram tempo

| armadilha | lição |
|---|---|
| **projetar sem ler o manual** | o `CLAUDE.md` não apontava `docs/manual/` e uma sessão nova projetou três arquiteturas contra o princípio da primeira página. Agora aponta |
| **skill que se declara** | `canCaptureConstructions` era a única regra do projeto em que a etiqueta tinha poder. Se renomear quebra, o poder está no lugar errado |
| **ferramenta que elege por GUID** | a janela do PodeCapturar varria assets e pegava "o primeiro com a flag". Com duas skills, ela reprovaria o que o jogo aprova |
| **otimizar por hipótese** | cortei 80% das chamadas ao sensor e o tempo **não se mexeu**. O custo estava nos 16 envelopes do claim service |
| **comparar rodadas incomparáveis** | pós-load a IA reembaralha a ordem e o cache está frio. Só compare com o mesmo save e mesma ordem |
| **`FrameSpike` com F11** | mede o frame inteiro, incluindo o input humano. Use `decision=` da linha `[AI Perf][Unit]` |
| **uma função, duas perguntas opostas** | `CollectCaptureCandidates` serve escolha de alvo e fome estrutural. Parâmetro obrigatório, sem default |
| **`FindObjectsByType` dentro de laço** | `GetConstructionAtCell` varre a cena por chamada. Se o chamador já tem o objeto, passe-o |
| **rota é cara** | 12-16ms por pathfind naval, e já produziu 71 s numa decisão. A cúbica é limite inferior — dá para podar exato |
| **`git add .`** | varre trabalho do Editor junto. Não é erro, mas confira o que entrou |
| **predicado no eixo errado** | `TeamId == unit.TeamId` é **time**, não slot — e apagava a reconquista em quatro papéis |

---

## Aquecimento barato

| # | tarefa | estado |
|---|---|---|
| L1 | apagar `AIController.Transportador.Courier.Attack.cs` — sem chamador | ainda de pé |
| L2 | `MelhorEstoqueService` é consumido? | ✅ é — `Stock.cs:189` e `Logistics.Restock.cs:44` |
| L3 | T3 do `Transporte.md` — `RepresentativeCell` com desembarque de distância zero | não conferido |
| L4 | rodar `Tools > AI > Auditar Chaves de Captura` de vez em quando | pega prédio capturável sem chave, que some do jogo em silêncio |

---

## Trilha paralela — Naval

Ordem **obrigatória**: `M4b → M3 → M4`. **Não rodar junto do degrau 4** — as duas
mexem em âncora.

Falta escrever o **magnético naval** no `governanca_entre_papeis.md` §2.3. E a
`docs/magnetic_tabela.md` já registra o caso que ele precisa: vigilância naval
não tem prédio embaixo d'água, então a referência vem do `MelhorVisão` como
célula de fronteira.

---

## Aviso

Lista grande, organizada e marcada **parece progresso**. O antídoto é o ritmo
acima.

O teste final continua sendo um só: **os 7 perfis chamando uma fonte única, não
7 perfis com 7 definições diferentes.**
