# v5.0.4 — Plano de Otimização do Tabuleiro, Melhor X e Jogabilidade 4/8

## Visão geral

Esta versão conclui a quarta parte do plano de otimização com a implementação
do `ConfirmedOccupancyIndex`, separado da topologia permanente criada nas
partes anteriores.

O índice representa somente o último snapshot confirmado de unidades. Ele
oferece consultas rápidas por célula, andar operacional e perfil funcional,
sem transformar movimento provisório, animação ou abertura de sensor em
verdade definitiva.

O mesmo marco incorpora correções de jogabilidade encontradas durante as
partidas de validação:

- captura com 50% da força quando falta o prédio pré-requisito;
- início de turno da IA independente do `PanelRodada`;
- botão humano do `PanelRodada` condicionado à prontidão real do tabuleiro;
- redução inicial do trabalho repetido em `MelhorEmbarque`;
- restauração coerente dessas barreiras após load.

## Índice de ocupação confirmada

O novo `ConfirmedOccupancyIndex` mantém registros derivados das unidades
ativas da cena e publica:

- unidades rastreadas;
- unidades presentes fisicamente no tabuleiro;
- unidades por célula;
- unidades por célula e `HeightBand`;
- transportadores;
- passageiros embarcados por transportador;
- fornecedores;
- Hubs;
- Receivers;
- revisão confirmada de ocupação.

Cada registro preserva a unidade, célula, domínio, altura, banda operacional,
slot, estado de embarque, transportador associado e perfil logístico.

As listas são mantidas em ordem determinística pelo identificador da unidade.
Isso evita que a troca de uma varredura global por uma consulta indexada crie
desempates instáveis.

## Atualização incremental

Movimento, alteração de slot, morte, spawn, embarque, desembarque e mudança de
camada notificam uma mudança potencial de ocupação. Essa notificação não
publica imediatamente o novo estado.

O índice marca apenas as unidades afetadas como sujas e reconcilia os registros
quando o fluxo retorna à fronteira confirmada em `CursorState.Neutral`.

Se uma alteração não puder ser representada incrementalmente, o índice agenda
uma reconstrução completa a partir de `UnitManager.AllActive`. A reconstrução
é um mecanismo de bootstrap, load e recuperação; não é a rota normal de cada
consulta.

A revisão só aumenta quando os registros confirmados realmente mudam.

## Contrato transacional

O índice obedece à regra fundamental das ações transacionais:

- deslocamento provisório não altera a ocupação publicada;
- animação não constitui compromisso;
- entrada em sensor ou submenu não constitui compromisso;
- cancelamento não deixa uma posição provisória no cache;
- consultas não reservam célula, camada, plataforma ou vaga;
- a reconciliação acontece somente após compromisso e retorno a `Neutral`;
- load concluído pode solicitar reconstrução explícita do snapshot restaurado.

Enquanto existem mudanças pendentes, `CanServeLiveQueries` retorna falso.
Consumidores voltam temporariamente às consultas históricas, evitando fornecer
uma resposta indexada obsoleta durante uma ação.

## Integração com regras de ocupação

`UnitOccupancyRules` passa a usar o índice para as consultas comuns:

- limite de unidades na célula;
- presença de aliado;
- ocupante de uma célula;
- lista de unidades na célula.

O fallback anterior continua disponível quando:

- o jogo não está em Play Mode;
- não existe índice para o tilemap;
- o índice ainda não foi hidratado;
- há uma mudança provisória ou confirmada aguardando reconciliação.

Nos casos raros de coabitação, em que algum consumidor pode depender da ordem
histórica de `UnitManager.AllActive`, a implementação preserva a rota antiga.
Assim, o acesso direto acelera células vazias ou com um ocupante sem alterar a
escolha histórica em células ambíguas.

## Integração com Melhor Pouso

`MelhorPousoService` consulta diretamente a lista confirmada de
transportadores ao procurar plataformas móveis.

Isso elimina a passagem por todas as unidades ativas apenas para descobrir
quais delas poderiam funcionar como plataforma. Vaga, compatibilidade,
ocupação, domínio, altura e `PodePousarSensor` continuam sendo reavaliados no
momento da decisão.

Se a ocupação confirmada estiver temporariamente indisponível, o serviço usa
`UnitManager.AllActive`, preservando a resposta histórica.

## Captura sem bloqueio rígido

Uma construção inimiga não deixa mais de ser capturável apenas porque o
jogador ainda não capturou seu prédio pré-requisito.

Enquanto o pré-requisito estiver ausente:

