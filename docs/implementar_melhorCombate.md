# Implementar o `MelhorCombate`

Documento de trabalho. A investigação preliminar é do auditor; esta versão
acrescenta **verificação no código**, cinco ajustes e a ordem de execução.

Nada foi alterado no código para escrever isto.

---

## 0. O que foi verificado

Quatro afirmações da investigação foram conferidas no código. **As quatro são
verdadeiras**, e uma é mais grave do que estava descrita.

```csharp
// Batches.cs:106 — nenhum parametro de arma
BuildAttackBatch(unit, team, from, to, targetId, targetCell, paths)

// AttackDecision.cs:167 — devolve bool e descarta o resto
private bool PassesAttackDecision(...)

// Capturer.Attack.cs:691 — logica geral agachada em arquivo de papel
private PositionDpqForAttackDecision ResolveDpqForAttackDecision(Vector3Int cell)
```

### A das escalas é pior que "incompatíveis"

Os mesmos números aparecem com **semânticas diferentes**:

| valor | onde | significa |
|---|---|---|
| `150000f` | `Assault.Targeting.cs:45` | primário de preferência de classe |
| `30000f` | `Capturer.Attack.cs:90` | primário de preferência de classe |
| `18000f` | `FireSupport.Helpers.cs:509` | primário de preferência de classe |
| `30000f` / `18000f` | **`AirCombat.cs:1338`** | **elite ≥1 vs não-elite** |
| `18000f` | `Repair.Combat.cs:69` | bônus de kill garantido |

Quem der `grep 18000f` recebe quatro ocorrências com **três significados
distintos**. Não é apenas escala divergente — é constante reaproveitada, que é a
espécie que **sobrevive ao refactor** porque parece intencional.

---

## 1. O desenho aceito

A divisão em camadas mapeia a doutrina do projeto (serviço burro → consumidor →
organizador), e o corte extra é real:

```text
PodeMirar
    ↓ legalidade e opção executável

CombatEvaluationService          função PURA de (atacante, célula, alvo, opção)
    ↓ avalia UM combate específico

MelhorCombateService             agrega células, agrupa combates, ranqueia
    ↓ consulta apenas — não cria PlayerAction

Papel / Router
    ↓ objetivo, capitão, captura, defesa, rally
```

É a mesma fronteira que separa `UnitReachEnvelopeService` de
`MelhorDesembarque`: um responde por sujeito, o outro cruza.

### Três acertos que devem ser preservados

- **nota da célula = melhor combate admissível, não soma.** É o mesmo modo de
  falha da banda de artilharia: soma premia quantidade, máximo premia decisão.
  Cinco trocas medíocres não podem vencer uma eliminação decisiva;
- **`PreparedNextTurn` nunca vira ataque imediato.** É a invariante transacional
  respeitada **por construção**, não por disciplina do chamador;
- **componentes em vez de nota opaca** (resultado militar, preferência da ficha,
  qualidade da posição, adequação ao alcance, custo de movimento). É o estilo do
  `MelhorDesembarque`/`MelhorCaptura` que já roda.

A lista de "o serviço **não** deve" da investigação está correta e completa —
mantenha-a como está.

---

## 2. Cinco ajustes

### 2.1 ⚠️ O índice de arma reordena a sequência — não é rodapé

Confirmado: `BuildAttackBatch` não carrega arma, e na execução vence a primeira
opção do sensor para aquele alvo.

Logo, um serviço que ranqueia a arma B enquanto o executor sempre dispara a A
produz **promessa ≠ execução** — e este projeto já pagou por essa classe de bug
exata (foi o motivo de o Serviço do Comando virar planner único + replay).

A investigação diz "a primeira versão precisa pontuar exatamente a opção
canônica". **Isso é frágil:** fica silenciosamente errado para toda unidade
multi-arma, e ninguém percebe até uma unidade escolher mal.

**Ajuste proposto:** o serviço devolve a opção canônica **marcada como tal** e
**recusa-se a ranquear alternativas** enquanto o batch não carregar
`weaponIndex`. Limitação alta e visível, não implícita.

### 2.2 O passo 3 deve ser o passo 1

Transformar `Attack Decision` em resultado estruturado é o item **mais barato e
mais valioso** da lista:

- **`SimulationUnavailable` hoje se disfarça de `Allowed`.** Um ataque aprovado
  *porque a simulação falhou* é indistinguível de um aprovado de verdade;
- é a armadilha do **gate inaplicável**, que já custou tempo aqui: todo gate
  precisa separar *"não satisfeito"* de *"impossível/desconhecido"*;
- é refactor puro, com wrapper booleano, **zero mudança de comportamento**.

Diagnóstico imediato, risco nulo. Não deve esperar dois passos.

### 2.3 Falta o `HexEvaluator` na lista de migração

