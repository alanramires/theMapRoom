# Relatório v4.0.32 — Sonar e caça submarina

## Visão geral

Atualização dedicada à separação data-driven das camadas de visão, à propagação correta do sonar e ao fechamento de vazamentos de informação durante ações provisórias.

## Sonar e visão por camada

- A visão `Submarine/Submerged` passa a se propagar somente por terrenos que suportam explicitamente essa camada.
- Praia e outros terrenos exclusivamente `Naval/Surface` não conduzem mais o pulso submarino.
- O alcance submarino usa distância conectada pelo fundo do mar, impedindo que o sonar atravesse praias ou alcance trechos de mar isolados.
- O modo **All** preserva os caminhos, alcances e terrenos válidos de cada camada em vez de reaproveitar distância reta entre especializações.
- Especializações alternativas só contribuem quando o terreno, a estrutura ou a construção do hex suporta o domínio e a altura consultados.
- `Naval/Surface` não amplia a visão terrestre: a camada **Superfície** continua sendo a união visual de Land e Naval, mas cada domínio conserva seu próprio alcance.
- A política de LoS permanece independente da propagação por terreno; sensores com `Force Off` ignoram bloqueio óptico sem atravessar terrenos incompatíveis.

## Inspeção e diagnóstico

- O terceiro clique de inspeção de uma unidade passa a exibir os hexes que ela enxerga na camada selecionada pelo atalho `L`.
- Adicionado tile de marcação de visão com coloração correspondente ao time.
- As ferramentas **Pode Enxergar** e **Hex Enxergado** passam a oferecer filtro pelo time ativo, evitando atribuir visão do adversário ao jogador local durante o diagnóstico.
- A montagem das camadas Aérea, Superfície, Submarina e All foi alinhada às mesmas regras usadas pelo sensor em runtime.

## Nevoeiro e ações provisórias

- Opções contextuais de captura e desembarque deixam de revelar conteúdo escondido enquanto o movimento ainda é provisório.
- A mesma barreira de informação foi aplicada ao planejamento da IA para captura, transporte e desembarque.
- Cancelar uma tentativa mantém intactos o nevoeiro, os sensores e os caches confirmados, conforme o contrato transacional.

## Interface e depuração

- Refinado o comportamento móvel e transitório do `panel_helper`, incluindo restauração da posição e apresentação de unidades transportadas.
- Adicionado controle de depuração para ocultar o painel de rodada em partidas hot seat.
- Atualizados recursos visuais de tiles e ícones usados pela inspeção.

## Validação

- Projeto runtime compilado sem erros.
- Verificação de diferenças concluída sem erros de whitespace.

