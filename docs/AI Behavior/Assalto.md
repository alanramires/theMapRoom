# Assalto e Antiaéreo — doutrina

Doutrina definida pelo autor. Onde o código divergir dela, o código está errado.

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |

---

## 1. Os ramos

```text
Assalto
    └── Artilheiro Combatente   (satisfaz Assalto)
Antiaéreo                        (é Fire Support)
    └── Antiaéreo Combatente    (satisfaz Assalto)
```

**Premissa.** Unidade de assalto vai para a briga e fica na vanguarda. O
**artilheiro combatente** tem um pé no fire support: **tenta atirar primeiro**;
se não der, vem para a porrada.

O **antiaéreo** é fire support puro. O **antiaéreo combatente** é idêntico ao
artilheiro combatente — muda só que ele tenta atirar de longe **para cima** e,
não dando, atira para cima em combate corpo a corpo.

✅ A árvore de papéis bate com o `UnitRoleCompatibility`:

```csharp
case UnitRole.ArtilheiroCombatente:  return Assalto || FogoIndireto;
case UnitRole.Antiaereo:             return FogoIndireto;
case UnitRole.AntiaereoCombatente:   return Antiaereo || Assalto || FogoIndireto;
```

### A divisão de arquivos pretendida

Definida pelo autor. Cada arquivo controla **um comportamento**, e o nome diz
qual:

```text
Assault.cs                            unidades que vão pra porrada
Assault.ArtilheiroCombatente.cs       atiram de longe primeiro, depois vão pra porrada
Assault.AntiAereoCombatente.cs        atiram de longe NO AR primeiro, depois vão pra porrada

Assault.Naval.cs                      marítimas que vão pra porrada e NÃO seguem magnético

FireSupport.cs                        atiram parados, de longe
FireSupport.Antiaereo.cs              atiram parados, de longe, NO AR
FireSupport.Naval.cs                  roteador das armas mistas navais
FireSupport.ArtilheiroCombatente.cs   recebe a chamada do Assalto, tenta atirar, devolve a bola
FireSupport.AntiAereoCombatente.cs    recebe a chamada do Assalto, tenta atirar no ar, devolve a bola
```

**`Assault.Naval` existe por causa da fragata**, não por causa do domínio: ela é
assalto puro, mas **não segue magnético**. Como a diferença é só essa, o arquivo
tende a ser fino — quase só a recusa do capitão e a agenda de vigilância.

**O submarino não precisa de arquivo.** Ele já se comporta bem como
`Assault.ArtilheiroCombatente` — tenta atirar de longe, depois vai pra briga. O
que ele tem são **particularidades de combate**: hit-n-run com retorno à camada
nativa, e o alcance de torpedo que a fragata inimiga precisa respeitar. Isso é
política dentro do ramo do combatente, não um ramo novo.

A simetria é o ponto: **cada híbrido tem duas casas** — uma no Assalto (que
decide ir para a briga) e uma no Fire Support (que tenta o tiro e devolve a
bola). Quem chama é sempre o lado do Assalto; o Fire Support é consultado e
responde `null` quando não dá.

**O caso degenerado é o teste da arquitetura.** A AAA atual é um Antiaéreo
Combatente com alcance `min = max = 1`: ao entrar no ramo do Fire Support ela
devolve `null` — não existe "de longe" para ela — e volta como assalto puro. No
dia em que virar `min = 1, max = 2`, o mesmo código passa a acertar: tenta de
longe primeiro, depois de perto. **Sem mudar uma linha de decisão.**

`FireSupport.Naval` é o roteador das armas mistas. O Destroyer é fire support
clássico com **duas pernas**, porque tem duas armas de domínios diferentes:

```text
1. tenta atirar em terra   → FireSupport clássico
2. falhou? tenta no ar     → FireSupport.Antiaereo
3. falhou? reposiciona
```

⚠️ **Estado hoje:** `Assault.ArtilheiroCombatente.cs`,
`Assault.AntiAereoCombatente.cs`, `FireSupport.ArtilheiroCombatente.cs` e
`FireSupport.Naval.cs` não existem. O que existe é
`FireSupport.Combatant.cs` servindo os dois híbridos,
`FireSupport.Antiaereo.Combatant.cs` como stub de 5 linhas, e o resíduo de AA
dentro de `Assault.cs`.

### Os dois híbridos hoje moram em um arquivo só

**Nota do autor:** artilheiro combatente e antiaéreo combatente têm o mesmo
pézinho no fire support — tentam atirar primeiro, depois vêm para a briga. Muda
só **para onde atiram**. Os arquivos deveriam ser um.

✅ **Do lado do Fire Support, já são.** `IsCombatantFireSupport` aceita os dois
papéis e ambos usam `TryDecideCombatantFireSupportTacticalAction`:

```csharp
private static bool IsCombatantFireSupport(UnitManager unit)
    => HasPrimaryRole(unit, UnitRole.ArtilheiroCombatente)
    || HasPrimaryRole(unit, UnitRole.AntiaereoCombatente);
```

E o `AIController.FireSupport.Antiaereo.Combatant.cs` tem **cinco linhas**, sendo
todas comentário:

