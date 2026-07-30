# v4.4.4 — Transporte e Logística: Pode Suprir revisado

## Objetivo

Revisar a validação de suprimento, eliminar regras duplicadas entre ferramentas,
sensores e IA, e consolidar o `PodeSuprirSensor` como fonte única de verdade para
o atendimento logístico.

## Pode Suprir autoritativo

- A ferramenta `Tools > Logística > Pode Suprir` deixou de manter uma
  implementação paralela das regras.
- Candidatos válidos e inválidos agora vêm diretamente do
  `PodeSuprirSensor`.
- A interface continua responsável somente por montar o contexto, apresentar o
  relatório, organizar a fila de debug e estimar os custos.
- Motivos de rejeição, domínio planejado e operações preparatórias são os mesmos
  usados pelo jogo e pela IA.

## Compatibilidade de domínio

O sensor central passou a validar toda a sequência necessária para atendimento:

1. verifica o domínio e a altura atuais;
2. para aeronaves, consulta a possibilidade real de pouso ou decolagem;
3. para submarinos, consulta a possibilidade de emergência;
4. valida a camada resultante no hex atual;
5. confirma serviços, necessidades, estoques e limite de atendimentos;
6. em Play Mode, confirma também a capacidade econômica.

Uma operação preparatória só torna o candidato válido quando a camada resultante
é compatível com o domínio operacional do prestador.

## Integração com a IA

- A logística deixou de executar um pré-check próprio de pouso e camada.
- A escolha de alvos logísticos usa apenas o `PodeSuprirSensor`.
- Isso evita divergência entre planejamento da IA, execução do jogador e
  ferramentas de diagnóstico.
- O Reach Controller continua responsável por escolher o nível tático,
  operacional ou estratégico; o sensor continua responsável por dizer se o
  atendimento é legal.

## Funcionamento em Scenes:Editor

- Fora do Play Mode, o saldo runtime não bloqueia candidatos.
- A ferramenta apresenta o custo esperado de cada atendimento.
- A fila apresenta a estimativa consolidada dos serviços.
- Alcance, terreno, domínio, camada, serviço, necessidade e estoque continuam
  sendo validados normalmente.
- Quando o registro runtime `UnitManager.AllActive` está vazio, o sensor usa um
  fallback de consulta às unidades da cena. Em jogo normal permanece o caminho
  rápido pelo registro runtime.

## Operações aéreas e navais

- Criada a ferramenta `Tools > Operações Aéreas > Pode Pousar`.
- Ela permite usar a unidade selecionada, escolher um hex de destino ou assumir
  o próprio hex da aeronave.
- A consulta apresenta a camada prevista após o pouso sem comprometer a ação.
- As ferramentas relacionadas a pouso, decolagem, emergência e submersão foram
  organizadas para compartilhar a apresentação das operações aéreas e navais.

## Arquitetura transacional

- As ferramentas executam apenas consultas e simulações restauráveis.
- Nenhum teste altera definitivamente posição, camada, combustível, estoque,
  ocupação, FOW ou estado de ação.
- Pouso, decolagem, emergência, submersão e suprimento só são definitivos após o
  compromisso explícito da ação e o retorno a `CursorState.Neutral`.

## Verificação

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- Resultado: build concluído com 0 erros.
