# Contratos de AI: Capturador, Assault, Fire Support

## Versão

`v6.1.5`

## Objetivo

Esta versão é quase toda **doutrina escrita**. O autor ditou os contratos de
comportamento dos papéis principais; cada regra foi conferida no código antes de
virar manual, e marcada com o estado real.

O código que entrou é consequência direta disso: ao escrever o contrato do Fire
Support ficou evidente que a banda do artilheiro está invertida no projeto
inteiro — e a ferramenta que a gente passou cinco dias construindo pintava a
banda errada.

---

## 1. Os quatro contratos

`docs/AI Behavior/` — `Capturador.md`, `Assalto.md`, `FireSupport.md`,
`Transporte.md`. Todos com o mesmo esquema:

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge |
| ❌ | não existe |

A disciplina é a que já custou caro antes: **nota de design só vira manual
depois de conferida no código**. Metade do valor dos arquivos está nos ⚠️ e ❌ —
foi escrevendo que apareceu quase toda a lista de pendências.

### O que a conferência achou de mais relevante

**O antiaéreo inteiro são 4 linhas úteis.** `FireSupport.Antiaereo.cs` é um
filtro de alvo — *"se for antiaéreo, só mira ar"* — e mais dois predicados de
identificação. Nenhuma decisão própria. A tese do autor de que Assault e
Antiaéreo poderiam ser um só arquivo não é aspiração: **do lado do Fire Support
já são**, e `FireSupport.Antiaereo.Combatant.cs` tem cinco linhas, todas
comentário, dizendo exatamente isso.

**O assalto tem comportamento rogue pronto e inalcançável.**
`DecideRogueAssaultBreakerAction` é completo — vacate de alvo de captura, vacate
de produção, ataque breaker, rally — e só é chamado de dentro de
`TryDecideAssaultAction`, que roda dentro do `if (plan != null)` do roteador. É a
mesma forma que a v6.1.2 resolveu no capturador: a lógica existe, o gate a
esconde.

**Três papéis perguntam "a facção tem QG?" onde a pergunta é "esta unidade tem
plano?".** Capturador (transporte de passageiro), assalto (`HQBreaker.cs:69`) e
o próprio transporte. `IsHeadQuarterlessTeam` virou proxy de "sem plano", e a
facção sem QG é só o caso em que 100% das unidades são assim.

**A `IsMaritime()` existe e quase ninguém usa.** É derivada, olha domínio **e**
domínios adicionais — existe porque o hidroavião é aeronave **e** marítimo. Tem
um único consumidor em toda a IA; o assalto identifica naval por
`GetDomain() == Domain.Naval`, que é a conclusão apressada que a derivada existe
para evitar.

**Fragata e submarino seguem o capitão terrestre.** Gambiarra para o jogo em
testes continuar rodando. Toda a lógica de domínio nativo do submarino
(`CanFinishInNativeDomain`, `PodeSubmergirSensor`) vive **dentro** desse fluxo de
perseguição — existe para impedir que ele encalhe enquanto persegue alguém que
não deveria estar perseguindo. Daí a ordem obrigatória registrada no documento:
**M4b → M3 → M4**, senão a regra de não encalhar some junto com a perseguição.

---

## 2. O dialeto militar entrou no `CLAUDE.md`

> "Estamos criando um dialeto e vocabulário militar pra tudo. Em todos os
> contratos eu vou sempre usar Tático, operacional, vanguarda, retaguarda,
> flancos. Foi por isso que passamos 5 dias criando a ferramenta de hotzone."

Seção nova com a tabela de termos — Tático, Operacional, vanguarda, retaguarda,
flancos, âncora, capitão/magnético, camada — e a regra que gera as outras:

> **Banda, âncora e camada são sempre parâmetro da unidade avaliada — nunca
> constante do papel.**

Com a evidência empírica logo abaixo, porque a regra não é estética: toda
regressão do projeto nessas áreas veio de congelar um dos três — raio fixo em
hexes no lugar de banda, âncora fixa no QG no lugar do capturável mais próximo,
camada fixa no ar no lugar da visão especializada. Os três apareceram nesta
sessão.

A Hotzone é o **dicionário executável** desse dialeto: sem ela, "Tático" é
adjetivo; com ela, é resposta computada e conferível.

---

## 3. O artilheiro inverte a banda — contrato e ferramenta

Foi ao escrever o Fire Support que a regra apareceu:

| banda | artilheiro | todo o resto |
|---|---|---|
| **Tactical** (verde) | 0 → alcance máximo da arma | alcance de movimento |
| **Operational** (azul) | **2 × alcance máximo** | movimento do turno seguinte |

