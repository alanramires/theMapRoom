# D3 — catálogo de papéis (UnitRoleCatalog)

Registro: 2026-11-11, decidido na `pergunte-me` sobre a matriz de grupo de combate.
Base: D1 `e8b6676`, D2 `43c0f51` + `53dce5f` + `0e1e1c6`.

**Estado: implementado pelo Claude em 2026-11-11 (sem commit ainda); aguarda revisão do autor em Play e na janela** — mexe em
`UnitRole.cs`, no Shopping, nas vagas do rally e na janela; não mexe na prontidão.

## Por que

Hoje a matriz vive em quatro `switch` de `Assets/Scripts/Units/UnitRole.cs`:
`ResolveFamily` (linha 99), `ResolveForce` (144), `ResolveBattleParticipation` (160),
`CanSatisfy` (206) e `ResolveCompositionRole` (287). É catálogo morando em código, o
oposto de "o catálogo diz o que uma coisa É". Consequência vista em Play: a prontidão do
rally conta Caça B como Ruptura, mas `CanSatisfy(Interceptador, Assalto)` é falso, então
a vaga de ruptura não aceita o caça e o Shopping segue comprando só blindado.

Frase do autor: "se um dia eu quiser que caças sejam dissuasão é só eu mesmo mudar a flag."

## O que entra

### 1. O asset

`UnitRoleCatalog` (ScriptableObject, **um só**, em `Assets/DB/`), uma linha por `UnitRole`
(exceto `None`):

| campo | tipo | hoje vem de |
|---|---|---|
| `role` | `UnitRole` | chave |
| `familia` | `UnitRoleFamily` | `ResolveFamily` |
| `forca` | `UnitForce` | `ResolveForce(familia)` — passa a ser por papel, não por família |
| `participacao` | `UnitBattleParticipation` | `ResolveBattleParticipation` |
| `contaComo` | `UnitRole` | `ResolveCompositionRole` (como o Shopping conta a ficha) |
| `satisfazTambem` | `List<UnitRole>` | `CanSatisfy` |

O asset **nasce com a tabela de hoje** (D1 inclusa: AtaqueAereo e ArtilheiroCombatente em
FogoDeSuporte), transcrita por um script de editor uma vez; depois é editado à mão.
Comportamento no primeiro commit: idêntico ao atual, provado pelas fixtures.

### 2. `UnitRoleCompatibility` lê o asset

Os cinco `switch` morrem. **Sem fallback para o código**: sem asset, erro alto no
`[RuntimeInitializeOnLoadMethod]` e na janela. Nada é "shipado"; não há migração a proteger.
As regras mecânicas de `CanSatisfy(UnitData, …)` (quem carrega satisfaz Transportador, quem
supre satisfaz Logística) continuam em código: são capacidade da ficha, não rótulo.

### 3. Vagas do rally por Força

`EnsureRallyAssemblySlots` / `EnsureRallyRoleSlots` em
`AIController.PlanEvaluator.RallyPoints.cs` (linhas 631–679): a vaga de ruptura é
preenchida por qualquer papel cuja Força seja Ruptura; a de suporte, por qualquer
Dissuasão. `CountOpenSlots` do Shopping passa a ver a vaga pela Força. Isso fecha o buraco
"conta como ruptura mas não preenche a vaga".

### 4. Shopping pergunta ao catálogo

Toda chamada a `CanSatisfy`/`ResolveCompositionRole` em `3. Shopping/` já passa a ler o
asset pela mudança 2; **não muda regra** de núcleo 2/2/1, elite, nem rota terrestre
fechada (ilha). Para o caça virar "porradeiro" basta o autor marcar
`Interceptador.satisfazTambem = [Assalto]` no asset.

### 5. `Tools > Role Families` vira editor

Mantém as duas visualizações (família, grupo de combate). Cada linha de papel ganha os
campos editáveis do asset (família, Força, participação, conta como, satisfaz também) com
`Undo` e `SetDirty`. As fichas continuam só leitura, listadas pela essência. A coluna ⚠
(composição diverge da família) segue derivada do asset.

### 6. Fixtures

- `verificar_matriz_forcas.cs`: mesma saída de antes (Soldados+Z → Assalto×2/Fogo×0, etc.).
- nova `verificar_catalogo_papeis.cs`: (a) asset carrega e cobre todo `UnitRole`;
  (b) clonar o asset em memória, marcar `Interceptador` satisfaz `Assalto`, e uma vaga de
  ruptura do rally aceita Caça B; desmarcar, não aceita; (c) `contaComo` de
  `ArtilheiroCombatente` = `FogoIndireto` continua movendo o Pesado para a demanda de Fogo.
- regressões: `verificar_prontidao_rally`, `verificar_tela_rally`,
  `verificar_shopping_rally_acesso`, `verificar_demanda_parede_artilharia`,
  `verificar_politicas_sem_plano`.

### 7. Docs

Diário do dia com o que só compilou. A doutrina da matriz (`docs/AI Behavior/Forcas.md`)
é da skill `consolidar-doutrina`, chamada pelo autor; não escrever aqui.

Um commit, parar para revisão.

## Decidido junto (não é código da D3)

- **Caça A conta ruptura** com peso 1 (interceptador puro é a superioridade aérea que a
  invasão por ilha precisa).
- **D4 = formação ativa**: Ruptura posicionada na vanguarda e flancos, as outras três
  Forças na retaguarda, usando a `RallyArea` que a tela já calcula. Só depois do Play da D2.
- Rally naval, bandeira/modelo naval e core naval 4+4: degrau 7, nota e matriz em
  [d2_prontidao_plano.md](d2_prontidao_plano.md).
