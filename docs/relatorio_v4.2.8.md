# v4.2.8 - Refactor do Save/Load tiles Invalidos

Esta versão conclui o refactor de tratamento de tiles inválidos na persistência e restauração rápida do Fog of War.

Depois de diagnosticar coordenadas externas e estabelecer o Tilemap principal como domínio canônico, a etapa final adiciona invariantes estruturais antes da publicação do estado restaurado.

## Objetivo

Garantir que um cache individualmente válido também tenha sido reconstruído de forma integral e coerente antes de produzir qualquer efeito observável:

- snapshot de visibilidade;
- detecção e stealth;
- contatos e inteligência;
- overlay;
- HUD;
- evento `OnFogOfWarUpdated`.

## Plano esperado de agregados

Depois de validar formato, configuração, fontes, checksums, assinaturas e células, o restore deriva da fotografia salva:

- quantidade esperada de contribuidores geográficos por célula;
- quantidade esperada de contribuidores sensoriais por célula.

Essa preparação não executa novamente o algoritmo de visão e não altera o runtime.

## Invariantes pós-reconstrução

Depois de reconstruir as estruturas internas, mas antes da primeira publicação, são verificadas:

- quantidade total de fontes;
- presença de cada identidade `FogContributionSourceId`;
- assinatura de estado de cada fonte;
- igualdade exata das células geográficas por fonte;
- igualdade exata das células sensoriais por fonte;
- igualdade dos agregados geográficos;
- igualdade dos agregados sensoriais.

Se qualquer invariável falhar:

```text
[FoW][LoadCacheRestore] success=false fallback=cold reason=rebuild_invariant_mismatch
```

O runtime reconstruído é descartado integralmente e o load continua pelo cold refresh.

## Publicação transacional

A ordem final do fast path é:

1. confirmar estado `Neutral`;
2. validar integralmente o conteúdo persistido;
3. derivar os agregados esperados;
4. reconstruir o runtime;
5. confirmar as invariantes da reconstrução;
6. publicar snapshot, visibilidade, contatos, overlay, HUD e evento.

Nenhuma publicação ocorre entre as etapas 2 e 5.

## Logs consolidados

O caminho de sucesso permanece em uma única linha e agora separa os tipos de fonte:

```text
[FoW][LoadCacheRestore] success=true restored sources=42 units=25 constructions=17 geographic=416 sensor=404
```

O diagnóstico detalhado de coordenada e ocupação de Tilemaps permanece restrito ao caminho de erro `invalid_cells`, sem custo de busca de camadas no load bem-sucedido.

## Teste no mapa grande

Dois loads consecutivos do cache corrigido concluíram com os mesmos valores:

- 42 fontes;
- 25 unidades não embarcadas;
- 17 construções;
- 416 células geográficas;
- 404 células sensoriais.

Nenhum dos testes executou cold refresh das 25 unidades.

### Comparação dos loads

| Medida | Primeiro load | Segundo load | Diferença |
|---|---:|---:|---:|
| Frame do restore | 2069 ms | 2038 ms | -31 ms |
| `ApplyActiveTeam.Total` | 1626 ms | 674 ms | -952 ms |
| Cinco frames principais | 8462 ms | 7062 ms | -1400 ms |

O segundo teste reduziu aproximadamente 16,5% do bloco principal do load. O restore permaneceu estável em cerca de dois segundos, indicando que as invariantes adicionais não introduziram custo relevante.

## Resultado do refactor

- O cache é persistido por `PlayerSlotId`.
- Unidades e construções mantêm contribuições próprias.
- Canais geográfico e sensorial permanecem separados.
- O Tilemap principal define o domínio válido do tabuleiro.
- Coordenadas externas não entram no runtime nem no save.
- Saves incompatíveis continuam usando fallback seguro.
- Saves corrigidos evitam a coleta integral de visão no load.
- O estado reconstruído é validado antes de qualquer publicação.

## Documentação

`docs/arquitetura/fow_canais_visibilidade.md` foi atualizado com:

- agregados esperados;
- invariantes pré-publicação;
- rollback para cold refresh;
- política final de logs.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- Dois loads reais confirmaram `success=true`.
- Fontes e agregados permaneceram idênticos nos dois testes.
- O contrato transacional foi preservado.
