# Ideias futuras — Replay

## Objetivo

Transformar o replay compacto em uma reprodução historicamente fiel ao ponto de vista de um observador.

O participante que executa o turno e o participante que assiste são identidades independentes:

- **Ator:** slot cujas ações serão reproduzidas.
- **Observador:** slot cuja visão, conhecimento e memória devem limitar a apresentação.

Exemplo desejado:

1. O jogador amarelo/local inicia seu turno e lê no Jornal do Comandante que recebeu um tiro da névoa.
2. Abre o replay, que seleciona automaticamente o amarelo como observador.
3. Escolhe o turno anterior do slot 2/vermelho.
4. O replay restaura o início daquele turno e executa as ações do vermelho em ordem.
5. O amarelo vê apenas aquilo que poderia ter observado naquele momento.
6. Ao chegar ao ataque, a câmera mostra o efeito observável sobre sua unidade, sem denunciar a origem oculta do disparo.

## Base já concluída

- A timeline usa um snapshot completo no início de cada turno como âncora.
- As ações intermediárias são persistidas como `PlayerAction`s compactas.
- Snapshots pós-ação ficam reservados para exceções que exigem correção determinística.
- Saves antigos continuam legíveis e são compactados ao salvar novamente.
- É possível selecionar um registro por turno e por slot atuante.
- O replay já diferencia modo onisciente de visão filtrada por slot.

## Trabalho futuro

### 1. Fixar automaticamente o observador local

- Se existir exatamente um humano local ativo, abrir o painel em `TeamFiltered` usando esse `PlayerSlotId`.
- Não usar cor/time como identidade quando houver um `PlayerSlotId` inequívoco.
- Manter a opção manual de trocar o observador.
- Manter o modo onisciente como escolha explícita, não como padrão nesse cenário.
- Em hot-seat com mais de um humano local, não escolher automaticamente um observador que possa vazar informações; exigir a identidade protegida pelo fluxo de privacidade.

### 2. Ampliar a âncora com o estado histórico de observação

O `TurnStartSnapshot` atual restaura tabuleiro, unidades, construções e estado básico da partida, mas não contém toda a memória histórica do FOW.

A âncora futura deve guardar, por slot observador:

- células exploradas naquele instante;
- contribuições/fontes de visão relevantes;
- contatos detectados e estado de stealth conhecido;
- memória de construções e ownership conhecido;
- demais informações persistentes que o observador já conhecia;
- dados suficientes para reconstruir a apresentação sem escrever nova inteligência confirmada.

Avaliar reaproveitamento das estruturas já usadas pelo save normal:

- `fogSourceCachesByObserverSlot`;
- `fogExploredCellsBySlot`;
- `fogConstructionMemory`;
- caches/ledgers de detecção que representem conhecimento do observador.

Esses dados devem existir uma vez por âncora de turno, não uma vez por ação.

### 3. Separar execução da ação e apresentação observável

Cada `PlayerAction` deve ser executada para manter a timeline correta, mas sua apresentação depende do observador.

Antes de apresentar um batch, classificar a ação:

- **Totalmente observável:** reproduzir seleção, cursor, câmera, animação e efeitos normalmente.
- **Parcialmente observável:** ocultar origem/ator e mostrar somente alvos, projéteis ou efeitos que o observador poderia perceber.
- **Não observável:** aplicar a ação silenciosamente ao estado reconstruído, sem cursor, câmera, sons informativos ou mensagens que revelem posição.
- **Torna-se observável durante a ação:** começar oculto e mostrar o ator somente a partir do instante em que as regras confirmarem sua revelação.

Essa decisão deve consultar as regras reais de FOW, detecção e stealth no estado histórico, evitando uma lista paralela hardcoded no replay.

### 4. Impedir vazamentos pelo cursor e pela câmera

O automatizador atual tende a mover o cursor até a origem gravada da ação. Sob visão filtrada isso pode denunciar uma unidade invisível.

Adicionar uma política de apresentação que:

