# v4.7.2 — Refactor da AI Asssault and Fire Support 2/4

## Objetivo

Executar a segunda parte do refactor de transporte de Assault e FireSupport,
substituindo a escolha adjacente e os limites de distância legados pelas
consultas comuns de necessidade de carona e melhor embarque.

Nesta etapa, a decisão completa é preservada, mas somente o embarque cuja LZ já
coincide com a posição atual do transportador é materializado. A progressão até
a LZ pertence à parte 3/4.

## Quero Carona

O passageiro combatente passa a consultar `QueroCaronaService` antes de procurar
um transportador.

A consulta considera:

- contexto com plano ou rogue/rebelde;
- setor atribuído quando houver;
- envelopes Tactical e Operational;
- emergência;
- alvo avaliado;
- custo de rota;
- motivo da decisão.

Uma resposta negativa encerra a procura sem criar ordem, reserva ou movimento.

## Melhor Embarque

Cada transportador aliado compatível é avaliado por
`MelhorEmbarqueService`, usando as capacidades descritas no `UnitData`.

A decisão preserva:

- passageiro;
- transportador;
- LZ;
- slot;
- tier Tactical ou Operational;
- estado da rota do passageiro;
- custo da rota do passageiro;
- custo da rota do transportador;
- distância do transportador;
- disposição normal ou emergencial;
- nota do serviço;
- ajuste da política consumidora.

Não foram criados branches por domínio ou plataforma. APC, trem, navio,
helicóptero e demais transportadores continuam sendo distinguidos pelos seus
dados e pelas regras dos serviços.

## Política Assault

Assault consome a decisão comum e aplica compatibilidade com o setor atribuído.

Foram preservadas as seguintes prioridades:

- objetivo de rally ativo antes da procura por carona;
- preferência por transportador livre;
- preferência maior por transportador no mesmo setor;
- compatibilidade entre setores relacionados;
- penalidade para transportador comprometido com setor incompatível.

Rogue/rebelde também pode solicitar transporte quando `QueroCarona` indicar
necessidade.

## Política FireSupport

FireSupport consome a mesma decisão comum, mas mantém seus filtros próprios de
segurança:

- destino operacional;
- retaguarda;
- rendezvous conservador;
- presença de suporte aliado;
- possibilidade de desembarque seguro;
- progressão compatível com posicionamento de fogo.

Assim, a escolha de LZ e transportador é comum, enquanto a autorização
operacional continua pertencendo ao papel FireSupport.

## Materialização preservada

Quando a LZ escolhida já é o hex atual do transportador:

1. `PodeEmbarcarSensor` confirma novamente transportador e slot;
2. os caminhos válidos do passageiro são calculados;
3. o batch de embarque existente é construído.

Quando a LZ exige deslocamento do transportador ou do passageiro, a decisão é
registrada, mas nenhuma ação parcial é criada nesta etapa.

## Ajuste adicional do inspector

O inspector de `UnitData` recebeu ajustes paralelos incluídos no mesmo snapshot:

- papel único exposto como `Unit Role`, preservando a serialização em lista;
- classificação de combate exibida junto ao papel;
- campo `Starts With Empty Supplies` exposto na seção de suprimentos;
- remoção do foldout redundante de papéis.

## Arquitetura transacional

- `QueroCaronaService` e `MelhorEmbarqueService` permanecem consultas puras.
- A varredura não altera posição, combustível, munição, HP, FOW ou detecção.
- Nenhum candidato é reservado durante a avaliação.
- Somente o controller constrói o batch já autorizado.
- O compromisso continua no fluxo explícito que retorna a
  `CursorState.Neutral`.

## Arquivos principais

- `Assets/Scripts/Match/AI/Units/Assault/AIController.Assault.cs`
- `Assets/Scripts/Match/AI/Units/Fire Support/AIController.FireSupport.Embark.cs`
- `Assets/Editor/UnitDataEditor.cs`

## Próxima etapa

A parte 3/4 deve:

- materializar progressão até a LZ escolhida;
- distinguir aproximação do passageiro, aproximação do transportador e espera;
- preservar a decisão selecionada durante a construção da ação;
- integrar `Antiaéreo` como especialização de FireSupport;
- integrar `Antiaéreo Combatente` como híbrido preparado para Assault e
  FireSupport antiaéreo;
- manter `Artilheiro Combatente` como híbrido Assault e FireSupport.

## Verificação

- auditoria da integração com `QueroCaronaService`;
- auditoria da integração com `MelhorEmbarqueService`;
- auditoria das políticas Assault e FireSupport;
- auditoria do contrato transacional;
- `dotnet build Assembly-CSharp.csproj --no-restore`;
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`;
- `git diff --check`.
