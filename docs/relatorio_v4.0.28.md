# v4.0.28 - Unity Update and Tutorial design

Esta versao consolida a migracao do projeto para a nova versao da Unity e salva o pacote de design/estrutura dos tutoriais.

## Unity Update

- Projeto atualizado para Unity 6000.5.
- Pacotes e configuracoes de projeto sincronizados com a nova versao do Editor.
- Corrigidos erros de compilacao causados pela remocao de `GetInstanceID()`.
- Usos internos de chave/cache passaram para `GetEntityId().GetHashCode()`.
- Conversoes implicitas de `SceneHandle` foram substituidas por `GetRawData()`.
- Corrigido crash do Editor durante restauracao de cena removendo o snap de Tilemap em `CursorController.OnValidate()`.

## Tutorial design

- Reorganizacao de dados de tutorial para a nova estrutura em `Assets/DB/Tutorial`.
- Inclusao de cenas, prefabs, paineis e controladores dedicados ao fluxo de tutorial.
- Atualizacao de assets de mapa, estruturas, construcoes e terreno usados pelos tutoriais.
- Adicao de material de apoio em `docs/tutorial/cena1.md`.
- Inclusao de asset visual de personagem para apresentacao/tutorial.

## AI e apresentacao

- Cursor automatico da AI passa a preferir trajetos pela nevoa quando a acao da AI esta oculta pelo FoW.
- O cursor continua andando celula por celula; se nao houver caminho oculto, cai para caminho normal sem teleporte.
- Ajustes de fluxo de turno, confirmacao e estados auxiliares foram preservados dentro do comportamento existente.

## Validacao

- `dotnet build Assembly-CSharp.csproj`: 0 erros.
- `dotnet build Assembly-CSharp-Editor.csproj`: 0 erros.
- Permanecem warnings de APIs obsoletas da Unity, principalmente `FindObjectsByType` com `FindObjectsSortMode`, sem bloquear a compilacao.
