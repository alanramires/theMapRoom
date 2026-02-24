# Relatorio Tecnico - v1.0.11

## Versoes cobertas

- `v1.0.10` (remoto existente) - commit `950c348` - `sistema de pouso e decolagem completo, sistema naval e submersivel`
- `v1.0.11` - commit `7820e7a` - `v1.0.10 - combate basico terminado`
- Branch: `main`

Este relatorio consolida o pacote de combate fechado em `v1.0.11`, incluindo o contexto da `v1.0.10` ja existente no remoto.

---

## 1) Fluxo de combate em gameplay (estado "A")

### Objetivo

Sair do placeholder de "confirmar e encerrar" para uma sequencia visual/sonora completa.

### O que entrou

1. Confirmacao de alvo com subetapas:
- ciclo de alvos
- confirma alvo
- rollback por `ESC`

2. Sequencia de combate:
- audio inicial por tipo de tiro (straight/parabolic)
- efeitos de impacto
- voo dos projetis
- aplicacao de HP no momento correto
- tratamento de morte
- finalizacao da acao

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`

---

## 2) Projetil e audio por arma

### Objetivo

Cada arma disparar com seu proprio som e sprite de municao em voo.

### O que entrou

1. `WeaponData`:
- `fireSfx`
- `fireSfxVolume`
- `ammunitionSprite`

2. Override de escala por arma:
- `useExplicitProjectileScale`
- `projectileScale`
- fallback para escala global do `AnimationManager`.

3. Projetil em reta/parabola no `AnimationManager`.

### Arquivos-chave

- `Assets/Scripts/Weapons/WeaponData.cs`
- `Assets/Scripts/Match/Animation/AnimationManager.cs`

---

## 3) Hit FX e resposta visual no alvo

### Objetivo

Dar feedback claro de dano no impacto.

### O que entrou

1. Taking hit por sprite sequence (`hit1..5`).
2. Flash vermelho/branco.
3. Shake na unidade atingida.

### Arquivo-chave

- `Assets/Scripts/Match/Animation/AnimationManager.cs`

---

## 4) Bump de combate ("bicada")

### Objetivo

Adicionar antecipacao corporal antes do disparo.

### O que entrou

1. `bump together`:
- quando ambos lados atacam em `Straight` e existe revide.

2. `bump towards`:
- quando apenas um lado ataca em `Straight`.

3. Regras:
- tiros parabolicos ficam fora do bump.

### Arquivos-chave

- `Assets/Scripts/Match/Animation/AnimationManager.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`

---

## 5) Morte: blink, explosao e sumico

### Objetivo

Executar morte em ordem visual correta.

### O que entrou

1. Fluxo por unidade morta:
- espera `combatDeathStartDelay`
- cursor vai ate a unidade (com som de cursor)
- unidade pisca varias vezes
- unidade some
- toca `explosion` (SFX) junto do VFX de explosao
- pequena pausa

2. Dupla morte:
- trata em sequencia.

3. Ajuste de blink:
- prioriza o sprite principal da unidade (nao tile/hex overlay).

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Match/Animation/AnimationManager.cs`
- `Assets/Scripts/Units/UnitManager.cs`

---

## 6) Dano proporcional em cadeia de embarcados

### Objetivo

Propagar impacto do transportador para unidades embarcadas (inclusive recursivo).

### O que entrou

1. Se transportador leva `X%` de dano:
- embarcados levam `X%` do HP atual.
- embarcados dos embarcados tambem.

2. Se transportador morre:
- cadeia embarcada morre sem animacao individual.

3. Combatente direto morto:
- permanece para animacao de morte (nao e removido cedo pela cascade).

### Arquivo-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`

---

## 7) Ajustes de UX de mirando

### Objetivo

Reduzir friccao no uso do sensor de ataque.

### O que entrou

1. Auto-select quando existe apenas 1 alvo.
2. `ESC` no confirma de alvo unico volta para sensores de movimento.
3. Linha de mira oculta no substep de confirmacao.

### Arquivo-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`

---

## 8) Ajustes auxiliares

### O que entrou

- reforco de auto-bind de DBs no `TurnStateManager` para evitar divergencia de calculo entre tools e gameplay.
- correcoes em cursor/camera e fluxo de neutral/hq aplicadas no periodo.

### Arquivos-chave

- `Assets/Scripts/Match/TurnStateManager.cs`
- `Assets/Scripts/Cursor/CursorController.cs`
- `Assets/Scripts/Camera/CameraController.cs`
- `Assets/Scripts/Match/MatchController.cs`

---

## Assets e conteudo adicionados/organizados

- `Assets/audio/combat/explosion.MP3`
- `Assets/img/animations/hit/*`
- `Assets/img/animations/explosion/*`
- `Assets/img/armas/*`
- `Assets/img/municao/*`

---

## Resumo de volume (base `950c348` -> `7820e7a`)

- `96 files changed`
- `23191 insertions`
- `9338 deletions`
