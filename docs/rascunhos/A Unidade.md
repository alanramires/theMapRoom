# A Unidade

Quando voce seleciona uma unidade, o turno dela passa por fases bem claras. A ideia e simples: primeiro voce resolve movimento, depois decide se quer usar alguma acao de sensor, e por fim encerra a acao dessa unidade.

## Fluxo da unidade (visao geral)
1. **Fase de movimento**: `Neutral -> UnitSelected -> MoveuParado` ou `MoveuAndando`.
2. **Fase pos-movimento**: os sensores rodam em segundo plano e liberam as acoes possiveis.
3. **Fase do sensor escolhido**: cada acao entra em um pequeno submenu (normalmente 2 a 3 etapas).
4. **Fim da unidade**: a unidade marca que ja agiu. A rodada do jogador continua com outras unidades.

## 1) Fase de movimento
No estado `Neutral`, voce escolhe uma unidade aliada.

Ao selecionar, ela vai para `UnitSelected`. Aqui voce decide:
- mover para outro hex (vira `MoveuAndando`), ou
- confirmar no proprio hex (vira `MoveuParado`).

Em ambos os casos, o jogo entra na fase pos-movimento.

## 2) Fase pos-movimento (sensores)
Depois do movimento, o sistema avalia automaticamente o contexto e ativa apenas as acoes validas para aquela unidade naquele momento.

Importante: voce **pode nao usar nenhum sensor** e apenas finalizar a unidade.

Acoes que podem aparecer:
- `A` Mirar: iniciar ataque em alvo valido.
- `E` Embarcar: entrar em um transportador valido.
- `D` Desembarcar: sair de transportador quando houver opcao valida.
- `C` Capturar: iniciar captura da construcao atual, se permitido.
- `F` Fundir: combinar com unidade compativel.
- `S` Suprir: aplicar servicos/suprimentos em alvo valido.
- `T` Transferir: transferir recursos, quando houver contexto.
- `L` Operacao de camada/altitude: pousar, subir, descer, emergir ou submergir (conforme a unidade).
- `M` Apenas mover: encerra a unidade sem executar outra acao.

## 3) Fase do sensor escolhido
Quando voce escolhe um sensor, o jogo entra em um submenu da acao.

Na pratica, quase sempre segue este formato:
1. escolher alvo/opcao,
2. confirmar,
3. executar.

Exemplos comuns:
- Mirar: escolher alvo -> confirmar ataque -> resolver combate.
- Embarcar: escolher transportador -> confirmar -> executar embarque.
- Operacao de camada (`L`): escolher opcao de altitude/camada -> confirmar -> aplicar.

## 4) Fim da acao da unidade
Depois da execucao (ou de `M`), a unidade e finalizada como "ja agiu".

Isso **nao termina sua rodada**. Voce ainda pode selecionar e mover outras unidades antes de encerrar o turno geral do time.

## Exemplo concreto de uso
- Um `Soldado` chega em uma estrutura inimiga e termina movimento em `MoveuAndando`.
- No pos-movimento, os sensores liberam `C` (capturar) e talvez `A` (mirar), dependendo do contexto.
- Voce escolhe `C`, confirma a captura e finaliza essa unidade.
- Em seguida, continua a rodada com outra unidade sua.

## Resumo curto
Selecionar uma unidade no Map Room nao e "clicar e atacar". E um mini fluxo tatico:
**mover -> ler sensores validos -> escolher acao (ou pular) -> finalizar unidade**.
