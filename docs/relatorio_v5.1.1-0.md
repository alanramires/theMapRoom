# v5.1.1-0 — Refinamento: Vigilância Aérea 0/8

## Visão geral

Este documento define o plano de refactor do papel atualmente chamado `Intel`.
O nome não representa sua função real: as unidades desse papel não executam
espionagem, análise de contatos ou inteligência estratégica. Elas vigiam o
espaço aéreo, revelam aeronaves e detectam alvos furtivos.

Existem somente duas unidades neste grupo:

- **Radar Móvel**, terrestre;
- **EWACS**, aéreo.

As duas devem operar na retaguarda e seguir uma postura conservadora, mas
possuem necessidades distintas de posicionamento, transporte e recuperação.

## Princípios

- O novo nome deve descrever vigilância do espaço aéreo.
- A inteligência estratégica continuará usando `AIIntelLedger`,
  `AIIntelReport`, `AISectorIntel` e nomes equivalentes.
- `PodeEmbarcar` permanece a autoridade mecânica do embarque terrestre.
- `PodePousar` permanece a autoridade mecânica de pistas e plataformas.
- `QueroCarona` decide necessidade de transporte terrestre.
- A consulta aérea decide necessidade de plataforma ou rebasing.
- Nenhuma ferramenta ou preview altera o estado confirmado do tabuleiro.
- Tactical toma decisões exatas; Operational e Strategic orientam direção.

## Parte 1 — Migração semântica

Renomear o papel operacional:

```csharp
UnitRole.Intel
```

para:

```csharp
UnitRole.VigilanciaAerea
```

O valor numérico serializado permanece `6`, preservando fichas, cenas e saves.

Também serão renomeados:

- `IsIntelUnit` → `IsAirSurveillanceUnit`;
- `TryDecideIntelAction` → `TryDecideAirSurveillanceAction`;
- logs `[Intel]` → `[VigilanciaAerea]`;
- nomes de demanda, limites e reservas de shopping;
- referências de preset e ferramentas;
- pasta e arquivo do comportamento operacional.

Campos serializados renomeados usarão `FormerlySerializedAs`.

## Parte 2 — Política compartilhada

Criar a política comum de Vigilância Aérea:

1. emergência e reparo;
2. transporte ou plataforma;
3. saída de posição obstruída;
4. ganho de cobertura aérea;
5. postura conservadora de retaguarda;
6. permanência.

As unidades continuam agindo cedo para revelar ameaças antes dos caças e
antiaéreos.

## Parte 3 — Radar Móvel estacionário

O Radar Móvel avaliará sua posição e as células Tactical alcançáveis.

A pontuação considera:

- cobertura aérea efetiva;
- detecção de aeronaves furtivas;
- novas células observáveis;
- bloqueios geográficos, inclusive montanhas;
- sobreposição com radares aliados;
- coesão e retaguarda;
- ameaça, isolamento e custo de movimento.

O comportamento `Stationary` exige ganho mínimo para abandonar a posição. Um
radar bem colocado permanece parado; um radar bloqueado por montanha procura uma
posição melhor.

## Parte 4 — Transporte terrestre da Vigilância Aérea

Antes de marchar, o Radar Móvel consulta `QueroCarona` e a política compartilhada
de passageiro.

Ele poderá usar Fragata, Trem de Carga e outros transportadores somente quando
as fichas e o `PodeEmbarcar` autorizarem.

A decisão deve:

- exigir ganho operacional de cobertura;
- escolher uma LZ materializável;
- coordenar passageiro e transportador;
- respeitar reservas;
- evitar abandonar uma posição excelente por ganho pequeno.

## Parte 5 — EWACS e recuperação

O EWACS seguirá esta prioridade:

1. emergência de combustível, HP ou reparo;
2. recuperação em pista ou plataforma compatível;
3. necessidade operacional de plataforma;
4. posição conservadora de vigilância;
5. permanência em órbita.

As regras de emergência já usadas pelos caças permanecem acima da decisão
normal de vigilância. O EWACS nunca deve cair por perseguir cobertura.

## Parte 6 — Plataforma aérea

A consulta hoje chamada `QueroCaronaAerea` será integrada ao runtime e terá sua
semântica esclarecida como necessidade de plataforma aérea ou base móvel.

Ela passará a aceitar:

- `Interceptador`;
- `AtaqueAereo`;
- `VigilanciaAerea`.

`MelhorPouso` e `PodePousar` continuam resolvendo plataforma, slot, classe,
skills, vaga e exclusividade.

O EWACS aceita um porta-aviões quando:

- existe emergência; ou
- a plataforma melhora significativamente a próxima zona de vigilância; ou
- oferece recuperação necessária sem abandonar cobertura crítica.

## Parte 7 — Cobertura e desempenho

Criar uma consulta pura de cobertura de vigilância aérea.

Ela deverá avaliar:

- alcance de visão aérea;
- detecção stealth;
- bloqueios geográficos estáticos;
- células aéreas relevantes;
- sobreposição aliada;
- ganho marginal de cobertura.

A cobertura estrutural poderá usar `BoardTopologyIndex` e cache por mapa,
célula, perfil e versão da topologia. Terreno e montanhas não mudam durante a
partida e podem ser pré-calculados, salvos e restaurados.

Não será permitido reconstruir o FOW completo para cada candidato.

## Parte 8 — Integração e validação

Completar shopping, presets, ferramentas, logs, documentação e testes.

Política de alcance:

- Tactical: avaliação exata das células alcançáveis;
- Operational: cobertura estimada e progressão;
- Strategic: escolha de âncora por distância cúbica;
- movimento: somente um destino alcançável nesta rodada;
- reavaliação: no turno seguinte.

## Matriz de testes

1. Radar atrás de montanha encontra cobertura melhor.
2. Radar bem posicionado permanece parado.
3. Radar não atravessa a linha de frente por ganho pequeno.
4. Radar embarca em Fragata ou Trem somente quando permitido.
5. Passageiro e transportador convergem para a mesma LZ.
6. EWACS crítico procura recuperação antes de vigiar.
7. EWACS normal não pousa sem ganho operacional.
8. EWACS usa porta-aviões quando ele melhora a missão.
9. EWACS embarcado não fornece visão.
10. Dois radares evitam cobertura redundante.
11. Vigilância Aérea age antes dos combatentes.
12. Cancelamento não altera FOW, ocupação ou caches confirmados.

## Contrato transacional

Toda avaliação deste plano é provisória até a confirmação explícita da ação.

- consultas não movem unidades;
- previews não revelam FOW;
- reservas de planejamento não alteram ocupação confirmada;
- embarque e pouso continuam materializados pelos sensores;
- caches só publicam resultados compatíveis com o snapshot confirmado;
- cancelamento restaura integralmente a apresentação temporária.