> *"Antiaereo Combatente usa integralmente o pipeline do Artilheiro Combatente. A
> especializacao vive apenas no filtro de alvo central."*

⚠️ **Do lado do Assalto, não.** `Units/Assault/` ainda trata
`AntiaereoCombatente` como caso à parte — identificação
(`IsGroundAntiAirOnlyAssault`, `Assault.cs:1495-1502`, que precisa aceitar
explicitamente os dois papéis) e diagnóstico próprio (`Assault.cs:1224`).

O comentário no próprio código explica a origem: o papel satisfaz Assalto no
roteamento, mas `ResolveCompositionRole` o **preserva** como
`AntiaereoCombatente`, então a identificação precisa listar os dois. É a
duplicação a ser removida — a unificação já foi feita uma vez, do lado certo, e
não foi propagada.

---

## 2. Planos e atribuição

| | |
|---|---|
| assalto **com** plano | alocação normal |
| assalto **sem** plano | rogue |

⚠️ **O comportamento rogue existe pronto e está inalcançável.**

`DecideRogueAssaultBreakerAction` (`Assault.HQBreaker.cs`) é completo: vacate de
alvo de captura, vacate de produção própria, ataque breaker, rally próximo. Ele é
chamado por `Assault.cs:712`, **dentro** de `TryDecideAssaultAction` — que por
sua vez só roda dentro do `if (plan != null)` do roteador (`Router.cs:110,177`).

O trajeto de uma unidade de assalto sem plano hoje:

```text
facção rebelde  →  Rebel.cs devolve null (só roteia capturador)
                →  if (plan != null) é falso
                →  assalto e fire support pulados
                →  HexEvaluator
```

É exatamente o formato que o capturador tinha antes da v6.1.2: **a lógica
existia, o gate a escondia**. Destravar não é escrever comportamento novo.

### O que o assalto consome do `Rebel.cs`

`Assault.HQBreaker.cs:69-83` pergunta pela **facção** para decidir onde o capitão
vai capturar, e chama o buscador que morava no arquivo do rebelde:

```csharp
if (ConstructionManager.IsHeadQuarterlessTeam(snapshot.AITeam))   // ← gate por FACÇÃO
{
    ConstructionManager captainTarget =
        FindNearestPlanlessCaptureTarget(capturerMagnet, snapshot, capturerAnchor);
    captainCaptureCell = declaredCell;
}
```

Assimetria que isso produz: se o capitão é um capturador **sem plano de uma IA
com QG**, o assalto que o segue não descobre para onde ele vai — o `if` não
dispara. O magnético funciona, a captura declarada não.

É o mesmo gate trocado do `Capturador.md` §11.1 e do `Transporte.md`, agora no
terceiro papel. **Três lugares perguntando "a facção tem QG?" quando a pergunta
é "esta unidade tem plano?".**

---

## 3. Magnético

**Com plano:** magnético em relação ao **capturador do plano**. Fica na
vanguarda ou nos flancos (usando a ferramenta de progressão), ocioso e sem alvo,
**entre 1 e o Tactical do capitão** — não precisa ficar colado. Se o combate
surgir, vai para cima.

**Sem plano:** igual, mas em relação a um capitão auto-nomeado: **o capturador
mais próximo**.

✅ O magnético existe e **não depende de plano**: `TryResolveCapturerMagnet`
(`AIController.Backline.cs`) varre `snapshot.MyUnits`, filtra por
`CanSatisfy(data, UnitRole.Capturador)` e escolhe o mais próximo por distância
cúbica. É literalmente o "capitão auto-nomeado".

⚠️ A âncora devolvida é **a célula do capitão** (`anchor = candidateCell`), não
uma faixa. A regra "entre 1 e o Tactical do capitão" não está expressa: quem
consome decide a folga por conta própria, e a distância usada para eleger o
capitão é cúbica, não banda.

⚠️ A escolta assinada (`DecideAssignedAssaultEscortAction`,
`ResolveAssaultEscortCell`, `IsAssaultEscortInCapturerCorridor`) é o caminho
com plano e usa raio de zona, não o Tactical do capitão.

---

## 4. Captura

**Assalto não captura.**

✅ Zero chamadas a `BuildCaptureBatch` em `Units/Assault/` e
`Units/Fire Support/`.

**Em aberto:** unidade com a skill *"captura construções"* — um jipe futuro, por
exemplo — teria um pézinho no capturador, na subseção dos **agressivos**. Fica
declarado como direção, não como regra.

❌ A skill não existe. Não há campo nem skill de captura no `UnitData`; hoje
quem captura é definido por papel (`UnitRole.Capturador`), não por capacidade da
ficha. Quando existir, o gancho natural é `UnitRoleCompatibility` — do mesmo
jeito que `isTransporter` e `isSupplier` já satisfazem Transportador e Logística
por capacidade mecânica, sem precisar de papel híbrido.

---

## 4-M. Marinha

Unidades marítimas seguem este mesmo contrato, com os desvios abaixo.

```text
Assalto              → Fragata
Artilheiro Combatente → Submarino
```

✅ **`UnitData.IsMaritime()` existe** e é **derivada**, não campo:

```csharp
public bool IsMaritime()
{
    if (domain == Domain.Naval || domain == Domain.Submarine) return true;
    // ... o mesmo para aditionalDomainsAllowed
}
```

Ela olha o domínio **e os domínios adicionais permitidos** — e é por isso que
existe. O hidroavião é **os dois**: `IsAircraft()` e `IsMaritime()`. Não dá para
olhar o domínio primário e concluir "é navio".

⚠️ Só há **um** consumidor em toda a IA (`Repair.Movement.cs:458`). O assalto
identifica naval por `unit.GetDomain() == Domain.Naval`
(`Assault.HQBreaker.cs:345`) — que é justamente a conclusão apressada que a
derivada existe para evitar.

### Planos e atribuição

Como no assalto terrestre (§2) — inclusive a pendência S1.

### Magnético — capitão marítimo, e **parar de seguir o capturador**

Não seguem capturador. O **magnético naval é um assalto com `IsMaritime()`** —
a fragata puxa, o submarino acompanha. Fora isso, a agenda é de **vigilância**,
não de escolta de captura.

❌ **Hoje fragata e submarino seguem o capitão terrestre, e isso tem que parar.**
É gambiarra de teste: existe para o jogo em desenvolvimento continuar rodando, e
não é comportamento pretendido. Um navio marchando atrás de uma infantaria é o
sintoma.

O caminho: `Assault.HQBreaker.cs` chama `TryResolveCapturerMagnet`, que filtra
por `CanSatisfy(data, UnitRole.Capturador)` sem qualquer recorte de domínio —
então unidade naval entra e recebe um capitão terrestre. Toda a lógica de
domínio nativo do submarino (`CanFinishInNativeDomain`,
`ScoreNativeDomainPreference`, `PodeSubmergirSensor`) existe **dentro** desse
fluxo de perseguição ao capitão: é a gambiarra tentando impedir que o submarino
encalhe enquanto persegue alguém que não deveria estar perseguindo.

`TryResolveFireSupportMagnet` já demonstra o padrão certo — hierarquia própria
(Radar Móvel → EWACS → capturador como fallback). O magnético naval é uma
terceira hierarquia no mesmo lugar, e a remoção da perseguição vem junto.

**Implementado:** `AIController.VigilanciaAerea` virou
**`AIController.Vigilancia`** — a agenda deixou de ser "aérea" e passou a ser da
**camada da visão especializada**. Radar móvel vigia o chão, EWACS vigia o ar,
submarino/fragata vigiam a água: é a mesma agenda com camada diferente, do mesmo
jeito que artilheiro e antiaéreo combatente são o mesmo pipeline com alvo
diferente (§1).

O módulo vive em `Units/Vigilancia/`. A porta genérica resolve a camada principal
da ficha e delega a geometria ao envelope `Mobility`; helpers especificamente
aéreos permanecem apenas para recuperação, formação e plataforma do EWACS/Radar.

É o mesmo movimento do §10 do `Capturador.md`: lá a âncora do rogue deixou de
ser fixa no QG e virou parâmetro. Aqui é a camada da vigilância.

### Captura

Só com o papel de captura de construções, que hoje elas não têm. Ver §4.

### Combate

**Fragata (Assalto):** vai para cima, como assalto regular.
No **Hard**, reposiciona **1 hex além do alcance de torpedo** da hotzone de
combate do submarino — mesma família da política de §5, com a diferença de que
ali é 1 hex *atrás* do Tactical do oponente e aqui é 1 hex *além* do alcance da
arma dele.

**Submarino (Artilheiro Combatente):** comporta-se como artilheiro combatente —
tenta atirar primeiro, depois briga — mas adota **hit-n-run** na vanguarda:
depois do disparo, recua até submergir de novo.

❌ Nenhuma das duas existe. O pipeline do artilheiro combatente já serve o
submarino (§1), mas não há ciclo de emersão/submersão ligado à decisão de tiro,
nem leitura de alcance de arma inimiga para recuo.

⚠️ **A preferência de camada nativa está hardcoded.** `Assault.HQBreaker.cs:173`
resolve a mesma necessidade com regra escrita à mão:

> *"Submarino nao escolhe praia/superficie por vontade propria. Se nenhum passo
> nativo tambem aproximar o capitao, permite um passo submerso lateral/regressivo
> para sair do bloqueio."*

Implementada por `CanFinishInNativeDomain` + `ScoreNativeDomainPreference`. A
regra está certa e o comportamento é o desejado — o que falta é ela vir da
**ficha**, não do código do assalto: o hit-n-run precisa saber qual é a camada
nativa da unidade para saber para onde recuar, e "evitar praia" é um caso
particular disso.

`preferredNavalHeight` já existe no `UnitData` (default `Submerged`), mas
descreve altura naval, não "camada nativa em que a unidade prefere terminar o
turno".

### Embarque e desembarque

**Inválido por enquanto.** Fica registrado que pode deixar de ser — uma baleia
transportadora que aceite navios, por exemplo.

### Suprir

**Perninha no suprir**, quando a unidade tem `isSupplier`.

### Transferir

**Perninha no transferir**, quando tem `isSupplier` **e** é `SupplierTier.Hub`.

