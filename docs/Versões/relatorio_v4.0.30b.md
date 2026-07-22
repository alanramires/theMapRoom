# v4.0.30b - Ajustes no FOW - a bíblia

## Foco

Formalização da lei central do jogo: toda ação é transacional. O jogador pode experimentar e desfazer livremente enquanto estiver fora de `Neutral`; nenhuma consequência definitiva pode ser publicada antes do compromisso explícito e do retorno ao estado confirmado.

## A lei do tabuleiro

```text
NEUTRAL confirmado
→ seleção e simulação provisória
→ compromisso explícito da ação
→ execução e efeitos definitivos
→ retorno a NEUTRAL
→ recálculo do tabuleiro confirmado
```

- Criação de `AGENTS.md` na raiz com a invariável obrigatória para agentes que trabalhem no projeto.
- Criação de `docs/arquitetura/acoes_transacionais.md` como referência completa para desenvolvimento.
- Inclusão da mesma regra em `CLAUDE.md`.
- Documentação do contrato de preview, commit, rollback e recálculo.
- Registro explícito de que fim de animação, chegada à célula ou abertura de sensores não constituem compromisso.

## FOW e compromisso

- Remoção do refresh de FOW ao concluir um movimento ainda provisório.
- `MarkAsActed()` agenda a publicação do novo estado quando chamado fora de `Neutral`.
- `ExecuteAndReset()` retorna primeiro a máquina de estados para `Neutral`.
- Somente depois do retorno a `Neutral` são atualizados FOW, detecção, stealth e contatos confirmados.
- Cancelamento e rollback deixam de revelar informações obtidas a partir de posições provisórias.
- O modo `ALL` é reconstruído a partir do snapshot comprometido, sem exigir troca manual de camada.

## Visão submarina

- Correção do `PodeDetectarSensor`, que convertia alcance especializado zero em um.
- `Submarine/Submerged = 0` passa a significar ausência real de visão submarina.
- A coleta virtual não revela nem a célula de origem quando o alcance da camada é zero.
- Ajuste dos cálculos auxiliares para preservar alcances especializados iguais a zero.

## Apresentação

- Manutenção da distinção entre visuais temporários e estado definitivo do tabuleiro.
- Ajuste sonoro ao emergir submarinos, acompanhando a mudança visível de camada.

## Estado

- Build de runtime verificado com `dotnet build Assembly-CSharp.csproj --no-restore`.
- Compilação concluída sem erros.
