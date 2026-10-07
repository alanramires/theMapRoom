# v9.3.2 — A aula 2 joga contra uma IA que briga

A v9.3.1 fechou a aula 1. Esta versão é a aula 2 (Extração) jogada de verdade,
e o que ela expôs não estava no roteiro: estava na **IA rebelde**, que não sabia
o que fazer num tabuleiro sem prédio para capturar, e no **save**, que não
guardava a aula.

O fio do dia: **uma aula sem prédio é o primeiro mapa em que a IA rebelde não
tem âncora.** Cada "o azul ficou parado" levou a um ramo que assumia que sempre
haveria algo para capturar ou consertar.

---

## 1. O save guarda a aula — e o mundo

Carregar no meio da aula recomeçava da fala 0 e repetia os spawns por cima do
tabuleiro salvo. O tabuleiro o save já tinha; o **roteiro** não.

`SaveGameData.tutorial` agora leva a fronteira do roteiro, os spawns/comandos já
executados, o estado de cada tarefa, as travas e os carimbos de turno (para "espera
o próximo turno" continuar esperando o MESMO turno). A trava de captura foi para o
save da construção.

O irmão do bug: save da Academia abria no Fixture. Batalha e Campanha só conheciam
o mundo serializado nelas. Agora há **Mundos conhecidos** nas duas, o mundo é
resolvido pelo `mundoId` do save, e o **manifesto** leva o mundo — a Campanha
escolhe antes de montar o mosaico. Sem isso ela nascia no Fixture por um instante
e recarregava ("a dança de mundos"). Save antigo, sem o campo, ainda dança uma vez.

## 2. A IA rebelde num mapa sem âncora

Três ramos assumiam que sempre haveria prédio:

| ramo | o que fazia | agora |
|---|---|---|
| capturador sem capturável | desistia (`HexEvaluator` → esperar) | **caça** o inimigo visível mais próximo |
| reparo | marchava até a bandeira do próprio time, que não conserta nada | sem prédio que conserte: a trava de HP cai; sem fusão à vista, volta ao combate |
| Apache sem plano | orbitava o capitão (o soldado rebelde) e ignorava o aluno | inimigo no Operacional dele vira a âncora, antes do capitão |

**A caça desistia duas vezes antes de atirar**, e cada uma foi um achado:

- `PassesAttackDecision` recusava toda troca do soldado ferido, sem log. Caçando é
  último recurso: segunda passada sem o filtro.
- `HasEnemyInEngageRadius` mede com `Vector3Int.Distance` — **reta na grade, não
  hex**. Inimigo dentro do tático real ficava fora da porta. Caçando, a varredura
  roda sempre. ⚠️ A mesma porta serve os outros capturadores e provavelmente erra
  para eles também; não foi mexida, porque mudaria a IA com QG.

Tudo isso vale **só para facção sem plano** (rebelde). A IA com QG sempre tem um
prédio que conserta e um plano, e a patrulha aérea do capitão é doutrina validada.

## 3. O motor das aulas ganhou peças de roteiro

O que a aula 2 pediu virou vocabulário:

- **fala avulsa** (`announceText`): o Sargento fala quando a tarefa completa, fora
  da fila. Fato que pode ou não acontecer não pode ser fala do roteiro — se nunca
  acontecer, o roteiro trava.
- **`completeCommand`** + **`money`** + **`spawn` dentro de comando**: a tarefa tem
  consequência. Com `complete <key>`, vira o padrão "o que vier primeiro, uma vez só"
  (Apaches pelo embarque OU pela rodada 10).
- **`activeUntilKey`** para derrota E gatilho: perder o caminhão só derrota até
  reabastecer; o relógio dos Apaches desarma se eles já chegaram.
- tarefas **`ENEMY_ELIMINATED`**, **`UNITS_NEAR`** (com `servico` = tático de
  serviço do supridor, pelo envelope), **`TURN_REACHED`**; **`HAS_EMBARKED_UNIT
  CH && SD`** = toda a tropa a bordo, com contador; **`&& LANDED`** na condição de
  combustível.
- **`semEconomia`** na aula (pronto, não usado: o autor preferiu o bônus de 240,
  o custo exato do reabastecimento).

**Achado de ordem:** a checagem de combustível no início do turno roda ANTES do
upkeep — `OnActiveTeamChanged` dispara antes do `ReleaseUnitsForActiveTeam`. O pouso
forçado só era notado um turno depois. A checagem foi para o poll no `Neutral`.

**Achado de importação:** clicar em Importar com a Unity ainda recompilando grava o
campo novo VAZIO — o asset já conhece o campo, o importador ainda é o antigo. A
fala da Capitã chegou muda por isso.

## 4. Escolher ≠ bloquear

Dois Apaches nasciam na mesma bandeira. "Aeronave no ar não bloqueia spawn" é regra
de design (helicóptero inimigo pairando não impede reforço) — mas a ESCOLHA da
bandeira usava a mesma regra, e o 1º Apache no ar deixava a bandeira "livre". Agora
a escolha prefere bandeira sem ninguém.

Mesma família no HOME: bandeira oculta contava como "casa", e sem prédio o cursor ia
para a unidade de menor id, não a mais próxima.

## O que não terminou

- **Nada da IA rebelde de hoje foi visto atirando em Play** depois das duas últimas
  correções da caça (decisão de ataque e porta de raio). O autor deixou soldados
  longe para testar; o resultado não voltou antes do fechamento.
- `HasEnemyInEngageRadius` com distância em reta segue errado para os capturadores
  com plano.
- O reparo rebelde entra e sai a cada decisão enquanto abaixo do gatilho (log e
  contador de sessão sobem). Inofensivo no rebelde; evitar a entrada, se incomodar.
- Supridor móvel (caminhão) não conta como "lugar para consertar" na regra do reparo.
- Aula 2: a ordem "combustível → tropa → Apaches" é garantida pela **sequência do
  roteiro**, não por "tanque > 0" na tarefa de embarque.
- Save no meio da aula: editar o roteiro invalida os saves daquela aula (índice da
  fala). Sem compatibilidade, de propósito — nada publicado.
