# Relatorio Consolidado v1.0.0 a v1.0.8

## Fontes incluidas
- `RELATORIO_V1.0.0.md`
- `RELATORIO_V1.0.4.md`
- `RELATORIO_V1.0.6.md`
- `RELATORIO_V1.0.7.md`
- `RELATORIO_V1.0.8.md`

## Versoes sem arquivo no repositorio
- `v1.0.1`
- `v1.0.2`
- `v1.0.3`
- `v1.0.5`

---

## Conteudo original: `RELATORIO_V1.0.0.md`

# Relatorio Tecnico - v1.0.0 a v1.0.3

## Escopo

- Base: `v1.0.0` (`6a41b98`) - `the map room - validando movimentos`
- Incrementos:
  - `v1.0.1` (`8afc15d`) - `antes da refatoracao`
  - `v1.0.2` (`ac555d8`) - `logistica`
  - `v1.0.3` (`67bd7bc`) - `sensores de mira, ajustes no cursor, linha de tiro, utilitarios de unidade`

Resumo de impacto no intervalo `v1.0.0..v1.0.3`:
- `336 files changed`
- `59238 insertions`
- `14232 deletions`

---

## 1) Base funcional em v1.0.0

### Fundacao do jogo em hex

Arquitetura inicial consolidada com:
- grade hexagonal e utilitarios de coordenada/caminho:
  - `Assets/Scripts/Hex/Core/HexCoordinates.cs`
  - `Assets/Scripts/Hex/Path/HexPathResolver.cs`
- controle de turno e estados:
  - `Assets/Scripts/Match/TurnStateManager.cs`
  - `Assets/Scripts/Match/TurnState/*`
- fluxo de movimento com range/ocupacao:
  - `Assets/Scripts/Units/Rules/UnitMovementPathRules.cs`
  - `Assets/Scripts/Units/Rules/UnitOccupancyRules.cs`

### Catalogos e dados iniciais

Primeira versao de bancos de dados de gameplay:
- unidades (`Assets/DB/Unit/*`)
- terrenos (`Assets/DB/terrain/*`)
- construcoes (`Assets/DB/construction/*`)
- estruturas/rotas (`Assets/DB/structures/*`)

### Base visual e ferramentas

Entraram os elementos editoriais/visuais que sustentam o loop:
- spawners e editores de unidade/construcao
- prefab de unidade e cena de teste
- tile palette inicial, cursor, HUD e audio de interface/movimento

---

## 2) v1.0.1 - consolidacao pre-refatoracao

### Foco da versao

Expansao de mapa tatico e preparacao de regras orientadas a dados.

### Mudancas principais

1. Skills e custos por contexto:
- introducao de `SkillData`/`SkillDatabase`
- overrides de custo de terreno por skill (`TerrainSkillCostOverride`)

2. Estruturas de rota (estrada/ponte):
- evolucao de `RoadNetworkManager`
- ampliacao de `StructureData`/`StructureDatabase`
- fortalecimento do painter de rota e ocupacao de estrutura

3. Ajustes de unidade/movimento:
- evolucao de `UnitData` e `UnitManager`
- atualizacoes em `UnitMovementPathRules` e `TurnStateManager`

4. Pacote de assets:
- novos terrenos/estruturas/tiles de rodovia e ponte
- atualizacoes relevantes na `SampleScene`

---

## 3) v1.0.2 - camada de logistica

### Foco da versao

Introduzir economia operacional (servicos, suprimentos, recursos/sensores) no modelo de dados e runtime.

### Mudancas principais

1. Novo dominio de dados de logistica:
- `Assets/Scripts/Services/*`
- `Assets/Scripts/Supplies/*`
- `Assets/Scripts/Resources/*`
- `Assets/Scripts/Construction/ConstructionSupplierSettings.cs`
- `Assets/Scripts/Construction/ConstructionSupplyOffer.cs`

2. Modelagem de unidade e arma mais rica:
- embarques de armas/suprimentos/recursos em `UnitData`
- expansao dos bancos:
  - `Assets/DB/Logistic/*`
  - `Assets/DB/Weapons/*`
  - `Assets/DB/Recursos/*`

3. Ferramentas de authoring:
- editores custom para Service/Supply/Weapon/Construction/Unit
- melhor suporte para manter consistencia de dados em massa

4. Atualizacao de catalogo de faccoes:
- ampliacao de unidades e parametros para exercito/aeronautica/marinha

---

## 4) v1.0.3 - sensor de mira, linha de tiro e DPQ

### Foco da versao

