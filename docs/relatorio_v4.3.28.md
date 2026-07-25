# v4.3.28 - fow local, remoto e AI

Esta versão consolida a política de apresentação do Fog of War conforme a
localidade de cada `PlayerSlotId`, preparando o fluxo para hot-seat, jogador
remoto, AI, replay e outras origens de batches.

## Identidade do observador

Cada participante agora possui localidade explícita no `MatchController`.

As decisões visuais deixam de depender apenas de `isAI` e passam a distinguir:

- humano local;
- humano remoto;
- AI;
- ausência de humano local, como em AI contra AI.

`PlayerSlotId` continua sendo a identidade de autoridade para visão, memória,
detecção, cache e inteligência. `TeamId` permanece responsável pela identidade
visual do time.

## Política de apresentação

Com um único humano local, a perspectiva visual permanece fixada nesse jogador
durante turnos de AI ou de participantes remotos.

Com dois ou mais humanos locais, o jogo entra em política de privacidade
hot-seat:

- cada humano local vê sua própria perspectiva no respectivo turno;
- a troca entre humanos é protegida pelo `Panel_Rodada`;
- durante turnos de AI ou de jogador remoto, nenhum dos humanos locais recebe
  o mapa do outro participante;
- a cortina cobre o `Game View`, enquanto o desenvolvedor ainda pode verificar
  a perspectiva correta no `Scene View`.

Em AI contra AI, sem humano local, o participante ativo continua disponível
como observador de apresentação.

## Cortina de privacidade

O `Panel_Rodada` passou a funcionar como cortina opaca nos turnos que não podem
ser observados pelos humanos locais presentes na mesma máquina.

A apresentação informa o time e o turno ativos, mantém o botão desabilitado e
permite que sons e processamento da partida continuem sem revelar o tabuleiro.

O indicador pulsante da AI foi reposicionado acima do botão, e o número do
turno permanece abaixo dele.

O `Panel_Debug` é promovido acima da cortina enquanto estiver aberto.

## Comandos de diagnóstico

Fog e cortina agora possuem controles independentes:

```text
fow on
fow off
fow partial
panelrodada on
panelrodada off
```

Os comandos `fow` alteram somente névoa e perspectiva. Os comandos
`panelrodada` alteram somente a cortina de privacidade, reduzindo ambiguidades
durante testes.

## Persistência

A localidade dos participantes é persistida no save.

Saves anteriores permanecem compatíveis: humanos legados são migrados em
memória como locais, preservando o comportamento histórico até que a
configuração seja explicitamente ajustada.

## Jornal de início de turno

O jornal de briefing não é apresentado durante turnos de AI, pois seu conteúdo
é informação destinada ao jogador que possui aquela perspectiva.

Resíduos de apresentação também são descartados na transição para impedir
vazamento entre slots.

## Hotzone logística

O caminhão de suprimentos passou a consumir o serviço compartilhado de hotzone.

O envelope é calculado a partir dos destinos de movimento realmente legais,
incluindo:

- custo de terreno;
- estradas;
- pontos de movimento restantes;
- bloqueios e ocupação;
- regras da unidade.

Somente depois o alcance da ferramenta de serviço é aplicado. Para alcance
adjacente, o comportamento equivale a uma arma fictícia de alcance 1.

Assim, um caminhão cercado por floresta não recebe um raio hexagonal artificial:
ele alcança apenas os hexes permitidos pelo custo real e atende o hex adjacente
ao destino alcançado.

`PodeSuprir` permanece como validação final das poucas origens pertencentes à
hotzone. Fora dela, somente unidades em manutenção podem orientar um
deslocamento futuro, respeitando retaguarda, ameaça e `playConservative`.

Também foram removidas varreduras redundantes de sensor por célula que causavam
picos longos durante decisões logísticas.

## Contrato transacional

As alterações preservam `docs/arquitetura/acoes_transacionais.md`:

- FoW e memória observam somente posições confirmadas;
- apresentação provisória não publica inteligência;
- a troca de observador não mistura caches entre slots;
- AI, humano local, humano remoto e replay seguem a mesma fronteira de commit;
- a cortina é exclusivamente apresentação e não altera a verdade do tabuleiro.

## Validação

- `Assembly-CSharp.csproj`: zero erros.
- `Assembly-CSharp-Editor.csproj`: zero erros.
- Verificação de whitespace nos arquivos alterados: concluída.
- FoW, perspectiva e cortina possuem comandos independentes.
- Hotzone logística usa movimento real seguido do alcance da ferramenta.
