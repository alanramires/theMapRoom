# Relatorio de Atualizacao - v1.1.0

## Em uma frase
Foi um grande passo para organizar multiplos mapas sem misturar dados.

## O que isso trouxe na pratica
- Catalogos por mapa ficaram mais claros.
- Estradas e construcoes ficaram mais centralizadas no mapa correto.
- Ferramentas de pintura/migracao reduziram retrabalho.

## Regras de venda (Selling Rules)
- `Free Market`: vende para o controlador atual da construcao.
- `Original Owner`: vende para o dono original definido no spawn.
- `First Owner`: fixa o primeiro capturador como dono de venda permanente.
- `Disabled`: nao vende unidades.

## Bloco tecnico curto
- Rotas movidas para contexto de `StructureDatabase`.
- Field de construcoes centralizado em `ConstructionDatabase`.
- `ConstructionManager` recebeu ajustes importantes:
- `Selling Rules` simplificado
- suporte completo a `First Owner`
- `Has Infinite Supplies (Override)` por instancia.
