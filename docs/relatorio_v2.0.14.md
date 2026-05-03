# Relatorio de Atualizacao - v2.0.14

## AI Refine - parte 2

Esta versao consolida a segunda rodada de refinamento da IA e fecha ajustes de cena, persistencia e assets ligados ao fluxo tatico atual.

## Em uma frase

A entrega reforca a limpeza de estado transiente apos carregamento e registra a atualizacao do mapa/base visual usada pela iteracao da IA.

## O que isso trouxe na pratica

- O carregamento passa a limpar estado transiente do servico de comandos antes de devolver o turno para `Neutral`.
- A cena `Battle Map` foi atualizada junto com a rodada de refinamento.
- Assets de fonte usados pela interface foram normalizados no pacote desta versao.

## Principais melhorias

1. Persistencia mais limpa
- O `SaveGameManager` agora chama a limpeza de estado transiente de replay/comandos durante o fluxo de carga.
- Isso reduz risco de sobra de comandos, highlights ou dados temporarios depois de recarregar uma partida.

2. Base de mapa atualizada
- A cena principal de mapa recebeu ajustes serializados para acompanhar o estado atual da iteracao.
- A versao passa a registrar essa base como referencia para os proximos testes de IA.

3. Assets visuais revisados
- Materiais e assets SDF de fonte foram atualizados junto com a cena.
- A entrega deixa os recursos textuais alinhados com o estado atual do projeto.

## Bloco tecnico curto

- Ajustado `Assets/Scripts/Save/SaveGameManager.cs` para chamar `ResetCommandServiceReplayTransientState()` antes de `ForceNeutral()` no carregamento.
- Atualizada `Assets/Scenes/Mapas/Battle Map.unity`.
- Atualizados assets SDF em `Assets/TextMesh Pro/Resources/Fonts & Materials/` e `Assets/fonts/VT323/`.

## Resultado

- Versao preparada como continuidade do pacote `AI Refine`, com limpeza de estado reforcada e cena/assets sincronizados.
