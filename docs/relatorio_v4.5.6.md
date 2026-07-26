# v4.5.6 — Pouso de Emergencia

## Objetivo

Adicionar à tomada de decisão de aeronaves `IsUnderRepair` uma contingência de
autonomia: quando não houver recuperação tática ou operacional disponível, a IA
passa a procurar um hex válido para um futuro pouso de emergência.

A IA não executa o pouso durante sua ação. Ela apenas termina o movimento
pairando sobre o local escolhido; o pouso forçado continua sendo responsabilidade
exclusiva do upkeep de início de turno.

## Coordenação de recuperação

- A busca usa `AIActionReachCoordinator`.
- São avaliados os horizontes tático e operacional.
- Entram como possibilidades de recuperação:
  - aeródromos e construções de reparo compatíveis;
  - supridores;
  - unidades elegíveis para fusão;
  - plataformas navais compatíveis.
- O horizonte tático respeita os caminhos realmente alcançáveis na rodada.
- O horizonte operacional considera a progressão configurada pelo coordenador.
- A consulta apenas lê o estado confirmado e não move unidades nem reserva
  ocupação.

## Faixa crítica de autonomia

A aeronave entra em urgência quando:

```text
combustível atual <= movimento da unidade + consumo do próximo upkeep
```

O movimento vem do perfil da unidade e o consumo de upkeep é consultado por
`OperationalAutonomyRules`.

Exemplo: uma aeronave com movimento `9` e upkeep `5` entra na faixa crítica com
combustível menor ou igual a `14`.

Quando crítica e sem recuperação tática ou operacional, a busca por uma posição
de pouso futuro prevalece sobre oportunidades de combate.

## Escolha da posição de espera

- Cada candidato é validado por `PodePousarSensor.CanLandAtCell`.
- A validação usa o hex hipotético sem alterar a posição runtime da aeronave.
- Ocupação aérea, perfil da aeronave, terreno, estrutura, construção, skills e
  demais regras de pouso continuam centralizados no sensor.
- O caminho precisa estar disponível no turno e ser compatível com o combustível
  atual.
- A pontuação favorece:
  - construções aliadas;
  - estradas utilizáveis como pista;
  - menor ameaça;
  - maior combustível residual;
  - menor custo de movimento;
  - desempate determinístico por coordenada.

## Aproximação sem LZ tática

Se nenhuma posição válida de pouso estiver ao alcance imediato, a IA procura uma
LZ futura no tabuleiro e escolhe o melhor passo alcançável em sua direção.

A aeronave permanece em voo. Se nem mesmo uma aproximação válida existir, o
sistema registra um diagnóstico explícito de risco inevitável de queda, sem
inventar um pouso ou ignorar as regras existentes.

## Upkeep e pouso efetivo

- A ação produzida pela IA continua sendo somente um `BuildMoveBatch`.
- Nenhum caminho novo chama pouso, decolagem, troca de camada ou engine state.
- A aeronave paira sobre a LZ durante o restante da rodada.
- O upkeep existente decide se ocorre pouso forçado ou queda.
- Jogadores e IA continuam sem acesso direto a comandos livres de pouso e
  decolagem.

## Arquitetura transacional

- A análise é pura e não altera posição, combustível, camada, ocupação, FOW,
  detecção ou caches confirmados.
- O destino escolhido é materializado como ação transacional normal.
- Nenhum efeito definitivo ocorre durante a decisão.
- O contrato `Neutral → ação provisória → compromisso → Neutral` permanece
  preservado.

## Arquivos principais

- `Assets/Scripts/Match/AI/Units/Repair/AIController.Repair.AirEmergency.cs`
- `Assets/Scripts/Match/AI/Units/Repair/AIController.Repair.cs`

O novo arquivo possui `.meta` próprio para preservação correta pelo Unity.

## Verificação

- `dotnet build Assembly-CSharp.csproj --no-restore`
- Auditoria das chamadas de pouso e mudança de camada no fluxo novo.
- Auditoria do uso de `PodePousarSensor` por hex hipotético.
- Auditoria do `AIActionReachCoordinator` nos níveis tático e operacional.
- `git diff --check` aplicado aos arquivos da implementação.
- Resultado: build concluído com 0 erros.