Dar capacidade real de decisao de combate antes da resolucao final.

### Mudancas principais

1. Sistema de sensor de ataque (`PodeMirar`):
- `Assets/Scripts/Sensors/PodeMirarSensor.cs`
- `Assets/Scripts/Sensors/PodeMirarTargetOption.cs`
- `Assets/Scripts/Sensors/PodeMirarInvalidOption.cs`
- `Assets/Scripts/Sensors/SensorHandle.cs`

2. Linha de tiro e visualizacao:
- pintura de area/linha valida em estado de turno
- debug dedicado:
  - `Assets/Editor/PodeMirarSensorDebugWindow.cs`

3. Camada DPQ inicial:
- introducao do pacote `Assets/Scripts/DPQ/*`
- banco `Assets/DB/DPQ/*`
- suporte a leitura de posicao/altura no combate e sensores

4. Ajustes de UX/timing de turno:
- refinamentos no cursor, scanner prompt e animacao de movimento
- utilitarios de unidade e tuning de cena para validacao rapida

---

## 5) Estado consolidado ao final de v1.0.3

Ao fechar `v1.0.3`, o projeto ja tinha:

1. **Nucleo tatico operacional**
- movimento em hex, ocupacao e estados de turno estaveis.

2. **Modelo de dados amplo**
- unidades, armas, skills, suprimentos, servicos, recursos, terrenos e estruturas.

3. **Pipeline de combate preparatorio**
- sensor de mira + linha de tiro + DPQ, com ferramentas de debug.

4. **Ferramentas de editor maduras**
- authoring e tuning com janelas dedicadas para os principais subsistemas.

Esse conjunto foi a base direta para a etapa seguinte (`v1.0.4`), onde entrou a matriz de combate RPS/DPQ e a resolucao de combate aprofundada.


---

## Conteudo original: `RELATORIO_V1.0.4.md`

# Relatorio Tecnico - v1.0.4

## Versao e objetivo

- Versao: `v1.0.4`
- Commit: `6ca0681`
- Branch: `main`
- Tag message: `matriz de combate RPS inicial`

Este relatorio documenta o que foi construido na etapa de combate com foco em:

1. Sensor de ataque (`PodeMirar`) com rastreio robusto.
2. Resolucao de combate com DPQ + RPS.
3. Ferramentas de simulacao (`Calcular Combate` e `Matriz de Combate`).
4. Estruturacao de dados de RPS e prioridade de armas.
5. Ajustes de integridade para `UnitDatabase` e propagacao de HUD/unidade.

---

## Visao geral da arquitetura

O sistema foi organizado em camadas claras:

1. Camada de leitura de contexto (sensor):
- decide se existe ataque valido;
- escolhe candidatos de arma por alcance/municao/layer;
- resolve revide do defensor;
- registra posicao taticamente relevante (construcao > estrutura > terreno).

2. Camada de regra matematica (motor):
- calcula ataque efetivo;
- calcula defesa efetiva;
- aplica bonus RPS de ataque e defesa;
- aplica matchup DPQ;
- aplica arredondamento e dano final em HP.

3. Camada de observabilidade/editor:
- mostra resultados em ferramentas de debug e simulacao;
- gera logs textuais detalhados por etapa;
- permite selecao de arma atacante e (na matriz) arma de revide do defensor.

Essa separacao permite evoluir o balanceamento (dados) sem reescrever o fluxo principal.

---

## 1) Sensor Pode Mirar

Arquivo central: `Assets/Scripts/Sensors/PodeMirarSensor.cs`

### O que o sensor faz

1. Coleta armas embarcadas da unidade atacante.
2. Filtra candidatas por faixa operacional considerando modo de movimento.
3. Calcula distancias em hex via BFS no tilemap.
4. Para cada alvo inimigo:
- valida alcance da arma;
- valida municao;
- valida compatibilidade de dominio/altura (layer);
- valida linha de tiro em trajetoria reta (quando aplicavel).
5. Resolve revide do defensor (apenas distancia 1 + arma min range 1 + municao + layer compativel).
6. Anexa metadados de depuracao (linha de tiro, posicao atacante/defensor, motivo de falha etc).
7. Ordena opcoes por prioridade de alvo/arma.

### Prioridade da posicao no hex

Implementado em `ResolveUnitPositionLabel`:

1. `Construcao`
2. `Estrutura`
3. `Terreno`

Isso foi propagado para opcoes validas e invalidas:
- `Assets/Scripts/Sensors/PodeMirarTargetOption.cs`
- `Assets/Scripts/Sensors/PodeMirarInvalidOption.cs`

