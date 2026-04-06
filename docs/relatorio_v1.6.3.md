# v1.6.3 — Escolta e Artilheiro em IA

## Contexto

Após a v1.6.2 introduzir flags declarativas para substituir `combatClassification`, os testes revelaram três bugs comportamentais e uma lacuna no sistema de perfis. Esta versão endereça tudo isso.

---

## Mudanças

### 1. `CaptureInterruptBias` — controle declarativo do limiar de interrupção de captura

**Problema:** o limiar para abandonar captura e atacar ("morde no caminho") estava hardcoded com uma heurística baseada na ordem dos sensores (`bazookaSkirmisherUnit ? 22000 : 28000`). Não era configurável, não era legível no Inspector e se aplicava apenas ao bloco pós-movimento.

**Solução:** novo enum `CaptureInterruptBias` (Passive / Normal / Aggressive) adicionado como campo em `AIUnitStanceBehavior`. O limiar é aplicado agora nas **duas fases** onde o capturador pode atacar:
- **Pré-movimento** (`AssignTargetForUnit`): se o score do alvo for menor que o limiar, o alvo é descartado antes de a unidade sequer se mover.
- **Pós-movimento** (`Phase2_MoveUnit`): o "morde no caminho" e o fallback oportunista já usavam o limiar; agora usa o mesmo valor da enum.

**Assets atualizados:**
- AI Bazooka → `Aggressive` (22.000) — interrompe captura com facilidade
- AI Capturador → `Passive` (38.000) — raramente abandona a missão

---

### 2. Escolta não atacava ao se aproximar do inimigo

**Problema:** tanques de escolta (AI Lutador) recebiam um alvo válido de `AssignTargetForUnit` mas ficavam em coesão de plano em vez de avançar para engajar. O log mostrava `score=25405 vencedor: Soldado_T0_U196` seguido de `acao: coesao`.

**Causa raiz:** o sensor Attack no loop de planejamento de `Phase2_MoveUnit` estava gateado em `turnStateManager.HasAutomatedAttackAvailable()`, que verifica se há ação de ataque disponível na posição *atual* da unidade. A unidade de escolta estava longe do inimigo → sem alvo no alcance atual → `'A'` não disponível → sensor Attack pulado → `targetEnemy = null` → coesão.

**Fix:** removido `HasAutomatedAttackAvailable()` da condição do sensor Attack no loop de *planejamento de movimento*. O gate permanece na execução real do ataque (linhas 3083 e 3093). O sensor agora planeja o movimento em direção ao inimigo mesmo que ele ainda esteja fora de alcance — o ataque ocorre ao chegar.

---

### 3. Artilheiro sem plano avançava para o ninho do inimigo

**Problema:** o `LançaFoguetes` sem plano atribuído marchava em direção ao HQ inimigo e entrava no cluster de unidades inimigas. Esperado: ancorar na posição atual e aguardar.

**Causa raiz:** dois problemas combinados:
1. Sem plano e sem alvo alcançável, a cascata de `moveTarget` caia no fallback `snapshot.EnemyHqs[0]` → avanço genérico em direção ao HQ inimigo.
2. O bloco `repositionToFireRange`, quando não encontrava célula em faixa de alcance, sobrescrevia `moveTarget` com a posição da âncora inimiga e avançava em direção a ela — ignorando que a unidade deveria ficar parada.

**Fix (código):** o fallback de avanço dentro do bloco `repositionToFireRange` agora respeita `holdGroundWhenIdle`: se a flag estiver ativa e não houver célula de tiro viável, a unidade não sobrescreve `moveTarget` — âncora onde está.

**Fix (config):** adicionado `holdGroundWhenIdle: true` à stance de Ataque/Invasão do AI Artilheiro. Em Defesa já existia `retreatToHqWhenIdle`.

---

### 4. `captureInterruptBias` — pré-movimento faltava

**Problema:** mesmo com `captureInterruptBias: Passive`, o capturador atacava inimigos em pré-movimento porque `AssignTargetForUnit` não aplicava o limiar — apenas o filtro de corredor.

**Fix:** após o loop de scoring em `AssignTargetForUnit`, se a unidade tem papel de captura (`captureRoleFilter`), o score vencedor é comparado ao limiar da enum. Se abaixo, o alvo é descartado e a unidade não recebe `assignedEnemy`.

---

## Arquivos modificados

| Arquivo | Tipo de mudança |
|---|---|
| `Assets/Scripts/AI/CaptureInterruptBias.cs` | Novo — enum Passive/Normal/Aggressive |
| `Assets/Scripts/AI/AIUnitProfile.cs` | Campo `captureInterruptBias` em `AIUnitStanceBehavior` |
| `Assets/Scripts/AI/AIPlayerController.cs` | 4 fixes: sensor Attack gate, captureInterruptBias pré-mov, repositionToFireRange + holdGround, remoção de `bazookaSkirmisherUnit` |
| `Assets/DB/AI/AI Artilheiro.asset` | `holdGroundWhenIdle: true` na stance de ataque |
| `Assets/DB/AI/AI Bazooka.asset` | `captureInterruptBias: Aggressive` |
| `Assets/DB/AI/AI Capturador.asset` | `captureInterruptBias: Passive` |
| `docs/AI Unit Profile.md` | Atualizado com todos os novos campos e comportamentos |

---

## Resultado esperado

- Escolta (AI Lutador) avança e engaja inimigos designados mesmo partindo de longe
- Capturador (AI Capturador, Passive) ignora alvos com score < 38.000 tanto no planejamento quanto no pós-movimento
- Artilheiro sem plano ancora na posição e aguarda — não vaga em direção ao inimigo
- Bazooka interrompe captura facilmente ao encontrar alvos no caminho (Aggressive)