- a captura inimiga usa 50% da força base;
- o arredondamento é feito para baixo;
- a força mínima continua sendo 1;
- um soldado com força 10 captura 5 pontos;
- uma unidade com força 5 captura 2 pontos;
- recuperação de construção aliada não recebe penalidade.

Quando o prédio pré-requisito entra no histórico de captura do slot, a força
normal volta a ser usada.

O mesmo cálculo efetivo foi aplicado à execução da captura, à avaliação da IA
e ao indicador visual de ameaça da construção. O manual canônico de captura
foi atualizado.

## PanelRodada e prontidão do tabuleiro

O `PanelRodada` foi separado da autoridade lógica de troca de turno.

`AdvanceTurn` continua responsável por liberar unidades, aplicar economia,
executar upkeep e publicar o novo estado confirmado. Desativar o painel por
comando de debug não interrompe nem antecipa esses efeitos e não impede a IA
de jogar.

Quando a apresentação está habilitada:

1. a cortina cobre a tela antes da mudança de perspectiva;
2. o botão humano começa desativado;
3. o painel observa a prontidão do tabuleiro;
4. filas de início, ações automáticas e estado do cursor precisam terminar;
5. o botão é habilitado somente quando o tabuleiro retorna a `Neutral`.

Durante um turno de IA, a cortina permanece ativa e o botão não é liberado. A
IA aguarda as condições lógicas do tabuleiro, não a apresentação.

O load segue o mesmo contrato: com o painel desativado, a apresentação é
cancelada sem bloquear a restauração; com o painel habilitado, o botão humano
só aparece quando o snapshot restaurado está pronto.

## Primeira redução de custo em MelhorEmbarque

As partidas de validação mostraram que `MelhorEmbarque` repetia a procura por
um encontro alcançável para cada combinação de LZ e passageiro.

Nesta versão, cada mapa de alcance do passageiro é transformado uma vez em um
mapa de custos de encontro, contendo a própria célula e seus vizinhos
topológicos. Consultar uma LZ passa a ser uma busca direta nesse mapa, em vez
de percorrer novamente todas as paradas alcançáveis.

Os milhares de logs individuais de LZ sem passageiro imediato também foram
condensados em um resumo por avaliação.

Ranking, pontuação, tiers, compatibilidade, `QueroCarona` e desempates não
foram alterados. A telemetria posterior comprovou, porém, que as tentativas
Tactical, Operational, Strategic, Pickup e Evac ainda repetem avaliações
completas. Esse achado orienta a próxima otimização compartilhada.

## Save e load

O `ConfirmedOccupancyIndex` não é serializado como uma segunda verdade do
save. O snapshot de unidades continua sendo a autoridade persistida.

Após a restauração:

- as unidades recuperam posição, camada, slot e embarque;
- o índice solicita reconstrução;
- a publicação acontece na fronteira confirmada do load;
- consultas posteriores recebem a mesma ocupação derivada do snapshot salvo.

Objetos runtime do índice usam `HideFlags.DontSave`, evitando contaminar cenas
ou criar uma fonte persistente concorrente.

## Conteúdo incluído no marco

Como este marco foi fechado com `git add .`, ele também registra o estado
atual do prefab base de unidade e da cena de teste
`Hot Seat 1 - Pvp`. Essas alterações de conteúdo pertencem ao snapshot de
trabalho validado junto desta versão.

## Validação técnica

- `Assembly-CSharp.csproj`: compilação concluída com 0 erros;
- `Assembly-CSharp-Editor.csproj`: compilação concluída com 0 erros;
- verificação de whitespace aprovada nos arquivos-fonte alterados;
- avisos de APIs obsoletas e serialização já existentes permanecem;
- índice possui fallback seguro enquanto há alterações pendentes;
- regras finais continuam nos sensores e resolvers oficiais;
- nenhuma consulta do índice compromete uma ação;
- testes em partida identificaram o próximo gargalo de transporte sem
  regressão de compilação.

## Próxima etapa

A Parte 5 atacará a repetição dos mapas de movimento e do planejamento de
transporte observada na telemetria.

O primeiro alvo é compartilhar, por transportador e revisão confirmada:

- alcance do transportador;
- alcance atual e futuro dos passageiros;
- resposta de `QueroCarona`;
- candidatos de LZ;
- opções já classificadas por tier.

Tactical, Operational, Strategic, Pickup e Evac deverão filtrar o mesmo
resultado, em vez de reconstruir centenas de mapas de movimento para a mesma
decisão.
