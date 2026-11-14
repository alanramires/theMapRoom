# Changelog

Índice das versões do projeto. Cada versão tem um relatório próprio explicando
**o porquê**, não só o que mudou.

## Como ler a versão — `vX.Y.Z`

| dígito | significa |
|---|---|
| **X** | grande mudança de arquitetura. Jogar uma IA fora e fazer outra, alterar regra de sensor já validada, mexer na velocidade do jogo |
| **Y** | mudança localizada importante. Pegar uma parte — o capturador, por exemplo — e trabalhar nela e nos filhos dela |
| **Z** | salvamento pontual de fim de trabalho |

**Onde vivem os relatórios:** o major **corrente** fica em `docs/`; quando o
major fecha, os relatórios dele são arquivados em `docs/Versões/`.

---

## v10 — Cada mapa, a mesma IA *(em curso)*

A IA é validada lance a lance nas marcas do autor, e cada marca tira uma regra de dentro dela e
a devolve ao mapa: banda em vez de hex, família em vez de rótulo, eixo, ilha e estrada como fatos
da cena. Um mapa novo tem de pedir autoria, não código.

| versão | título | relatório |
|---|---|---|
| v10.0.0 | A IA lance a lance aprende o mapa (o de trás paga, o veículo escolhe o par, a estrada atravessa o QG, núcleo por família e ilha sem ligação) | [relatório](docs/relatorio_v10.0.0.md) |
| v10.0.1 | A ilha sem ponte, porta por porta (counter, elite, operação, AA e vigilância sabem da ilha; alocador pelo Tático; blitz só com rota terrestre; LZ do Chinook pousado) | [relatório](docs/relatorio_v10.0.1.md) |
| v10.1.0 | Você não vai fazer dinheiro não (Disruptor de captura e o capitão = poder de captura combinado; Bombardeiro nega a escolta; demanda de Disruptor; carona que trabalha enquanto espera) | [relatório](docs/relatorio_v10.1.0.md) |
| v10.2.0 | O dinheiro do elite para de vazar, e o jogo vai para outra pessoa (escolta segura o caixa, folga da conscrição, swap pelo Tático, fila de carona; checklist de publicação, texturas Web, volume, estrada inteira na Campanha) | [relatório](docs/relatorio_v10.2.0.md) |
| v10.3.0 | Não precisa empatar, precisa não perder (capturadores por prédio, teto com embarcados, escolta pré-comprada, counter por anel e tolerância, Interceptador em dois ramos, antiaéreo pela presa) | [relatório](docs/relatorio_v10.3.0.md) |
| v10.4.0 | Ler o que o jogo já sabe (Fase 2 volta a ver o inimigo; escada aérea — vanguarda pede caça, cadeia na demanda; Avião Tanque pelo vaivém; AI Step e Shopping Pressure param de refazer o jogo; GetConstructionAtCell sem busca na cena; entrega pela banda Operacional) | [relatório](docs/relatorio_v10.4.0.md) |
| v10.5.0 | Ferido não é carga (EVAC aéreo pela regra do reparo, anel do HQ como último recurso, EVAC conjunto; reparo avaliado antes do Serviço do Comando; Disruptor por faixa estrita; cruz EVAC no HUD; diretrizes "procure a que já existe" na skill de fechamento) | [relatório](docs/relatorio_v10.5.0.md) |
| v10.6.0 | Só aceita o que a etapa seguinte aceitaria (régua do supridor em 4 faixas + prazo Tático; recuperação aérea na célula futura; fusão só onde a fusão aceita; EVAC sem avanço → anel do HQ; reserva de pouso da aeronave ferida só na IA; swap que captura; skill pergunte-me) | [relatório](docs/relatorio_v10.6.0.md) |
| v10.7.0 | A compra precisa cumprir a missão (parede ponderada e resposta híbrida; rally com acesso; caça persegue no Operacional e não nasce para combater infantaria; pré-requisito respeita ilha; envolvidos em rosa) | [relatório](docs/relatorio_v10.7.0.md) |
| v10.12.0 | O Chinook leva 2 do soldado e 7 da AAA (ameaça do EVAC por dano previsto da Matriz de HP; sair da zona rumo à base vence; segundo ferido prefere hospital e o anel é o último recurso; preferência por ramo restaurada; ferido no anel do HQ age por último) | [relatório](docs/relatorio_v10.12.0.md) |
| v10.11.0 | Quem alcança o alvo, e onde o ferido desce (capitão da invasão é quem tem rota, Ruptura na falta de capturador; só Ruptura e Ocupação seguram a marcha; carona da invasão na frente da fila; estagnação congela esperando carona, teto absoluto de 12; EVAC e reparo pela instalação do próprio ramo, anel do HQ filtrado; suprimento infinito por debug) | [relatório](docs/relatorio_v10.11.0.md) |
| v10.10.0 | O jogo congelava 14 segundos por turno da IA, e ninguém tinha medido (ResolveAISlotKey com cache, registro de RoadNetworkManager, matriz do plano por custo em MP: 14 s → 3 s; frota de Chinooks pela fila persistente; acesso do Serviço do Comando é do transportador; recuperação aérea em célula livre; checklist de logs) | [relatório](docs/relatorio_v10.10.0.md) |
| v10.9.1 | A primeira noite da D2 em Play: o GoGreen disparou sozinho (ar com vaga fica na área do rally e faz surtidas; interceptador não inicia terciário; conscrição com teto absoluto e massa elite 0; projeção simétrica; carga sem tarefa vai ao montinho) | [relatório](docs/relatorio_v10.9.1.md) |
| v10.9.0 | A conta, a vaga e o gate falam a mesma língua (matriz de Forças; rally sem artilharia obrigatória: piso 4+2, razão local por perfil, saturação, capitão e ESPERANDO; presença aérea pela área; catálogo de papéis como asset editável) | [relatório](docs/relatorio_v10.9.0.md) |
| v10.8.0 | O rebelde era o rogue (facção sem QG vira três fatos fora da IA; porta por "tem vaga?", não por "sou rebelde?"; compra pela sellingRule; 0 unidades derrota; shopping sem base reativo; Caça A controlador dos céus) | [relatório](docs/relatorio_v10.8.0.md) |

