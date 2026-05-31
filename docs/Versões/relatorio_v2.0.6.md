# Relatorio de Atualizacao - v2.0.6

## AI Capturador

Esta versao transforma o capturador da IA em uma unidade com plano proprio: ele recebe setores-alvo, avalia risco, escolhe rotas por objetivo, captura oportunidades no caminho, lida com FoW e prioriza combate quando precisa abrir passagem ate predios estrategicos.

---

## Em uma frase

A IA agora planeja a captura por setores, atribui capturadores a objetivos concretos e executa avancos mais inteligentes em vez de apenas buscar o melhor hex generico do turno.

---

## O que isso trouxe na pratica

- Capturadores deixam de agir como unidades comuns e passam a seguir um plano de conquista territorial.
- Setores do mapa agora carregam informacao de distancia aos QGs e risco relativo por time.
- A IA consegue comprar, mover e ordenar capturadores com mais consciencia de objetivo, evitando bloqueios simples e priorizando predios relevantes.

---

## Principais melhorias

### 1. Plano de objetivos por setor

- Foi adicionado um `ObjectiveManager` para guardar planos por time.
- O `AIController` agora monta objetivos de captura antes da fase de acoes das unidades.
- Cada setor pendente pode receber slots de `Capturador`, com prioridade calculada por risco, disputa e distancia ao QG.
- Capturadores ja posicionados sobre predios capturaveis recebem prioridade imediata.
- A atribuicao de capturadores livres usa backtracking para minimizar a distancia total unidade-objetivo.

### 2. Capturador especializado

- O comportamento foi separado em `AIController.Capturer.cs`.
- Capturadores atribuidos avancam para o setor definido, capturam quando chegam e defendem quando o setor ja esta resolvido.
- Capturadores rogue avancam em direcao ao QG inimigo, capturam predios oportunistas e atacam bloqueadores quando necessario.
- O fluxo considera ocupantes invisiveis por FoW e pode escolher uma celula adjacente de melhor EV/DPQ para revelar o alvo.
- Quando ha defensor visivel no predio-alvo, a IA procura um hex de movimento que permita mover e atacar no mesmo batch.

### 3. Scoring de movimento e combate

- O avanco do capturador passou a pontuar proximidade ao alvo, DPQ e ameaca local.
- Unidades com `preferMoveOnBestDPQ` valorizam terrenos/posicoes melhores no caminho.
- Unidades com `playConservative` penalizam hexes ameacados por inimigos visiveis.
- O `HexEvaluator` ganhou `positionQuality`, separando bonus de DPQ do valor bruto de combate.
- Capturadores passam a favorecer inimigos posicionados sobre construcoes, abrindo caminho para captura.

### 4. Setores com risco estrategico

- `SectorManager` agora calcula distancias de cada setor aos QGs conhecidos.
- Cada setor expoe `RiskRatio` e `RiskLevel` por time: `Safe`, `Low`, `Medium`, `High` e `DeepRaid`.
- A prioridade dos objetivos usa esses dados para escolher melhor a ordem de expansao.
- O editor recebeu drawers para exibir distancias e riscos por time de forma legivel.

### 5. Compras orientadas a captura

- `AIShoppingPlanner` passou a existir como componente com opcao de debug `onlyCapturers`.
- O planner identifica slots abertos de capturador em setores seguros/baixos e favorece compras compativeis.
- Em postura defensiva, capturadores secundarios tambem podem ganhar prioridade.
- O sistema continua respeitando saldo, edificio produtor, dominio terrestre e ocupacao da celula de spawn.

### 6. Dados de unidade e HUD de IA

- `UnitData` recebeu flags novas para comportamento de IA: `playConservative` e `preferMoveOnBestDPQ`.
- `UnitRole` foi expandido com `Intel` e `Suprimentos`.
- O editor de unidades passou a mostrar os novos campos de comportamento e a lista de roles de forma mais clara.
- O `AIController` ganhou modo de HUD/debug para imprimir avaliacao de hexes, objetivo atribuido e scores de movimento.

---

## Bloco tecnico curto

- Arquivos principais: `AIController.Capturer.cs`, `AIController.PlanEvaluator.cs`, `ObjectiveManager.cs`, `SectorManager.cs`, `AIShoppingPlanner.cs`, `HexEvaluator.cs`.
- `AIController` virou partial class para separar orquestracao, planejamento e comportamento especializado do capturador.
- Batches de movimento, captura e ataque agora podem carregar `MovementPath`, preservando o caminho calculado pela IA.
- A foto do mundo da IA e o FoW sao atualizados apos cada batch de unidade, reduzindo decisoes com visibilidade stale.
- Assets de unidades do Exercito e a cena `Battle Map` foram ajustados para suportar os novos roles, flags e planejamento de setores.

---

## Resultado

O jogo passa a ter uma IA de captura com intencao estrategica: ela escolhe setores, distribui capturadores, compra reforcos adequados e usa combate/posicionamento para conquistar predios. A base de IA deixa de ser apenas reativa e ganha uma camada clara de objetivo territorial.

---

## Validacao

Build C# executado:

```powershell
dotnet build Assembly-CSharp.csproj
```

Resultado: 0 erros. Permanecem warnings antigos de APIs obsoletas do Unity.
