# AI Shopping

> **Estado:** primeiro documento deste modulo. O §1 e §2 sao **inventario
> verificado** — arquivo e linha conferidos. O §3 e desenho, sem implementacao.
> O §4 lista o que NAO foi verificado, para ninguem planejar em cima.

---

## 1. O buraco: elegibilidade existe, preferencia nao

O shopping tem duas perguntas a fazer sobre uma unidade, e **so uma delas tem
mecanismo**:

```text
POSSO comprar esta para o slot?   UnitRoleCompatibility.CanSatisfy    existe
DEVO comprar esta AGORA?          — nada —                            nao existe
```

Sem a segunda, a preferencia foi escrita com o mecanismo da primeira: **virou
nome de papel**. A prova esta escrita literalmente:

```csharp
// AIShoppingPlanner.Demand.cs:2870
case AINeedKind.AAA: role = UnitRole.Antiaereo; exact = UnitRole.AntiaereoCombatente; break;
```

`role` e a elegibilidade. `exact` e a preferencia. As duas expressas como valor
de enum, porque nao havia outro lugar.

## 2. Os tres papeis-fantasma

Sao papeis que existem para o shopping conseguir comprar, nao para descrever
comportamento. O autor confirmou a intencao dos tres.

### 2.1 `CapturadorAgressivo` (12) — bazooka e metranca

**Cinco dos oito usos reais sao de shopping:**

```text
SHOPPING (5)
  AIShoppingPlanner.cs:1157              boughtAggressiveCapturer
  Demand.cs:3067, 3074                   capturador de verdade ganha do agressivo no slot
  UnitPicker.cs:201, 427

TAXA DE CAPTURA (1)   PodeCapturarSensor.cs:152     o 0.5
RESERVA (1)           CaptureOpportunityClaimService.cs:603
COMPORTAMENTO (1)     AIController.Capturer.Agressive.cs
```

Motivo declarado pelo autor: *"forcadinha do shopping — bazookas e metrancas tem
melhor rating e sao baratos na defesa"*. Isso e a **matriz arma x classe**
(Tools > Units > Unit Analysis), propriedade mensuravel da unidade — nao
identidade.

> Rating modelado como identidade. E a mesma doenca do `MelhorVisao` contando
> hexagono onde a moeda era contato: a conta esta certa, a unidade de medida e de
> outra pergunta.

### 2.2 e 2.3 `ArtilheiroCombatente` (13) e `AntiaereoCombatente` (14)

**Sao o mesmo comportamento**, e o codigo ja os trata como um:

```csharp
// AIController.FireSupport.Combatant.cs:8-9
return HasPrimaryRole(unit, UnitRole.ArtilheiroCombatente)
    || HasPrimaryRole(unit, UnitRole.AntiaereoCombatente);
```

Um predicado, dois papeis. O comportamento e *"atira de longe primeiro; se nao
der, vai pra porrada"*. O que difere entre os dois e a **familia da arma** —
`WeaponCategory.AntiAerea` contra a da artilharia —, e a arma **ja carrega isso**.

Decompondo os tres valores:

```text
um comportamento     "longe primeiro, corpo a corpo depois"   -> ordem / RoleData
uma familia de arma  AntiAerea vs AntiTanque                  -> JA esta na arma
uma preferencia      "compre este quando X"                    -> precisa de canal
```

## 3. O canal certo ja existe, encostado no errado

```csharp
// AIShoppingPlanner.CounterPressure.cs
public WeaponCategory CounterCategory;
public float Get(WeaponCategory category)   // AntiInfantaria, AntiTanque, AntiAerea, AntiNavio
public WeaponCategory DominantCategory
```

**O `CounterPressure` ja raciocina em capacidade**, com pressao por categoria e
categoria dominante. O `Demand` raciocina em nome de papel. Os papeis-fantasma
moram no segundo porque o primeiro nao estava la quando eles nasceram.

**CONTRATO:** a preferencia de compra deve ser lida da **capacidade** (matriz
arma x classe, `WeaponCategory`, custo), nunca de um nome de papel. Papel
responde *pode preencher o slot*; capacidade responde *vale a pena agora*.

### 3.1 A ordem de remocao

`UnitRole` tem precedente de remocao e ele esta comentado no proprio enum
(`UnitRole.cs:13-24`, o antigo `RaidAntiSub = 11`), com a garantia que torna isso
barato:

> **`UnitData.roles` NAO e persistido no save** — o load recupera a ficha pelo
> `unitId`. O risco e asset, prefab e cena; nao e arquivo de partida.

```text
13 e 14  o caso limpo: ja compartilham predicado, entao fundir NAO muda
         comportamento. Muda so quantos nomes existem para a mesma coisa.
         1. unificar num papel so (ou num RoleData com a ordem)
         2. a demanda passa a pedir WeaponCategory + alcance, nao o nome
         3. tirar 13 e 14 do enum, com comentario "nao reutilizar"

12       depende do item 10 do docs/ideias_futuras.md (chave de eficiencia):
         1. chave "Capturador Alternativo" 0.5              ja existe
         2. listar nas construcoes capturaveis
         3. trocar nas fichas hoje CapturadorAgressivo
         4. auditar (CaptureKeyAuditor): nenhuma ficha com o papel sem a chave
         5. FindSwapIncomingCapturer passa a comparar CAP POWER, nao HP    <- senao
            fica uma janela em que a chave vale e o swap ainda compara errado
         6. tirar o roles[0] == CapturadorAgressivo do GetCapturePower
         7. dar ao shopping o gancho de preferencia pela matriz
         8. tirar o 12 do enum
```

O passo 7 e o que este documento acrescenta ao item 10: **sem ele, remover o
papel apaga a demanda defensiva barata sem substituto**, e a IA para de comprar
bazooka quando apanha.

## 4. O que NAO foi verificado

- **A semantica exata de `exact`.** A forma e de preferencia (`role` + `exact`
  lado a lado), mas nao conferi se ele e requisito duro ou desempate.
- **Se o `CounterPressure` e alcancavel** para estas demandas, ou se so serve o
  caminho de contra-pressao. Pode ser que o canal exista e nao chegue ate aqui.
- **O "abre passagem" do `Capturer.Agressive`.** Se for **selecao de alvo**
  (atirar no que bloqueia a rota), e politica de verdade e sobrevive a remocao do
  papel; se for so ordem, dissolve junto. Li os logs, nao a logica.
- **Nenhuma partida** foi rodada para nada deste documento.

## 5. Leituras

| documento | por que |
|---|---|
| `docs/ideias_futuras.md`, item 10 | a chave de eficiencia de captura e o roteiro seguro |
| `Units/Capturer/Capturer.md` | o lema do capturador, e por que o agressivo nao precisa de papel |
| `docs/AI Behavior/ficha_do_papel.md` | papel como dado; ordem e politica, nao identidade |
| `Assets/Scripts/Units/UnitRole.cs:13-24` | como um papel foi removido antes, e por que foi barato |
