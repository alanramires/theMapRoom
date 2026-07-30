# v4.5.4 — Refactor de Mudança de camada 4/5

## Objetivo

Separar mudanças de altitude das entradas de camada baseadas no hex e formalizar
operações compostas de retorno ao voo e retorno à submersão.

## Pode Mudar de Altitude

- Criado `PodeMudarAltitudeSensor` para `AirLow ↔ AirHigh`.
- O sensor exige uma aeronave em voo e valida suporte à altitude de destino.
- Locks de camada e ocupação da banda aérea continuam prevalecendo.
- Nivelamento em voo não consulta terreno, estrutura, construção ou skills de
  entrada do hex inferior.
- O menu de mudança de camada, os comandos de debug e a ferramenta de altitude
  passaram a consumir o sensor.
- A execução revalida o sensor imediatamente antes de aplicar a altitude.

## TookOffRecently

- `TookOffRecently` bloqueia somente a subida `AirLow → AirHigh`.
- Aeronaves que fazem decolagem curta de estrada ou porta-aviões permanecem em
  `AirLow` durante a janela da flag.
- Decolagens completas que já terminam em `AirHigh` não são rebaixadas nem
  bloqueadas retroativamente.
- Quando a flag expira no fluxo normal de início de turno, a subida volta a ser
  permitida.
- O planejamento de serviço aéreo mantém aeronaves recém-decoladas em `AirLow`;
  o supridor em `AirHigh` desce para atendê-las.

## Pode Arremeter

- Criado `PodeArremeterSensor` para sequências
  `pousar → executar operação → tentar decolar`.
- O sensor recebe o snapshot anterior à operação.
- Aeronaves que já estavam pousadas não arremetem.
- Aeronaves que pousaram para transferência permanecem pousadas.
- Combustível recebido durante o batch não cria autorização retroativa.
- Apenas operações explicitamente autorizadas podem tentar a arremetida.
- A decolagem final continua passando por `PodeDecolarSensor`.
- Suprimento logístico e serviço de comando passaram a consumir o sensor.
- A arremetida acontece no mesmo hex e não adiciona custo de deslocamento.

## Pode Submergir Rapidamente

- Criado `PodeSubmergirRapidamenteSensor` para sequências
  `emergir → executar operação → tentar submergir`.
- Operações de suprimento atuais não autorizam retorno rápido.
- Operações futuras precisam declarar autorização explícita.
- Mesmo autorizada, a tentativa final continua passando por
  `PodeSubmergirSensor`; disparo, dano, detecção, locks e exposição continuam
  prevalecendo.

## Exposição após suprimento naval

- Criada no `UnitManager` a flag runtime `SurfacedForSupplyThisTurn`.
- A flag é ligada quando um submarino emerge para receber suprimentos.
- `PodeSubmergirSensor` mantém a unidade na superfície durante a rodada.
- A flag expira no início normal do próximo turno da equipe.
- O estado foi incluído no save/load e no espelho runtime do Inspector.

## Arquitetura transacional

- Os três sensores são consultas puras.
- Snapshots anteriores à operação determinam se um retorno composto pode ser
  tentado.
- Recursos recebidos posteriormente não reescrevem a autorização original.
- As transições continuam sendo aplicadas somente nos fluxos comprometidos e
  revalidadas antes da mutação.

## Verificação

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- `git diff --check`
- Resultado: builds concluídos com 0 erros e diff sem erros de whitespace.
- Implementação atual do refactor: `4/5`.