---

## v9 — Auditável *(fechado)*

O que está configurado tem de ser o que está valendo, e dá para provar olhando.
Cada valor sabe dizer de onde veio, cada save se lê sem arqueologia, e as regras
de sensor têm uma fonte só. A direção vem do guia de modding do autor
(`docs/modding/guia de modding.md`): antes de ter mod, o jogo tem de ser legível.

| versão | título | relatório |
|---|---|---|
| v9.0.0 | Ver, detectar e atirar partem da mesma linha | [relatório](docs/Versões/relatorio_v9.0.0.md) |
| v9.1.0 | O save manda na partida | [relatório](docs/Versões/relatorio_v9.1.0.md) |
| v9.2.0 | O jogo aguenta o celular de quem testa (primeiro MVP no ar) | [relatório](docs/Versões/relatorio_v9.2.0.md) |
| v9.2.1 | A IA pergunta antes de decidir (contrato do questionário) | [relatório](docs/Versões/relatorio_v9.2.1.md) |
| v9.2.2 | O primeiro estranho jogou (Configurações, Ação Direta, a névoa com uma memória só) | [relatório](docs/Versões/relatorio_v9.2.2.md) |
| v9.3.0 | A aula vira um quadrante (Academia: tutorial como mundo, regras de fim da aula, tarefas de captura) | [relatório](docs/Versões/relatorio_v9.3.0.md) |
| v9.3.1 | A primeira aula fecha de ponta a ponta (travas só do aluno, atalhos fechados, fala final, volta automática) | [relatório](docs/Versões/relatorio_v9.3.1.md) |
| v9.3.2 | A aula 2 joga contra uma IA que briga (save da aula e do mundo, rebelde caça, peças de roteiro) | [relatório](docs/Versões/relatorio_v9.3.2.md) |
| v9.4.0 | Revisão do Motor (fórmula e DPQ de combate em fonte única, IA só simula o que o sensor oferece, Neutral com dois momentos) | [relatório](docs/Versões/relatorio_v9.4.0.md) |
| v9.5.0 | Uma IA validando a IA (Claude joga pelo Unity CLI; liberar produtora, compra por vaga do plano; fatos de medalha; diagnóstico no save) | [relatório](docs/Versões/relatorio_v9.5.0.md) |
| v9.6.0 | A IA lance a lance (AI Step: o autor marca, o agente lê; grana antes do counter no Médio/Difícil; postura no mesmo ponto do ciclo; log do handoff) | [relatório](docs/Versões/relatorio_v9.6.0.md) |

