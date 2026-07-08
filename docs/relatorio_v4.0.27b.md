# v4.0.27b - Minor Fixes e Road boost por terreno

Esta versao consolida ajustes de nevoa de guerra, apresentacao do Panel Helper e especializacao das regras de rodovia por terreno.

## Nevoa de guerra

- O cursor passa para a sorting layer `FogOfWar` durante o turno humano, acima da nevoa opaca, e retorna para `SFX` no turno da AI.
- Os caminhos validos seguem a mesma regra e permanecem visiveis sobre a nevoa no turno humano.
- Unidades e HUD deixam a propria nevoa opaca controlar a oclusao, evitando desaparecimentos e aparicoes tardias durante o movimento inimigo.
- O comando de debug foi renomeado de `set fog x` para `set fow x`.

## Panel Helper

- Estruturas de rota agora sao sobrepostas ao sprite do terreno no icone de local.
- A estrutura usa 40% da largura e 100% da altura, preservando a leitura do terreno nas laterais.
- O tempo de exibicao de inspecao foi uniformizado em 6 segundos para unidades, construcoes e terrenos.
- `StructureData` recebeu descricoes especificas por par Estrutura+Terreno, com fallback para a descricao geral.

## Road boost por terreno

- Cada par de descricao Estrutura+Terreno pode marcar `Road Boost Off`.
- O override desativa o passo extra gratuito somente naquele par, mantendo o boost global nos demais terrenos.
- O calculo de caminhos validos consulta o terreno real do hex antes de conceder o bonus.
- A prioridade de custo foi corrigida: regras gerais do terreno sao aplicadas primeiro e o par Estrutura+Terreno, mais especifico, e aplicado por ultimo.
- Na Rodovia sobre Montanha, o override da skill `Motor` passa a resultar corretamente em custo 2, sem ser sobrescrito pelo custo 6 de `Off-Road`/`Alpino` da Montanha.
- A mesma precedencia foi aplicada aos calculos de movimento, fusao e analise de setores da AI.

## Ferramentas

- `Road Route Painter` foi movido de `Tools > Logistica` para `Tools > Transporte`.
- A orientacao exibida no editor de `StructureData` foi atualizada para o novo caminho.

## Validacao

- `Assembly-CSharp.csproj`: build concluido com 0 erros.
