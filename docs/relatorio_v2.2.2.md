# Relatorio v2.2.2 - AI Air movement

## Tema

Consolidacao dos ajustes de movimento aereo, sensores pos-movimento e ferramentas de debug para a IA operar melhor aeronaves em camadas diferentes do terreno.

## Principais mudancas

- Ajuste do avanco rogue de aeronaves para preferir progresso mais direto em direcao ao alvo, evitando rotas laterais quando existe uma diagonal melhor.
- Atualizacao da ferramenta `Tools > Transporte > Caminhos Validos` para calcular aeronaves em voo, respeitar a altitude atual da unidade selecionada e exibir uma nota de progressao mais util que apenas deslocamento bruto.
- Correcao da regra de hex disputado para considerar tambem `Domain` e `HeightLevel`, evitando que um soldado no chao bloqueie as opcoes de ataque de uma aeronave sobrevoando o mesmo hex.
- Encapsulamento da consulta de ocupacao por celula/camada no `HexOccupancyQuery`.
- Ajustes em combate e suporte de fogo para lidar melhor com alvos aereos, reposicionamento e casos em que o sensor ve o alvo mas a linha de tiro nao permite disparo.
- Melhorias incrementais no planejamento operacional e no shopping da IA para demandas aereas, suporte defensivo e necessidades de reabastecimento/manutencao.
- Persistencia adicional de estado da IA no save/load para reduzir reprocessamento de estagios ja executados apos carregar um jogo salvo no meio do turno.
- Ajustes visuais e de coabitacao de unidades no mesmo hex em camadas diferentes.

## Debug e ferramentas

- A janela de caminhos validos agora mostra a camada efetiva de calculo e o database usado para a unidade selecionada.
- A progressao de rota usa score ponderado: progresso por PM, aproximacao hexagonal e penalidade por desvio da linha origem-destino.
- A ferramenta ajuda a comparar rotas aereas com melhor fidelidade ao comportamento esperado da IA.

## Validacao

- Build de runtime validado com `dotnet build Assembly-CSharp.csproj`.
- Build de editor validado com `dotnet build Assembly-CSharp-Editor.csproj`.
- Restam apenas warnings conhecidos de APIs obsoletas do Unity.

## Observacoes

Esta versao ainda nao introduz a camada completa de task force. O foco foi estabilizar a base de movimento aereo, sensores e ferramentas para que os testes de IA em mapas com avioes, helicopteros e unidades em camadas sobrepostas fiquem confiaveis.