O passo 8 cita FS, Assault, Capturer, Repair "e os demais". Mas o `HexEvaluator`
é o **fallback** — o que roda quando nenhum papel responde, ou seja, o caminho
mais comum do tabuleiro.

Se ele mantiver pontuação de combate própria, **sobrevivem duas fontes de verdade
justamente no caso padrão**, e o objetivo do serviço não é atingido.

### 2.4 `longRangeStationary` e `playConservative` travam — não são ressalvas

A investigação os lista como "não prontos para interpretação automática". O
status correto é mais forte: **são decisões de design que bloqueiam**, e vêm
antes do passo 1.

`longRangeStationary` tem hoje dois contratos em uso — o tooltip diz "não
reposiciona após compra/spawn"; vários consumidores tratam como "jamais
reposiciona".

Repare no que esses dois campos são: **campos cujo significado é decidido pelo
leitor, não pelo dono.** O serviço burro estruturalmente não pode adivinhar qual
contrato vale — e escolher um dos dois em silêncio muda comportamento de combate
sem que nada no diff denuncie.

**Só o autor fecha isto.** Duas perguntas, e a implementação destrava:

1. `longRangeStationary` — "não reposiciona **após compra/spawn**" ou "**jamais**
   reposiciona"?
2. `playConservative` no combate — significa "priorizar **menor perda própria**"
   ou "**não piorar exposição**"? (hoje o campo também governa retaguarda,
   logística e transporte, então precisa de um significado *só de combate*)

### 2.5 Unificar a medição **não** unifica a escala

Ponto que a investigação assume resolvido e não está.

Os `150k / 30k / 18k` existem porque cada papel pendurou preferência na
**própria** pontuação. Se os papéis continuarem multiplicando a saída do serviço
por pesos próprios, as escalas voltam com outro nome.

O conserto não é arquitetural, é **disciplina declarada**:

> **O papel consome os componentes do `MelhorCombate` e não os reescala.** Se
> precisa de mais peso, o ajuste vai na ficha ou no predicado de admissão — nunca
> num multiplicador local.

Sem essa regra escrita, o serviço vira mais uma fonte que cada consumidor
tempera, e o problema volta em seis meses.

---

## 3. Duas extrações que servem ao capturador hoje

Independente do resto, duas peças são refactor puro e melhoram a pasta
`Capturer/` sem mudar comportamento:

| extrair | de onde | ganho |
|---|---|---|
| resolver geral de DPQ de posição | `Capturer.Attack.cs:691` | tira lógica geral do maior arquivo da pasta (899 linhas) |
| avaliador puro de um combate | espalhado | base do `CombatEvaluationService` |

Sobre o `UnitCounterEvaluator`: a ressalva da investigação procede — ele avalia
fichas em HP máximo, escolhe arma sozinho e não trabalha com a opção exata do
sensor. **A matemática econômica dele é boa e deve descer** para um avaliador
canônico que aceite HP atual e a opção do `PodeMirar`; ferramenta, Shopping e
`MelhorCombate` passam a usar o mesmo núcleo.

---

## 4. Ordem de execução

| quando | o quê | risco |
|---|---|---|
| **antes de tudo** | fechar `longRangeStationary` e `playConservative` (§2.4) | decisão do autor, não código |
| **1** | `Attack Decision` → resultado estruturado + wrapper booleano | refactor puro |
| **2** | extrair o resolver geral de DPQ | refactor puro |
| **3** | extrair o avaliador puro de um combate | refactor puro |
| **4** | `MelhorCombate` como **consulta apenas**, sem consumidor | sem risco runtime |
| **5** | ferramenta `Tools > Hotzone > Melhor Combate` | valida antes de migrar |
| **6** | migrar **um** consumidor simples | primeiro risco real |
| **7** | comparar ranking antigo × novo nos logs | **é aqui que o tempo vai** |
| **8** | migrar FS, Assault, Capturer, Repair, **HexEvaluator** | uma classe por vez |

### Enquadramento

O `MelhorCombate` completo é um **major disfarçado de serviço** — 8 passos, 7
consumidores, e o passo 7 é onde o custo real mora. Pelo esquema do autor é
**X ou Y**, nunca Z.

Mas os passos **1 a 3 são seguros e servem ao capturador hoje**, e não competem
com a migração `Capturer.Explorer → MelhorVisao` (462 linhas, serviço já
validado). Podem andar juntos.

---

## 5. O resumo em uma linha

```text
PodeMirar        diz "pode"
simulação        diz "o que acontece"
Attack Decision  diz "aceito"      ← hoje joga fora tudo menos o bool
MelhorCombate    diz "entre todas as células e combates aceitos,
                  estes são os melhores segundo a ficha"
o papel          diz "por que este combate interessa à missão"
```

O órgão já está praticamente desenhado. O que falta não é inventá-lo — é
**parar de recompor uma nota diferente em cada consumidor**.
