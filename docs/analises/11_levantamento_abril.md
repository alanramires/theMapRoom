# Levantamento Abril 2026

## A. Visão Geral do Projeto

### Estado Atual do Projeto (Baseado no Código-Fonte)

O projeto "The Map Room" é um jogo de estratégia militar em tempo real desenvolvido em Unity, com foco em combate tático entre unidades terrestres, aéreas, navais e logística. O código-fonte atual indica um projeto maduro em termos de infraestrutura básica, mas com limitações significativas na inteligência artificial e balanceamento.

#### O Que Está Funcionando de Ponta a Ponta
- **Sistema de Unidades e Movimento**: Unidades se movem pelo mapa hexagonal, respeitando regras de terreno, DPQ (defesa posicional) e custos de movimento. Combate é resolvido via simulador de HP com modificadores de elite, RPS (pedra-papel-tesoura) e habilidades especiais.
- **Captura de Prédios**: Mecânica completa de captura de construções, com pontos de captura, ocupação e controle territorial. Integrado com planejamento de IA.
- **Sistema de IA Básico**: IA controla unidades com perfis comportamentais (Artilheiro, Bazooka, Capturador, etc.), sensores de prioridade (Capture, Attack, Supply, Reposition) e flags de comportamento. Planejamento de missões por setor funciona para captura e escolta.
- **Interface e HUD**: Sistema de UI para unidades, ícones de postura, debug visual e controles de câmera. Suporte a input de jogador humano.
- **Persistência e Configuração**: Assets para perfis de IA, bancos de dados de combate e configurações de batalha. Sistema de snapshots para avaliação de IA.

#### O Que Está Parcialmente Implementado
- **Análise de Postura Estratégica**: Mecanismo de mudança entre Attack/Defend/Invasion existe, mas usa critérios muito simples (apenas proximidade ao HQ e % de construções controladas). Não avalia perdas globais, força relativa ou tendências de avanço.
- **Planejamento de IA**: Funciona para missões básicas de captura, mas faltam análises avançadas como ameaças secundárias, rotas otimizadas ou adaptação dinâmica a mudanças no campo de batalha.
- **Modo Reparo e Supply**: Unidades entram em modo reparo quando danificadas, mas a lógica de supply (reabastecimento) é básica e não prioriza alvos críticos.
- **Fog of War (FoW)**: Implementado parcialmente – unidades revelam visão, mas IA não explora ativamente áreas desconhecidas de forma inteligente.

#### O Que Está Quebrado
- **Balanceamento de Dificuldade**: IA "burra" devido a falta de análise global; continua atacando enquanto perde unidades em outras frentes. Sem memória entre turnos, não aprende com padrões de derrota.
- **Dependências Cruzadas**: Mudanças em flags de IA (ex: `engageNearestEnemies`) podem causar comportamentos inesperados devido a interações não documentadas (ex: conflito entre `captureInterruptBias` e rescan pós-movimento).
- **Performance em Grandes Mapas**: Overhead de planejamento pode ser alto com muitas unidades; não otimizado para cenários de alta densidade.
- **Debug e Testabilidade**: Sistema de logs existe, mas falta ferramentas automatizadas de teste para comportamentos de IA complexos.

#### O Que Ainda É Placeholder ou Stub
- **Análise Global de Estado**: Placeholders para métricas como "perdas totais", "valor estratégico de prédios" ou "velocidade de avanço inimigo" – não implementadas no `EvaluateStance()`.
- **IA Avançada**: Sem aprendizado de máquina ou comportamentos emergentes; tudo é rule-based hardcoded. Perfis de IA são estáticos, sem adaptação baseada em histórico de partidas.
- **Multijogador e Networking**: Código focado em single-player; placeholders para sincronização de estado em multiplayer.
- **Conteúdo e Assets**: Muitos assets de unidades e mapas existem, mas balanceamento de stats e curvas de progresso são placeholders (ex: thresholds de reparo fixos).

