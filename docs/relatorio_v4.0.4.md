# v4.0.4 - AI Shopping Pressure e Correções

Esta versão refina a leitura macro-territorial da IA, troca o gate de pivô elite baseado em tamanho de exército por um conceito de **core operacional**, introduz **compromisso persistente de compra elite** e corrige o comportamento ocioso do transportador.

## Macro: pontos de captura como medida de controle

- O controle territorial passa a considerar **pontos de captura** das construções, não apenas a contagem bruta de setores.
- Cada construção dona contribui com seus pontos atuais (`OwnedControlPoints` / `EnemyControlPoints`) e o que falta para virar (`DisputedControlPoints`).
- `OwnedRatio` agora é calculado sobre pontos de captura efetivos, refletindo melhor disputas em andamento do que a posse nominal do setor.
- Setores neutros (sem dono) não entram na razão de controle.

## Shopping Pressure (janela de inspeção)

- A janela `Tools > Utils > Shopping Pressure` exibe a linha de **capture points**: seus / inimigos / em disputa, quando há disputa ativa.
- O log macro (`[AI Macro]`) passa a registrar `pontos=…/…` e `disputa=…` junto da razão de controle e do cap ofensivo.

## Core operacional substitui o piso de tamanho de exército

- O antigo `MinArmySizeForElitePivot` (piso de 12 unidades) foi **aposentado** como critério de liberação elite — o campo permanece apenas como legado de serialização (`HideInInspector`, não usado).
- O novo gate é a **prontidão do core operacional** (`HasOperationalCore`): massa mínima de capturadores **e** assalto **e** ao menos um fogo indireto.
- A maturidade econômica para reserva estratégica passa a derivar da composição do core (`ComputeOperationalCoreMaturity`), não do número cru de unidades.
- `IsOperationalCoreReadyForElite` também exige renda positiva e ausência de demandas urgentes ou de captura ainda prioritárias antes de admitir a compra elite.

## Compromisso persistente de compra elite

- Quando o core está pronto, a IA **assume um compromisso** com uma unidade elite específica e o memoriza em `AIIntelLedger` (`AIElitePurchaseCommitment`: unidade, papel, nível elite, custo-alvo, turno do compromisso).
- O compromisso é revalidado a cada turno (oferta ainda existe, mesmo nível elite, mesmo papel, cadeia elite disponível) e **cancelado** se a oferta ou a cadeia ficar indisponível.
- Enquanto vigente, ele gera/mantém a demanda correspondente e ganha prioridade no carrinho (logo abaixo de demandas urgentes).
- A reserva estratégica é direcionada ao alvo comprometido; o compromisso é **concluído e limpo** assim que a unidade é comprada.
- O estado do compromisso entra no save consolidado, sobrevivendo entre turnos e sessões.

## Opening econômico: prioridade de capturadores no carrinho

- Na fase macro `EarlyExpansion` com setores neutros disponíveis, o carrinho prioriza **repetir capturadores** até atingir a massa de suporte antes de diversificar combate.
- O alvo é limitado pela massa faltante (`MinCapturerMassForSupport`) e pela quantidade realmente demandada.
- O critério de capturadores de expansão entra no ranqueamento do carrinho logo após o compromisso elite, refletindo que renda antecipada vale mais que diversidade nesse estágio.
- O log do carrinho reporta `expansãoCap=atendidos/alvo`.

## Transportador ocioso: pressão e estacionamento

- APC vazio sem passageiro/TOW agora pode **avançar para pressionar inimigos de assalto** próximos (alvo escolhido por preferência de mira, distância, HP e desempate estável), em vez de apenas recuar para pickup/base.
- O avanço nunca termina sobre uma construção produtora aliada.
- Quando não há pressão a fazer, o APC **estaciona fora da produtora** numa célula escolhida por proximidade do alvo de espera, baixa ameaça, bônus de terreno (DPQ) e custo de caminho — em vez de campar o spawn.

## Validação

- `dotnet build Assembly-CSharp.csproj --no-restore`
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`
- Resultado: 0 erros.
- Permanecem apenas avisos preexistentes de APIs Unity obsoletas.