### Prioridade de arma contra classe de alvo

Integracao com `WeaponPriorityData`:
- `Assets/Scripts/Weapons/WeaponPriorityData.cs`

Cada categoria de arma pode marcar classes preferenciais de alvo.
No sensor, isso influencia desempate para o mesmo par atacante->alvo.

---

## 2) Resolve Combat (fluxo principal)

Arquivo central: `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`

### Pipeline implementado

1. Entrada
- atacante, defensor, arma atacante, arma de revide, distancia, posicoes, HP, ammo snapshot.

2. Consumo de municao
- atacante consome 1 (obrigatorio);
- defensor consome 1 apenas se revide valido/executado.

3. Ataque efetivo com RPS
- atacante: `HP * max(0, arma + rpsAtaque)`;
- defensor (revide): `HP * max(0, armaRevide + rpsAtaqueRevide)`.

4. DPQ da posicao
- resolve DPQ para atacante e defensor com prioridade:
  construcao > estrutura > terreno.

5. Defesa efetiva com RPS
- `defesaUnidade + defesaDPQ + rpsDefesa`.

6. Matchup DPQ
- consulta `DPQMatchupDatabase`.

7. Conta de eliminacao + arredondamento
- calcula bruto por divisao;
- aplica regra de arredondamento por outcome DPQ;
- log explicito da divisao (`numerador/denominador = bruto -> resultado`).

8. Aplicacao final
- subtrai HP;
- registra municao depois;
- registra se revide foi executado.

### Integracao no estado de turno

Arquivos:
- `Assets/Scripts/Match/TurnStateManager.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Sensors.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Sensors/SensorHandle.cs`

O scanner de turno atualiza opcoes de ataque e alimenta o fluxo `Mirando`.
Na confirmacao do alvo, o combate e resolvido com o pipeline acima.

---

## 3) Calcular Combate (tool)

Arquivo: `Assets/Editor/CombatCalculatorWindow.cs`

### Papel da ferramenta

Ferramenta de rascunho para testar 1 par atacante/defensor com:

1. Selecao de unidades (ou usar selecionado em cena).
2. Coleta do par via sensor `PodeMirar`.
3. Escolha da arma atacante do par (`Opcao de Arma`).
4. Relatorio completo em texto com:
- ataque efetivo,
- DPQ,
- defesa efetiva,
- matchup DPQ,
- eliminacao bruta,
- arredondamento final.

### Beneficio

Permite validar formula e dados antes de executar fluxo oficial de turno.

---

## 4) Matriz de Combate (tool)

Arquivo: `Assets/Editor/CombatMatrixWindow.cs`

### Objetivo

Simular todas as combinacoes de DPQ entre atacante e defensor:

- 5 estados DPQ do atacante x 5 estados DPQ do defensor = 25 celulas.

### Funcionalidades implementadas

1. Escolha de atacante/defensor por selecao de cena.
2. Escolha de arma atacante para o par.
3. Escolha de arma de revide do defensor:
- `Auto` (sensor),
- manual por arma embarcada valida,
- `Sem revide (forcado)`.
4. Geracao da matriz com HP restante `Atacante x Defensor`.
5. Clique em celula abre log completo daquela simulacao.
6. Celula `DPQ_Padrao x DPQ_Padrao` destacada em negrito/cor como baseline.

### Valor para design/balanceamento

A matriz transforma grande variacao de contexto de mapa em leitura simples por eixo DPQ.
Isso acelera tuning e comparacao entre unidades/armas.

---

## 5) Sistema RPS (dados e banco)

Arquivos:
- `Assets/Scripts/RPS/RPSData.cs`
- `Assets/Scripts/RPS/RPSDatabase.cs`
- `Assets/Editor/RPSDataEditor.cs`

### Modelo

Cada entrada possui dois blocos:

1. Chave Ataque
- `unitClass`
- `weaponCategory`
- `targetClass`
- `attackBonus`
- `notes`
- `RpsAttackText` (gerado automaticamente)

2. Chave Defesa
- `targetClass`
- `unitClass`
- `weaponCategory`
- `defenseBonus`
- `notes`
- `RpsDefenseText` (gerado automaticamente)

### Regras aplicadas

1. `RPSData` resolve bonus por match exato.
2. `RPSDatabase` agrega varias tabelas e usa primeira correspondencia valida.
3. Textos de RPS sao auto-gerados e mantem sinal explicito (`+0`, `+1`, `-2`).
4. Editor custom:
- rotulo dinamico de entrada (`Unit [Categoria] vs Target`);
- reorder manual da lista;
- espelhamento automatico de chave ataque -> chave defesa.

