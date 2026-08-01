# Fire Support — doutrina

Doutrina definida pelo autor. Onde o código divergir dela, o código está errado.

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |

---

## 0. Tactical e Operational são **exceção** aqui

Para fire support, a banda **não é movimento**:

| banda | fire support | todo o resto |
|---|---|---|
| **Tactical** | **alcance da arma** | alcance de movimento |
| **Operational** | **o dobro do alcance da arma** | movimento do turno seguinte |

Motivo: a Artilharia de Campanha move **1**. Se o Operational dela fosse
movimento, seria o hex 2 — ridículo para uma peça cuja razão de existir é
alcançar longe.

✅ **O contrato do envelope foi emendado pelo autor** e agora diz o mesmo:
`Artilheiro` devolve verde do hex 0 até o alcance máximo (com o vermelho
sobrescrevendo) e azul no dobro do alcance máximo. Ver
`docs/contrato_envelope_alcance.md`, seção *"Artilheiro inverte a banda"*.

⚠️ **O código não faz nem o antigo nem o novo.** `BuildFireSupportPaths` —
consumido por 11 sítios — devolve
`CalcularCaminhosValidos(unit, RemainingMovementPoints)`: banda de
**movimento**, exatamente o que a doutrina rejeita.

⚠️ **A ferramenta Hotzone também pinta movimento.** A janela precisa da mesma
inversão, provavelmente como **modalidade** própria (o seletor "Modalidade" já
existe) para combate de artilheiro: verde 0→alcance, vermelho por cima, azul no
dobro.

### Por que o azul é o dobro, e não outra coisa

O azul do artilheiro não responde *"para onde eu ando"* — responde **"de onde eu
posso ser alcançado, ou alcançar, se a situação mudar"**. É banda de ameaça
recíproca.

A regra nasceu de uma perda concreta: artilharia de alcance 6 contra um tanque
rápido a 7 hexes; a peça embarcou para reposicionar e o adversário avançou e a
pegou desprevenida, indefesa dentro do transporte.

Daí a regra de §7: **artilharia de alcance 4 não embarca com inimigo no raio 8.**

---

## 1. Propósito e ramos

Unidades que **atiram paradas** e ficam na **RETAGUARDA** — jamais na vanguarda
ou nos flancos.

```text
FireSupport                      atiram parados, de longe
    └── Antiaéreo                atiram parados, de longe, NO AR
    ├── Artilheiro Combatente    consultado pelo Assalto: tenta atirar, devolve a bola
    └── Antiaéreo Combatente     idem, mirando o ar
```

**O Antiaéreo é o Fire Support clássico que atira para cima.** Não tem doutrina
própria: tudo neste documento vale para ele — retaguarda, iniciativa alta,
observador avançado, recusa de embarque com inimigo no operacional, elite
covarde no reparo. A única diferença é o **domínio do alvo**.

✅ É exatamente assim no código, e de forma quase caricata: o antiaéreo inteiro
são **4 linhas úteis** em `FireSupport.Antiaereo.cs` —

```csharp
PassesFireSupportRoleTargetFilter(attacker, target)
{
    if (não é antiaéreo)  return true;              // todo mundo passa
    return target.GetDomain() == Domain.Air;        // antiaéreo só mira ar
}
```

— mais dois predicados de identificação. Nenhuma decisão própria: mirar,
reposicionar, escoltar e embarcar vêm todos do pipeline compartilhado.

Os dois combatentes **não são deste papel**: eles pertencem ao Assalto e apenas
consultam o Fire Support. Ver `docs/AI Behavior/Assalto.md` §1.

---

## 2. Iniciativa

Costumam ter **iniciativa alta**, à frente dos blindados: a artilharia amacia
primeiro.

✅ Existe: `CompareCachedFireSupportAttackInitiative` e
`TryGetFireSupportCurrentAttackInitiative` ordenam quem tem tiro disponível para
agir antes.

### Observador avançado — desejo do autor, mecanismo em aberto

A artilharia pode **puxar qualquer unidade da vanguarda para iluminar seu alcance
máximo** antes de decidir atirar. Ela **passa a vez** para esse observador, que
vai a um hex de onde ela consiga disparar no alcance máximo — **mesmo que não
haja ninguém lá** (*forward observer*) — e então a iniciativa **volta para ela**.

Quem pode ser observador: qualquer aliado com classificação de combate
**Combatente** ou **Híbrido**. Descarta civis e outros artilheiros.