- não mova o cursor para a origem de um ator oculto;
- não selecione nem destaque unidades invisíveis;
- não centralize a câmera em células que o observador não poderia associar à ação;
- não reproduza sons posicionais que revelem a origem indevidamente;
- possa iniciar a apresentação no primeiro evento observável, normalmente alvo ou impacto;
- preserve o comportamento cinematográfico completo no modo onisciente.

### 5. Integrar o Jornal do Comandante

O Jornal serve como convite e contexto para o replay, mas não substitui a timeline.

Possibilidades futuras:

- uma linha como “tiro da névoa” oferecer atalho para o turno/slot correspondente;
- iniciar o replay próximo da ação relacionada sem revelar metadados secretos;
- destacar apenas o evento observável, não o autor oculto;
- manter o texto já resolvido de forma fog-honesta no momento do registro.

Para isso, o evento do Jornal poderá guardar uma referência neutra à timeline, como identificador do record e índice da ação, sem expor esses dados na interface.

### 6. Papel do `JogadasManager`

O `JogadasManager` não deve ser usado como âncora nem como fonte primária do playback.

Ele permanece responsável por:

- auditoria resumida da partida;
- memória e análise da IA;
- estatísticas;
- resultados estruturados úteis para validação.

Ele não possui todos os dados necessários para dirigir o replay, como caminho real, camadas, posição do cursor, índice de compra e subpassos de sensores.

Pode futuramente ser usado para validar o resultado reconstruído ou fornecer detalhes observáveis, mas a sequência executável continua sendo o `ActionStack`.

### 7. Determinismo e checkpoints excepcionais

- Reexecutar ações usando o mesmo fluxo transacional do jogo.
- Toda ação reproduzida deve começar e terminar em `Neutral`.
- Replay não pode registrar novas ações, atualizar inteligência confirmada nem modificar a partida live.
- `Stop` deve restaurar integralmente o snapshot live anterior ao replay.
- Manter checkpoints completos somente para ações comprovadamente não determinísticas ou difíceis de reconstruir.
- Quando houver divergência, abortar o batch de forma segura e informar o erro, sem continuar sobre estado corrompido.

## Fluxo conceitual final

```text
Escolher turno do ator vermelho
        ↓
Escolher/fixar observador amarelo
        ↓
Restaurar âncora do início do turno
        ↓
Restaurar conhecimento histórico do amarelo
        ↓
Para cada PlayerAction do vermelho:
    executar ação transacionalmente
    classificar observabilidade para o amarelo
    apresentar somente informações autorizadas
    reaplicar FOW/detecção históricos derivados do novo estado
        ↓
Stop → restaurar a partida live exatamente como estava
```

## Critérios de aceite

1. Um atirador oculto não tem posição revelada por cursor, câmera, seleção, som ou texto.
2. O alvo visível consegue assistir ao impacto mesmo sem conhecer o atacante.
3. Se o disparo revelar o atacante pelas regras normais, ele aparece apenas a partir desse momento.
4. Uma ação totalmente fora da percepção do observador altera a reconstrução sem produzir apresentação informativa.
5. Trocar o observador recalcula a apresentação usando a memória histórica do novo slot.
6. O modo onisciente continua mostrando a ação completa.
7. O replay de um turno antigo não usa células exploradas, contatos ou ownership descobertos somente depois daquele turno.
8. Abrir, avançar, voltar, pausar e parar o replay não altera a verdade confirmada da partida live.
9. O formato permanece baseado em uma âncora por turno mais ações compactas, sem retornar a snapshots completos por ação.
10. Save/load preserva a timeline, o estado histórico de observação e a compatibilidade com saves anteriores.

## Cenários mínimos de teste

- Humano amarelo recebe tiro de unidade vermelha completamente oculta.
- Unidade vermelha oculta dispara e se revela após o ataque.
- Unidade vermelha executa movimento totalmente fora da visão amarela.
- Unidade vermelha entra e sai da visão durante o mesmo batch.
- Construção muda de dono dentro e fora da visão do observador.
- Replay do mesmo turno em visão amarela, vermelha e onisciente.
- Partida com um humano local e várias IAs.
- Hot-seat com dois humanos locais e cortina de privacidade.
- Load de save antigo, reprodução e novo save no formato compacto.