### Dados carregados

Assets adicionados:
- `Assets/DB/RPS/Catalogo de RPS.asset`
- `Assets/DB/RPS/RPS Infantry.asset`
- `Assets/DB/RPS/RPS Vehicle.asset`
- `Assets/DB/RPS/RPS Armored.asset`
- `Assets/DB/RPS/RPS Artillery.asset`

---

## 6) Armas: categoria tatica + prioridade

Arquivos:
- `Assets/Scripts/Weapons/WeaponCategory.cs`
- `Assets/Scripts/Weapons/WeaponData.cs`
- `Assets/Editor/WeaponDataEditor.cs`
- `Assets/Scripts/Weapons/WeaponPriorityData.cs`

### Mudancas principais

1. Introduzida `WeaponCategory` para combate:
- `AntiInfantaria`
- `AntiTanque`
- `AntiAerea`
- `AntiNavio`

2. Mantida `WeaponClass` para logistica (auto por `basicAttack`).

3. `WeaponDataEditor` passou a expor `Weapon Category` e manter `Weapon Class` somente leitura.

4. `WeaponPriorityData` centraliza preferencia por classe-alvo sem bloquear tiros alternativos.

---

## 7) Ferramentas de suporte e debug

### Propagacao de unidade/HUD

Arquivo: `Assets/Scripts/Editor/UnitLayerTools.cs`

`Tools/Units/Propagate Unit Data (Apply From Database)` agora:
- reaplica dados de unidade do database;
- propaga layout completo da arvore do `UnitHudController` a partir do prefab base;
- sincroniza posicoes/anchors/scale dos elementos do HUD.

### Debug do sensor

Arquivo: `Assets/Editor/PodeMirarSensorDebugWindow.cs`

Continua servindo como painel de:
- opcoes validas/invalidas;
- motivos de invalidez;
- linha de tiro;
- posicao atacante/defensor;
- status de revide.

---

## 8) Integridade de UnitDatabase (correcao de bug)

Arquivo: `Assets/Scripts/Units/UnitDatabase.cs`

Problema observado:
- apos renomear `id` de `UnitData`, alguns fluxos (ex.: painter/spawn/propagate) falhavam no lookup por cache desatualizado.

Correcao aplicada:

1. Dicionario de lookup agora `case-insensitive`.
2. `TryGetById` faz nova tentativa apos `RebuildLookup()` se nao encontrar na primeira.

Efeito:
- maior robustez em edicao quando IDs mudam.

---

## 9) Dados e catalogos impactados

Principais mudancas de assets:

1. DPQ
- padronizacao de nomes de assets (`DPQ_Desfavoravel`, `DPQ_Padrao`, etc.).

2. RPS
- criacao de banco e tabelas por classe.

3. Units
- introducao/ajuste de `Exercito_obusLeve`.
- alteracoes em unidades usadas para testes de combate.

4. Weapons
- atualizacao de categorias em multiplos assets.
- criacao da tabela `Tabela de Prioridades`.

5. Cena/prefab
- atualizacoes em `Assets/Scenes/SampleScene.unity` e `Assets/Prefab/unit.prefab` para refletir setup atual.

---

## 10) Arquivos de codigo principais da v1.0.4

Novos:

- `Assets/Editor/CombatCalculatorWindow.cs`
- `Assets/Editor/CombatMatrixWindow.cs`
- `Assets/Editor/RPSDataEditor.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`
- `Assets/Scripts/RPS/RPSData.cs`
- `Assets/Scripts/RPS/RPSDatabase.cs`
- `Assets/Scripts/Weapons/WeaponCategory.cs`
- `Assets/Scripts/Weapons/WeaponPriorityData.cs`

Atualizados com integracao:

- `Assets/Scripts/Sensors/PodeMirarSensor.cs`
- `Assets/Scripts/Sensors/PodeMirarTargetOption.cs`
- `Assets/Scripts/Sensors/PodeMirarInvalidOption.cs`
- `Assets/Scripts/Sensors/SensorHandle.cs`
- `Assets/Scripts/Match/TurnStateManager.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Sensors.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Editor/UnitLayerTools.cs`
- `Assets/Scripts/Units/UnitDatabase.cs`
- `Assets/Scripts/Weapons/WeaponData.cs`
- `Assets/Editor/WeaponDataEditor.cs`

---

## 11) Limites atuais e proximos incrementos naturais

