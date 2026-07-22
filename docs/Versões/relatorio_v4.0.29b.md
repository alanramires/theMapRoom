# v4.0.29b - Tutorial para novatos em andamento

## Foco

Ciclo de direcao de cena da Historia 1: o contato com o inimigo virou sequencia cinematografica (spawn no turno da IA, camera, reacao do Sargento, ordem de segurar o morro e o primeiro tiro), sustentada por tres recursos novos no motor de roteiro e pelo refactor do fluxo de falas.

## Refactor do fluxo de falas (TutorialData)

- Cada fala agora declara `advance` — quando ela avanca para a seguinte: `Immediate`, `Objective Completed`, `All Units Acted`, `Player Turn Started` ou `Enemy Turn Started`.
- `objectiveKey` + `revealObjective` unificados: a mesma key serve para revelar a tarefa e/ou gatear o avanco. Campos legados (`waitObjectiveKey/Index`, `waitAllUnitsActed`, `waitPlayerTurnStart`, `revealObjectiveKey/Index`) escondidos, com migracao automatica em `MigrateLegacyDialogFlow`.
- `turn` virou enum re-travavel (`No Effect` / `Locked` / `Unlocked`): o roteiro pode travar e destravar o passar a vez quantas vezes quiser.
- Contadores de turno edge-triggered por fala (jogador e inimigo em separado): gates imunes a eventos antigos ou da carga da cena.

## Tres recursos novos do motor

- **`Enemy Turn Started`**: fala que dispara quando o turno da IA comeca — e o gatilho da direcao de cena no campo inimigo.
- **Fala muda**: fala com texto vazio executa os comandos (`spawnCommand`/`statCommand`/`turn`/`movement`) sem abrir o balao. Voltar/Avancar no historico pulam falas mudas; bronca em cima de fala muda devolve o painel escondido.
- **Movimento em 3 niveis** (`movement` por fala): `Locked` (nem mover, nem manter), `Hold Only` (manter posicao/atacar parado SIM, sair da celula NAO) e `Unlocked`. Bronca propria para o Hold Only ("Ninguem desce desse morro, recruta!"), configuravel no asset. O atalho "M" ficou sem guard no Hold Only por design: ele finaliza a unidade onde ela ja esta (nao move ao cursor), entao nunca viola a ordem — sair da celula ja e barrado no confirm.

## Historia 1 — o contato, do jeito certo

1. Ryan chega na bandeira → "Missao dada... passe a vez" (avanca no turno do inimigo).
2. **Turno da IA**: fala muda spawna o soldado inimigo na estrada (`slot1 SD 7,-2 acted`) com pan da camera — ele aparece e NAO se move (nasceu "ja agiu").
3. Turno do jogador: "Espere! Estou vendo movimento na estrada" + pan ate o inimigo.
4. "Em posicao defensiva... Use Manter posicao" → revela HOLD_POSITION + `movement: Hold Only`.
5. Segurou o ponto → "Muito bem" → "Passe a vez e observe a infantaria inimiga. Quem age, espera."
6. **Turno da IA**: automata avanca 7,-2 → 4,-2 (adjacente ao Ryan) sem atirar.
7. Turno do jogador: "Contato a frente" revela ATTACK_UNIT → tiro de cima da montanha, parado (Hold Only permite).

- Objetivos renumerados `hist_1_01..hist_1_07`; tarefas de END_TURN removidas (passar a vez nao precisa ser tarefa); keys mortas (`hist_1_08/09`) e spawn duplicado nos parameters do ATTACK limpos.
- Asset de automata criado: `Tutorial 1 - Inimigos na Estrada` (marcha por `moveTowardsTarget`).

## Turno do inimigo com presenca

- `TutorialEnemyTurnIndicator`: caixa central pulsante "TURNO DO INIMIGO" (mesmo visual do "Turno da IA" do Battle Map), com estagio "MOVIMENTANDO TROPAS..." / "OBSERVANDO O CAMPO...". Auto-instala em toda cena de tutorial (`IsTutorialMode`), sem passo manual no editor; nao duplica quando ha AIController.

## Fix: spawn respeitando o flip do MatchController

- Causa raiz: `ConstructionSpawner.Start()` chamava o auto-computo de flip cru e sobrescrevia os overrides por slot (ordem de Start indeterminada).
- `RecomputeTeamFlips()` virou o ponto unico (auto → overrides → aplicar na cena); o auto-computo ficou privado; OnValidate em play aplica ao vivo (trocar Normal/Espelhado no inspector reflete na hora).
- Override de flip por slot (`Auto`/`Normal`/`Espelhado`) preservado no save por slot.

## Editor QoL

- Botoes por fala no inspector do roteiro: mover para cima/baixo, duplicar e remover — sem arrastar pro fim da lista.
- Cabecalhos dos objetivos concatenam key + tipo ("hist_1_01 — CAMERA_ZOOM").
- Drawer da fala mostra `objectiveKey` so quando relevante (reveal ligado ou advance por objetivo).

## Estado

- Tutorial para novatos segue em andamento.
- Pendencias da Historia 1: vozes do Sargento (falas e broncas), retrato de bronca opcional, playtest completo do contato (corrida vitoria vs falas finais apos o tiro).
