# Relatorio de Atualizacao - v1.1.1

## Em uma frase
A v1.1.1 foi uma versao grande de consolidacao: quick save/load, correcoes de fluxo de turno e recalibracao de dados de combate.

## O que isso trouxe na pratica
- Partidas longas ficaram mais seguras com save/load rapido em runtime (F8/F9).
- Menos quebra de fluxo em combate, captura, sensores e compras em construcao.
- Catalogos de unidade/arma e dados de combate ficaram mais consistentes para balanceamento.
- Ferramentas de editor foram reforcadas para acelerar calibracao e manutencao.

## Principais melhorias
1. Save/Load completo de partida
- Novo `SaveGameManager` com save em JSON por slot.
- Restauracao de estado de turno, time ativo, unidades, construcoes e ownership.
- Suporte a estados de embarque, ammo/fuel/HP e runtime de ofertas/servicos/suprimentos.

2. Estabilidade do TurnState
- Ajustes em `TurnStateManager` (capture, combat, shopping, scanner prompt e sensors).
- Fluxo de compra em construcao mais robusto, com atalhos numericos e validacoes melhores.
- Menor chance de ficar preso em estados intermediarios apos acao/animacao.

3. Dados e balanceamento
- Revisao ampla de assets de unidades (exercito, aeronautica, marinha) e armas por categoria.
- Ajustes em matriz RPS e traits Elite para coerencia de combate.
- Atualizacao de catalogos por mapa e organizacao de conteudo para manter consistencia entre cenas.

4. Ferramentas e suporte de producao
- Novas/atualizadas janelas de editor para matriz grande de combate e calculos par-a-par.
- Melhorias em editores de modificadores e dados para reduzir retrabalho de tuning.
- Atualizacao de documentacao tecnica de apoio (matriz e calibracao).

## Regras importantes
- `Quick Save/Load`: F8 salva e F9 carrega no slot rapido configurado.
- `Cross-scene load`: por padrao, pode bloquear load de save gerado em outra cena.
- `Force load when busy`: opcional para forcar load fora do estado ideal.

## Bloco tecnico curto
- Arquivo novo-chave: `Assets/Scripts/Save/SaveGameManager.cs`.
- Arquivos de fluxo alterados: `TurnStateManager.*`, `MatchController`, `ConstructionManager`, `UnitManager`, `UnitSpawner`, `ConstructionSpawner`.
- Escopo total do commit: 211 arquivos alterados, com foco em save/load, bug fixes de runtime, dados de combate e ferramentas de editor.

## Resultado
A v1.1.1 nao foi so um patch pequeno: ela melhora confiabilidade de partida, reduz friccao de testes e fortalece a base para as proximas iteracoes de gameplay e balanceamento.
