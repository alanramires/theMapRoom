# Vigilância — doutrina

Doutrina definida pelo autor em 2026-08-06. Onde o código divergir dela, o código
está errado.

> **Tem uma guerra acontecendo em algum lugar e eu não me importo, contanto que eu
> ache minha presa.**

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |
| ❓ | não conferido |

**Subpapéis:** Vigilância Aérea, Anti-Sub.

---

## 1. A prioridade

```text
Detectar, Mirar, Reposicionar, Suprir, Transferir,
Desembarcar, Embarcar, Capturar, Fundir, Enxergar
```

**`Detectar` em 1º e `Enxergar` em 10º — o último.** É a separação mais extrema das duas
verdades em todo o projeto: este papel vive de **contato** e é praticamente
indiferente a **hexágono**.

> *"Não ligam pra mergulhar na névoa preta — detectar é mais importante do que
> saber o que está ali."*

**`Mirar` em 2º, mas só para quem pode agir na mesma rodada.** EWACS e Radar
Móvel **pulam** o sensor.

**Modalidade híbrida.** Todas as unidades de vigilância seguem a linha do
Artilheiro Combatente: **primeiro fogo de suporte, depois assalto**. É o terceiro
usuário da modalidade (ver `ficha_do_papel.md` §7.8).

**`Embarcar` logo depois de `Desembarcar`** — o par anda junto, e existe de
verdade: EWACS e Super Tucano em porta-aviões. Abaixo dele ficam as três casas
fracas deste papel: `Capturar`, `Fundir` e `Enxergar`.

---

## 2. Detectar ≠ Enxergar — e aqui a diferença é REGRA DE JOGO

```text
detecção de vigilância    6+ hexes, MESMO sobre hexes não descobertos
visão tradicional         1 a 4 hexes
```

A unidade detectada aparece **desfocada por cima da névoa**: o jogador sabe
**onde ela está**, mas não o que está embaixo nem **quem mais** está embaixo.

É o quadrante *"contato detectado + hex preto"* da doutrina das duas verdades
(`CLAUDE.md`), agora com consequência visual declarada.

**Quem faz isso:** EWACS e Radar Móvel (vigilância aérea) procurando F-22 e B-2;
Super Tucano (patrulha naval), Fragata ASW e o **próprio submarino** procurando
outros submarinos.

---

## 3. Chave e fechadura

A doutrina da chave, literal — o **alvo** carrega a etiqueta, o **caçador** lista
qual etiqueta enxerga:

| quem se esconde | etiqueta | quem acha | lista |
|---|---|---|---|
| caças e bombardeiros | `AR Stealth` | sensores | `Aeronave Furtiva` |
| submarinos | `Sub Ops` | sonoboias | `Detect Sub` |

✅ O maquinário existe: `UnitStealthSkillRule` no lado de quem esconde e
`detectUnitsWithFollowingSkills` no lado de quem procura.

---

## 4. Posicionamento — e ele bifurca por subpapel

**Não há um posicionamento do papel.** Há dois, opostos.

### 4.1 Vigilância aérea — REPELE, e a cobertura envelhece

> *"As unidades de vigilância aérea se repelem no tabuleiro a fim de ocupar a
> maior área detectável, que vai **degradando com o tempo**, forçando o
> patrulhamento. Dois radares móveis comprados juntos: um vai para o norte, outro
> para o sul."*

Vale entre **Radar↔Radar, EWACS↔EWACS e EWACS↔Radar**.

**A degradação é o ledger de idade** de `contrato_recencia_de_cobertura.md` —
desenhado no mesmo dia, e aqui declarado como **regra de posicionamento** e não
só como serviço. ❌ Não existe no código.

### 4.2 Anti-sub — AGRUPA, e a âncora é o leito

> *"As unidades anti-sub andam em grupos, ao contrário da vigilância aérea. O que
> as atrai é **o leito do fundo do oceano ou dos canais**. Elas não se importam
> com a guerra que acontece na superfície — é possível ver 2 subs e 1 fragata
> navegando juntos. O oponente vai com 1 sub achando que é uma presa e não faz
> ideia que tem um Super Tucano por perto."*

✅ **Fecha o `ABERTO` do `contrato_recencia_de_cobertura.md` §8**: a âncora da
patrulha naval é o **corredor** (leito, canais), com a idade por cima.

### 4.3 Magnético — só quem é conservador tem

> *"Algumas unidades têm `play conservative` e outras não. As que têm procuram a
> retaguarda como um fogo de suporte faria. **As que não têm não têm magnético** —
> só se forem alocadas em algum plano."*

✅ Confere com o censo do turno 1: EWACS e Radar Móvel têm `playConservative`;
submarino, fragata e Super Tucano não. E o EWACS usa `FollowMagnet` com o
capturador como ímã.

⚠️ **Confundimento ao testar:** hoje *"é aérea?"* e *"tem playConservative?"* dão a
mesma resposta para toda unidade de vigilância. Política construída sobre o flag
passa pelo motivo errado.

---

## 5. Ataque em vantagem — e o preço de atirar

> *"As unidades furtivas aéreas podem **ignorar combates no caminho** até seus
> objetivos se não estiverem em vantagem numérica ou oportunística. **Atacar é
> revelar a posição para todos por X rodadas.**"*

❓ Não conferido. Existe precedente na mecânica de emersão forçada do submarino
(lock pendente, tempo revelado) — ver `project_pending_forced_layer` —, mas o
equivalente aéreo não foi procurado.

---

## 6. A moeda: a área coberta, e a idade dela

> *"Não se fundem — **menos cobertura do tabuleiro**."*

| papel | onde o valor mora | fundir |
|---|---|---|
| Capturador | o corpo — HP é a taxa | **ganha** |
| Transportador | as vagas | perde |
| Assalto | a arma | perde |
| Fogo de Suporte | a formação (cones cruzados) | perde, e agrupar também |
| **Vigilância** | **a área coberta e a idade dela** | perde |

A quinta moeda, e a única com **duas geometrias opostas para o mesmo valor**:
espalhar maximiza área (aérea); agrupar protege quem caça a presa (anti-sub).

---

## 7. O resto quase não acontece

**Suprir e Transferir** acontecem de verdade: suprem **embarcados** e obtêm
estoque — *"a Fragata transporta Apaches"* — e os **liberam quando curados**
(desembarque). É o modo Hospital do transporte, aqui como função secundária.

**Raramente capturam.** E embarcar é caso raro — mas quando acontece, o par
`Desembarcar`/`Embarcar` anda junto, acima das três casas fracas.

---

## 8. Leituras

| documento | por quê |
|---|---|
| `contrato_recencia_de_cobertura.md` | o ledger de idade, a bifurcação aérea × naval, e o valor de N ainda ABERTO |
| `ficha_do_papel.md` §7.8 | o quadro canônico dos papéis e as três modalidades |
| `CLAUDE.md`, "As duas verdades" | por que `Detectar` em 1º e `Enxergar` em 10º não é contradição |
| `FireSupport.md` | a modalidade híbrida, que este papel também usa |
