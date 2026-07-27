# v4.8.5 — Refactor da AI Logística e de Estoque 5/5

## Visão geral

Esta etapa encerra o ciclo de Logística e Estoque com uma regra simples de
prioridade: uma rede de carga deve responder imediatamente a uma pane real,
mas não pode interromper toda a operação por uma reposição apenas preventiva.

O resultado é que Hubs híbridos — como Trem de Carga, Navio-Tanque e
Porta-Aviões — passam a usar seus estoques sem perder sua missão principal.
Eles atendem uma construção ou unidade criticamente vazia antes de procurar
passageiros; fora dessa emergência, transporte, atendimento de campo e demais
papéis continuam fluindo na ordem normal.

## Estoque crítico vem antes, estoque preventivo não paralisa

O `MelhorEstoque` recebeu um filtro de política para que o controller possa
pedir apenas opções que correspondam à urgência de uma decisão específica.
Esse filtro entra depois da validação prospectiva do `PodeTransferir`: a IA não
considera uma troca apenas por proximidade, mas somente se ela for compatível,
possuir quantidade útil e puder ocorrer no encontro avaliado.

Para um transportador que também é Hub:

- construção ou recebedor crítico pode interromper EVAC/Pickup;
- recarga crítica da própria unidade também entra nessa prioridade;
- uma necessidade preventiva fica no fluxo normal de Estoque e não cancela uma
  coleta tática ou operacional pronta;
- destino cheio, sem carga compatível ou sem transferência útil não cria uma
  ordem de movimento.

Isso separa a reação a uma pane da manutenção cotidiana da rede.

## Papéis híbridos conservam sua identidade

O papel `Estoque` continua distribuindo ativamente para construções e unidades
recebedoras. A Logística preserva sua ordem: primeiro atendimento de campo,
depois recarga própria quando necessária e, sem cliente urgente, circulação de
carga pela rede.

O Transportador-Hub ganha somente o desvio crítico acima. Ele não vira um
caminhão de estoque por causa de uma reserva preventiva, nem deixa uma estação
vazia sem resposta quando há uma falta material de recursos.

## Artilharia rebocada permanece cautelosa

A antiga proibição baseada em `IsInvading` não voltou. A Artilharia de Campanha
continua podendo pedir carona fora de uma invasão formal, usando as consultas
modernas de `Quero Carona`, `Melhor Embarque` e `PodeEmbarcar`.

Enquanto a avaliação própria de posição de artilharia não existe, permanece uma
guarda temporária e localizada: uma unidade que exige reboque não embarca rumo
a uma hotzone sem retaguarda ou zona segura de desembarque. A regra não afeta
as demais unidades de apoio de fogo.

O próximo refinamento dessa peça será uma ferramenta de implantação de
artilharia: avaliar frente, território consolidado, retaguarda e faixa útil de
tiro antes de a unidade declarar que quer transporte.

## Contrato transacional preservado

O filtro de prioridade é apenas uma consulta. Ele não move unidades, não
transfere estoque, não altera ocupação e não revela FOW. A transferência e o
embarque só são executados pelos batches e compromissos oficiais, com as
revalidações de sensor já existentes.

## Validação

- build do runtime e do Editor sem erros;
- conferência de Hub transportador priorizando destinatário crítico;
- conferência de necessidade preventiva mantendo Pickup disponível;
- conferência de opções sem carga útil sendo descartadas antes do movimento;
- conferência de Artilharia de Campanha recusando hotzone sem retirada segura;
- `git diff --check` sem problemas nos arquivos desta etapa.