❌ Não existe. E o autor registra explicitamente que **não sabe qual deve ser a
regra** — o que está escrito aqui é o desejo, não o desenho.

O que isso implica, e ainda não tem resposta:

- iniciativa deixa de ser uma ordenação e passa a ser **cedível e retomável**
  dentro do mesmo turno;
- a artilharia precisa saber **qual hex** ilumina seu alcance máximo antes de
  alguém ir até lá — é uma consulta de LOS a partir de célula hipotética;
- ir a um hex "mesmo que não haja ninguém lá" é movimento **sem alvo**, que
  nenhum papel hoje justifica sozinho.

---

## 3. Atribuição e planos

Rotina normal.

---

## 4. Magnético

Ficam na **retaguarda do capitão** — o capturador líder do plano — ou do setor
designado, para unidades com plano.

⚠️ `TryResolveFireSupportMagnet` existe e já tem hierarquia própria (Radar Móvel
→ EWACS → capturador como fallback), mas a âncora devolvida é a **célula do
líder**, não "a retaguarda dele". A noção de retaguarda existe em outro lugar
(`IsBacklineSupportUnit`, `IsLogisticsForwardOfMainLine`) e não está ligada ao
magnético.

### Destroyer

Segue o capitão **pela costa**, o máximo que conseguir.

**Em rally de alto-mar, o Destroyer É o magnético durante o assembly**: as
unidades navais convergem para ele — transportes, fragatas, submarinos — porque
os navios seguram o fogo naval de cobertura enquanto o desembarque anfíbio é
preparado.

❌ Não existe. Regra ainda a pensar e implementar.

---

## 5. Combate

Disparam da retaguarda. Se por qualquer razão ficarem na vanguarda — a linha
caiu, o capitão morreu — **reposicionam para a retaguarda**.

⚠️ `FireSupport.Reposition.cs` existe, mas usa distância de hex contra orçamento
de MP (`MP × 3` em um dos ramos), não banda nem noção de linha.

---

## 6. Captura

Só com a skill *"captura construções"*, que hoje ninguém tem. Mesmo gancho do
`Assalto.md` §4.

---

## 7. Embarque

**RECUSAM embarque** se estiverem em combate, ou com inimigos dentro do seu
alcance **operacional** — que para elas é **o dobro do alcance da arma** (§0).
Embarcar é ficar indefeso, e o inimigo entra no alcance tático a qualquer
momento.

Concreto: **Artilharia de Campanha de alcance 4 não embarca com inimigo no raio
8.**

**Aceitam embarque no Operational** se houver **vanguarda no local** — porque
têm problema de combustível. Verificação: *teleporte uma unidade fantasma até lá
e veja se há vanguarda*.

**Unidades de elite** aceitam embarque se **não** alcançarem no operacional: são
caras demais para embarcar em território não seguro.

⚠️ `FireSupport.Embark.cs` já tem a forma certa da pergunta — consulta
`IsLogisticsForwardOfMainLine` e recusa destino "hot" sem drop de retaguarda —
mas o critério é o da logística, não "inimigo dentro do meu operacional", e não
há distinção por elite.

---

## 8. Desembarque

**RECUSAM desembarque** se não houver **vanguarda estabelecida** no local para
onde irão recuar.

Aceitam pelo **Melhor Desembarque** quando há vanguarda.

**Unidades de elite** aceitam desembarque apenas **no máximo do Tactical do
local para onde querem ir**. Exemplo: a Artilharia de Campanha desembarca a 3
hexes do setor Alpha e faz o resto do caminho a pé.

**Exceção:** na invasão naval final, em rally point no mar ou depois de montadas
as forças, aceitam desembarcar junto com as demais levas. *(ainda a implementar)*

❌ A condição de vanguarda no desembarque não existe. É irmã do
`Assalto.md` S6 e do `Transporte.md` §2.

---

## 9. Reparo e fusão

Sempre na **retaguarda**.

> **Unidades de elite são covardes** — e não vão reparar em construções aliadas
> na vanguarda.

Note a **inversão proposital** em relação ao Assalto: lá, *"unidade de elite não
é covarde"* e fica no prédio conquistado mesmo na vanguarda
(`Assalto.md` §8). Aqui é o contrário, e pelo mesmo motivo: a peça vale caro e
não briga de perto.

**Artilharia de elite tem lugar garantido** em qualquer construção do setor
`baseX`: pode ser curada e consertada **enquanto dispara em defesa**.