1. Reportar falhas por unidade no `Propagate Unit Data`:
- hoje conta quantas sincronizaram, mas nao lista detalhadamente as que falharam por `unitId`.

2. Unificar estrategia de escolha de arma entre:
- fluxo principal (`ResolveCombatFromSelectedOption`),
- calculadora simples,
- matriz.

3. Expandir cobertura de testes automatizados (edit mode) para:
- lookup de `UnitDatabase` apos rename;
- selecao de revide manual na matriz;
- arredondamento por outcome DPQ.

4. Consolidar padrao de encoding para labels de setas no editor (`RPSDataEditor`) para evitar caracteres inconsistentes em alguns terminais.

---

## Fechamento

A `v1.0.4` entrega um primeiro pacote completo de combate orientado a dados:

1. Sensor robusto e rastreavel.
2. Motor de resolucao com DPQ + RPS.
3. Ferramentas de simulacao com leitura detalhada do calculo.
4. Base de dados modular para balanceamento continuo.

Com isso, o combate saiu do nivel de experimento isolado e passou a ter base de engine com observabilidade, repetibilidade e manutencao futura viavel.


---

## Conteudo original: `RELATORIO_V1.0.6.md`

# Relatorio Tecnico - v1.0.6

## Versoes cobertas

- `v1.0.5` - commit `3f5ff93` - `v1.0.5 - Elite combat`
- `v1.0.6` - commit `ba4f924` - `v1.0.6 - road bonus, domain change`
- Branch: `main`

Este relatorio resume as principais mudancas entregues entre `v1.0.5` e `v1.0.6`.

---

## 1) Combate elite (v1.0.5)

### Objetivo

Adicionar inflexao de combate para unidades de mesma classe com diferenca de elite, sem quebrar o RPS base.

### O que entrou

1. Skill de combate com modificador de RPS condicional:
- filtros por classe do dono/oponente;
- filtro por categoria de arma;
- comparacao de elite e diferenca minima.

2. Modelo de bonus expandido para 4 eixos:
- bonus de ataque do owner;
- bonus de defesa do owner;
- bonus de ataque do oponente;
- bonus de defesa do oponente.

3. `eliteLevel` explicito em `UnitData` (default `0`).

4. Integracao no runtime de combate:
- calculo final considera RPS base + bonus de skill elite;
- logs de combate passaram a exibir elite/skill nas contas.

5. Ferramentas de editor alinhadas ao runtime:
- `Tools/Combat/Calcular Combate`
- `Tools/Combat/Matriz de Combate`
- ambas passaram a refletir elite + RPS + DPQ no mesmo padrao.

### Arquivos-chave

- `Assets/Scripts/Skills/SkillData.cs`
- `Assets/Scripts/Units/UnitData.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`
- `Assets/Editor/CombatCalculatorWindow.cs`
- `Assets/Editor/CombatMatrixWindow.cs`

### Documentacao criada/atualizada

- `docs/Elite.md`
- `docs/Combat.md`
- `docs/Sensor PodeMirar.md`

---

## 2) Mobilidade por estrada e troca de domain (v1.0.6)

### Objetivo

Melhorar leitura tatico-operacional de movimento com bonus de estrada controlado por dados e controle manual de camada/domain por unidade.

### O que entrou

1. Bonus de estrada no pathfinding:
- regra: ao fazer full move em estrada, ganha `+1` passo;
- restrito a `Land/Surface` com `move >= 4`;
- passo bonus custa `0` de autonomia;
- o passo bonus tambem precisa cair em estrada (sem exploit em montanha).

2. Bonus orientado a dados de estrutura:
- `StructureData` ganhou flag `roadBoost`;
- apenas estruturas com `roadBoost = true` habilitam o bonus;
- ponte pode ficar sem bonus (`roadBoost = false`).

3. Inspector do `UnitManager` com controle de camada:
- botoes `Subir Domain` e `Descer Domain`;
- ordenacao por pilha de altitude (bottom->top):
  - `Submerged = 2`
  - `Surface = 3`
  - `AirLow = 4`
  - `AirHigh = 5`
- sem loop circular: no topo/na base, nao avanca.

4. Atualizacao de assets de mapa/arte:
- pacote de tiles de quebra-mar;
- palette dedicada para costa;
- ajustes de cena para nova leitura de litoral.

### Arquivos-chave

- `Assets/Scripts/Units/Rules/UnitMovementPathRules.cs`
- `Assets/Scripts/Structures/StructureData.cs`
- `Assets/Editor/UnitManagerEditor.cs`
- `Assets/DB/structures/Rodovias.asset`
- `Assets/Scenes/SampleScene.unity`