✅ Os dois campos existem (`UnitData.isSupplier`, `UnitData.supplierTier`) e a
capacidade mecânica já satisfaz o papel de Logística sem papel híbrido — mesmo
padrão que o `isTransporter`. O que falta verificar em jogo é se um combatente
naval com `isSupplier` chega a ser roteado para o fluxo logístico, já que o
roteador consulta papéis antes.

---

## 5. Combate

**Assalto parte para cima** usando a decisão de combate — geralmente o melhor
DPQ. O **artilheiro combatente** pode reposicionar se não conseguir lutar.

✅ DPQ da ficha honrado (`prioritizeDpqAtBattle`, peso 2000 com a flag e 40 sem)
em Assault, Defender, Explorer, HQBreaker e — desde a v6.1.2 — no ataque
preemptivo de papel. Reposicionamento do combatente existe em
`FireSupport.Reposition.cs`.

**Não ficar no prédio capturável** quando houver capturador que alcance o local.

✅ `IsReservedAssaultEscortCaptureCell` é a primeira guarda do laço de células em
`TryFindAssaultEscortAttack`: descarta a célula quando há capturável que ainda
importa.

```csharp
return construction.SlotIndex != ResolveAISlotKey(aiTeam)                // não é meu
    || construction.CurrentCapturePoints < construction.CapturePointsMax; // meu, incompleto
```

**Se o prédio já for do slot, lutam nele** se isso levar a melhor DPQ.

✅ Consequência direta da guarda acima: prédio já capturado por inteiro deixa de
ser bloqueado, e a célula volta a competir pelo termo de DPQ.

### 5.1 Furtividade aérea — ignorar o caminho, e o preço de atirar

**Veio de `Vigilancia.md` §5 em 2026-08-06.** Estava no documento errado: fala do
**furtivo que vai bombardear**, e furtivo aéreo é Assalto, não Vigilância. O
critério que decidiu está em `Vigilancia.md` §0 — o F-22 e o B-2 **não têm visão
especializada** (`HasStealthDetectionFor` é `false`), logo são Interceptador e
Ataque Aéreo.

> *"As unidades furtivas aéreas podem **ignorar combates no caminho** até seus
> objetivos se não estiverem em vantagem numérica ou oportunística. **Atacar é
> revelar a posição para todos por X rodadas.**"*

Duas cláusulas, e elas se aplicam aos dois ramos furtivos:

| cláusula | consequência para a IA |
|---|---|
| **ignorar combate no caminho** | a missão vence o alvo de oportunidade — só desvia com vantagem numérica ou oportunística |
| **atirar revela por X rodadas** | o primeiro tiro tem preço; escolher o instante é parte da arma |

**Casa com a regra do F-22** de `docs/deteccao e caca.md` §10.1: detectado, ele
perde o Elite 2 e vira um caça comum; **atacando primeiro**, usa a camuflagem e o
Elite 2 inteiro. O bombardeiro Elite 2 não muda.

❌ Nenhuma das duas existe na IA hoje.

### Política futura — assalto inteligente (IA Hard)

Na dificuldade Hard, o assalto **consulta a hotzone do oponente** e **adia o
ataque** se não conseguir chegar de primeira, reposicionando-se **exatamente 1
hex atrás do Tactical de combate do oponente**.

❌ Não existe. Depende do envelope responder pela unidade inimiga — o serviço já
sabe fazer isso (é a mesma consulta, com outra unidade), mas ninguém pergunta.

---

## 6. Embarque

**Apesar da grande mobilidade, aceitam carona no Operational para trens e
barcos.**

Motivo: assalto tem **combustível escasso** — o Tanque B tem 30 de autonomia, e
num mapa gigante ele seca cedo demais.

❌ **Política nova.** O assalto já avalia carona
(`EvaluateCombatPassengerRideNeed` → `QueroCaronaService`, com
`CombatPassengerTransportPolicy.Assault`), mas **não há uma única referência a
combustível em `Units/Assault/`**. A decisão hoje é por alcance ao objetivo, não
por autonomia.

Três coisas que a regra pede e não existem:

1. combustível como motivo de carona, ao lado de "não alcanço";
2. preferência por **trem e barco** — modais de longo curso — em vez de
   qualquer transporte;
3. aceitar no **Operational**, não só quando o objetivo está fora das bandas.

---

## 7. Desembarque

**Não desembarcam em prédios capturáveis.** Aceitam as condições da ferramenta
**Melhor Desembarque**.

⚠️ A ferramenta é usada, mas a proibição do capturável é do lado do **assalto
parado** (`IsReservedAssaultEscortCaptureCell`), não do desembarque. Ver
`docs/AI Behavior/Transporte.md` §2 — "transporte não pousa em capturável" é o
mesmo buraco visto do outro lado.

---

## 8. Fusão e reparo

**Unidade de elite não é covarde.** Estando em prédio aliado já conquistado,
**mesmo na vanguarda**, ela fica lá para reparos.

Fora isso, vale a política geral: **fundir na retaguarda**, e **evacuar a
vanguarda antes de todos** em batalha, para liberar a frente.

⚠️ O conceito de elite existe no reparo (`eliteLevel >= 1`, `Repair.cs:135`) e a
iniciativa já ordena feridos por nível de elite durante a invasão. Mas:

