# v4.0.36 — Performance e Planejamento Multiplayer

Data: 17/07/2026

## Visão geral

Esta versão reduz um dos maiores travamentos observados em partidas avançadas e consolida a fundação técnica do futuro multiplayer online. O foco foi preservar o contrato transacional do tabuleiro: ações provisórias continuam sem publicar informação definitiva, enquanto FOW, caches, hash e replay trabalham apenas sobre estado comprometido.

## Performance do Fog of War e compras

- A compra de uma unidade não executa mais um `FullVisual` de FOW que apagava os caches e recalculava a visão de todas as unidades do time.
- A nova unidade agenda somente seu delta de visão, publicado depois que o fluxo de compras retorna a `CursorState.Neutral`.
- O mesmo caminho incremental atende compras humanas e compras da IA.
- A atualização incremental mantém FOW, detecção, contatos e apresentação derivados do estado confirmado, sem usar a permanência no menu ou o término do spawn como compromisso implícito.
- Nova telemetria `[FoW][Perf][Incremental]` informa unidade, custo total, custo de coleta e quantidade de células processadas.

### Resultado medido

Em uma partida com mais de 60 unidades em campo:

- antes: aproximadamente **5,3–5,5 segundos** por compra, recalculando 31–32 observadores e registrando zero hits de cache;
- depois: aproximadamente **0,4–0,6 segundo** no pipeline incremental completo;
- coleta de visão da unidade nova: de poucos milissegundos para unidades simples até cerca de 160 ms para unidades navais com cobertura maior;
- ganho percebido: compra volta a responder imediatamente, com redução próxima de **10×** no caso reproduzido.

O diagnóstico também isolou o custo secundário da seleção: o pipeline estava em torno de 62 ms, quase todo concentrado no cálculo e pintura do alcance de movimento. Ele permanece como oportunidade de otimização, mas não era a causa do travamento multissegundo do shopping.

## Fundação para multiplayer assíncrono

Foi introduzido o `MatchStateHasher`, responsável por produzir um SHA-256 canônico do `SaveGameData`:

- listas relevantes são ordenadas por chaves estáveis antes da serialização;
- campos voláteis e estados derivados, como caches de FOW, ficam fora do hash para evitar falsos desyncs;
- o save persiste a representação canônica, registra `state_hash=` no log e armazena o hash no manifest;
- saves antigos continuam compatíveis, com `stateHash` vazio quando o campo não existe.

O round-trip foi validado em jogo em 17/07/2026: `load → hash A`, `save → hash A`, `load → hash A`. A igualdade das três medições confirma idempotência no cenário testado e cria uma ferramenta objetiva para encontrar divergências futuras.

Também foram adicionados comandos de diagnóstico:

- `state hash`: calcula o hash canônico do estado vivo sem gravar um save;
- `state dump`: grava o JSON canônico para comparação campo a campo entre estados.

## Highlights de `ideias_futuras_multiplayer.md`

O planejamento registrado em `docs/ideias_futuras_multiplayer.md` define o multiplayer assíncrono, no estilo PBEM, como primeira entrega. O jogador envia um pacote de turno; o oponente abre o jogo, recebe um resumo e pode assistir ao automata reproduzir as ações sob o próprio FOW.

Pontos centrais do plano:

- o `ReplayManager` já funciona como executor de ações, não apenas como gravador;
- somente ações comprometidas entram no log, evitando transmitir previews, cancelamentos ou informação obtida por tentativa;
- a apresentação por observador e o FOW usado para assistir à IA já resolvem grande parte da experiência do turno remoto;
- o save já acumula `matchHistory` completo, com snapshot inicial e ações por turno; falta principalmente a interface do viewer;
- o caminho competitivo foi auditado como determinístico: RNG de gameplay está confinado ao tutorial e escolhas transmitidas são reproduzidas por `InstanceId`;
- o pacote futuro deve conter log versionado, hash final e save completo como fallback recuperável;
- a primeira versão pode funcionar por exportação manual de arquivo, sem servidor;
- tempo real é tratado como evolução do mesmo protocolo, entregando cada ação comprometida imediatamente;
- partidas ranqueadas exigiriam servidor autoritativo para impedir leitura de unidades ocultas no pacote completo, mas isso não bloqueia uma primeira versão entre jogadores confiáveis.

### Próximos passos seguros

1. Canonicalizar as listas restantes do planner/intel da IA.
2. Criar `export turn` para gerar um pacote `.tmrturn` versionado com ações, hash e save de segurança.
3. Criar `import turn` em dry-run para validar versão, cena e hash sem alterar o tabuleiro.
4. Gerar um resumo fog-honesto do turno recebido.
5. Somente depois aplicar ações estrangeiras pelo automated player.

## Conteúdo e dados

- Atualizações de dados de unidades, construções e mapas acompanham o estado atual das cenas de desenvolvimento e Hot Seat.
- Ajustes de catálogo e apresentação foram preservados no mesmo snapshot de versão.

## Validação

- Build de `Assembly-CSharp.csproj`: **0 erros**.
- Telemetria de compra confirmou o uso de `[FoW][Perf][Incremental]` para somente a unidade criada.
- O refresh completo permanece reservado para inicialização, troca de perspectiva/time e reconstruções legítimas do estado global.
- Hash canônico validado por round-trip de load/save/load.