---

## 3) Resultado consolidado (v1.0.5 + v1.0.6)

O projeto ficou mais forte em tres frentes:

1. **Balanceamento de combate**
- RPS base continua simples;
- elite adiciona inflexao controlada por dados.

2. **Movimento tatico**
- estrada agora tem identidade mecanica clara;
- custo de autonomia e alcance valido seguem consistentes.

3. **Legibilidade de jogo**
- fluxo de altitude/domain mais controlavel no inspector;
- costa/agua comunicam melhor as regras de mapa.

---

## Referencias de apoio

- `docs/Combat.md`
- `docs/Elite.md`
- `docs/Sensor PodeMirar.md`
- `docs/regras de LoS e LdT.md`
- `docs/v1.md`


---

## Conteudo original: `RELATORIO_V1.0.7.md`

# Relatorio Tecnico - v1.0.7

## Versoes cobertas

- `v1.0.7` - commit `e21737d` - `v1.0.7 - embarque, take off and landing (inicio)`
- Branch: `main`

Este relatorio resume as principais mudancas entregues na versao `v1.0.7`.

---

## 1) Sensor Pode Embarcar (validos + invalidos)

### Objetivo

Dar visibilidade completa do motivo de bloqueio de embarque e estabilizar regras de validacao de transportador/slot.

### O que entrou

1. Estrutura de invalidacao detalhada:
- novo tipo `PodeEmbarcarInvalidOption`;
- retorno de lista de validos e invalidos, com motivo por etapa.

2. Ferramenta de debug de sensor:
- `Tools > Sensors > Pode Embarcar` agora mostra:
  - transportadores validos;
  - resultados invalidos com motivo (contexto, slot, custo, movimento, lotacao, etc).

3. Regra de contexto e slot:
- embarque exige transportador adjacente valido;
- valida contexto por `allowedEmbarkTerrains` / `allowedEmbarkConstructions`;
- se listas estiverem vazias, usa fallback por compatibilidade de dominio/altura do transportador com o hex.

4. Regras solicitadas de dominio:
- transportador em `Domain.Air` bloqueia embarque;
- removida regra anterior de "dominios diferentes com mesma altura".

5. Custo de embarque:
- usa custo normal de entrada quando possivel;
- com fallback para custo do terreno base (incluindo overrides de skill) quando o passageiro nao pisaria no hex do transportador.

### Arquivos-chave

- `Assets/Scripts/Sensors/PodeEmbarcarSensor.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarOption.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarInvalidOption.cs`
- `Assets/Editor/PodeEmbarcarSensorDebugWindow.cs`

---

## 2) Refactor de slots de transporte

### Objetivo

Remover redundancias e deixar o modelo de slot mais flexivel.

### O que entrou

1. Simplificacao de filtros:
- removidos `filterAllowedClasses` e `requireAllSkills` do slot;
- lista vazia = sem restricao;
- lista preenchida = validacao ativa.

2. Camadas permitidas por lista:
- `allowedDomain/allowedHeight` migrado para lista de modos;
- criado tipo dedicado `TransportSlotLayerMode` (sem sprites).

3. Compatibilidade de dados:
- migracao automatica de dados legados no `OnValidate/EnsureDefaults`.

### Arquivos-chave

- `Assets/Scripts/Units/UnitTransportSlotRule.cs`
- `Assets/Scripts/Units/TransportSlotLayerMode.cs`
- `Assets/Scripts/Units/UnitData.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarSensor.cs`

---

## 3) Landing/Takeoff: regras movidas para contexto (Construction/Structure/Terrain)

### Objetivo

Tirar regras aereas do `UnitData` e centralizar permissao no hex/contexto.

### O que entrou

1. `UnitData` simplificado:
- removidos campos de pouso/decolagem por unidade (`canLandOn...`, `takeoffConsumes...`, `preferredAirHeight`, `aircraftType`, etc).
- classe da unidade (`Jet/Plane/Helicopter`) passa a definir se e aeronave.

2. Novos campos de contexto:
- `ConstructionData`:
  - `landingAllowedClasses`
  - `landingRequiredSkills`
  - `takeoffAllowedMovementModes`
- `StructureData`:
  - `landingAllowedClasses`
  - `landingRequiredSkills`
  - `roadLandingRequiresStoppedClasses`
  - `takeoffAllowedMovementModes`
- `TerrainTypeData`:
  - `landingAllowedClasses`
  - `landingRequiredSkills`
  - `takeoffAllowedMovementModes`

