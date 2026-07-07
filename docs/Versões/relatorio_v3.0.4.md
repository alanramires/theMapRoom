# v3.0.4 - AI Bombardeiro

Esta versao ajusta o planejamento de compras da AI para tratar o bombardeiro como peca ofensiva de pressao e invasao, e nao apenas como upgrade tardio depois de uma frota de Apaches. O objetivo e fazer a AI usar aviacao pesada para romper frente, pressionar base e acompanhar planos ofensivos quando ja existe economia e exercito minimo para sustentar a compra.

## Compras aereas

- `bomba_demand` agora considera plano ofensivo/tatico, economia pronta e composicao minima do exercito.
- A regra antiga de proporcao por Apaches continua existindo, mas nao e mais a unica fonte de demanda.
- O log de demanda do bombardeiro mostra os fatores usados: plano ofensivo, economia, exercito, capturadores, assaltos, Apaches e turno.
- O seletor aereo agora diferencia caca B urgente de caca B preventivo.
- Caca B preventivo tem score menor quando nao ha ameaca aerea visivel nem intel aerea forte, permitindo que o bombardeiro vença quando ele e a peca ofensiva pedida.

## Intel de Jogadas

- Corrigido o fallback de classificacao de siglas no `AIIntelAnalyzer`.
- `CA` deixou de ser buscado como substring ampla e passou a ser token exato.
- Isso evita classificar nomes como capturador como ameaca aerea por acidente.
- Compras e pressao aerea vindas do JogadasManager ficam menos ruidosas para o shopping.

## Elite terrestre

- O gate do tanque elite foi afrouxado em ofensiva ativa.
- Quando existe dinheiro para a compra, plano ofensivo/tatico e pelo menos 50% dos capturadores preenchidos, o tanque elite pode ser liberado antes do threshold padrao de 60%.
- A intencao e alinhar tanque elite e bombardeiro como pecas de ruptura: unidades caras que existem para invadir, pressionar e quebrar resistencia.

## Resultado esperado

Em situacoes como T12, com dinheiro suficiente, plano ofensivo em andamento e pouca ameaca aerea real, a AI deve preferir investir em bombardeiro em vez de comprar outro caca apenas por presenca preventiva. Se houver ameaca aerea clara, o caca continua mantendo prioridade.

## Validacao

- `dotnet build Assembly-CSharp.csproj`
- Resultado: 0 erros.
- Avisos: warnings obsoletos de Unity ja existentes no projeto.
