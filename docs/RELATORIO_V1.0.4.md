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