⚠️ Existe política de reparo sob pressão à base (não-elite é barrada de reparar
em base/âncora/HQ), mas é o inverso desta regra — barra por *não* ser elite, em
vez de garantir vaga *por* ser elite de artilharia.

---

## 10. O Porta-Aviões **não é deste papel**

Ele é **Transportador**, com uma perna que **consulta** o Fire Support — as duas
armas antiaéreas de longo alcance. A agenda dele é de transporte; o tiro é
consulta, não vocação.

É exatamente o mesmo padrão do Artilheiro Combatente (§1), com os papéis
trocados de lado:

| unidade | papel primário | consulta |
|---|---|---|
| Artilheiro Combatente | Assalto | Fire Support (tenta atirar, devolve a bola) |
| Antiaéreo Combatente | Assalto | Fire Support (mirando o ar) |
| **Porta-Aviões** | **Transportador** | **Fire Support (antiaéreo de longo alcance)** |

Três unidades, um só mecanismo: *"tento o tiro; se não der, volto para a minha
agenda"*. A diferença é qual agenda espera a bola de volta.

A doutrina do Porta-Aviões mora em `docs/AI Behavior/Transporte.md`. Aqui ficam
só as duas notas que pertencem a este papel:

- o tiro dele obedece o filtro de alvo do antiaéreo — só ar, §1;
- ele **não** herda a doutrina de retaguarda, iniciativa alta nem recusa de
  embarque deste documento: essas são de quem tem o Fire Support como papel
  primário.

As outras pernas — suporte logístico e Hub de transferência (recebe do porto
naval, repassa ao avião-tanque, com perna no controlador de Estoque) — também
pertencem aos respectivos documentos.

⚠️ A cadeia logística direcional (Hub → Receiver) existe no modelo e o
porta-aviões é caso citado dela, mas a IA ainda não opera a cadeia.

---

## Pendências

| # | o que falta | onde | tamanho |
|---|---|---|---|
| **F1** | banda do artilheiro no **serviço**: verde 0→alcance, azul 2×alcance. Hoje `BuildFireSupportPaths` devolve banda de movimento em 11 sítios | `FireSupport.Helpers.cs:142`, `UnitReachEnvelopeService` | G |
| **F1b** | **modalidade de artilheiro na ferramenta Hotzone** — pintar a inversão: verde 0→alcance, vermelho por cima, azul no dobro | `HotzoneWindow` | M |
| **F5b** | recusa de embarque com inimigo no **dobro do alcance** (art. 4 → raio 8) | `FireSupport.Embark.cs` | M |
| **F2** | observador avançado: ceder e retomar iniciativa no mesmo turno, com hex que ilumina o alcance máximo | `Initiative.cs` + novo | G — **mecanismo em aberto** |
| **F3** | magnético devolve **a retaguarda** do líder, não a célula dele | `Backline.cs` | M |
| **F4** | Destroyer: seguir a costa; e ser **o magnético** do rally de alto-mar durante o assembly | novo | G |
| **F5** | recusar embarque com inimigo dentro do **operacional** (hoje o critério é o da logística) | `FireSupport.Embark.cs` | M |
| **F6** | aceitar embarque no operacional **se houver vanguarda no local** (unidade fantasma teleportada) | `FireSupport.Embark.cs` | M |
| **F7** | elite: embarca quando **não** alcança no operacional | `FireSupport.Embark.cs` | P |
| **F8** | recusar desembarque **sem vanguarda estabelecida** | `MelhorDesembarque` | M |
| **F9** | elite desembarca no **máximo do Tactical** do destino e caminha o resto | `MelhorDesembarque` | M |
| **F10** | exceção da invasão naval final | novo | M |
| **F11** | elite de artilharia com vaga garantida em construção do setor `baseX` | `Repair.cs` | M |
| **F12** | reposicionamento por **banda**, não por `MP × 3` e distância de hex | `FireSupport.Reposition.cs` | M |
| **F13** | porta-aviões consulta o Fire Support como perna (antiaéreo), sem herdar a doutrina do papel — doutrina dele em `Transporte.md` | `Transporte.md` + roteador | M |

### Compartilhadas

| # | item | também em |
|---|---|---|
| F8 | não desembarcar sem vanguarda / em capturável | `Assalto.md` S6, `Transporte.md` §2 |
| — | skill "captura construções" como capacidade | `Assalto.md` M7 |
| — | `roles[0] == X` em vez de `CanSatisfy` | `Assalto.md` S11 |
