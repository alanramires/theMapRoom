# v7.0.2 — A habilidade é uma chave, e o capitão virou dado

Cinco frentes num dia. O fio que amarra três delas é o mesmo, e está escrito
na primeira página do manual do jogo:

> **Uma habilidade não é um poder. É uma chave.**
>
> O nome que aparece na ficha não faz nada sozinho. É um rótulo, e só. Quem dá
> sentido a ele é o resto do mundo. **Cada lugar pendura a mesma etiqueta e
> define, ali, o que ela significa.**

Três regras do projeto estavam do lado errado dessa frase. Todas voltaram.

---

## 1. A elegibilidade de captura sai do sensor (3a)

`IsRebelCapturable` passou a delegar ao `PodeCapturarSensor`. Os nove
chamadores ficaram intactos — só o corpo mudou.

Não tinha nada de rebelde nele. O nome era fóssil de quando `AIController.Rebel`
era controlador paralelo; hoje quem chama é o rogue do capturador, o
`MelhorDesembarque` (4 sítios), o `Courier` (2) e uso interno. O
`Transportador.Naval` chega a guardar o resultado numa variável `rebelTarget`.

O que saiu de dentro:

| predicado | problema |
|---|---|
| `construction.TeamId == unit.TeamId` | **eixo errado** — time em vez de slot. E apagava a reconquista inteira: prédio aliado sob captura era descartado antes de perguntar ao sensor |
| `matchController.CanCaptureConstruction` | meia regra. O sensor aplica essa e as outras, e continua aplicando se mudarem |

Como **quatro papéis** herdavam esse predicado, os quatro eram cegos para
reconquista. É a mesma linha que a v7.0.1 já tinha corrigido no `QueroCarona` —
ela estava viva aqui, contaminando transporte, assalto e desembarque.

### O terceiro portão de hora-de-agir

`PodeCapturarSensor` ganhou `applyEmbarkedGate`, default `true`. Sem ele o 3a
quebraria o transporte: **seis dos nove chamadores passam passageiro
embarcado**, porque a pergunta deles é *"onde eu largo este cara?"* — e projetar
a unidade numa célula já pressupõe que ela desembarcou lá. Com o portão ligado
essa pergunta não tem resposta possível.

Junta-se à névoa e ao `knownConstruction` como filtro que vale na hora de agir e
atrapalha no planejamento.

---

## 2. Melhor Capitão

Quarto `Melhor*`. Responde **uma** coisa — *"é aquele cara ou aquele prédio"* —
e para aí.

Onde se posicionar em relação a ele continua sendo do papel. O contrato está
literal no `Princípio Magnético`:

> *O Magnetismo não escolhe obrigatoriamente um hexágono exato. (…) A posição
> final é escolhida pelo serviço responsável.*

Substitui, quando ligado, **quatro resolvedores hardcoded** que repetiam as
mesmas seis guardas e discordavam entre si: um filtrava papel com `roles[0]`
estrito enquanto o irmão ao lado usava `CanSatisfy`; um media em cúbica
enquanto o outro media em rota — e o comentário do que media em rota explicava
por que ele estava certo: *"EWACS sobre uma ilha ou no mar não pode arrastar o
SAM até uma costa sem saída"*.

### Três decisões que valem mais que o código

**Distância é de rota.** Um capturador a quatro hexes em linha reta atrás de uma
serra está mais longe que um a cinco de estrada. Aeronave recebe hex
automaticamente — a geometria é da unidade, não opção do chamador.

**Não corta por banda.** Capturar exige chegar; acompanhar não. Um capitão a dez
hexes continua sendo a direção certa, e cortar mataria o magnetismo de longo
alcance, que é o que segura a formação atravessando o mapa.

**A resposta pode ser célula.** É o que permite a `RepCell` ser capitão abstrato
sem gambiarra — e, pelo mesmo campo, a fronteira da névoa entrar como referência
quando o `MelhorVisão` existir. **Construção é ponto, névoa é campo**; campo
reduz a ponto, e o serviço nem fica sabendo o que é névoa.

### Capitão embarcado

Decisão do autor: *o seguidor com plano não troca de capitão porque o dele
entrou num veículo — pede carona atrás dele.*

Virou `allowEmbarked`, desligado por padrão. Morto, em reparo e inativo nunca
lideram; **embarcado vai, só não a pé.** O serviço devolve o capitão marcado e
quem o carrega. Mirar o hex atual do transportador (corrida atrás de alvo móvel)
ou o destino da viagem (estável, mas exige saber para onde ele vai) é decisão do
papel, não do serviço.

### O corte de pathfind

A cúbica é **limite inferior** da rota. Então: ordena por cúbica, calcula rota em
ordem, para quando a cúbica do próximo já empata a melhor rota achada.

É corte **exato**, não heurística — o vencedor é o mesmo que sairia calculando as
N rotas. E não é elegância: o comentário do próprio código registra 12-16ms por
pathfind naval e uma decisão que chegou a **71 segundos**.

### A tabela virou asset

`AICaptainData` em `DB/AI/AICaptain.asset`, gerável por menu. Reconfigurar quem o
antiaéreo segue passa a ser arrastar uma linha no inspector.