---

## v8 — Onde o dado mora *(fechado)*

O catálogo diz o que uma coisa **É**; a cena diz **onde ela ESTÁ**. Este major
separa os três andares — global, catálogo e cena — para que um mapa novo nasça
vazio e nenhum tabuleiro herde o layout de outro.

| versão | título | relatório |
|---|---|---|
| v8.0.0 | A ausência precisa de nome próprio | [relatório](docs/Versões/relatorio_v8.0.0.md) |
| v8.0.1 | As seis armas: o vocabulário fecha, o código não começou | [relatório](docs/Versões/relatorio_v8.0.1.md) |
| v8.6.1 | O que está configurado não é o que está valendo | [relatório](docs/Versões/relatorio_v8.6.1.md) |
| v8.6.0 | A etiqueta muda de dono | [relatório](docs/Versões/relatorio_v8.6.0.md) |
| v8.5.2 | A forma tem que casar com o dado | [relatório](docs/Versões/relatorio_v8.5.2.md) |
| v8.5.1 | O que atravessa a cena | [relatório](docs/Versões/relatorio_v8.5.1.md) |
| v8.5.0 | O laço fecha, e o dono deixa de ser uma cor | [relatório](docs/Versões/relatorio_v8.5.0.md) |
| v8.4.1 | O recorte aprende que a peça tem lado | [relatório](docs/Versões/relatorio_v8.4.1.md) |
| v8.4.0 | O catálogo diz o que uma coisa É, e hoje parou de dizer onde ela está | [relatório](docs/Versões/relatorio_v8.4.0.md) |
| v8.3.0 | Três formas erradas até o dado caber, e o primeiro quadrante em 2 ms | [relatório](docs/Versões/relatorio_v8.3.0.md) |
| v8.2.2 | O tronco existe no papel, e a bancada aprendeu a ver o mapa | [relatório](docs/Versões/relatorio_v8.2.2.md) |
| v8.2.1 | O taxi nao estaciona na renda que ele mesmo viabilizou | [relatório](docs/Versões/relatorio_v8.2.1.md) |
| v8.2.0 | O plano dá o endereço; os rogues dividem o resto | [relatório](docs/Versões/relatorio_v8.2.0.md) |
| v8.1.2 | O dado existia; faltava estar publicado no instante em que alguém olha | [relatório](docs/Versões/relatorio_v8.1.2.md) |
| v8.1.1 | Cinco portas em série, e a última é um aperto de mão pela metade | [relatório](docs/Versões/relatorio_v8.1.1.md) |
| v8.1.0 | O comportamento estava certo; a ordem dos `if` é que não estava | [relatório](docs/Versões/relatorio_v8.1.0.md) |

---

## v7 — Terminais burros e desacoplamento da IA *(fechado)*

A arquitetura da IA deixa de ser "cada papel resolve o seu alcance" e passa a ser
uma **escada**: sensores `PodeX` → serviços de área → consumidores `Melhor*` →
papéis → variações de papel. Cada degrau só começa depois de o de baixo estar de
pé.

O impacto no jogo é mínimo por construção: a IA é **consumidora** dos sensores,
não dona deles. O tabuleiro, os `PodeX`, o FoW e o ciclo transacional seguem
intactos.

