# v4.2.6 - Refactor do Save/Load Slots Invalidos 1/3

Esta versão conclui a sexta etapa do refactor de persistência do Fog of War por `PlayerSlotId` e abre a primeira de três etapas para tratar, de forma conservadora, células rejeitadas durante a restauração.

## Restauração rápida do FoW

O load agora tenta restaurar as contribuições persistidas antes de executar o cold refresh.

Quando todas as validações são satisfeitas:

- as contribuições geográficas e sensoriais são reconstruídas por fonte;
- o cache incremental das unidades é recomposto;
- o snapshot confirmado é publicado;
- visibilidade, stealth, contatos, overlay e HUD são atualizados;
- a coleta integral de visão das unidades é evitada.

O resultado é registrado por `[FoW][LoadCacheRestore] success=true`.

## Validação all-or-nothing

Nenhum dado salvo é aplicado parcialmente. Antes de alterar o runtime, o restore verifica:

- estado `Neutral`;
- `PlayerSlotId` observador;
- ausência de apresentação provisória;
- versão do formato do cache;
- hash da configuração;
- identidade e elegibilidade de cada fonte;
- duplicidade de fontes e células;
- checksum de cada contribuição;
- assinatura do estado da fonte;
- células geográficas e sensoriais;
- regras específicas de unidades e construções.

Qualquer divergência preserva o runtime e aciona o cold refresh integral.

## Formato v17

O formato de save foi elevado para v17.

Foram adicionados:

- identificador da versão do cache de contribuições;
- hash da configuração usada para produzi-lo;
- checksum determinístico por fonte.

Esses campos são derivados e permanecem fora do hash autoritativo da partida.

Saves v16 ou anteriores continuam compatíveis, mas usam cold refresh por não possuírem todos os metadados exigidos pelo fast path.

## Construções e unidades

As contribuições continuam separadas por fonte:

- unidades persistem seus canais geográfico e sensorial;
- construções persistem visão geográfica e cobertura sensorial conforme suas regras;
- cada fonte é vinculada ao slot observador e ao seu `InstanceId`.

O restore não consolida dados por `TeamId`, preservando corretamente partidas em que mais de um slot aponta para o mesmo time.

## Fallback seguro

Quando a restauração falha, o log informa:

```text
[FoW][LoadCacheRestore] success=false fallback=cold reason=...
```

Em seguida:

1. o FoW é recalculado a partir do estado confirmado;
2. o cache salvo é comparado com o runtime;
3. `[FoW][LoadCacheVerify]` registra a equivalência ou as divergências.

Esse caminho mantém compatibilidade e impede que cache obsoleto se torne verdade do tabuleiro.

## Caso de regressão: células inválidas

Os testes no mapa grande encontraram saves cujo fast path retorna `invalid_cells`, embora a verificação posterior ao cold refresh resulte em `exact=True`.

Isso indica divergência entre a definição de célula válida usada pela restauração e a aceita pelo cálculo normal do FoW. O fallback funcionou corretamente: nenhuma restauração parcial foi publicada.

O save que reproduz `invalid_cells:3` deve ser preservado como caso de regressão para as próximas etapas.

## Contrato transacional

A restauração somente ocorre depois da reidratação do estado confirmado e do retorno a `Neutral`.

- nenhuma posição provisória alimenta o cache;
- nenhuma contribuição é publicada antes da validação completa;
- falhas não alteram FoW, detecção, memória ou inteligência;
- o cold refresh continua sendo a fonte segura de recuperação.

## Documentação

`docs/arquitetura/fow_canais_visibilidade.md` foi atualizado com:

- formato persistido;
- validações do fast path;
- ordem da restauração;
- reconstrução dos agregados;
- comportamento do fallback;
- exclusão dos campos derivados do hash autoritativo.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- Teste de save antigo confirmou `fallback=cold`.
- Testes com cache persistido confirmaram equivalência posterior `exact=True`.
- O caso `invalid_cells` permaneceu seguro e não publicou estado parcial.

## Próximas etapas

Refactor de células inválidas:

1. instrumentar coordenada, fonte, canal e motivo de rejeição;
2. unificar a definição canônica de célula válida;
3. validar o fast path com o save de regressão e remover instrumentação temporária.
