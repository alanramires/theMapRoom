# v4.0.20 - Ajustes para AI FOW Total

Esta versão prepara o comportamento da AI no modo **Fog of War Total**, reduz inferências acidentais de clique e completa as regras de observação avançada.

## AI e Fog of War Total

- A câmera deixa de acompanhar o cursor durante o turno da AI no FOW Total, evitando revelar ações executadas sob a neblina.
- O cursor continua funcionando internamente para a execução dos comandos da AI.
- O comportamento dos demais modos de partida permanece inalterado.

## Atalho contextual

- Novo toggle `Atalho Contextual` no `MatchController`, desativado por padrão e exposto no Inspector.
- Inferências de clique durante o movimento passam a depender explicitamente dessa opção.
- Confirmações finais por clique de mirar, embarcar, desembarcar, reunir, suprir e comprar também respeitam o toggle.
- Com o atalho desativado, Enter e os botões explícitos continuam confirmando normalmente.

## Manter posição e ajuda de alcance

- Novo botão `MANTER POSIÇÃO` no painel da unidade selecionada.
- O botão reutiliza o fluxo oficial de mover parado e não substitui a confirmação por Enter.
- Unidades com qualquer arma embarcada de alcance à distância recebem a dica para manter posição antes de mirar.

## Observadores avançados

- Construções controladas e não neutras passam a contribuir para a observação do próprio time.
- `ConstructionData` recebe alcance visual configurável, com valor padrão `0`, restringindo a visão ao próprio hex.
- Observadores de unidade precisam respeitar alcance visual, linha de visão e a correspondência chave-fechadura das habilidades de detecção e furtividade.
- A regra cobre também submarinos e unidades submersas.
- Construções não detectam furtividade enquanto não houver um modelo próprio de habilidades de detecção para elas.

## Interface e plataforma

- Base de mensagens e painel auxiliar atualizados para os novos atalhos e dicas.
- Ajustes de qualidade e suporte de performance para WebGL incluídos no checkpoint.
- Documentação de pendências do MVP atualizada.

## Validação

- `Assembly-CSharp.csproj`: build sem erros.