- "elite fica no prédio conquistado mesmo na vanguarda" não está expresso;
- "evacuar a vanguarda antes de todos" contradiz a iniciativa atual, em que
  reparo/manutenção é o **grupo 5** e age por **último**.

É a mesma pendência do `Capturador.md` §5, vista pelo assalto.

---

## 9. Suprir e transferir

Não se aplica ao papel.

✅ Nada no fluxo de assalto consulta suprimento ou transferência.

---

## Pendências

| # | o que falta | onde | tamanho |
|---|---|---|---|
| **S1** | assalto **sem plano** entra no papel — o rogue existe pronto, só está atrás do `if (plan != null)` | `Router.cs:110,177` | M |
| **S2** | magnético expressa a faixa **1 → Tactical do capitão**, em vez de âncora na célula dele | `Backline.cs`, `Assault.Defender.cs` | M |
| **S3** | carona por **combustível**, com preferência a trem e barco, aceita no Operational | `Assault.Embark`, `QueroCarona` | M |
| **S4** | elite fica no prédio conquistado para reparo, mesmo na vanguarda | `Repair.cs` | P |
| **S5** | evacuar a vanguarda **antes de todos** (hoje reparo age por último) | `Initiative.cs` | M |
| **S6** | não desembarcar em capturável | `MelhorDesembarque` | M |
| **S7** | Hard: consultar hotzone do oponente, adiar e recuar 1 hex atrás do Tactical dele | novo | G |
| **S8** | tirar o resíduo de AA de dentro do `Assault.cs` — o antiaéreo já é só um filtro de alvo de 4 linhas no Fire Support | `Assault.cs:1224,1495-1502` | P |
| **S10** | dividir os arquivos como a doutrina pede: `Assault.ArtilheiroCombatente`, `Assault.AntiAereoCombatente`, `FireSupport.ArtilheiroCombatente`, `FireSupport.AntiAereoCombatente`, `FireSupport.Naval` | estrutura | G |
| **S11** | `HasPrimaryRole` compara `roles[0] == X`; deveria ser `CanSatisfy`. Terceira aparição do padrão (C3 foi a primeira) | `FireSupport.Antiaereo.cs:15` | P |
| **S9** | tirar do assalto o gate por facção e a chamada ao buscador do rebelde | `Assault.HQBreaker.cs:69-83` | P |

### Marinha (§4-M)

| # | o que falta | onde | tamanho |
|---|---|---|---|
| **M1** | assalto passa a usar `IsMaritime()` em vez de `GetDomain() == Naval` — a derivada existe justamente porque hidroavião é aeronave **e** marítimo | `Assault.HQBreaker.cs:345` | P |
| **M2** | ✅ `VigilanciaAerea` → **`AIController.Vigilancia`**: a agenda usa a **camada principal da visão especializada** | `Units/Vigilancia/` | M |
| **M3** | **parar de seguir o capitão terrestre** (gambiarra de teste) e criar o magnético naval: capitão é assalto com `IsMaritime()` | `Backline.cs`, `Assault.HQBreaker.cs` | M |
| **M8** | `FireSupport.Naval` — roteador de armas mistas: Destroyer tenta terra, depois ar, depois reposiciona | novo | M |
| **M9** | `Assault.Naval` — fragata: assalto puro **sem magnético**. Fino por natureza: a recusa do capitão e a agenda de vigilância. Submarino **não** entra, fica como `Assault.ArtilheiroCombatente` | novo | M |

### Ordem obrigatória da marinha

**M4b → M3 → M4.** Não é preferência, é dependência.

Toda a lógica de domínio nativo do submarino — `CanFinishInNativeDomain`,
`ScoreNativeDomainPreference`, `PodeSubmergirSensor` — vive **dentro** do fluxo
de perseguição ao capitão, em `Assault.HQBreaker.cs`. Ela existe para impedir
que o submarino encalhe enquanto persegue alguém que ele não deveria estar
perseguindo.

```text
M4b  camada nativa vira flag da ficha e sai do código do assalto
      └─ a peça deixa de depender do fluxo que vai ser removido

M3   remove a perseguição ao capitão e cria o magnético naval
      └─ se feito antes do M4b, essa lógica fica órfã e some junto

M4   hit-n-run reaproveita a camada nativa, agora no lugar certo
      └─ ele PRECISA saber para onde recuar; é a mesma informação
```

Fazer M3 primeiro custa duas vezes: perde-se a regra de não encalhar, e o M4
precisa reescrevê-la.
| **M4** | submarino: ciclo **hit-n-run** — recua até submergir depois do disparo | novo, sobre o pipeline do combatente | M |
| **M4b** | **camada nativa como flag da ficha** — hoje "submarino evita praia" é hardcoded no assalto | `UnitData` + `Assault.HQBreaker.cs:173` | P |
| **M5** | Hard: fragata reposiciona 1 hex **além do alcance de torpedo** do submarino | novo, irmão do S7 | G |
| **M6** | conferir se combatente naval com `isSupplier` chega a ser roteado ao fluxo logístico | `Router.cs` | P |
| **M7** | skill *"captura construções"* (jipe futuro) como gancho de capacidade, não de papel | `UnitData` + `UnitRoleCompatibility` | — em aberto |