**O que não virou dado:** "aeronaves detectadas", "construção aliada falida",
"feridos", "capturável" não são papéis, são **predicados** — precisam consultar
sensor, ficha, estoque, detecção. O asset guarda *qual* predicado e em *que
ordem*; o código guarda *como* cada um responde.

**A coluna "com plano" nasce vazia e é derivada:** mesma lista + restringir ao
setor + aceitar embarcado + `RepCell` no fim. Essa derivação consertou duas
linhas erradas do rascunho original — Capturador e Estoque iam direto para a
`RepCell` e deixavam de olhar o próprio setor.

---

## 3. A captura volta a ser chave

`SkillData.canCaptureConstructions` era a **única exceção do projeto**: a skill
declarando o próprio poder. Terreno, construção e estrutura já faziam o
contrário, em cinco listas.

Agora é `ConstructionData.requiredSkillsToCapture`, ao lado das irmãs. Lista
vazia = ninguém captura aquele tipo. Não é o interruptor de *"isto é
capturável"* — esse continua sendo `Is Capturable`; a lista diz **por quem**.

**A checagem mudou de lugar dentro do sensor**, e não por estilo: era antes de
resolver a construção e passou para depois. Quem pergunta pela etiqueta é o
alvo, então a pergunta não existe antes de haver alvo.

Um chamador ficou mais preciso de graça: o `TryFindCapturerOnCell` já tinha a
construção na mão, então pergunta pela chave **daquele prédio** em vez de "é
capturador em abstrato". Uma unidade pode ter a chave do galpão e não a do
bunker.

### A ferramenta que discordava do jogo

`Tools > Sensors > Pode Capturar` tinha um campo "Skill Required" que ela
preenchia varrendo os assets e elegendo **o primeiro com a flag, por ordem de
GUID**. Com duas skills de captura no projeto, o jogo aceitaria as duas e a
janela exigiria uma — reprovando o que o jogo aprova. Removido.

### A falha silenciosa que isso cria

`Is Capturable` ligado + lista vazia = prédio que parece capturável no editor,
parece capturável na cena, e **ninguém captura**. Nada reclama em runtime,
porque "não pode capturar" é resposta legítima.

Dois guardas: o editor do `ConstructionData` acusa em vermelho caso a caso, e
`Tools > AI > Auditar Chaves de Captura` varre o projeto de uma vez.

---

## 4. O terreno para de decidir o desembarque

`TerrainTypeData.allowDisembark` removido. Portão global e cego — *"aqui ninguém
desembarca, ponto"* — redundante desde a v7.0.1, quando a ficha do transportador
passou a declarar os locais válidos de largada.

Com as duas regras vivas, o terreno podia vetar uma largada que a ficha do
transportador autorizava, e nada explicava a contradição.

O `BoardTopologyIndexBuilder` lia o campo no filtro de candidatas **e na
impressão digital do índice**; saiu dos dois. Fingerprint velha deixa de bater,
então o índice reconstrói na próxima abertura.

---

## 5. Replay com timeline compacta

`CompactTimelineFormatVersion = 2`. A timeline deixa de guardar um snapshot de
tabuleiro inteiro por ação: ancora no snapshot de início de turno e reexecuta as
`PlayerAction` intermediárias.

Checkpoint completo pós-ação sobrevive só onde o replay não é determinístico.
Save antigo continua carregando — quem tem snapshot cheio usa o snapshot.

Junto: seek por âncora em corrotina, com o modo rápido restaurado ao fim, e poda
dos snapshots redundantes na hora de salvar.

---

## O que ficou por fazer

**Nenhum consumidor do Melhor Capitão está ligado.** Os quatro resolvedores
antigos continuam mandando, igual ao Melhor Captura quando nasceu. Falta o
tradutor `AICaptainData → List<MelhorCapitaoAttraction>` e a implementação dos
predicados que ainda não existem.

**O `Rebel.cs` continua vazando.** `FindNearestPlanlessCaptureTarget` é chamado
por Transporte (2), Assalto e o rogue do capturador. Ele é a ponte para os
degraus 4 e 5 — matá-lo converte três papéis de uma vez.

**Sobram 7 varreduras de tabuleiro e 6 arquivos com `IsCapturable`** no
`Capturer/`, mais o `QueroCaronaContext`.

**Os três "para onde revelar"** — `Capturer.Explorer`, `Transportador` e
`VigilanciaAerea` — respondem a mesma pergunta com pesos próprios. É o
`MelhorVisão` escrito três vezes, e é o que o caso naval do magnetismo vai
precisar.

---

## A lição do dia, que não é técnica

O `CLAUDE.md` mandava ler o resumo e os contratos de IA, e **não mencionava
`docs/manual/` em lugar nenhum**. Uma sessão nova lia o que ele mandava e
projetava contra um princípio que está na primeira página do manual do jogo.

Foi o que aconteceu: três propostas de arquitetura erradas antes de alguém
mandar ler o manual.

O `CLAUDE.md` agora aponta, e leva junto o teste que faltava:

> **O designer consegue renomear esta skill para qualquer coisa e tudo continua
> funcionando?** Se renomear quebra, o poder está no lugar errado.
