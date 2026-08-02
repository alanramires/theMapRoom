# Ideias futuras — decolagem a partir de transportadores

## Motivação

O desembarque de aeronaves transportadas funciona hoje, mas ainda não é uma
"caixa de Lego". O comportamento de lançamento está distribuído entre o sensor
de desembarque e o executor, com decisões específicas para porta-aviões.

O objetivo futuro é fazer toda saída de aeronave transportada consultar o
`PodeDecolarSensor` e receber dele um plano puro, sem alterar estado durante a
consulta. O executor deve apenas aplicar o plano depois do compromisso da ação.

## Contrato atual do PodeDecolar

`PodeDecolarSensor` é uma consulta pura e autoritativa para decolagens normais.
Seu relatório já informa:

- se a decolagem é válida;
- o procedimento de decolagem;
- quantos hexágonos podem ou devem ser percorridos;
- o `endHeight` da aeronave.

Decolagens completas usam a altura preferida ou nativa da aeronave. Procedimentos
curtos de pista podem terminar em `Air/Low`.

Atualmente, porém, o sensor rejeita aeronaves embarcadas. Ele também não recebe
uma plataforma de lançamento como contexto.

## Problemas encontrados

### O porta-aviões não usa PodeDecolar

O fluxo naval está implementado como exceção dentro do desembarque:

- `PodeDesembarcarSensor` reconhece transportador naval com slot de aeronave;
- esse caso ignora a validação normal do `PodeDecolarSensor`;
- a guarda de runtime aprova diretamente transportadores navais;
- o executor força a aeronave para `Air/Low`;
- a saída custa 1 de autonomia e exige deslocamento de 1 hex.

Portanto, o `Air/Low` naval não é atualmente uma decisão do `PodeDecolar`. É um
hardcode externo ao sensor.

### Land/Surface artificial

Durante o embarque e o spawn visual do desembarque, a aeronave é forçada para
`Land/Surface`. Isso inventa um solo que pode não existir.

Enquanto uma unidade está embarcada, sua camada operacional efetiva deve vir do
transportador:

```text
camada operacional efetiva = EmbarkedTransporter.GetCurrentLayerMode()
```

Isso não significa necessariamente copiar o domínio do transportador para o
`currentDomain` do passageiro. Passageiros embarcados não ocupam o tabuleiro e
algumas aeronaves com `Aircraft Carrier Landing` não declaram `Naval/Surface`
nos próprios modos. A plataforma deve ser contexto da consulta, não uma camada
forçada na ficha da aeronave.

### Escada S/D ainda é ferramenta de debug

Os controles do Inspector `Subir Domain (S)` e `Descer Domain (D)` revelam a
escada desejada:

```text
Submerged -> Surface -> Air/Low -> Air/High
```

Para aeronaves:

```text
Surface -> Air/Low  = 1 subida
Surface -> Air/High = 2 subidas
Air/Low -> Air/High = 1 subida
```

Entretanto, a implementação atual desses controles não pode ser usada como
fundamento do sensor:

- parte da ordenação está no código de Editor;
- `TryStepLayerStateForDebug` é mutável;
- o fallback de debug pode forçar uma camada não declarada pela unidade.

É necessário extrair uma regra runtime pura para consultar degraus e destinos
sem modificar o `UnitManager`.

## Modelo futuro: regras de lançamento por slot

O comportamento de lançamento deve ser dado da plataforma, preferencialmente no
`UnitTransportSlotRule`, pois um transportador pode possuir slots com funções
diferentes.

Modelo conceitual:

```text
Aircraft Launch Rule
  Enabled
  Vertical Steps
  Minimum Exit Hexes
  Maximum Exit Hexes
  Autonomy Cost
  Ends Action
```

Os nomes e tipos definitivos ainda precisam ser escolhidos. O ponto importante é
que o sensor interpreta dados; ele não identifica tipos específicos de unidade.

## Exemplos

### Porta-aviões convencional

```text
Plataforma atual: Naval/Surface
Vertical Steps: +1
Saída horizontal: exatamente 1 hex
Custo de autonomia: 1
Resultado: Air/Low
Encerra a ação: sim
```

### Helicarrier em Air/High

```text
Plataforma atual: Air/High
Vertical Steps: 0
Saída horizontal: exatamente 1 hex
Custo de autonomia: 1
Resultado: Air/High
Encerra a ação: sim
```

Assim, um caça lançado de uma plataforma em `Air/High` permanece em `Air/High`,
sem ser rebaixado artificialmente para `Air/Low`.

## Consulta proposta

Uma nova entrada do `PodeDecolarSensor` deve receber explicitamente a plataforma
de lançamento. Exemplo conceitual:

```text
PodeDecolar(aeronave, plataforma, slot, célula)
```

O sensor deve:

1. confirmar que a aeronave está realmente embarcada naquela plataforma/slot;
2. ler a regra de lançamento do slot;
3. usar a camada atual da plataforma como origem operacional;
4. calcular a camada final pela escada de camadas;
5. validar que a aeronave suporta a camada final;
6. validar ocupação e locks relevantes da camada de destino;
7. devolver procedimento, degraus, camada final, deslocamento e custo;
8. não alterar `UnitManager`, ocupação, autonomia, FOW ou caches confirmados.

## Execução proposta

O fluxo de desembarque deve guardar ou reconsultar o relatório antes do
compromisso. Depois da confirmação explícita, o executor deve:

1. liberar a aeronave do slot;
2. aplicar as transições visuais indicadas pelo plano;
3. aplicar exatamente o domínio e a altura finais do relatório;
4. deslocar a quantidade de hexes autorizada;
5. descontar o custo informado;
6. marcar a ação conforme a regra;
7. retornar a `CursorState.Neutral` e então atualizar o estado confirmado.

Nenhuma regra de camada deve ser recalculada ou inventada pelo executor.

## Compatibilidade e migração

Ao criar o novo dado, os slots atuais precisam receber valores que preservem o
comportamento existente. Em especial, o hangar do porta-aviões deve começar com
a regra equivalente a `+1 degrau`, `1 hex`, `1 autonomia` e encerramento da ação.

Até todos os consumidores adotarem o novo relatório, não remover os fallbacks
antigos sem verificar:

- jogador humano;
- IA de transporte;
- replay;
- ferramentas de debug;
- serviços que compõem pouso e decolagem;
- save/load de passageiros embarcados.

## Sequência sugerida de implementação

1. Extrair uma consulta pura da escada de camadas para código runtime.
2. Criar a regra serializável de lançamento no slot de transporte.
3. Preencher os slots existentes com migração explícita e validada.
4. Adicionar a consulta de plataforma ao `PodeDecolarSensor`.
5. Fazer `PodeDesembarcarSensor` consumir o relatório de decolagem.
6. Fazer o executor aplicar `endDomain/endHeight`, degraus, hexes e custo do relatório.
7. Remover os bypasses naval e `Air/Low` do desembarque.
8. Validar humano, IA e replay.

## Casos mínimos de teste

- caça `Air/Low` lançado de porta-aviões em `Naval/Surface`;
- caça de altitude nativa `Air/High` lançado do mesmo porta-aviões e terminando em `Air/Low`;
- aeronave sem `Aircraft Carrier Landing` rejeitada;
- destino aéreo ocupado rejeitado;
- cancelamento sem alterar camada, autonomia ou ocupação;
- Helicarrier em `Air/High` lançando caça e mantendo `Air/High`;
- plataforma em `Air/Low` lançando passageiro compatível em `Air/Low`;
- passageiro que não suporta a camada final rejeitado;
- replay reproduzindo a mesma camada e o mesmo custo do jogo original.