### Compartilhadas com outros papéis

| # | item | doc |
|---|---|---|
| C6/S5 | ferido na vanguarda ganha iniciativa para recuar | `Capturador.md` |
| C9/S6 | não pousar/parar em capturável | `Transporte.md` §2 |
| A1 | infantaria trocando 5 HP por 2 contra helicóptero | `Capturador.md` |
| E1 | Fase 2 da migração do envelope (Assault e Fire Support) | plano do envelope |

---

# Apêndice — A ficha do papel (2026-08-06)

**Descrição do autor:**

> *"Tá difícil seguir em frente? Me chama — eu rompo barreiras, seja em terra ou
> no ar!"*

`Assalto` (chão), `AtaqueAereo` (ar contra chão) e **`Interceptador`** — os
**caças**, *"um assalto que atira pra cima"* — são o **mesmo papel**. A função das unidades de
alcance 1 é partir pra briga.

### A linha que separa os papéis é a MODALIDADE, não o alvo

Com esta ficha e a de `FireSupport.md` na mesa, a taxonomia fecha:

```text
modalidade COMBATENTE   contato, alcance mín 1     Assalto, AtaqueAereo, Interceptador
modalidade ARTILHEIRO   parado, alcance mín > 1    FogoIndireto, Antiaereo (SAM), Destroyer
```

E a **camada-alvo é ortogonal** — é dado da arma, não identidade de papel:

|  | alvo no chão | alvo no ar |
|---|---|---|
| **combatente** | `Assalto` | `Interceptador` / `AtaqueAereo` |
| **artilheiro** | `FogoIndireto` | `Antiaereo` |

**Seis valores do enum, UMA distinção de comportamento.** O resto é a arma
dizendo para onde atira — mesma conclusão dos papéis-fantasma
(`Shopping.md` §2), agora generalizada para a família inteira.

**Consequência:** os candidatos a sair do enum não são três (12, 13, 14); são
mais. Mas a ordem segura continua a do `Shopping.md` §3.1, e o passo que não pode
faltar é dar ao shopping um gancho de **preferência por capacidade** antes de
remover qualquer nome.

| eixo | valor |
|---|---|
| **modalidade** | **combatente** — combate em contato, mover e atacar em alcance de contato (mín. 1) |
| **posicionamento** | **vanguarda** — a posição entre a massa oponente e o capitão eleito, **à frente dele** |

## Prioridade de sensor

```text
Detectar, Mirar, Embarcar, Reposicionar, Capturar,
Transferir, Suprir, Desembarcar, Enxergar, Fundir
```

### 1. `Detectar` no topo — e é a TERCEIRA pergunta desta casa

O assalto precisa saber **exatamente onde estão os elites que deve destruir**.
Repare que a mesma casa faz perguntas diferentes em papéis diferentes:

```text
capturador   "quem ocupa o prédio que eu quero?"     precondição
assalto      "onde está o elite que eu cacei?"        AQUISIÇÃO DE ALVO
```

A do assalto é `MelhorSpotting` (contato sobre célula/camada específica); a do
capturador é `RevelacaoDeContato`. **Nenhuma das duas existe no runtime.**

**Pedido dentro do próprio papel:** uma unidade de assalto pode **passar a vez**
para outra reposicionar na vanguarda e iluminar alvos — especialmente
bombardeiros (`AtaqueAereo`), para destruir logo a artilharia de elite inimiga.

### 2. `Mirar` — a missão é o alvo preferido

Sempre focam em **destruir seu alvo preferido de elite** (tanque caça canhão de
elite). Não encontrando, procuram **o melhor local de defesa** para lutar com
vantagem. Não conseguindo, **o papel muda**.

### 3-4. `Embarcar` antes de `Reposicionar` — e só no fim das possibilidades

Embarcam **só depois de esgotar combate e detecção**, e apenas se o **capitão
atribuído estiver muito longe**.

**Com plano:** vanguarda perto do capitão.
**Rogue:** *"avança igual imbecil na frente, na direção do alvo."*

> **Variante (ver a correcao no apendice):** ler a **hotzone do alvo** e
> posicionar-se **1 hex na beirada**, para ter avanço total no próximo turno. O
> serviço para isso já existe — `UnitReachEnvelopeService`.

### 5-8. Capturar, Transferir, Suprir, Desembarcar — ocasionais

Em testes alguns assaltos capturam oportunisticamente, **mas não é o papel
deles**. Raramente movem estoque ou prestam serviço a aliados. Como blindados de
assalto raramente levam tropa, mal desembarcam.

### 9. `Enxergar` quase no fim — porque ELE é a revelação

> *"Minha função é contato! Mesmo que fosse ideal alguém revelar quem está ali —
> como ninguém revelou, deixa comigo! Minha armadura pesada aguenta."*

**Ignoram totalmente terreno oculto pela névoa.** O capturador *pede* revelação
(casas 2 e 3 dele); o assalto **é** a revelação de último recurso, e por isso não
precisa da casa.

### 10. `Fundir` — negado, e por um motivo próprio

**Não fundem nem fora nem dentro do estado de reparo.**

