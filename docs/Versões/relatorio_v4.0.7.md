# v4.0.7 - AI Rally Point

Esta versão transforma o Rally Point em um mecanismo real de concentração de força. A AI agora identifica qual rally está mais próximo de atingir o **GoGreen**, recruta fogo indireto livre nas proximidades, diferencia artilharia leve e pesada e converte a carência restante em pressão estratégica de compra. O painel `Tools > Utils > Shopping Pressure` também evoluiu para expor o estado mental completo da AI: rally, composição inimiga, cobertura própria, compromissos elite e fila efetiva do shopping.

## GoGreen visível e explicável

- O cabeçalho do `Shopping Pressure` ganhou o bloco **GO GREEN**.
- Cada rally ativo mostra estado (`WaitHold`, `Assembling`, `Ready` ou `GoGreen`), força atual/meta e requisitos ainda ausentes.
- O texto vem diretamente de `RallyReadinessReason`, a mesma fonte usada pelo planner; não existe uma segunda estimativa apenas para o Editor.
- Ao liberar a invasão, o painel mostra `LIBERADO desde T...`.
- Unidades atribuídas ao assembly deixaram de exibir o badge ambíguo `+`. Agora o destino aparece no mapa: `C+` para Charlie, `H+` para Hotel etc.

## Concentração em um rally-foco

Ter vários Rally Points não significa montar várias forças incompletas. O planner agora escolhe um único rally-foco, favorecendo o assembly em estado mais avançado e usando a prioridade estratégica como desempate.

- Um passe específico roda antes da distribuição genérica de fogo indireto.
- Unidades de fogo indireto sem slot, em até 8 pontos de movimento, são pescadas para o rally-foco.
- A seleção começa pela unidade mais próxima; peso de artilharia desempata distâncias iguais.
- A unidade recebe imediatamente o plano do rally e o badge identificável (`C+`, `H+`...).
- Unidades já comprometidas com captura, defesa ou outro objetivo não são roubadas por esse passe.
- O log registra a decisão como `Rally <setor> pesca FireSupport rogue`, incluindo distância, peso individual e poder já atribuído.

Somente presença física libera GoGreen. Uma unidade recrutada e ainda a caminho preenche o planejamento e deixa de gerar compra redundante, mas só aumenta o readiness quando entra no raio de apoio do rally.

## Artilharia ponderada

O antigo teste binário considerava apenas Astros II, Obus Médio e Artilharia de Campanha como “artilharia real”, descartando completamente o Obus Leve. Agora o assembly usa poder ponderado:

- Obus Leve: `0,5`.
- Astros II: `1,0`.
- Obus Médio: `1,0`.
- Artilharia de Campanha: `1,5`.
- Meta do rally: `3,0`.

O Obus Leve passa a ser útil para formar massa, sem valer o mesmo que uma peça pesada. As vagas de fogo indireto expandem dinamicamente até seis unidades, evitando que três obuses leves ocupem todos os slots e bloqueiem reforços melhores. O score de força do rally também usa esse poder ponderado.

## Pressão estratégica no shopping

Depois de pescar as unidades disponíveis, a carência real de fogo indireto do rally-foco entra no shopping como demanda isolada:

`pri=10 FogoIndireto origem=rally-assembly`

Essa prioridade coloca o gargalo do GoGreen acima de captura, composição e operações comuns, mas mantém emergências defensivas, counter crítico e compromisso elite à frente. Déficits de fogo indireto dos outros rallies são suprimidos enquanto o foco atual estiver incompleto, impedindo a AI de espalhar compras por várias massas de invasão.

A demanda `rally-assembly` não é mesclada com pedidos comuns de fogo indireto. Isso evita elevar acidentalmente a prioridade de artilharia solicitada por captura ou defesa.

## Shopping Pressure como HUD da AI

O painel passou a detalhar a pressão de counter por categoria e por classe inimiga:

- pressão bruta, cobertura própria e saldo descoberto;
- contatos visíveis, memória do ledger e ameaças anônimas de combate;
- unidades próprias que contribuem para cada cobertura;
- resposta comum, pré-requisito elite ou escalada elite solicitada;
- compromisso persistente ativo, com unidade, custo, turno e matchup associado;
- superioridade qualitativa de Assalto e Fogo Indireto, incluindo meta e fila elite.

Os textos agora distinguem **escalada solicitada** de **compromisso persistente**, evitando apresentar uma intenção nova como se já fosse a reserva mantida pelo ledger.

## Counter pressure e elites

A pressão anti-tank e anti-infantaria deixou de repetir counters baratos indefinidamente:

- Compras próprias reduzem o saldo da classe que realmente cobrem.
- Preferência de alvo, elite, custo e poder atual participam da cobertura.
- Cada unidade própria cobre um único matchup dominante por snapshot, evitando dupla contagem.
- Resíduos numéricos exibidos como `0,0` não geram compras.
- O limite de escalada elite usa o saldo agregado da categoria; a pressão não pode mais ser fragmentada entre `Armored`, `Artillery`, `Vehicle` e sinais anônimos para escapar do limiar.
- Quando a categoria escala, filas baratas paralelas são suprimidas e a AI concentra dinheiro no counter elite adequado.
- Um compromisso elite permanece enquanto ainda existe pressão residual na categoria, mesmo que a subclasse dominante mude.
- Cadeia temporariamente indisponível não apaga o compromisso; o planner aguarda ou compra o pré-requisito.
- Cancelamentos agora registram o motivo real.

Quando as pressões terrestres estão cobertas, a AI usa a folga para buscar superioridade qualitativa. As metas de elites são acompanhadas separadamente para Assalto e Fogo Indireto e aparecem no painel.

## Diagnóstico tático

Os logs de Fire Support e Assalto ganharam motivos detalhados para falhas de ataque. O reposicionamento agora informa candidatos geométricos, alcance, reservas, ocupação, filtros do `AttackDecision` e a última razão de rejeição, permitindo distinguir uma decisão tática deliberada de uma ausência de opção válida.

## Validação

- `Assembly-CSharp.csproj`: 0 erros.
- `Assembly-CSharp-Editor.csproj`: 0 erros.
- Permanecem apenas warnings de APIs Unity obsoletas já existentes no projeto.

