# v5.1.1-1 — Refinamento: Vigilância Aérea 1/8

## Objetivo

Concluir a migração semântica do papel operacional anteriormente chamado
`Intel` para `VigilanciaAerea`, deixando explícito que Radar Móvel e EWACS
realizam vigilância do espaço aéreo, revelação de aeronaves e detecção de
alvos furtivos.

Esta etapa não altera a política tática das unidades. Ela prepara nomes,
configurações e pontos de integração para as próximas partes do refactor.

## Alterações

- `UnitRole.Intel` passou a ser `UnitRole.VigilanciaAerea`.
- O valor numérico serializado do papel permanece `6`.
- `TryDecideIntelAction` passou a ser
  `TryDecideAirSurveillanceAction`.
- `IsIntelUnit` passou a ser `IsAirSurveillanceUnit`.
- A pasta e o arquivo do comportamento operacional foram renomeados para
  `Vigilancia Aerea`.
- Router, iniciativa, rally points e ferramenta de retaguarda agora usam o
  novo nome.
- Demanda, reserva, limites e seleção do shopping foram renomeados.
- Presets e gerador de presets agora expõem Vigilância Aérea.
- Logs operacionais passaram de `[Intel]` para `[VigilanciaAerea]`.

## Compatibilidade

O valor `6` do enum foi preservado para manter compatibilidade com fichas,
cenas e saves que serializam o papel numericamente.

Os campos serializados renomeados usam `FormerlySerializedAs`, preservando os
valores existentes de componentes e presets:

- `MinTurnForIntel`;
- `MaxAirIntel`;
- `MaxMobileAirIntel`;
- equivalentes em camelCase do preset.

## Limite semântico

Os sistemas que realmente representam inteligência estratégica não foram
renomeados:

- `AIIntelLedger`;
- `AIIntelReport`;
- `AISectorIntel`;
- Intel de Jogadas e estruturas equivalentes.

Esses nomes continuam corretos porque tratam de conhecimento, memória,
contatos e análise estratégica, não do papel operacional de Radar Móvel e
EWACS.

## Contrato transacional

Esta etapa altera somente identificação, integração e apresentação semântica.
Nenhuma consulta passa a modificar FOW, detecção, ocupação ou caches
confirmados antes do compromisso explícito da ação.

## Validação

- Auditoria sem referências executáveis ao antigo `UnitRole.Intel`.
- Nomes antigos restantes limitados aos atributos de migração
  `FormerlySerializedAs`.
- Metas da pasta e do script preservadas na mudança de caminho.
- Compilação de runtime e editor concluída com zero erros e zero avisos.

## Próxima etapa

A Parte 2 introduzirá uma política compartilhada de Vigilância Aérea com a
seguinte ordem de decisão:

1. emergência e reparo;
2. transporte ou plataforma;
3. saída de posição obstruída;
4. ganho de cobertura aérea;
5. postura conservadora de retaguarda;
6. permanência.

Radar Móvel e EWACS manterão especializações próprias nas etapas posteriores.