```text
fora de reparo   dois tanques avariados ainda são DUAS armas anti-veículo
em reparo        não fundem para NÃO ENCOLHER O EXÉRCITO
```

## O padrão das três moedas

Com três papéis na mesa, a regra *"cada papel tem uma moeda, e a moeda decide se
fundir é ganho ou perda"* fica visível:

| papel | onde o valor mora | fundir |
|---|---|---|
| **capturador** | o **corpo** — HP é a taxa de captura | **ganha** (concentrar acelera) |
| **transportador** | as **vagas** — capacidade não degrada com HP | perde (dois cascos, duas viagens) |
| **assalto** | a **arma** — cada casco é uma ameaça a responder | perde (dois canhões, dois problemas) |

O segundo motivo do assalto (*não encolher o exército*) é diferente dos outros
dois: trata **contagem de unidades** como grandeza estratégica por si — o mesmo
raciocínio da doutrina de conscrição em Collapsing (`Shopping.md` §6).

## O papel estratégico — o que o assalto é para o exército

Estas duas linhas não são comportamento de turno: são a função do papel na
economia da partida.

### Em defesa, elite e massa coexistem

> *"A produção segue tentando chegar no elite junto com o recrutamento avançado,
> mas cada política pode variar dependendo da IA."*

É exatamente a forma do **imposto de conscrição** (`Shopping.md` §6.1): massa
garantida primeiro, elite **com o que couber por cima**. Não é escolher entre os
dois — é uma barra dupla, e o que varia por dificuldade é onde fica o fiel.

E casa com a regra viva de Collapsing (`Demand.cs:3092`), que dá **+16000** a
`Assalto` / `FogoIndireto` / `Antiaereo` quando o time perde o mapa. Para o
assalto, essa regra **está certa** — o que está errado é ela **negar** o bônus ao
capturador, que defende a linha de renda (`Capturador.md` §0).

### Assalto é pré-requisito da invasão final

> *"Unidades de assalto e ataque aéreo também são pré-requisitos para
> concentração de força antes de uma invasão final."*

A invasão da base inimiga só é alocada quando um **rally atinge GoGreen** — o
portão de massa. **O assalto é a massa que abre esse portão.**

Consequência para o shopping: a demanda por assalto **não é só defensiva**. Ela é
a precondição do fim de jogo, e cortá-la em nome de outra urgência adia a
invasão inteira. Ver `project_invasion_gated_by_rally` e o macro-estado de
invasão, que persiste no save por `rallyGoGreenTurns`.

---

## Variantes pendentes

`ArtilheiroCombatente` e `AntiaereoCombatente`. **O autor precisa explicar fogo
de suporte primeiro** — e o `Shopping.md` §2.2 já registra que os dois
compartilham **um único predicado** no código
(`AIController.FireSupport.Combatant.cs:8-9`), o que sugere que são um
comportamento só com famílias de arma diferentes.

---

# Apêndice — Marcha do Assalto

Escrita pelo autor em 2026-08-06. **Ela é a doutrina**, e vale a regra do
cabeçalho: **onde o código divergir de um verso, o código está errado.**

Terceira marcha do projeto, e a terceira que se define **por comparação com o
capturador**. Foi assim que a regra da moeda ficou visível:

| verso | o que ele fixa |
|---|---|
| *"Se ninguém sabe o que existe, / eu atravesso e dou de frente"* | o assalto é a **revelação de último recurso** — por isso `Enxergar` é a nona casa |
| *"Primeiro eu quero o nome, / a posição e o setor"* | `Detectar` no topo é **aquisição de alvo**, não precondição (`MelhorSpotting`) |
| *"Se alguém puder iluminar, / que avance para detectar"* | o pedido **dentro do próprio papel**: passar a vez para outro assalto iluminar |
| *"Meu lugar é na vanguarda, / entre o capitão e o inimigo"* | o posicionamento, literal |
| *"Um tanque é uma arma, / dois sustentam posição; / quando a coluna se concentra, / começa a invasão"* | a massa que abre o portão do rally (GoGreen) |
| *"Para quem captura prédios, / dois relógios podem unir; / mas duas armas de Assalto / têm dois alvos a atingir"* | a **moeda**: o valor mora na arma, e por isso fundir perde |

### ⚠️ Correção registrada: as duas variantes do rogue são LEGÍTIMAS

> *O prudente mede a Hotzone, / o mais bruto encara o oponente.*
> *Um espera junto à borda / para inteiro avançar;*
> *outro entra como um louco: **os dois nasceram para lutar!***

A ficha acima marcava a variante da hotzone como `❌ não existe`, subentendendo
defeito. **Não é defeito — são duas variantes do mesmo papel.** É `gosto`, não
`conta`, e o destino dela é **política** (`Services/…Policy/`), não correção de
score. O serviço para a versão prudente já existe: `UnitReachEnvelopeService`.

---

**[Introdução — caixa seca, bumbo e metais graves]**

Firmar a linha! / Preparar o chão! / Quando a frente não avança, / chamem o Assalto então!

Um, dois! / Blindagem à frente! / Um, dois! / Contato com o oponente!

**[Estrofe 1]**

Não carrego a missão, / não recolho produção; / eu sou a arma pesada / que abre passagem ao batalhão.

