# v4.0.8 - AI Rally Point Principal

Esta versão dá identidade visual própria ao **rally principal** — o ponto de encontro que está no foco da operação de massa. Antes, todos os rallies de uma mesma operação (o principal e os feeders) compartilhavam o mesmo semáforo. Agora o principal pode exibir sprites dedicados, deixando claro no mapa onde a força está realmente se concentrando.

## Sprites novos no HUD

- `ConstructionHudController` ganhou três campos no grupo **"Rally Main"**:
  - `Rally Main Red Sprite`
  - `Rally Main Yellow Sprite`
  - `Rally Main Green Sprite`
- Eles convivem com os semáforos normais (`rallyRed/Yellow/Green`) já existentes, e seguem os mesmos estados de prontidão (vermelho = WaitHold, amarelo = Assembling/Ready, verde = GoGreen).

## Identificação do rally principal

- O rally principal é o que está no **`FocusSector`** da operação — o mesmo critério que o planner já usa em `FindPrimaryRallyObjective` (via `BuildRallyAggregate(aiTeam).FocusSector`).
- Os demais rallies da mesma operação seguem como *feeders* e mantêm o semáforo normal.

## Amarração do controle (AI → ConstructionManager → HUD)

- `AIRallyHudSnapshot` passou a carregar `IsMain`, gravado em `PublishRallyHudState` como `rally.Sector == readiness.FocusSector` nos dois pontos de publicação do estado.
- `TryGetRallyHudState` passou a devolver `out bool isMain`.
- `ConstructionManager.ResolveRallyHudState(out bool isMain)` propaga o flag até `ConstructionHudController.Apply` e `ApplyRallyTrafficLight`, e o cache de "dirty" do HUD passou a considerar a mudança de `isMain` para repintar quando o foco troca de rally.
- `ResolveRallyTrafficSprite` escolhe o sprite Main quando é o principal; senão, o normal.

## Fallback seguro

- A seleção usa cascata `Main → normal → off` (`PickRallySprite`): se algum sprite Main ainda não estiver atribuído no Inspector, o rally principal simplesmente cai no semáforo normal — nada quebra.

## Pendência de Inspector

- Os três campos aparecem no Inspector do `ConstructionHudController` (HUD de construção), no grupo "Rally Main". É preciso **arrastar os sprites** correspondentes para ativar o visual dedicado do principal — assim como já era feito com os semáforos normais.

## Validação

- `Assembly-CSharp.csproj`: 0 erros (apenas warnings de obsoletos Unity já existentes).
- Sem mudança de comportamento da AI: a alteração é puramente de apresentação/HUD; o estado de prontidão e a lógica de operação de rally permanecem idênticos.