3. Reescrita de `AircraftOperationRules`:
- validacao de pouso/decolagem baseada no contexto do hex;
- takeoff controlado por lista de modo permitido (`MoveuParado` / `MoveuAndando`);
- altura aerea preferida inferida do dominio/camada da unidade.

### Arquivos-chave

- `Assets/Scripts/Units/Rules/AircraftOperationRules.cs`
- `Assets/Scripts/Units/UnitData.cs`
- `Assets/Scripts/Construction/ConstructionData.cs`
- `Assets/Scripts/Structures/StructureData.cs`
- `Assets/Scripts/Terrain/TerrainTypeData.cs`
- `Assets/Editor/UnitDataEditor.cs`

---

## 4) Construction Configuration: captura e mercado

### Objetivo

Adicionar parametros de economia/captura por construcao.

### O que entrou

1. Novo valor de captura:
- `capturedIncoming` (default `1000`).

2. Politica de producao/venda por lista:
- `canProduceAndSellUnits` com:
  - `FreeMarket`: vende para quem capturou (exceto neutro);
  - `OriginalOwner`: vende apenas para o dono original.

3. `ConstructionManager` atualizado:
- rastreia `originalOwnerTeamId`;
- `CanProduceUnits` agora respeita a politica configurada.

4. Editores atualizados:
- `ConstructionDataEditor`
- `ConstructionFieldDatabaseEditor`
- `ConstructionSpawnerEditor`

### Arquivos-chave

- `Assets/Scripts/Construction/ConstructionSiteRuntime.cs`
- `Assets/Scripts/Construction/ConstructionManager.cs`
- `Assets/Editor/ConstructionDataEditor.cs`
- `Assets/Editor/ConstructionFieldDatabaseEditor.cs`
- `Assets/Editor/ConstructionSpawnerEditor.cs`

---

## 5) Ajustes de inspector e animacao

1. `UnitManager` inspector:
- removeu `Current Ammo` / `Max Ammo` da UI;
- `Has Acted` movido para o topo logico;
- `Match Controller` reposicionado junto de controllers/databases.

2. Velocidade de movimento:
- override por `UnitData` movido para `AnimationManager`;
- sem match na lista, velocidade padrao = `1`.

### Arquivos-chave

- `Assets/Editor/UnitManagerEditor.cs`
- `Assets/Scripts/Match/Animation/AnimationManager.cs`
- `Assets/Scripts/Units/UnitManager.cs`

---

## Observacao importante

- `supplierResources.maxCapacity` ainda nao foi conectado ao runtime logistico.
- Portanto, nesta versao, `0` ainda nao esta operando como "infinito" em logica de consumo/restricao.


---

## Conteudo original: `RELATORIO_V1.0.8.md`

# Relatorio Tecnico - v1.0.8

## Versoes cobertas

- `v1.0.8` - commit `d2d0191` - `v1.0.8 - sistema de embarque concluido`
- Branch: `main`

Este relatorio resume as principais mudancas entregues na versao `v1.0.8`.

---

## 1) Sistema de embarque (fluxo completo em gameplay)

### Objetivo

Fechar o ciclo de embarque com selecao, confirmacao, animacao, validacao de vaga e aplicacao de estado.

### O que entrou

1. Fluxo jogavel completo:
- `sensor -> lista de transportadores validos -> confirmacao -> execucao de embarque`.
- transporte invalido continua visivel no debug, mas selecao em gameplay ocorre apenas entre validos.

2. Preview visual e navegacao:
- linha de preview entre passageiro e transportador (reuso da linha de `Pode Mirar`);
- ciclagem de opcoes por setas;
- ao entrar no submenu do sensor valido, cursor vai para o primeiro item.

3. Confirmacao e estado:
- confirmacao do embarque integrada ao loop de input;
- durante confirmacao, linha de preview permanece ativa;
- `ESC` retorna para sensores com cursor restaurado na unidade quando aplicavel.

4. Execucao do embarque:
- animacao de movimento do passageiro ate o transportador;
- suporte a `forced landing` do transportador aereo quando necessario;
- unidade embarcada assume cell do transportador e fica invisivel;
- unidade embarcada deixa de participar dos sensores de alvo.

