# v1.6.4 — AI e Iniciativa

## Contexto

Após a v1.6.3 consolidar o sistema de perfis declarativos, esta versão introduz controle explícito de **ordem de turno** para unidades da IA, refina o sistema de captura com o nível `None`, adiciona um ícone de debug para modo reparo e corrige navegação no menu.

---

## Mudanças

### 1. `AIInitiative` — ordem de turno declarativa

**Problema:** a ordem em que as unidades da IA agiam dentro de um turno era indefinida (dependia da ordem interna da lista de instâncias). Artilharia e capturadores podiam agir em qualquer sequência.

**Solução:** novo enum `AIInitiative` (Priority / High / Medium / Low) adicionado ao `AIUnitProfile`. Antes do loop de turno, as unidades são ordenadas por `initiative` crescente.

| Valor | Ordem | Perfis |
|---|---|---|
| Priority | 1º | Artilheiro, Estacionaria, Kamikaze |
| High | 2º | Lutador |
| Medium | 3º | Bazooka, Híbrido, Supridor |
| Low | 4º | Capturador |

**Tiebreaker de HP:** dentro do mesmo nível de initiative, unidades com HP mais alto agem primeiro. Capturadores mais inteiros executam as missões críticas; os mais fracos ficam como reserva.

---

### 2. `CaptureInterruptBias.None` — rush puro de captura

**Problema:** mesmo com `Passive` (limiar 38.000), o capturador ainda podia ser interrompido por inimigos de alto score, e o fallback oportunista pós-movimento continuava ativo.

**Solução:** adicionado `None = -1` ao enum `CaptureInterruptBias`. Com `None`:
- Nenhum inimigo gera `assignedEnemy` em pré-movimento (descartado em `AssignTargetForUnit`).
- O sensor Attack em Phase2 não faz fallback para `FindClosestVisibleEnemy`.
- O "morde no caminho" e o fallback oportunista pós-movimento são bloqueados completamente.
- **Exceção única:** inimigo **no próprio hex objetivo** sempre é atacado para desbloquear a captura.

**Asset atualizado:** AI Capturador → `captureInterruptBias: None`.

---

### 3. `SectorEnemy` não ressuscitava `assignedEnemy` zerado pelo bias

**Problema:** após `AssignTargetForUnit` descartar o alvo (bias `None`), o bloco `unitIntent.SectorEnemy` em Phase2 reatribuía um inimigo ao `assignedEnemy` — contornando o descarte.

**Fix:** gate adicionado após a atribuição de `SectorEnemy`: se `captureRoleFilter` e `bias == None`, o `assignedEnemy` é re-zerado caso o inimigo não esteja no hex objetivo.

---

### 4. Ícone de manutenção (debug)

Quando `showPlanDebugAtUnit` está marcado no `AIPlayerController`, unidades em modo reparo exibem um ícone de chave+martelo sobre o sprite durante o turno da IA.

- Campo `maintenanceIconImage` adicionado ao `UnitHudController` (sob o header AI Stance).
- Método `SetAIMaintenanceActive(bool)` adicionado ao `UnitManager`.
- Visibilidade segue as mesmas regras do badge de stance: só aparece se `showPlanDebugAtUnit` estiver ativo e a unidade for IA.
- Ferramenta de desenvolvimento — não aparece em build final.

---

### 5. Navegação de menu — botões desabilitados

**Problema:** ao abrir o menu durante o turno da IA, alguns botões ficavam desabilitados (Status, Comando, Rodada, Destruir, Render), mas o cursor navegava normalmente por eles e o foco inicial era sempre o índice 0 (potencialmente um botão desabilitado).

**Fix em `Navigate()`:** a navegação por cima/baixo agora pula botões com `interactable = false`, procurando o próximo habilitado na direção pressionada.

**Fix em `SetPanel()`:** ao abrir ou trocar de painel (`resetIndex: true`), o foco inicial é definido no **primeiro botão interactable** da lista em vez de sempre ir para o índice 0.

---

## Arquivos modificados

| Arquivo | Tipo de mudança |
|---|---|
| `Assets/Scripts/AI/AIInitiative.cs` | Novo — enum Priority/High/Medium/Low |
| `Assets/Scripts/AI/CaptureInterruptBias.cs` | Adicionado `None = -1` |
| `Assets/Scripts/AI/AIUnitProfile.cs` | Campo `initiative` em `AIUnitProfile` |
| `Assets/Scripts/AI/AIPlayerController.cs` | Ordenação por initiative+HP, gate SectorEnemy, sensor Attack sem fallback para capturador None |
| `Assets/Scripts/Units/UnitManager.cs` | `aiMaintenanceActive`, `SetAIMaintenanceActive()`, `RefreshAIAssignedPlanBadge` atualizado |
| `Assets/Scripts/Units/UnitHudController.cs` | `maintenanceIconImage`, `SetMaintenanceIconVisible()` |
| `Assets/Scripts/UI/BattleMapMenuRootController.cs` | `Navigate()` pula desabilitados, `SetPanel()` foco no primeiro habilitado |
| `Assets/DB/AI/AI Capturador.asset` | `captureInterruptBias: None`, `initiative: Low` |
| `Assets/DB/AI/AI Artilheiro.asset` | `initiative: Priority` |
| `Assets/DB/AI/AI Estacionaria.asset` | `initiative: Priority` |
| `Assets/DB/AI/AI Kamikaze.asset` | `initiative: Priority` |
| `Assets/DB/AI/AI Lutador.asset` | `initiative: High` |
| `Assets/DB/AI/AI Bazooka.asset` | `initiative: Medium` |
| `docs/AI Unit Profile.md` | Seção Turn Order, initiative em todos os perfis, ícone de manutenção |

---

## Resultado esperado

- Artilharia e Kamikaze agem antes de combatentes, que agem antes de capturadores
- Capturadores mais inteiros executam a missão principal; os mais fracos seguem como reserva
- Capturador com `None` nunca abandona a missão por oportunismo — rush puro até o objetivo
- Ícone de chave+martelo aparece sobre unidades em reparo quando debug está ativo
- Menu abre com foco no primeiro botão habilitado; navegação não para em botões desabilitados
