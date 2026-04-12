# Relatorio de Atualizacao - v1.8.0

## Em uma frase
Refatoração estrutural da IA (AIPlayerController), decompondo o sistema monolítico em arquivos especializados (parciais) para facilitar a manutenção e expansão.

## O que isso trouxe na pratica
- **Melhoria na Organização**: O código da IA, que antes era concentrado em um único arquivo gigante, agora está dividido por responsabilidades (Compras, Transporte, Planejamento, Combate, etc).
- **Facilidade de Depuração**: Erros relativos a subsistemas específicos (como o Shopping ou o Planner) podem ser encontrados e corrigidos de forma mais isolada.
- **Fluxos Mais Claros**: A separação por arquivos reflete as fases do turno da IA, tornando o ciclo de vida do TurnStateManager mais legível.

## Principais melhorias
1. **Desmontagem do AIPlayerController**
- O arquivo principal foi transformado em uma classe `partial`.
- Divisão em arquivos temáticos: `.Capture`, `.CombatTargeting`, `.Phase2`, `.Planner`, `.PlannerPersistence`, `.Shopping`, `.Support`, `.Transport` e `.TurnSummary`.
- Limpeza de dependências internas e melhor isolamento de variáveis de estado.

2. **Documentação Técnica de Subsistemas**
- Criação de documentos descritivos para o funcionamento do `AI Shopping` e do `AIPlanEvaluator`.
- Formalização das regras de decisão de compras em um guia de referência rápida.

3. **Consolidação de Perfis**
- Ajustes no `AI Lutador` e outros perfis de IA para suportar a nova estrutura de dados.

## Bloco tecnico curto
- **Arquivos-chave**: `AIPlayerController.cs` (agora partial) e suas variantes de subsistema em `Assets/Scripts/AI/`.
- **Compatibilidade**: Totalmente compatível com saves da v1.7.x, mantendo a persistência do `PlannerPersistence`.
- **Novas Docs**: `docs/AI Shopping.md`, `docs/AIPlanEvaluator.md`.

## Resultado
- Redução drástica da complexidade cognitiva ao abrir o código da IA, permitindo implementações mais complexas e otimizações futuras sem o risco de regressões em áreas não afetadas.