5. Ato e audio:
- ao concluir embarque, passageiro finaliza acao (`HasActed = true`);
- transportador tambem marca `HasActed` conforme regra atual;
- audio `load.mp3` tocado na conclusao.

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarSensor.cs`
- `Assets/Scripts/Cursor/CursorController.cs`
- `Assets/Scripts/Match/Animation/AnimationManager.cs`

---

## 2) Runtime de transporte no UnitManager

### Objetivo

Permitir controle manual e visual de vagas ocupadas/disponiveis por instancia em campo.

### O que entrou

1. Slots runtime por instancia:
- `transportedUnitSlots` no `UnitManager`;
- vinculo bidirecional passageiro <-> transportador (`EmbarkedTransporter`, `EmbarkedTransporterSlotIndex`).

2. Inspector de runtime:
- lista de vagas por slot/capacidade;
- atribuicao manual de passageiro por vaga no inspector;
- suporte a limpar vagas e refresh a partir do `UnitData`.

3. Indicador visual de transporte:
- HUD de transporte aparece quando houver pelo menos 1 vaga ocupada;
- nao aparece para unidade sem passageiros ou nao-transportadora.

### Arquivos-chave

- `Assets/Scripts/Units/UnitManager.cs`
- `Assets/Editor/UnitManagerEditor.cs`
- `Assets/Scripts/Units/UnitHudController.cs`

---

## 3) Autonomia: migracao para AutonomyData/AutonomyDatabase

### Objetivo

Desacoplar autonomia de classes fixas e deixar configuracao por data asset.

### O que entrou

1. Novo modelo de dados:
- `AutonomyData` (perfil de multiplicador de movimento e upkeep);
- `AutonomyDatabase` (catalogo de perfis).

2. Vinculo por unidade:
- `UnitData` passa a ter referencia `autonomyData`;
- regras de autonomia passam a ler o perfil vinculado na unidade.

3. Regras aplicadas:
- multiplicador de custo por passo via perfil de autonomia;
- upkeep por virada de turno conforme dominio/altura no inicio do turno.

4. Ajuste de regra:
- deteccao de autonomia operacional passou a usar `autonomyData` como fonte de verdade.

### Arquivos-chave

- `Assets/Scripts/Units/AutonomyData.cs`
- `Assets/Scripts/Units/AutonomyDatabase.cs`
- `Assets/Scripts/Units/Rules/OperationalAutonomyRules.cs`
- `Assets/Scripts/Match/MatchController.cs`
- `Assets/Scripts/Units/UnitData.cs`

---

## 4) HUD de altitude e sincronizacao de camada

### Objetivo

Exibir estado de altitude/submersao da unidade no HUD e manter sincronia com mudancas de camada.

### O que entrou

1. Indicador `altitude` no `Unit HUD`:
- `air high` -> icone high;
- `air low` -> icone low;
- `land/surface` -> oculto;
- `submerged` -> icone submerged.

2. Sincronizacao:
- atualizacao ao propagar dados da unidade (`Tools > Propagate Unit Data`);
- atualizacao ao usar `Subir Domain` / `Descer Domain`.

3. Atalhos de camada:
- no gameplay em `UnitSelected`, atalhos `S/D` para ciclar camadas (sem animacao de Y).

### Arquivos-chave

- `Assets/Scripts/Units/UnitHudController.cs`
- `Assets/Scripts/Editor/UnitLayerTools.cs`
- `Assets/Editor/UnitManagerEditor.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`

---

## 5) Ajustes visuais e UX adicionais

1. Sorting temporario em embarque:
- unidade em pouso/embarque sobe temporariamente o `Order in Layer` e restaura ao final.

2. Move preview:
- ocultacao do `move preview path` ao entrar no submenu de sensor.

3. Logs e feedback:
- painel de sensores com disponibilidade de acoes;
- feedback sonoro consistente para opcoes validas/ciclagem.

4. Construcoes:
- construcao escurece quando ocupada por unidade do mesmo time que ainda nao agiu;
- volta ao normal quando unidade ja agiu;
- unidades de time diferente nao disparam escurecimento.

### Arquivos-chave

- `Assets/Scripts/Units/UnitManager.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Construction/ConstructionManager.cs`
- `Assets/Scripts/Cursor/CursorController.cs`

---

## 6) Ajuste final de custo de embarque

### Objetivo

Melhorar legibilidade do consumo de autonomia durante o embarque.

### O que entrou

- custo de autonomia do embarque agora e aplicado antes do delay final da sequencia;
- em caso de falha apos desconto, ha rollback do valor;
- log final exibe `custo` e `autonomia antes->depois`.

### Arquivo-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`

---

## Observacoes

- O sistema de embarque foi fechado com regras e UX de selecao/confirmacao/execucao.
- A calibracao de `YPos` para troca de sprite por camada foi removida por variacao artistica entre sprites.
- O estado atual mantem troca de camada instantanea com atalhos `S/D`.