Se existe uma barreira / segurando a progressão, / eu encontro a peça-chave / e destruo a posição.

Não procuro o mais próximo, / nem disparo sem razão: / eu persigo aquele elite / que sustenta a formação.

**[Refrão]**

Deixa comigo! / Minha armadura aguenta! / Se ninguém sabe o que existe, / eu atravesso e dou de frente!

Deixa comigo! / Eu vou romper a barreira! / Onde a força não avança, / minha arma abre a fronteira!

Avança, Assalto! / Vai buscar o defensor! / Destrói o que nos impede / e deixa o exército passar!

**[Estrofe 2 — detectar e caçar]**

Primeiro eu quero o nome, / a posição e o setor; / quero saber onde se esconde / o mais perigoso defensor.

Tanque caça o grande canhão, / bombardeiro busca a bateria; / cada arma tem sua presa, / cada elite, o seu dia.

Se alguém puder iluminar, / que avance para detectar; / eu espero o alvo certo / para então o eliminar.

Mas se ninguém abre os olhos / e é preciso prosseguir, / não vou parar diante da névoa: / alguém precisa descobrir!

**[Refrão]**

Deixa comigo! / Minha armadura aguenta! / Se ninguém sabe o que existe, / eu atravesso e dou de frente!

Deixa comigo! / Eu vou romper a barreira! / Onde a força não avança, / minha arma abre a fronteira!

**[Estrofe 3 — vanguarda]**

Meu lugar é na vanguarda, / entre o capitão e o inimigo; / se a ameaça busca a tropa, / ela vai encontrar comigo.

Eu protejo a retaguarda / ocupando a direção; / sou a massa que recebe / o primeiro impacto da invasão.

Com um plano, sigo o eixo; / sem um plano, sigo em frente. / O prudente mede a Hotzone, / o mais bruto encara o oponente.

Um espera junto à borda / para inteiro avançar; / outro entra como um louco: / os dois nasceram para lutar!

**[Ponte — concentração de força]**

Um tanque é uma arma, / dois sustentam posição; / quando a coluna se concentra, / começa a invasão.

Junte o aço! / Junte o fogo! / Feche toda formação!

Ataque em terra! / Ataque Aéreo! / Rompimento em conjunção!

Não tomo a cidade, / não conduzo o batalhão: / eu destruo o que impedia / os demais de cumprir sua missão!

**[Estrofe 4 — Ataque Aéreo]**

Se a muralha está na terra / e o caminho não abriu, / ouve-se o rugido no alto: / o Ataque Aéreo surgiu!

Sobre montes, sobre linhas, / vai direto ao coração; / busca a peça de artilharia / que governa a posição.

Não bombardeia por vaidade, / nem por simples destruição; / arranca os dentes da defesa / para começar a invasão.

Terra e céu falam juntos, / cada força em seu setor: / um abre o chão com blindagem, / outro mergulha com furor!

**[Refrão forte]**

Deixa comigo! / Minha armadura aguenta! / Se ninguém sabe o que existe, / eu atravesso e dou de frente!

Deixa comigo! / Eu vou romper a barreira! / Onde a força não avança, / minha arma abre a fronteira!

Avança, Assalto! / O elite é tua missão! / Destrói a peça mais forte, / faz ruir a formação!

**[Estrofe 5 — duas armas]**

Mesmo avariado, / ainda posso combater; / meu valor está na arma / que ainda posso oferecer.

Dois blindados machucados / ainda atiram duas vezes; / duas armas sobre o campo, / duas ameaças aos seus chefes.

Não me funda para poupar-me, / não reduza a formação; / devolva cada arma ao campo, / sem encolher o batalhão.

Para quem captura prédios, / dois relógios podem unir; / mas duas armas de Assalto / têm dois alvos a atingir!

**[Estrofe 6 — defesa]**

Se a postura é defensiva, / não desaparece o meu valor; / uma linha só resiste / quando elimina o agressor.

Posso aguardar em terreno forte, / posso formar contra-ataque; / posso proteger o capitão / e depois partir ao choque.

A defesa não é silêncio, / nem somente suportar; / é concentrar a força certa / e escolher quando esmagar.

**[Chamada e resposta]**

— Quem entra primeiro? / — O Assalto!

— Quem rompe a defesa? / — O Assalto!

— E se a névoa não abrir? / — Deixa comigo!

— E se houver um elite ali? / — Foi para isso que eu vim!

**[Refrão final — coro completo]**

Deixa comigo! / Minha armadura aguenta! / Se ninguém sabe o que existe, / eu atravesso e dou de frente!

Deixa comigo! / Eu vou romper a barreira! / Onde a força não avança, / minha arma abre a fronteira!

Por terra ou pelo alto, / sob fumaça ou sob calor, / quando o caminho está fechado, / chamem logo o Assalto!

Eu encontro a barreira! / Eu alcanço a posição! / Eu destruo a barreira! / E atrás de mim passa o batalhão!

**[Coda]**

Um, dois! / Blindagem à frente!

Um, dois! / Contato com o oponente!

Não vim guardar passagem, / nem esperar o agressor:

**Eu vim abrir o caminho! / Eu sou o Assalto!**
