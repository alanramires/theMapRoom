# v5.1.1-2 — Refinamento: Vigilância Aérea 2/8

## Objetivo

Estabelecer uma política compartilhada e observável para as unidades de
Vigilância Aérea, preservando as autoridades já existentes de reparo,
transporte, movimento e ocupação.

## Ordem da política

A decisão passa a declarar os seguintes estágios:

1. `EmergencyAndRepair`;
2. `TransportOrPlatform`;
3. `ExitObstructedPosition`;
4. `ImproveAirCoverage`;
5. `ConservativeRear`;
6. `Hold`.

Os estágios especializados de transporte terrestre e plataforma aérea serão
preenchidos nas Partes 4 e 6. Nesta etapa, a política reutiliza somente as
operações globais já materializadas pelo runtime.

## Emergência e reparo

Para Radar Móvel e EWACS, a autoridade global de reparo agora é consultada
antes do desbloqueio de uma construção. Assim, uma unidade em emergência não
abandona sua recuperação apenas porque está sobre uma célula produtiva.

A ordem histórica foi preservada para todos os demais papéis.

Não foi criado um segundo sistema de reparo: `TryDecideRepairAction` continua
sendo a única autoridade dessa decisão.

## Retaguarda conservadora

Quando não existe âncora operacional segura, Vigilância Aérea deixou de usar o
fallback herdado de fogo indireto e passou a reutilizar
`TryBuildConservativeRearFollowAction`.

Esse serviço:

- respeita `UnitData > AI Behavior > Play Conservative`;
- acompanha a faixa formada pelos combatentes aliados;
- evita assumir a vanguarda;
- considera ameaça, coesão e custo do caminho;
- permanece parado quando já está bem posicionado;
- não inventa um objetivo sem direção conhecida da frente.

Radar Móvel e EWACS já possuem `Play Conservative` habilitado em suas fichas.

## Diagnóstico

Os logs de Vigilância Aérea agora identificam o estágio que venceu:

```text
[VigilanciaAerea] <unidade> policy=<estágio> <motivo>
```

Isso permite distinguir recuperação, desbloqueio, reposicionamento por
cobertura, acompanhamento da retaguarda e permanência sem inferir a decisão
apenas pelo movimento final.

## Contrato transacional

- A política apenas escolhe e prepara um `PlayerAction`.
- Reparos, movimento, ocupação e transporte continuam materializados por suas
  autoridades existentes.
- Nenhum preview publica FOW, detecção ou caches confirmados.
- Permanecer parado usa o batch normal e não antecipa `HasActed`.

## Validação

- Demais papéis mantêm a ordem anterior entre desbloqueio e reparo.
- Vigilância Aérea prioriza emergência.
- O fallback conservador é compartilhado e respeita a ficha.
- `git diff --check` sem erros.
- Compilação de runtime e editor concluída sem erros.

## Próxima etapa

A Parte 3 especializará o Radar Móvel como unidade estacionária. Ele somente
abandonará uma posição válida quando uma célula Tactical alcançável oferecer
ganho suficiente de cobertura aérea, detecção e segurança.