#### Estado Geral
O projeto está **jogável** com IA funcional para cenários básicos, mas limitado para experiências profundas. A arquitetura é sólida (Unity + C#), mas a IA precisa de refinamento para parecer "inteligente". Estimativa: 70% completo para um protótipo funcional, 40% para um produto polido.

---

## Visão Geral (Original)
Este documento consolida análises realizadas em abril de 2026 sobre o sistema de IA do jogo "The Map Room". Foco principal: perfis de comportamento de unidades e mecanismo de mudança de postura estratégica.

## 1. Perfis de IA de Unidades (AI Unit Profiles)

### Estrutura Atual
- **Arquivo principal**: `docs/AI Unit Profile.md`
- **Implementação**: `Assets/Scripts/AI/AIUnitProfile.cs` e relacionados
- **Perfis ativos**: Artilheiro, Bazooka, Capturador, Estacionária, Híbrido, Kamikaze, Lutador, Supridor

### Análise de Comportamentos

#### Sensor Priority
A ordem de sensores define prioridades de ação:
- `Capture > Attack > Reposition` = capturador que briga se necessário
- `Attack > Capture > Reposition` = combatente que captura se não tiver inimigo

#### Attack Decision
Critérios para engajar inimigos:
- Min/Max Damage %
- Must Survive
- Target Preference (Primary/Secondary)

#### Behavior Flags
Flags principais:
- `engageNearestEnemies`: permite ataques oportunistas
- `captureInterruptBias`: controla interrupção de captura para atacar
- `holdPositionWhenInRange`: atira parado quando em alcance
- `retreatToHqWhenIdle`: volta ao HQ quando ocioso

### Problemas Identificados

#### Capturador com Bias Passive
- **Comportamento observado**: ataca inimigos "mais fáceis" fora do prédio alvo
- **Causa**: `captureInterruptBias: Passive` permite interrupção se score ? 38000
- **Impacto**: capturador deixa de ser "rush puro", ataca alvos no caminho

#### Falta de Análise de Setor
- **Limitação**: capturador não avalia ameaças secundárias
- **Exemplo**: ignora inimigos em prédios adjacentes ao alvo principal
- **Solução proposta**: lógica adicional de "peso de ameaças secundárias" (não implementada)

## 2. Mecanismo de Mudança de Postura (Stance)

### Estrutura Atual
- **Arquivo**: `Assets/Scripts/AI/Profiles/BeginnerAIProfile.cs`
- **Posturas**: Attack, Defend, Invasion
- **Avaliação**: ocorre a cada turno via `EvaluateStance()`

### Critérios de Mudança

| Postura | Gatilho |
|---------|---------|
| **Invasion** | Controla > X% das construções capturáveis |
| **Defend** | Inimigo visível dentro do raio do HQ |
| **Attack** | Default (fallback) |

### Problemas Identificados

#### Ausência de Análise Global
A IA **não avalia** sinais amplos de derrota:
- Perdas de unidades (% de HP coletivo)
- Destruição de frotas (comparação Our vs Enemy units)
- Expansão territorial (células perdidas/ganhas)
- Velocidade de avanço inimigo
- Valor estratégico (prédios importantes perdidos)

#### Por que Parece "Burra"
- Muda para Defesa **só quando vê inimigo perto do HQ fisicamente**
- Em mapas grandes, continua em Attack enquanto frota é destruída em outra região
- Sem memória entre turnos ou percepção de "estou perdendo rápido"

### Soluções Propostas (Não Implementadas)
Expandir `BattleStanceDatabase` e `BeginnerAIProfile.EvaluateStance()` com:
- `AlliedUnitLossesAbovePercent`: se perdeu > 40% das tropas
- `EnemyArmyValueAboveRatio`: se força inimiga é 2x maior
- `StrategicObjectivesLost`: se perdeu prédios críticos
- `HqUnderActiveThreats`: quantos inimigos vendo o HQ

## 3. Recomendações Gerais

### Para IA de Unidades
- **Capturador**: usar `captureInterruptBias: None` para rush puro
- **Adicionar flags**: `sectorThreatAnalysis` para avaliar ameaças secundárias
- **Testar combinações**: documentar impactos de flags conflitantes

### Para Postura Estratégica
- **Implementar análise global**: adicionar métricas de perda e avanço
- **Memória entre turnos**: rastrear tendências (ex: "inimigo avançando rápido")
- **Configurabilidade**: permitir ajustes via `BattleStanceDatabase`

### Riscos de Mudanças
- **Dependências cruzadas**: mudanças em `AIPlayerController.cs` afetam planejamento e execução
- **Balanceamento**: novas lógicas podem quebrar equilíbrio de dificuldade
- **Performance**: análises globais adicionam overhead computacional

## 4. Próximos Passos
- Priorizar implementação de análise de perdas para postura
- Testar capturador com `bias: None` em cenários reais
- Documentar novos perfis de unidade se criados

---

**Data**: Abril 2026  
**Analista**: GitHub Copilot  
**Status**: Levantamento concluído, recomendações pendentes de implementação