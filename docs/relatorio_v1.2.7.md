# Relatorio v1.2.7 - Guerra submarina

## Resumo
Esta versao consolida o fluxo de guerra submarina com foco em deteccao por sensores, persistencia visual de "detected" (olhinho), e atualizacao de visibilidade por unidade em runtime.

## Principais mudancas
- Sistema de observacao por unidade no `UnitManager`:
  - cada unidade agora armazena quais times estao vendo ela em runtime;
  - o indicador "detected" passa a refletir esse estado por unidade.
- Fluxo de sensores refinado:
  - `PodeDetectar` atualiza o estado de observacao por unidade detectada;
  - `AlguemMeVe` recalcula quem ainda observa a unidade e remove apenas os times que perderam contato.
- Regras de turno:
  - no inicio do turno, roda `AlguemMeVe` para unidades stealth do time da vez (sem cache bloqueando refresh);
  - ao finalizar acao (`hasAct=true`), roda sensores para a unidade que terminou a acao.
- Ajustes de gameplay e visibilidade:
  - correcoes para evitar "vazamento" de observacao entre unidades diferentes;
  - correcoes para manter o olhinho consistente em movimentacao/estado runtime;
  - ajuste de alcance do `AlguemMeVe` para 7 hexes.
- Regras de ocupacao em hex disputado:
  - bloqueio explicito para impedir duas unidades do mesmo time no mesmo hex disputado.

## Audio e categorias
- Expansao de categorias e SFX de movimento.
- Inclusao de assets de audio para sonar e movimento (incluindo train/turbo helice).

## Debug e ferramentas
- Melhorias no `panel_debug` e comandos relacionados a camada/altura.
- Ajustes e logs no fluxo de sensores para facilitar rastreio de deteccao e observacao.

## Assets e docs
- Atualizacoes em UnitData/Scene/Prefabs relacionadas ao pacote de guerra submarina.
- Atualizacao de documentacao tecnica em `docs/` para refletir o comportamento atual.

## Git
- Commit: `192b17c` (`v1.2.7 - Guerra submarina`)
- Tag: `v1.2.7`
- Push: branch `main` e tag `v1.2.7` enviados para `origin`.
