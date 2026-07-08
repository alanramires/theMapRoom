# v4.0.27a - AI Minor Fow Fixes

Correções pontuais na apresentação do Fog of War Total. O foco é eliminar o "aparecimento mágico" de unidades ao cruzar a fronteira revelada sem reabrir a brecha de renderizar unidades não detectadas em terreno já revelado.

## Oclusão pelo overlay durante toda a partida

- A névoa preta (tilemap na sorting layer FogOfWar) passa a assumir a oclusão visual durante toda a partida Total FoW, inclusive no turno humano — antes isso valia apenas no turno da IA.
- Com os renderers de unidade/HUD permanecendo ligados, a unidade surge progressivamente pela própria animação ao atravessar a fronteira revelada, em vez de religar o sprite já na posição final após o refresh de FoW.
- `UsesFogOverlayForWorldOcclusion` deixou de depender de qual time está ativo. A perspectiva apresentada continua decidida por `ShouldUseHumanFogPresentation`; a posse da oclusão, não.
- A névoa permanece na layer FogOfWar durante toda a partida.

## Guarda contra unidade não detectada em terreno revelado

- O overlay só oculta uma unidade onde de fato existe tile de névoa sobre a célula. Em terreno revelado (tile limpo) mas com a unidade ainda não detectada — por exemplo, quando um raio de visão revela o terreno mas não spotta a unidade — o hide individual do sensor volta a comandar.
- Isso corrige o caso de uma unidade invisível aparecendo "flutuando" sobre o QG inimigo revelado.
- O `PodeDetectarSensor` permanece a fonte de verdade da detecção; a cobertura da célula é resolvida pela API oficial `IsCellVisibleInFogPresentation`, sem lógica paralela.

## Implementação

- Decisão de renderer centralizada em `ResolveFogRenderVisibility`: renderiza se o sensor disser visível, ou se o overlay opaco cobre a célula.
- Aplicada de forma consistente no refresh de runtime, na restauração do cache de save e na visibilidade conservadora de carregamento.
- A visibilidade lógica (`fogUnitVisibilityByCache` / `IsUnitVisibleForTeam`) segue valendo para seleção, sensores e regras, sem controlar renderers diretamente.
- O hardcode de `SetSpriteVisible`/`SetHudVisible` permanece como fallback para FoW parcial/transparente e modos sem overlay opaco cobrindo o mundo.

## Conteúdo e assets

- Novo cursor de FoW (`cursor (fow).png`) e atualização dos sprites de cursor.
- Atualização dos tiles de anel (`white ring`, `white ring black`).
- Ajustes no Battle Map 1 - Ground.

## Validação

- `dotnet build Assembly-CSharp.csproj`
- Resultado: 0 erros.
- Permanecem apenas avisos obsoletos já existentes nas APIs Unity.
