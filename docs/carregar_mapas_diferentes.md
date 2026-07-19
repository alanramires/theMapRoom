# Carregar save de outro mapa (load cross-scene)

Status: levantamento — nada implementado.

## Problema

Dentro de uma partida, carregar um save do **mesmo mapa** funciona. Carregar um save de **outro mapa** dá erro.

O comportamento correto é: trocar de cena e fazer o load lá.

## Diagnóstico

O bloqueio é uma guarda deliberada, não um crash — `SaveGameManager.cs:1311-1319`:

```csharp
string currentScene = SceneManager.GetActiveScene().name;
if (!string.IsNullOrWhiteSpace(data.sceneName) && !string.Equals(data.sceneName, currentScene, ...))
{
    if (blockCrossSceneLoad)
    {
        cursorController?.PlayErrorSfx();
        yield break;
    }
}
```

`blockCrossSceneLoad` é campo serializado, default `true` (`SaveGameManager.cs:68`).

**Problema de ordem:** essa checagem roda *depois* de abrir o zip, desserializar o JSON e já ter subido o panel_rodada (`SaveGameManager.cs:1231`). Hoje o jogador vê a cortina "Carregando jogo..." subir e o load abortar por baixo dela.

## O fluxo desejado já existe

`BeginLoadFromMainMenuSlot` (`SaveGameManager.cs:945`) já faz exatamente "troca de cena e carrega lá". É o caminho que o menu principal usa, e está em produção.

```text
lê apenas o metadata (barato) → descobre a cena alvo
    ↓
grava pendingMainMenuLoad (campo STATIC — sobrevive à destruição da cena)
    ↓
    ├── cena alvo == cena atual → pula LoadScene, vai direto pro pending
    └── cena alvo != cena atual → SceneManager.LoadScene(targetScene)
                                      ↓
                          SaveGameManager da cena destino, no Start()
                                      ↓
                          TryStartPendingMainMenuLoadForActiveScene()
                          confere que a cena bate → dispara LoadSlot local
```

Referências:

| Peça | Local |
|------|-------|
| Entrada do fluxo | `SaveGameManager.cs:945` |
| `pendingMainMenuLoad` (static) | `SaveGameManager.cs:30` |
| Bifurcação mesma-cena | `SaveGameManager.cs:980-986` |
| Retomada na cena destino | `SaveGameManager.cs:227` (`Start`) → `1509` |
| Porta de dentro do jogo | `BattleMapMenuRootController.cs:1271` → `OpenLoadSlotPromptFromMenu` → `LoadSlot` |

**O trabalho não é escrever o fluxo — é fazer a porta de dentro do jogo desviar pra ele quando o slot for de outro mapa.** A bifurcação mesmo-mapa/outro-mapa já está resolvida.

## A cortina: panel_rodada

O panel_rodada **já é** a cortina do load. `BeginLoadingPresentation` (`PanelRodadaController.cs:192`) ativa o painel, faz `SetAsLastSibling`, alpha 1, `blocksRaycasts`, trava input de gameplay e escreve "Carregando jogo...". `ReleaseLoadingPresentation` segura até o jogador confirmar. A janela do load inteiro já vive atrás dele.

**O furo:** o `PanelRodadaController` vive na cena de batalha e morre junto com ela.

```text
cortina sobe (cena A) → cena A destruída → cena B aparece CRUA → cortina sobe de novo (cena B)
```

Aquele flash da cena B crua é o que falta resolver. Duas saídas:

| Opção | Custo | Resultado |
|-------|-------|-----------|
| Subir o panel_rodada da cena B no `Awake`, sem esperar o `SetLoadingTeam` (`SaveGameManager.cs:1308`, que só roda depois da desserialização) | Baixo, aproveita o que existe | Ainda deixa 1-2 frames de brecha |
| Cortina persistente `DontDestroyOnLoad` (overlay separado, só preto/logo) | Peça nova | Resolve de verdade |

Recomendação: começar pela primeira e só construir a cortina persistente se o flash incomodar na prática.

## Riscos e cascatas

### 1. Guardas perdidas — BLOQUEANTE

`LoadSlot` protege contra load com IA rodando, `TurnState` errado, fila de aeronaves caindo e replay ativo (`SaveGameManager.cs:1010-1034`).

`BeginLoadFromMainMenuSlot` **não tem nenhuma dessas** — e tudo bem, porque vem do menu principal onde não há partida em andamento.

Vindo de dentro do jogo, essas guardas têm que rodar **antes** de gravar o pending e trocar de cena. Depois da troca é tarde: a partida já foi embora.

### 2. `mainMenuLoadTransitionActive` preso em `true`

Só é limpo no `finally` da corrotina de load (`SaveGameManager.cs:1370`), que roda na cena destino. Se o `LoadScene` der certo mas o load na cena B falhar antes de entrar na corrotina, a flag fica presa.

É lida por `MatchController.cs:894` e `MatchMusicAudioManager.cs:121` via `HasPendingMainMenuLoadRequest`. Vale um timeout ou clear defensivo.

### 3. `ApplyPendingNewGame` roda antes

No `Start()`, `SaveGameManager.cs:222`, antes do pending load. Se sobrar config de novo jogo pendente de um fluxo anterior, ela aplica na cena destino e o load vem por cima. Provavelmente inofensivo, mas é ordem a confirmar com log.

### 4. Música

O fluxo do menu chama `BeginTurnTransition` + `StopForTurnTransition` antes de trocar de cena (`MainMenuLoadPanelController.cs:656-660`). Saindo de uma partida, sem isso a trilha do mapa A continua tocando por cima do mapa B.

### 5. Confirmação do jogador

Trocar de cena descarta a partida em andamento de forma **irreversível**. Hoje o `blockCrossSceneLoad` é o que impede isso por acidente.

Se a guarda cair, o prompt precisa deixar explícito que o mapa atual será abandonado. O label do slot já mostra o nome do mapa (`MainMenuLoadPanelController.cs:592-598`) — dá pra reaproveitar.

## Ordem sugerida

1. Guardas de persistência (risco 1) rodando antes de qualquer decisão de troca de cena.
2. Decisão mesmo-mapa/outro-mapa a partir do **metadata**, antes de abrir o zip.
3. Desvio da porta de dentro do jogo para o fluxo pending.
4. Prompt de confirmação com nome do mapa (risco 5).
5. Música (risco 4).
6. Cortina — panel_rodada cedo na cena destino.
7. Clear defensivo da flag (risco 2).

Os itens 1 e 2 são pré-requisito; o resto é polimento em cima de um fluxo já funcionando.
