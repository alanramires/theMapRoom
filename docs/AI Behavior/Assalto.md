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
