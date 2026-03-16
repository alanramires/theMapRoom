# Relatorio de Atualizacao - v1.1.4

## Em uma frase
A v1.1.4 consolida o fluxo de dialog/helper em banco de dados e refatora a fusao para operar com candidatos validos/invalidos de forma previsivel em gameplay.

## O que isso trouxe na pratica
- Mensagens de interface ficaram editaveis por dados (`Dialog Database` e `Helper Database`), sem depender de string fixa no codigo para o fluxo principal.
- O sensor de fusao passou a separar candidatos validos e invalidos com motivo, tanto no tool quanto no runtime.
- O jogador consegue navegar pelos candidatos, enxergar invalidos no helper e receber motivo no panel dialog ao tentar confirmar um invalido.

## Principais melhorias
1. Dialog/Helper orientados a dados
- Padronizacao de `id`, `condition` e `message` para mensagens do panel dialog e panel helper.
- Tokens documentados e aplicados em runtime (`<unit>`, `<sensor>`, `<state>` e outros por contexto).
- Estrutura e nomes de objetos/bancos simplificados para manutencao.

2. Fusao com validacao real de movimento
- `PodeFundirSensor` passou a retornar listas de validos e invalidos com motivo.
- Validacao considera caminho/custo ate o receptor (nao apenas adjacencia simples).
- Candidatos invalidos aparecem no helper com estilo cinza + motivo.
- Confirmacao de candidato invalido em gameplay bloqueia acao e toca erro.

3. UX de fusao e preview
- Queue de fusao com preview de resultado.
- Preview de confirmacao e de process queue no helper.
- `X` no Servico do Comando agora abre preview/confirmacao antes da execucao automatica da fila.
- `panel_helper` mostra expectativa compacta de atendidos, ganhos agregados, custo previsto e saldo apos a fila.
- A execucao so comeca apos confirmar; cancelamento nao consome saldo.
- Limpeza de helper/estado durante execucao da fila para evitar recalculo visual indevido.

4. Debug e apoio a testes
- Novo comando: `set move_remain <valor>` para ajustar movimento restante da unidade sob cursor.
- Ajustes de inspeção para facilitar validar cenarios de fusao e sensores.

## Regras importantes
- Em `Fundindo`, validos e invalidos podem ser navegados; somente validos confirmam.
- Motivos de invalidez devem sair de mensagem configuravel quando houver `id` de dialog correspondente.
- Fusao usa retorno do sensor no runtime, evitando divergencia entre tool e gameplay.

## Bloco tecnico curto
- Arquivos-chave: `PodeFundirSensor`, `TurnStateManager.Merge`, `TurnStateManager.HelperPanel`, `PanelDialogController`, `PanelHelperController`, `DebugManager`.
- Novo cadastro de mensagem de fusao invalida por movimento no `Dialog Database` (`fuse.invalid.insufficient_movement`).

## Resultado
A versao deixa o fluxo de fusao mais confiavel, legivel e configuravel por dados, reduzindo comportamento inesperado entre ferramenta de teste e runtime.

---

## Anexo - v1.1.4a (hotfix)

### Em uma frase
O anexo v1.1.4a fecha ajustes finais do fluxo de fusao e remove prefixo hardcoded nas mensagens de erro.

### Ajustes aplicados
1. Pos-fusao
- A unidade resultante termina com `movement remaining = 0` (regra de acao consolidada).

2. Mensagem de erro configuravel
- Removido prefixo hardcoded `Fusao:` no push de mensagem.
- O texto exibido passa a respeitar exatamente o `message` do `Dialog Data` (ex.: `fuse.invalid.insufficient_movement`).

### Resultado do anexo
- Comportamento final da fusao fica alinhado com a regra definida.
- Customizacao de texto no panel dialog passa a funcionar sem interferencia de prefixo no codigo.
