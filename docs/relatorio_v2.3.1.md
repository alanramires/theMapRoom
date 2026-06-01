# v2.3.1 - AI Ajustes defensivos

## Contexto

Versao de ajuste comportamental da IA durante defesa, suporte de fogo e preparacao de avancos por rally point. O foco foi remover bloqueios antigos que faziam unidades ficarem paradas por leituras defensivas obsoletas e separar melhor ameaca ativa de pressao residual.

---

## Defesa de Base/HQ

### Problema

A IA tratava uma base como "sob ameaca" mesmo depois de o inimigo que iniciou a captura ter sido destruido. Como a construcao ficava com capture points parciais, o plano mantinha SOS de Base/HQ ativo e redirecionava unidades de assalto para a retaguarda.

### Correcao

- Captura parcial residual nao dispara mais SOS de Base/HQ sozinha.
- Defesa critica agora exige ameaca ativa: inimigo visivel proximo ou captura ativa com inimigo terrestre no raio.
- Construcoes neutras no setor da base nao contam mais como "base propria sob captura".
- `AITacticalAnalyzer` tambem passou a filtrar captura de base residual antes de abrir `BaseDefense URGENTE`.

### Resultado

Unidades de assalto deixam de abandonar objetivos ofensivos por uma captura antiga ja resolvida. A defesa de base continua acionando quando ha inimigo visivel ou ameaca real nas proximidades.

---

## Fire Support e Screen

### Problema

O suporte de fogo podia ficar travado quando a operacao de captura nao tinha `screen minimo` valido. Na pratica, artilharias atribuidas ao setor paravam em vez de tentar reposicionar, embarcar ou se aproximar do rendezvous.

### Correcao

- A falta de screen deixou de ser bloqueio duro.
- O log de screen continua existindo como diagnostico.
- Depois do diagnostico, a unidade segue a cadeia normal: linha de tiro bloqueada, embarque, reposicionamento, postura max-range e rendezvous.

### Resultado

Artilharia nao e mais congelada por uma leitura antiga de task force. Ela ainda evita ser ponta de lanca, mas tenta encontrar uma acao util antes de aguardar.

---

## Rally Points

### Ajustes

- Rally points passaram a ter target slots multiplos.
- A colorizacao e leitura dos setores foram recalibradas para distinguir rally points de ataque a cada HQ.
- Rally assembly passou a influenciar concentracao de assalto e suporte de fogo quando o ponto esta conquistado ou suficientemente segurado.

### Resultado

Os rally points deixam de competir diretamente com o planejamento natural de captura e passam a funcionar como pontos de concentracao para cerco e preparacao de ofensiva.

---

## Forward Observer

### Ajustes

- Capturadores em papel de explorer ganharam preferencia por construcoes marcadas como `Forward Observer Spot`.
- A iniciativa promove unidades capazes de revelar FoW relevante quando outra unidade pretende agir contra alvo oculto.
- O explorer pode ocupar ou se aproximar do spot quando nao consegue chegar no mesmo turno.

### Resultado

Infantaria passa a ajudar a abrir campo de visao para artilharia e unidades de ataque, especialmente antes de avancos contra objetivos cobertos por FoW.

---

## Compras e Intel

### Ajustes

- Shopping ofensivo passou a considerar pressao de infantaria inimiga ja inferida pelo `AIIntelAnalyzer`.
- Bonus de compra favorece suporte de fogo anti-infantaria quando a pressao de infantaria inimiga e relevante.
- A leitura respeita as familias de forca inimiga ja existentes, sem generalizar por contagem visivel bruta.

### Resultado

A IA fica mais propensa a comprar artilharia anti-infantaria em cenarios de acumulacao inimiga, sem depender apenas de defesa emergencial.

---

## Outras Correcoes

- `AIController.Supridor.Shuttle` foi renomeado para `AIController.Logistic.Shuttle`.
- Fire support passou a usar decisao propria de embarque e respeitar o limiar de setores distantes.
- Roteamento de combate ganhou fallback para avaliar outros ataques validos quando o primeiro alvo escolhido e bloqueado pela `AttackDecision`.
- Construcoes fake ganharam controle de visibilidade em runtime via `isVisible`.

---

## Validacao

`dotnet build Assembly-CSharp.csproj --no-restore`

Resultado: build concluido com 0 erros. Permanecem apenas warnings antigos de APIs obsoletas do Unity.
