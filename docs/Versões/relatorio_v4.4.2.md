# v4.4.2 — Transporte e Logística ajustes

## Objetivo

Consolidar os ajustes de comportamento e infraestrutura realizados após a
introdução das ferramentas de transporte, aproximando transporte, logística,
reparo e transferência de estoque das mesmas regras orientadas por sensores,
dados e caminhos válidos usadas pelo jogador.

## Transporte e courier

- O roteador universal diferencia transportadores vazios, carregados e lotados
  antes de escolher entre courier, papel primário e logística.
- Transportadores híbridos vazios consultam passageiros compatíveis antes de
  executar seu comportamento secundário.
- Hidroaviões, Chinooks, APCs, navios e trens validam as células de espera pelas
  regras de embarque configuradas no `UnitData`.
- A seleção de passageiros contempla unidades planejadas, rogues e rebeldes
  nos fluxos normal e sem QG.
- O courier preserva a prioridade por turno de embarque e usa a vaga física
  apenas como desempate.
- Transportadores lotados permitem que os papéis especializados atuem antes do
  courier, evitando que a simples existência de carga masque ações necessárias.
- Incluídos tratamentos para transporte hospitalar e cargas em manutenção.

## Melhor desembarque

- A ferramenta e o serviço passaram a preservar desembarques conjuntos para
  dois passageiros rebeldes ou rogues quando a geografia oferece uma LZ válida.
- A progressão de múltiplas rodadas conserva o grupo enquanto se aproxima da
  melhor LZ conhecida.
- Quando a geografia comprovadamente não comporta duas unidades, o passageiro
  prioritário pode desembarcar sem criar um bloqueio permanente.
- Objetivos ocupados, células inválidas e spots incompatíveis continuam sendo
  rejeitados pelos sensores e pelas regras reais de ocupação.
- Rogues procuram capturas oportunistas no corredor de avanço; rebeldes
  distribuem passageiros por objetivos capturáveis próximos.
- Passageiros com plano mantêm o destino definido pelo planejador.
- A avaliação compartilha mapas reversos de custo por passageiro e evita
  reconstruir a mesma rota para cada spot de uma LZ.

## Hotzone e progressão

- O serviço de hotzone ganhou modalidades especializadas para serviço e fusão.
- A hotzone de suprimento usa destinos legais de movimento e alcance de serviço
  antes de executar a validação detalhada do sensor.
- A hotzone de fusão respeita a necessidade de movimento restante para concluir
  a ação, inclusive em terrenos de custo elevado.
- Movimento e progressão continuam usando caminhos válidos; distância
  geométrica serve apenas como ordenação auxiliar, nunca como prova de acesso.

## Logística e transferência

- Criado o perfil automático de serviços logísticos:
  - `None`: não oferece serviços;
  - `StockTransfer`: oferece somente transferência de estoque;
  - `FieldService`: oferece ao menos um serviço de campo.
- O perfil aparece nos editores de `UnitData` e `ConstructionData` e é
  recalculado a partir dos serviços configurados.
- Unidades exclusivamente de transferência não perseguem alvos como se fossem
  caminhões de serviço de campo.
- O restock passou a consultar uma fonte abstrata, que pode ser construção ou
  unidade móvel.
- Cada fonte candidata é validada pelo próprio `PodeTransferirSensor`, incluindo
  estoque, domínio, tier, pouso, alcance e célula de encontro.
- A implementação logística foi separada em arquivos de orquestração,
  suprimento, transferência, restock, reposicionamento e helpers.
- Logs de reposicionamento agora identificam explicitamente o alvo de serviço.

## Reparos e transportadores

- A busca por reparo contempla destinos fixos e transportadores compatíveis com
  a unidade avariada.
- Unidades da Marinha capazes de transportar e aceitar as skills da carga podem
  participar como destino de pouso ou manutenção.
- A procura de fusão usa a hotzone especializada em vez de varrer diretamente
  todo o tabuleiro.
- Supridores transportadores podem tratar uma unidade embarcada, procurar
  restock por transferência e somente depois recorrer ao desembarque de
  manutenção.

## Dados e Inspector

- Os campos de embarque e desembarque em construções receberam rótulos mais
  explícitos no Inspector.
- `ConstructionFacilityType.Everything` foi renomeado para
  `AnyConstruction`, preservando o valor serializado.
- Atualizados dados de transportadores, praia, estruturas, construções e setores
  usados na calibração.
- Incluídos ajustes de cena, catálogo e arte para os cenários de teste atuais.

## Arquitetura transacional

- As novas consultas de hotzone, desembarque, restock e transferência são
  avaliações sem efeitos definitivos.
- Movimento, embarque, desembarque, reparo e transferência continuam sendo
  aplicados somente pelo batch confirmado.
- Nenhuma simulação de destino altera FOW, ocupação confirmada, recursos,
  revisões ou memória da IA antes do compromisso e do retorno a
  `CursorState.Neutral`.

## Verificação

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- Resultado esperado: build concluído com 0 erros.