Medir banda em movimento é absurdo para quem atira parado: a Artilharia de
Campanha move **1**, então o Operational dela seria o hex 2 — para uma peça cuja
razão de existir é alcançar longe.

**O azul não responde "para onde eu ando"** — responde *"de onde eu posso ser
alcançado, ou alcançar, se a situação mudar"*. É banda de ameaça recíproca, e
nasceu de uma perda concreta:

> Artilharia de alcance 6, tanque rápido a 7 hexes. A peça embarcou para
> reposicionar, o adversário avançou e a pegou desprevenida, indefesa dentro do
> transporte.

Daí a regra: **artilharia de alcance 4 não embarca com inimigo no raio 8.**

O contrato do envelope foi emendado pelo autor — a linha que dizia *"Artilheiro:
sem azul"* virou a tabela acima. A contradição entre dois documentos normativos
deixou de existir; o que sobrou foi código atrasado.

### Código

**`BuildArtilleryBand`** intercepta `Combate + Artilheiro` nas **duas** bandas.
Antes, só a Tactical era interceptada: Combate + Operational caía no caminho
genérico e virava malha de **movimento**. Era isso o caos na ferramenta.

| campo | conteúdo |
|---|---|
| `MovementCells` | disco 0 → raio da banda (`alcance`, ou `2 × alcance` no Operational) |
| `ActionCells` | o que a arma **realmente** atinge parada, pelo sensor |
| `CostByCell` | distância cúbica |
| `OriginByActionCell` | sempre a própria unidade, `EnterCost = 0` — o tiro não custa MP |

**Ferramenta Hotzone:** na subetapa artilheiro o vermelho passa a ser
`ActionCells` — sobrescrevendo o verde — em vez de `OuterCells`, que só mostra o
anel externo. E o teto do degradê deixa de ser orçamento de MP e vira o raio da
banda.

**Efeito colateral que virou recurso:** como o verde é o disco inteiro e o
vermelho é o tiro real, a **zona morta do alcance mínimo aparece sozinha** —
numa arma de mínimo 2, os hexes 0 e 1 ficam verdes e sem vermelho. O ponto cego
que faz artilharia morrer fica visível de graça.

---

## 4. Combate + Terrestre exige arma de alcance mínimo 1

Terrestre é "move e atira", e o tiro pós-movimento colapsa para 1: uma peça de
mínimo 2 não dispara depois de andar. Pedir Terrestre para ela é **pedido
inválido** — mesma natureza de pedir geometria cúbica para unidade de
superfície — e `Build` devolve `null`.

A validação entrou em `IsSubStepValid`, que o `Build` já consulta.

E `GetSubSteps(intent, unit)` — o método que monta o dropdown da ferramenta —
passou a filtrar pelo **mesmo predicado**, em vez de só pela geometria. Antes os
dois divergiam: a janela oferecia a opção e o serviço devolvia nada. Agora a
ferramenta nem oferece, que era o que o próprio contrato prometia.

---

## Verificação

Abrir `Tools > Utils > Hotzone` na Artilharia de Campanha:

- **Combate + Terrestre** não deve aparecer no dropdown;
- **Combate + Artilheiro** deve pintar verde até o alcance máximo, vermelho por
  cima no tiro real, e azul até o dobro;
- o diagnóstico deve trazer
  `artilheiro: alcance máximo=N; banda=2N (2× alcance); disco=…; tiro real=…`.

Nenhuma decisão de IA consome a banda nova ainda: `BuildFireSupportPaths`
continua devolvendo malha de movimento em 11 sítios. A ferramenta vem primeiro
de propósito — dá para conferir a regra com os olhos antes de qualquer
comportamento depender dela.

---

## Pendências

As tabelas vivem nos contratos:

| documento | frentes |
|---|---|
| `docs/AI Behavior/Capturador.md` | C4-C9, R1-R3, T1-T4, A1, D2, L1, E1 |
| `docs/AI Behavior/Assalto.md` | S1-S11, M1-M9 (marinha) |
| `docs/AI Behavior/FireSupport.md` | F1-F13 |
| `docs/AI Behavior/Transporte.md` | zona de largada, não pousar em capturável, esteira |

Destaques que atravessam papéis:

- **F1** — banda do artilheiro no serviço (11 sítios de `BuildFireSupportPaths`);
- **S1** — assalto sem plano entra no papel: destravar, não escrever;
- **C8 / S9** — o gate por facção onde a pergunta é por plano, em três papéis;
- **F2** — observador avançado: iniciativa cedível e retomável no mesmo turno.
  Registrado como **desejo com mecanismo em aberto**, nas palavras do autor.
