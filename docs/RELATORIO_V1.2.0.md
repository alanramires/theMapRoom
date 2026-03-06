# Relatorio de Atualizacao - v1.2.0

## Em uma frase
A v1.2.0 consolida a refatoracao de dialog/helper orientada a dados e estabiliza de ponta a ponta o fluxo de fusao em runtime.

## O que isso trouxe na pratica
- Todas as mensagens principais de `panel_dialog` e `panel_helper` ficaram centralizadas em bancos editaveis.
- O comportamento de fusao em gameplay ficou alinhado com o sensor de teste, com validacao real por candidato.
- A UX de fusao ficou previsivel: candidato invalido mostra motivo, valido confirma, e a execucao da fila encerra sem efeitos colaterais.

## Principais melhorias
1. Dialog e Helper em database
- Introducao de `DialogData/DialogDatabase` e `HelperData/HelperDatabase`.
- Cadastro por `id`, `condition`, `message`, com suporte a tokens.
- Mensagens de erro da fusao agora podem ser customizadas por `id` (`fuse.invalid.insufficient_movement`).

2. Fluxo de fusao (sensor + gameplay)
- `PodeFundirSensor` retorna listas de candidatos validos e invalidos com motivo.
- Gameplay consome esse retorno para montar lista, pintar candidatos e bloquear confirmacao invalida.
- Candidatos invalidos aparecem no helper em cinza com justificativa.

3. Runtime de fusao e UX
- Queue/confirm preview no helper para leitura de resultado.
- Limpeza de estado/helper durante execucao da fila para evitar recalculo visual indevido.
- Unidade resultante finaliza com `movement remaining = 0` conforme regra.

4. Mensageria e consistencia
- Remocao de prefixo hardcoded em mensagens de erro de fusao no panel dialog.
- Texto exibido passa a respeitar exatamente o `message` cadastrado no `Dialog Data`.

5. Debug para teste rapido
- Comando `set move_remain <valor>` para alterar movimento restante da unidade sob cursor.

## Regras importantes
- Em `Fundindo`, validos e invalidos podem ser navegados, mas so validos podem confirmar.
- Ao confirmar invalido, toca erro e o motivo vem do catalogo/dialog DB.
- Pos-fusao, a unidade receptora termina com movimento restante zero.

## Bloco tecnico curto
- Scripts-chave: `TurnStateManager.Merge`, `TurnStateManager.HelperPanel`, `PodeFundirSensor`, `PanelDialogController`, `PanelHelperController`, `DebugManager`.
- Estruturas novas em assets: `Assets/DB/Dialog/*` (dialog/helper data e databases).

## Resultado
A v1.2.0 fecha o ciclo de refatoracao de fusao e mensagens: menos hardcode, mais controle por dados e comportamento consistente entre tool e gameplay.
