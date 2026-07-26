# v4.5.5 — Refactor de Mudança de camada 5/5

## Objetivo

Concluir o refactor de mudança de camada organizando a caixa de ferramentas do
Editor, alinhando nomes de arquivos, classes, menus e sensores sem alterar as
regras de gameplay.

## Operações Aéreas

- `Tools > Operações Aéreas > Pode Decolar`
- `Tools > Operações Aéreas > Pode Pousar`
- `Tools > Operações Aéreas > Pode Mudar de Altitude`
- `Tools > Operações Aéreas > Pode Arremeter`

Cada janela monta apenas o contexto necessário, chama seu sensor autoritativo e
apresenta o relatório retornado.

## Operações Navais

- `Tools > Operações Navais > Pode Emergir`
- `Tools > Operações Navais > Pode Submergir`
- `Tools > Operações Navais > Pode Submergir Rapidamente`

As ferramentas navais seguem o mesmo contrato: nenhuma janela reimplementa
terreno, estrutura, construção, skills, ocupação, exposição ou locks.

## Arquivos e classes

- `PodeDecolarWindow.cs` corresponde a `PodeDecolarWindow`.
- `PodePousarWindow.cs` corresponde a `PodePousarWindow`.
- `PodeMudarAltitudeWindow.cs` corresponde a `PodeMudarAltitudeWindow`.
- `PodeArremeterWindow.cs` corresponde a `PodeArremeterWindow`.
- `PodeEmergirWindow.cs` corresponde a `PodeEmergirWindow`.
- `PodeSubmergirWindow.cs` corresponde a `PodeSubmergirWindow`.
- `PodeSubmergirRapidamenteWindow.cs` corresponde a
  `PodeSubmergirRapidamenteWindow`.

O antigo `PodePousarSensorDebugWindow` foi convertido em `PodePousarWindow`, e a
janela anteriormente usada para a consulta genérica passou a representar
exclusivamente `PodeMudarAltitudeWindow`.

## Preservação dos metadados Unity

- Arquivos existentes foram movidos junto de seus respectivos `.meta`.
- O GUID da antiga ferramenta de pouso foi preservado em `PodePousarWindow`.
- O GUID da antiga ferramenta genérica foi preservado em
  `PodeMudarAltitudeWindow`.
- As novas janelas receberam `.meta` próprios.
- Nenhuma referência Unity foi recriada desnecessariamente.

## Ferramentas finas

- As sete janelas chamam diretamente seus respectivos sensores.
- Não existem consultas diretas a `LayerTransitionRules`,
  `UnitOccupancyRules`, skills ou regras de terreno dentro dessas janelas.
- Textos e menus foram ajustados para refletir a operação realmente avaliada.
- A ferramenta genérica `Pode Mudar de Camada` não foi mantida porque deixou de
  oferecer uma responsabilidade útil após a separação dos sensores.

## Gameplay e arquitetura transacional

- Esta etapa altera somente ferramentas e organização do Editor.
- Nenhuma regra de gameplay, transição runtime ou estado confirmado foi
  modificada.
- As janelas permanecem consultas puras e não confirmam ações.
- O contrato de ações transacionais continua preservado.

## Verificação

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- Auditoria das sete entradas `MenuItem`.
- Auditoria dos nomes de arquivos e classes.
- Comparação dos GUIDs antes e depois dos movimentos.
- `git diff --check`
- Resultado: builds concluídos com 0 erros e diff sem erros de whitespace.
- Implementação final do refactor: `5/5`.
