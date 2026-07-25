# v4.2.7 - Refactor do Save/Load tiles Invalidos 2/3

Esta versão conclui a segunda de três etapas do tratamento de tiles inválidos na persistência do Fog of War.

O problema foi reproduzido, diagnosticado e corrigido na origem. Coordenadas calculadas além da borda do mapa não passam mais a integrar as contribuições confirmadas nem o save.

## Diagnóstico confirmado

O log detalhado identificou a primeira rejeição:

```text
sourceIndex=3 source=Unit:99
channel=geographic cellIndex=29
cell=(2,17,0)
cause=board_tile_missing
insideBounds=False
layers=[]
```

O sufixo numérico do log antigo representava o índice da fonte persistida, não a quantidade de células inválidas.

A célula havia sido produzida pelo alcance de uma unidade próxima à borda, mas:

- estava fora dos limites do Tilemap principal;
- não possuía tile no tabuleiro;
- não existia em nenhuma camada do mesmo Grid.

O `LoadCacheVerify exact=True` anterior comprovou que a coordenada não era corrupção do arquivo: o próprio cálculo normal estava armazenando-a.

## Instrumentação

Falhas de validação de células agora informam:

- índice e identidade da fonte;
- canal geográfico ou sensorial;
- índice e coordenada da célula;
- causa da rejeição;
- presença dentro dos bounds;
- Tilemaps do mesmo Grid que possuem tile na coordenada.

A inspeção de camadas ocorre apenas na falha. O caminho de sucesso não paga esse custo.

## Tilemap como domínio canônico

O Tilemap principal retornado por `ResolveFogBoardTilemap()` passa a ser a fonte da verdade sobre o domínio do tabuleiro.

Uma célula somente pode integrar uma contribuição quando:

```text
cell.z == 0 && boardMap.GetTile(cell) != null
```

A regra canônica é aplicada na fronteira compartilhada de entrada das contribuições:

- unidades;
- construções;
- canal geográfico;
- canal sensorial;
- full refresh;
- atualização incremental;
- restauração do save.

Os algoritmos de alcance ainda podem calcular coordenadas além da borda. Essas coordenadas são descartadas antes de entrar no cache confirmado.

## Compatibilidade

Um save v17 criado antes desta correção e contendo coordenadas externas continua sendo rejeitado com fallback cold.

O cold refresh executado por esta versão:

1. recalcula as fontes;
2. recorta as contribuições ao Tilemap;
3. elimina as células externas;
4. permite que o próximo save seja restaurado pelo fast path.

Nenhum save incompatível é aceito parcialmente.

## Resultado no mapa grande

Depois de carregar, recalcular e salvar novamente:

```text
[FoW][LoadCacheRestore] success=true restored sources=42 geographic=416 sensor=404
```

As 42 fontes correspondem a:

- 25 unidades não embarcadas do slot observador;
- 17 construções.

As seis unidades embarcadas não contribuem individualmente enquanto permanecem dentro dos transportes.

O load restaurado não apresentou:

- cold refresh das 25 unidades;
- `[FoW][Cache] hits=0 misses=25`;
- coleta integral de células por unidade.

O frame crítico caiu de aproximadamente `5908 ms` para `2024 ms`, economia de cerca de `3884 ms` e redução aproximada de 66%.

## Segurança transacional

- O cache continua vinculado ao `PlayerSlotId` observador.
- O restore continua disponível apenas sobre estado confirmado em `Neutral`.
- A validação termina antes da primeira mutação.
- Falhas continuam acionando cold refresh integral.
- Nenhuma contribuição provisória é persistida ou publicada.
- A nova regra apenas restringe contribuições a células reais do tabuleiro.

## Documentação

`docs/arquitetura/fow_canais_visibilidade.md` foi atualizado com:

- diagnóstico de células rejeitadas;
- significado dos campos do log;
- Tilemap principal como domínio canônico;
- aplicação uniforme da regra geográfica e sensorial.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- Save incompatível confirmou fallback seguro.
- Save regenerado confirmou `success=true`.
- As 42 fontes foram restauradas sem cold refresh.

## Próxima etapa

Etapa 3/3:

- validar os estados finais do fast path contra o cold refresh;
- revisar o custo residual do restore;
- reduzir a instrumentação temporária sem perder diagnósticos úteis;
- consolidar o teste de regressão.