| versão | título | relatório |
|---|---|---|
| v7.0.0 | Fundação, desacoplamento e generalização do uso dos sensores | [relatório](docs/Versões/relatorio_v7.0.0.md) |
| v7.0.1 | O alvo de captura tem um dono, e o desembarque tem um preço | [relatório](docs/Versões/relatorio_v7.0.1.md) |
| v7.0.2 | A habilidade é uma chave, e o capitão virou dado | [relatório](docs/Versões/relatorio_v7.0.2.md) |
| v7.0.3 | A camada virou parâmetro, e a taxonomia destrancou | [relatório](docs/Versões/relatorio_v7.0.3.md) |
| v7.0.4 | A partida começou a caber no Scene View | [relatório](docs/Versões/relatorio_v7.0.4.md) |
| v7.1.0 | Enxergar e detectar deixaram de ser a mesma pergunta | [relatório](docs/Versões/relatorio_v7.1.0.md) |
| v7.1.1 | Uma pergunta, uma implementação | [relatório](docs/Versões/relatorio_v7.1.1.md) |
| v7.1.2 | A reta virou uma só | [relatório](docs/Versões/relatorio_v7.1.2.md) |
| v7.2.0 | Apagar também é publicar | [relatório](docs/Versões/relatorio_v7.2.0.md) |
| v7.2.1 | A pergunta errada também responde | [relatório](docs/Versões/relatorio_v7.2.1.md) |

10 versões, de `v7.0.0` a `v7.2.1`. Relatórios arquivados em
[`docs/Versões/`](docs/Versões/).

### Documentos normativos criados neste major

| documento | papel |
|---|---|
| [AI Behavior/governanca.md](docs/AI%20Behavior/governanca.md) | **norma** acima de todos os papéis: ordens, ciclo, ações, sensores de sistema, visão |
| [AI Behavior/governanca_entre_papeis.md](docs/AI%20Behavior/governanca_entre_papeis.md) | as arestas: os três tipos de governo e o Comportamento Magnético |
| [refactor/plano_de_trabalho.md](docs/refactor/plano_de_trabalho.md) | a escada e a fila das pendências, ordenada por dependência |

---

## v6 — Envelope de alcance e contratos de IA *(fechado)*

A Hotzone deixou de ser ferramenta de inspeção e virou **serviço**: uma única
fonte de alcance, consultada por intenção e banda. A doutrina de cada papel
passou a existir por escrito.

14 versões, de `v6.0.0` a `v6.1.5`. Relatórios arquivados em
[`docs/Versões/`](docs/Versões/).

### Documentos normativos criados neste major

| documento | papel |
|---|---|
| [contrato_envelope_alcance.md](docs/AI%20Behavior/contrato_envelope_alcance.md) | **norma** das bandas de alcance |
| [AI Behavior/Capturador.md](docs/AI%20Behavior/Capturador.md) | doutrina do capturador |
| [AI Behavior/Assalto.md](docs/AI%20Behavior/Assalto.md) | doutrina do assalto e da marinha |
| [AI Behavior/FireSupport.md](docs/AI%20Behavior/FireSupport.md) | doutrina do fogo indireto |
| [AI Behavior/Transporte.md](docs/AI%20Behavior/Transporte.md) | doutrina do transporte |
| [hotzone e bandas de alcance.md](docs/AI%20Behavior/hotzone%20e%20bandas%20de%20alcance.md) | apresentação do conceito (não-normativo) |
| [refactor/ai_sem_plano.md](docs/refactor/ai_sem_plano.md) | plano do refactor de unidade sem plano |
| [implementar_logistica.md](docs/implementar_logistica.md) | investigação parqueada da fome da artilharia |

---

## Majors anteriores

Relatórios em [`docs/Versões/`](docs/Versões/) — 311 arquivos.

| major | versões | tema |
|---|---|---|
| v5 | 26 | — |
| v4 | 137 | o maior ciclo do projeto |
| v3 | 11 | — |
| v2 | 47 | — |
| v1 | 90 | primeiro ciclo, do zero ao jogo |

---

## Como gerar a lista de uma versão

Com commits pequenos, o changelog de uma versão se escreve sozinho:

```bash
git log --oneline v6.1.4..v6.1.5
```

Um commit grande devolve uma linha inútil; doze commits pequenos devolvem doze
linhas prontas para o relatório.
