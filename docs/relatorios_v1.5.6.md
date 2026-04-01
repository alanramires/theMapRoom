# AI Player refactor 1

## Resumo
- Ajuste incremental no AI Player com foco em estabilização de base para refactor.
- Inclusão de caminho explícito de visibilidade por time sem depender de cache ativo (`NoCache`) no `MatchController`.
- Mantido escopo de correção (sem introduzir novo sistema de AI Plan nesta etapa).

## Arquivos relevantes
- `Assets/Scripts/Match/MatchController.cs`
- `Assets/Scripts/AI/AIPlayerController.cs`
- `Assets/Scripts/AI/AISnapshot.cs`

## Validação
- Build `Assembly-CSharp` executado com sucesso, sem erros.
