# v4.0.19a - Ajustes para a versão web parte II

Continuação dos preparativos para a **versão web (WebGL)**, focada em **carregamento de save** que não trava no navegador e em **persistência correta de slot/time** — além de um vazamento de ícone de debug no HUD e a nova logo.

## Carregamento de save no WebGL

- No **WebGL** o load rodava o pré-processamento via `Task.Run`, mas o navegador normalmente roda sem workers: a task podia nunca executar e deixar o **indicador de carregamento preso** antes do restore começar.
- Agora, sob `UNITY_WEBGL && !UNITY_EDITOR`, o pré-processamento roda **síncrono** (o `syncfs` inicial já trouxe o arquivo persistente para o MEMFS). Fora do WebGL, o caminho assíncrono com task continua igual.

## Persistência de slot e time (save v9)

- Formato de save subiu para **versão 9**: cada unidade agora guarda seu **`slotIndex`**.
- Os **slots do match são restaurados antes dos spawns**. Como `UnitSpawner`/`ConstructionSpawner` resolvem o slot a partir do `TeamId`, saves de **Yellow/Blue** perdiam o vínculo quando a cena-base ainda estava em Green/Red e viravam objetos sem slot. Agora o slot é restaurado primeiro.
- No restore de unidade, se o save não trouxer `slotIndex` (compatibilidade), ele é resolvido pelo `TeamId` via `GetSlotIndexForTeam`.
- A **geometria da campanha** passou a pertencer ao **slot/lado da cena**, não à cor: slot 1 = lado antes chamado Red, slot 0 = lado Green.
- **Time da IA no restore**: se o time salvo como IA não corresponder mais à configuração, cai para o primeiro time de IA configurado (`TryGetFirstAITeam`), evitando IA "sem dono" ao recarregar.

## HUD — ícone de manutenção

- O **ícone de manutenção** é informação de debug da IA (como o badge de eixo) e estava vazando para o HUD normal. Agora só aparece quando a flag global **"Show AI Unit HUD"** está ligada.

## Assets

- Nova **logo** do jogo ("A Sala de Mapas").

## Validação

- `Assembly-CSharp`: compilação a ser confirmada no Editor.
- A validar: **carregar save no build WebGL** sem travar o indicador; salvar/carregar partidas com times **Yellow/Blue** mantendo slots e vínculos; restore de partida com IA reassumindo o controle; e o ícone de manutenção só visível com o HUD de IA ligado.
