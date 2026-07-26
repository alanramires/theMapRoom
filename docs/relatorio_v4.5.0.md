# v4.5.0 — Refactor de Mudança de camada 0/5

## Objetivo

Registrar o plano completo para consolidar as mudanças de camada aéreas e
navais. Esta versão representa o marco inicial `0/5`: define responsabilidades,
regras e critérios de validação, sem implementar ainda as cinco etapas do
refactor.

Toda implementação futura deve preservar o contrato de ações transacionais:
nenhuma mudança de camada, ocupação, combustível, detecção, Fog of War, cache
confirmado ou flag operacional se torna definitiva antes do compromisso
explícito da ação e do retorno a `CursorState.Neutral`.

## Separação de responsabilidades

- `LayerTransitionRules` responderá: “esta camada cabe neste hex?”.
- Sensores específicos responderão: “esta operação pode acontecer agora?”.
- Janelas de ferramentas apenas coletarão entradas, chamarão sensores e
  apresentarão seus relatórios.
- `TurnStateManager` consumirá as decisões e aplicará seus efeitos somente no
  compromisso.
- Cancelamento deverá restaurar integralmente posição, camada e contexto
  provisório, sem revelar informação nem recalcular o tabuleiro confirmado a
  partir da prévia.

## Etapa 1 — Fechar a escada naval

Criar `PodeSubmergirSensor` como fonte única para a transição
`Naval/Surface → Submarine/Submerged`.

O sensor deverá validar:

- compatibilidade da unidade e camada atual;
- suporte à camada submersa;
- hex consultado e ocupação da banda de destino;
- terreno;
- combinação de estrutura e terreno;
- construções;
- skills obrigatórias e bloqueadas;
- regras que forçam emersão ou proíbem submersão/eclipsamento;
- trava após disparo;
- trava após dano ou emersão forçada;
- detecção recente;
- demais janelas de exposição e locks operacionais.

Regras de construção, estrutura e terreno que forçam emersão prevalecerão sobre
o simples suporte físico à camada submersa. O `TurnStateManager` e os demais
consumidores deixarão de reimplementar essas decisões.

## Etapa 2 — Consolidar a escada aérea básica

Revisar `PodeDecolarSensor` e `PodePousarSensor` para garantir:

- guarda explícita de aeronave;
- consultas equivalentes por hex atual ou hipotético;
- combustível quando aplicável;
- perfil aéreo;
- pista ou local de pouso;
- terreno, estrutura e construção;
- espaço aéreo;
- locks de camada;
- decolagem curta, VTOL, completa e entrada na altitude preferencial quando
  autorizada.

O estado de solo deverá distinguir explicitamente:

- aeronave que começou o turno pousada, com operação encerrada;
- aeronave que pousou durante a ação ou batch atual;
- aeronave que já estava pousada no início da operação;
- combustível existente antes da operação;
- combustível recebido durante a operação;
- suprimento recebido antes de uma futura seleção normal.

`ReceivedSuppliesThisTurn` não será usado isoladamente como substituto desse
contexto. Receber combustível não disparará decolagem automaticamente nem
criará permissão retroativa no mesmo batch. Uma aeronave no chão e abastecida
poderá tentar posteriormente uma decolagem normal quando for selecionada; essa
nova tentativa passará integralmente por `PodeDecolarSensor`.

## Etapa 3 — Criar a fundação comum

Criar `LayerTransitionRules` em `Assets/Scripts/Units/Rules`, responsável
exclusivamente por:

- suporte da unidade à camada;
- terreno;
- estrutura combinada com terreno;
- construções;
- skills obrigatórias e bloqueadas;
- modos adicionais;
- ocupação da banda de destino.

A fundação não avaliará combustível, disparo, dano, detecção,
`TookOffRecently`, suprimento, ordem de ações, arremetida ou retorno rápido.

Remover gradualmente as duplicações de:

- `TurnStateManager.CanUseLayerModeAtCurrentCell`;
- `PodePousarWindow`;
- especializações físicas dentro de outros sensores;
- helpers próprios de suprimento, fusão e ferramentas de debug.

A migração deverá preservar a precedência atual de construção, estrutura com
terreno e terreno.

## Etapa 4 — Altitude e operações compostas

### Pode Mudar de Altitude

Criar sensor específico para `AirLow ↔ AirHigh`, validando:

- aeronave e suporte à altitude de destino;
- ocupação da banda aérea;
- locks de camada;
- restrições operacionais;
- perfil e resultado da decolagem recente.

Nivelamento em voo não consultará terreno, construção, estrutura ou skills de
entrada do hex inferior.

`TookOffRecently` não será um veto universal a `AirHigh`. O resultado legítimo
de `PodeDecolarSensor` deverá ser preservado:

- aeroporto poderá colocar a aeronave diretamente em `AirHigh`;
- estrada, porta-aviões ou procedimento equivalente poderá terminar em
  `AirLow`;
- uma aeronave recém-decolada em `AirLow` não poderá subir indevidamente ao
  acompanhar um avião-tanque;
- o avião-tanque deverá descer para atendê-la em `AirLow`;
- helicóptero e caça recém-decolado serão atendidos em `AirLow`;
- após o término normal da restrição específica, a subida volta a ser possível.

O contexto poderá precisar preservar altitude de saída, procedimento utilizado
e eventual lock temporário de subida, em vez de depender apenas do booleano
`TookOffRecently`.

### Pode Arremeter

Criar `PodeArremeterSensor` para
`pousar → operação autorizada → tentar decolar no mesmo hex`.

Como a sequência ocorre no mesmo hex, ela não consumirá autonomia de
deslocamento. A ordem dos eventos, porém, será obrigatória. O sensor consultará
um snapshot com camada inicial, estado de voo, combustível anterior, combustível
recebido, operação intermediária e autorização explícita.

Regras:

- aeronave já pousada no início não arremete;
- pouso para transferência não autoriza arremetida por padrão;
- aeronave que pousou sem combustível e depois foi abastecida permanece
  pousada;
- suprimento não cria autorização retroativa nem decolagem automática;
- apenas operações explicitamente autorizadas poderão tentar arremetida;
- a decolagem final ainda passará por `PodeDecolarSensor`.

### Pode Submergir Rapidamente

Criar `PodeSubmergirRapidamenteSensor` para
`emergir → operação autorizada → tentar submergir`.

Regras:

- submarino que emerge para receber suprimento não submerge na mesma rodada;
- disparo, dano, detecção e locks continuam prevalecendo;
- terreno, estrutura com terreno, construções e emersão forçada continuam
  prevalecendo;
- apenas operações futuras explicitamente autorizadas poderão solicitar
  retorno rápido;
- a transição final sempre passará por `PodeSubmergirSensor`.

## Etapa 5 — Organizar a caixa de ferramentas

Estrutura planejada:

```text
Tools > Operações Aéreas > Pode Decolar
Tools > Operações Aéreas > Pode Pousar
Tools > Operações Aéreas > Pode Mudar de Altitude
Tools > Operações Aéreas > Pode Arremeter

Tools > Operações Navais > Pode Emergir
Tools > Operações Navais > Pode Submergir
Tools > Operações Navais > Pode Submergir Rapidamente

Tools > Sensors > Pode Mudar de Camada
```

A ferramenta genérica permanecerá somente se continuar útil para consultar a
fundação física. Nomes de arquivos corresponderão às janelas, regras não serão
reimplementadas no Editor, arquivos `.meta` serão preservados ou movidos junto
com seus assets e ajustes de menu/texto não alterarão gameplay.

## Critérios de validação

- Submersão bloqueada corretamente por terreno, estrutura, construção,
  ocupação, disparo, dano, detecção e emersão forçada.
- Aeroporto preservando decolagem direta para `AirHigh`.
- Estrada e porta-aviões preservando saída em `AirLow`.
- `TookOffRecently` sem bloquear indevidamente uma decolagem legítima em
  `AirHigh`.
- Avião-tanque descendo para atender aeronaves restritas a `AirLow`.
- Combustível recebido durante pouso sem criar arremetida retroativa.
- Aeronave abastecida no chão usando uma futura decolagem normal, mediante nova
  avaliação completa.
- Operações não autorizadas mantendo aeronave pousada ou submarino emergido.
- Cancelamento restaurando o snapshot confirmado.
- Fog of War, detecção, IA e caches sem observar estados provisórios.

## Estado desta versão

- Planejamento: concluído.
- Implementação das etapas: `0/5`.
- Alterações de gameplay: nenhuma.
