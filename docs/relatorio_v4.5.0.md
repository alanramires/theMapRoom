# v4.5.0 — Refactor de Mudança de camada 0/5

## Objetivo

Planejar a consolidação das mudanças de camada aéreas e navais, separando as
regras físicas do hex das condições operacionais de cada ação.

Esta versão registra o ponto inicial do refactor. As cinco etapas estão
definidas, mas ainda não foram implementadas.

## Escada naval

- A primeira etapa criará o `PodeSubmergirSensor` como fonte única para a
  transição de `Naval/Surface` para `Submarine/Submerged`.
- A consulta considerará unidade, camada atual, ocupação, disparo, dano,
  emersão forçada, detecção e demais janelas de exposição.
- Terreno, estrutura combinada com terreno e construções também serão
  avaliados.
- Regras que forçam emersão ou impedem eclipse não poderão ser ignoradas pelo
  simples suporte do hex à camada submersa.
- O `TurnStateManager` deixará de reimplementar a decisão e passará a consumir o
  sensor.

## Escada aérea

- `PodeDecolarSensor` e `PodePousarSensor` serão revisados para compartilhar
  consultas equivalentes por hex.
- Aeronave, combustível, perfil aéreo, pista, local de pouso, espaço aéreo e
  locks de camada continuarão sendo validados.
- Decolagem curta, VTOL e decolagem completa permanecerão procedimentos
  distintos.
- O contexto distinguirá aeronaves que começaram o turno no chão das que
  pousaram durante uma operação composta.
- Receber combustível não autorizará retroativamente uma decolagem no mesmo
  batch nem disparará decolagem automática.
- Uma aeronave abastecida no chão poderá tentar depois uma decolagem normal,
  quando for selecionada e novamente avaliada pelo `PodeDecolarSensor`.

## Fundação comum

- A terceira etapa criará `LayerTransitionRules` em
  `Assets/Scripts/Units/Rules`.
- A fundação responderá se a camada de destino cabe no hex.
- Suporte da unidade, terreno, estrutura, construção, skills, modos adicionais
  e ocupação da banda serão centralizados.
- Combustível, dano, disparo, detecção, suprimento e ordem das operações
  permanecerão nos sensores específicos.
- As cópias existentes no `TurnStateManager`, em janelas de Editor e em outros
  sensores serão removidas durante a migração.

## Altitude e operações compostas

- Um sensor próprio tratará `AirLow ↔ AirHigh` sem consultar terreno,
  construção ou skills do hex inferior durante nivelamento em voo.
- `TookOffRecently` não será um bloqueio universal de `AirHigh`.
- Aeroportos poderão preservar decolagem direta para `AirHigh`.
- Estradas, porta-aviões e procedimentos equivalentes poderão manter a aeronave
  recém-decolada em `AirLow`.
- Aviões-tanque deverão descer para atender aeronaves temporariamente restritas
  a `AirLow`.
- `PodeArremeterSensor` tratará sequências autorizadas de pouso, operação e
  decolagem no mesmo hex, respeitando a ordem e o combustível anterior à
  operação.
- `PodeSubmergirRapidamenteSensor` tratará futuros retornos autorizados após
  emersão, sem superar bloqueios de suprimento, disparo, dano ou detecção.
- As transições finais continuarão passando por `PodeDecolarSensor` ou
  `PodeSubmergirSensor`.

## Ferramentas

As janelas serão organizadas nos menus de operações aéreas e navais:

- `Pode Decolar`;
- `Pode Pousar`;
- `Pode Mudar de Altitude`;
- `Pode Arremeter`;
- `Pode Emergir`;
- `Pode Submergir`;
- `Pode Submergir Rapidamente`.

Cada janela apenas montará o contexto, chamará seu sensor e exibirá o relatório.
Nomes de arquivos e menus serão alinhados, preservando os respectivos `.meta`.

## Arquitetura transacional

- Sensores e ferramentas permanecerão consultas sem efeitos definitivos.
- Nenhuma prévia alterará camada, combustível, ocupação, FOW, detecção, caches
  confirmados ou memória da IA.
- Mudanças de camada serão aplicadas somente no compromisso explícito da ação.
- Cancelamento restaurará o snapshot confirmado.
- O recálculo definitivo continuará ocorrendo após o retorno a
  `CursorState.Neutral`.

## Verificação

- Plano dividido em cinco etapas.
- Implementação atual: `0/5`.
- Alterações de gameplay nesta versão: nenhuma.
