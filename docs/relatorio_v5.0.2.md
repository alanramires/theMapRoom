# v5.0.2 — Plano de Otimização do Tabuleiro, Melhor X e Jogabilidade 2/8

## Visão geral

Esta versão conclui a segunda parte do plano de otimização: implementar a
representação permanente da geografia física da partida.

O novo `BoardTopologyIndex` transforma o conteúdo imutável da cena em registros
serializáveis e estruturas de consulta direta. Terreno, praia, costa,
vizinhança hexagonal, construções físicas, estruturas e segmentos declarados de
rota deixam de precisar ser redescobertos por cada ferramenta.

Esta etapa prepara a infraestrutura compartilhada. Os consumidores da família
Melhor X permanecem com o comportamento anterior nesta versão para que a
migração das varreduras possa ser isolada e comparada na Parte 3.

## Conteúdo do índice

Cada célula física do tabuleiro registra:

- posição normalizada;
- terreno resolvido pelo `TerrainDatabase`;
- estrutura física selecionada pela prioridade oficial;
- construção física presente;
- até seis vizinhos pertencentes ao tabuleiro;
- assinatura dos tiles de origem;
- indicação de praia e costa;
- possibilidade estrutural de pouso;
- possibilidade estrutural de embarque;
- possibilidade estrutural de desembarque.

As rotas declaradas são armazenadas como arestas normalizadas. A consulta não
depende da direção em que o segmento foi cadastrado e pode retornar todas as
estruturas associadas à aresta.

O índice não contém unidades, ocupação, dono, estoque, FOW, contatos, vagas,
combustível ou qualquer outro estado operacional da partida.

## Construção determinística

O `BoardTopologyIndexBuilder` percorre uma vez os tilemaps compatíveis da mesma
grade, resolve as autoridades físicas e produz uma ordenação estável de células
e arestas.

A precedência preservada é:

1. construção física;
2. estrutura declarada;
3. terreno.

O pouso estrutural respeita a autoridade correspondente a esse contexto. A
presença no índice significa apenas que a célula é candidata; skills,
ocupação, camada, alcance, combustível e vagas continuam sendo verificados
pelos sensores oficiais.

## Fingerprint e versão

O conteúdo recebe:

- identificador estável de mapa;
- `topologyVersion`;
- fingerprint SHA-256;
- assinatura ordenada dos tiles, terrenos, estruturas, construções,
  propriedades físicas e segmentos de rota.

O fingerprint permite detectar cenas alteradas, índices antigos e conteúdo
serializado que não corresponde mais às fontes atuais.

## Hidratação para consultas rápidas

As listas serializadas são hidratadas uma única vez no runtime em:

- dicionário de célula para registro físico;
- dicionário de aresta para estruturas de rota;
- lista de praias;
- lista de células costeiras;
- lista de superfícies potenciais de pouso;
- lista de candidatos de embarque;
- lista de candidatos de desembarque.

As consultas posteriores usam acesso direto e não percorrem novamente
`cellBounds`.

## Ciclo de carregamento e fallback

Um bootstrap acompanha o carregamento de todas as cenas de gameplay, inclusive
as carregadas depois do menu principal.

Quando existe um índice serializado válido, ele é hidratado e comparado uma vez
com as fontes da cena. Quando o componente está ausente, inválido ou
desatualizado, o mapa recebe um fallback construído uma única vez em memória no
load e um diagnóstico solicita a persistência pelo Editor.

O fallback usa `HideFlags.DontSave` e não transforma dados derivados de runtime
em estado de partida.

## Ferramentas de Editor

Foi acrescentado o menu:

```text
Tools > Tabuleiro > Board Topology
├── Rebuild Active Scene
├── Validate Active Scene
└── Rebuild Enabled Build Scenes
```

Também existe um ponto de entrada para execução em lote pela linha de comando.
O inspector apresenta versão, quantidade de células, quantidade de arestas e
uma forma curta do fingerprint.

A reconstrução de todas as cenas habilitadas:

- ignora cenas sem tabuleiro, como menus;
- cria ou atualiza o componente;
- valida o resultado contra as fontes;
- salva somente depois da reconstrução;
- informa falhas de cena ou persistência.

## Validação estrutural

O validador detecta:

- tile sem correspondência no `TerrainDatabase`;
- construção física fora do tabuleiro;
- mais de uma construção física na mesma célula;
- rota com célula fora do tabuleiro;
- salto de rota entre células não adjacentes;
- referência de rota pertencente a outro `StructureDatabase`;
- célula ou segmento duplicado ou inválido;
- versão, mapa ou fingerprint ausentes;
- divergência entre o fingerprint serializado e as fontes atuais.

Mensagens detalhadas de tiles não mapeados possuem limite para evitar inundar o
Console em mapas com erro de configuração.

## Contrato transacional

O índice representa somente a topologia imutável da cena. Ele não observa nem
publica movimento provisório e não participa de revisões confirmadas.

Cancelar movimento, pouso, embarque, desembarque ou qualquer preview não
reconstrói e não invalida o índice. Unidades podem se mover ou ser destruídas,
mas praias, terrenos, estradas e construções físicas permanecem nas mesmas
células.

Os sensores continuam sendo a autoridade final. Consultar rapidamente uma
praia candidata não permite revelar informação pelo FOW nem tornar uma ação
legal antes do compromisso explícito e do retorno a `CursorState.Neutral`.

## Validação técnica

- `Assembly-CSharp.csproj` compilado sem erros com os novos arquivos incluídos;
- `Assembly-CSharp-Editor.csproj` compilado sem erros;
- `git diff --check` aprovado;
- nenhum estado dinâmico ou transacional foi encontrado nos dados do índice;
- os avisos de compilação encontrados já pertenciam ao projeto;
- nenhuma cena foi alterada automaticamente enquanto o Unity estava aberto.

Mapas ainda sem o componente permanecem funcionais pelo fallback de load. A
ferramenta de Editor permite persistir o índice quando as cenas puderem ser
reconstruídas com segurança.

## Próxima etapa

A Parte 3 substituirá as varreduras completas de `MelhorPouso` e
`MelhorEmbarque` pelas listas de candidatos do `BoardTopologyIndex`.

Somente a descoberta estática será substituída. Alcance, ocupação, skills,
vagas, segurança, FOW e legalidade final continuarão sendo calculados no
snapshot atual e validados pelos mesmos sensores.
