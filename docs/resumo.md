# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-03, **depois** da tag `v7.0.4`. Leia isto
primeiro; ele descreve o estado pós-versão e não pertence à tag.

---

## Primeira coisa a fazer

Implementar a **etapa 1 do Melhor Spotting** em
`docs/implementar_melhor_spotting.md`: levar o snapshot/bake ao
`MelhorVisaoService` e à janela.

O ponto exato é este:

```text
MelhorVisaoService.cs
  FocusCells                         hoje é peso, não requisito
  CollectAlliedCoverage              hoje recalcula os aliados

FogKnowledgeSnapshot
  VisibilityContributorsByCell       já permite retirar a contribuição
                                      da própria unidade sem chamar sensores
```

Preservar dois caminhos:

- com snapshot runtime ou bake: consumir conhecimento já pronto;
- sem snapshot: cálculo estrutural bruto, útil no Scene Edit.

Não cozinhar FOW automaticamente ao pintar, remover ou mover peças. O autor
monta o tabuleiro livremente e aperta `Cozinhar FOW 0` quando quiser fotografar
a rodada.

Depois: criar `MelhorSpottingService` plural e somente então a janela
`Tools > Hotzone > Melhor Spotting`. Nenhum `AIController` deve consumir a
primeira entrega.

---

## Estado

`v7.0.4` tagueada e publicada. Relatório:
`docs/relatorio_v7.0.4.md`.

**Build medido depois da tag:** `0 erros, 264 avisos` — todos pré-existentes
(`CS0618` de API obsoleta da Unity, `UAC1009` de serialização). O relatório dizia
"0 avisos"; corrigido lá, e a causa virou armadilha registrada abaixo.

**A sanidade do `PodeMirar` foi conferida.** Ele ganhou 147 linhas nesta versão,
e alterar regra de sensor validado seria **X**. Não é: a mudança é aditiva e o
caminho de jogo continua no `perceptionSnapshot == null`. Só a janela de Editor
passa snapshot — **nenhum `AIController` passa**. Se algum dia um passar, aí sim
o comportamento em partida muda.

### A descoberta que organiza o próximo trabalho

> **Previsão precisa de duas verdades separadas.**

```text
geometria hipotética     “o que esta unidade enxergaria/atacaria dali?”
conhecimento confirmado  “o que o time sabe antes de comprometer a ação?”
```

Misturar as duas permitiu ao primeiro protótipo do Melhor Combate mover uma
unidade, descobrir um alvo e atirar nele dentro da mesma consulta. Isso viola o
ciclo real: movimento provisório não publica FOW, e uma unidade não ganha um
ataque retroativo contra algo que só descobriria depois de chegar.

O `FogKnowledgeSnapshot` passou a ser a fotografia compartilhada; as posições
virtuais continuam sendo projeções puras.

### O Scene View mudou de função

O autor descreveu o resultado: *“aos poucos vai parecendo com uma partida
jogada offline”.* O Scene View é o tapete onde ele distribui peças e audita uma
unidade selecionada. Os sliders do `UnitManager` já fornecem HP, munição e
autonomia; por isso a hipótese de que Melhor Combate deveria ser runtime-only
foi descartada.

---

## A escada

```text
0. sensores PodeX               ✅ prontos (falta PodeConstruir se o engenheiro nascer)
1. serviços de área (Hotzone)   ✅ prontos
2. consumidores Melhor*         ⚠️ Melhor Combate existe; falta Fusão;
                                   Melhor Spotting está planejado
3. papéis → só POLÍTICA         docs/revisao_papeis.md — 1 linha de 7 levantada
4. variações de papel           vira perfil/trait depois da extração das linhas
```

Consumidores existentes: **Captura, Capitão, Visão, Combate, Desembarque,
Embarque, Estoque, Pouso**, mais `QueroCarona`.

O Melhor Spotting será um consumidor orientado a missão sobre Melhor Visão, não
um sensor novo.

---

## O que ler, e nesta ordem

| # | documento | por quê |
|---|---|---|
| 1 | `docs/manual/01_principios_e_vocabulario.md` | decide onde uma regra pode morar |
| 2 | `docs/relatorio_v7.0.4.md` | explica o fio e os erros corrigidos nesta versão |
| 3 | `docs/implementar_melhor_spotting.md` | ponto de execução atual e critérios de aceite |
| 4 | `docs/implementar_melhorCombate.md` | contrato e limite do MVP já entregue |
| 5 | `docs/revisao_papeis.md` | matriz, traits e correções da taxonomia |
| 6 | `docs/arquitetura/acoes_transacionais.md` | obrigatório antes de ligar ferramenta a runtime |

---

## Onde eu parei

### Melhor Combate — ferramenta pronta, consumidor runtime não

Arquivos centrais:

- `Assets/Scripts/Combat/CombatEvaluationService.cs`;
- `Assets/Scripts/Combat/AttackDecisionResult.cs`;
- `Assets/Scripts/DPQ/PositionDpqResolver.cs`;
- `Assets/Scripts/Match/AI/Services/MelhorCombateService.cs`;
- `Assets/Editor/MelhorCombateWindow.cs`.

O serviço cruza origens, alvos, opção canônica do `PodeMirar`, HP, DPQ, Attack
Decision e preferências da ficha. A janela funciona em Scene Edit e runtime,
separa tiro parado de assalto, mostra LoS/LdT, arma, spotter e rejeições.

Os `AIController` antigos já reutilizam `CombatEvaluationService` pelo wrapper,
mas **não consomem `MelhorCombateService`** e mantêm políticas/escalas próprias.
O batch ainda não carrega `weaponIndex`; alternativas à arma canônica não podem
ser prometidas.

### FOW cozido — infraestrutura pronta

Arquivos centrais:

- `Assets/Scripts/Sensors/FogKnowledgeSnapshotBuilder.cs`;
- `Assets/Scripts/Match/MatchController.cs`;
- `Assets/Editor/MatchControllerEditor.cs`.

Runtime copia o snapshot confirmado. Edit Mode usa o bake manual persistido por
slot no `MatchController`. Melhor Combate e Melhor Captura têm atalho
`Cozinhar FOW 0`.

O snapshot inclui contribuições por célula e por alvo. Ferramentas consumidoras
não devem voltar a chamar `PodeEnxergar`, `PodeDetectar`, `Alguém Me Vê` ou
`Hex Enxergado` para reconstruir a mesma percepção.

### Melhor Captura — resposta tripla pronta, IA não migrada

Arquivos centrais:

- `Assets/Scripts/Match/AI/Services/MelhorCapturaService.cs`;
- `Assets/Editor/MelhorCapturaWindow.cs`.

Com FOW ligado, cada alvo separa:

```text
Arrival          chegada/ocupação
ImmediateAction  fow/ação
Eligibility      captura/reconquista
```

Prédio conhecido e encoberto continua no ranking como captura futura. Prédio
ocupado informa a melhor chegada adjacente, não “inalcançável”. Construção
aliada parcialmente perdida entra como reconquista; só sai quando a captura
está no máximo. Linhas de contribuição mostram quem ilumina o local usando o
snapshot.

O `AIController.Capturer` ainda não consome essa resposta.

### Melhor Spotting — somente plano

`docs/implementar_melhor_spotting.md` define:

```text
unidade + ObjectiveCells + política All/Any/Maximize
    → origens táticas que realmente iluminam a missão
```

Um alvo é um conjunto com um elemento. O contrato já nasce plural para a futura
cobertura de artilharia. “Iluminar o objetivo” é gate; cobertura geral só
ranqueia as origens admissíveis.

### Revisão de papéis — avaliação, não implementação

`docs/revisao_papeis.md` corrigiu dois vereditos próprios:

- `Antiaereo` morre como papel se significa apenas capacidade da arma;
- `TransportadorAereo` morre porque shopping já pode demandar domínio.

Somente a agenda reordenável do router pode virar perfil. Auto-reparo,
desbloqueio de produção e transporte obrigatório continuam invariantes acima
de traits. Não parametrizar antes de extrair as sete linhas.

---

## Pendências abertas

**Melhor Visão ainda não consome o bake.** É o primeiro degrau do próximo
trabalho e pré-requisito do Melhor Spotting.

**`FocusCells` é peso, não obrigação.** Não reutilizar o campo silenciosamente
como se fosse gate; o Spotting precisa de contrato explícito.

**Cobertura de artilharia é plano B.** Primeiro uma unidade e um conjunto de
objetivos. Selecionar vários spotters é cobertura incremental e pertence ao
coordenador.

**A Vigilância da `v7.0.3` continua sem validação registrada no Unity.** Conferir
iniciativa da fragata, `AlliedObserverFilter` em `Submerged` e devolução de
autoridade ao Melhor Visão quando não há tiro legal.

**Dois “para onde revelar” continuam locais:** `Capturer.Explorer` e
`Transportador`. Não migrar antes de o Melhor Spotting ficar auditável.

**`MelhorCapitao` continua sem consumidor.** Falta o tradutor `AICaptainData →
List<MelhorCapitaoAttraction>` e seus predicados.

**`roles[0] == CapturadorAgressivo` continua no `GetCapturePower`.** Só remover
depois da migração das fichas para a chave `Capturador Alternativo` e auditoria;
o modo de falha é silencioso.

**O `Rebel.cs` ainda vaza para outros papéis.**
`FindNearestPlanlessCaptureTarget` é usado por Transporte, Assalto e capturador
rogue.

**Melhor Combate não governa a IA.** Migrar todos os papéis e o HexEvaluator é
um trabalho maior que o MVP da ferramenta.

---

## Regras de trabalho

- **Uma classe por vez.** Compilar e rodar no jogo antes da próxima fase.
- **Avaliar não é executar.** Plano pedido não autoriza implementação.
- **Verificar antes de documentar.** Ler diff e contrato real.
- **Ler `docs/manual/` antes de decidir onde uma regra mora.**
- **Nada provisório publica verdade confirmada.** Movimento hipotético não abre
  FOW nem cria contato utilizável.
- **Tem relatório, tem tag. Não tem relatório, é só commit.**
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Um commit por frente de trabalho**, não um pelo lote. É o que torna uma
  frente revertível sem tocar nas outras.
- **Número de build só entra em relatório se veio de build COM restore.**
  `dotnet build Assembly-CSharp.csproj -v q --nologo` — sem `--no-restore`.
- Fechar o dia: skill `.claude/skills/fechamento-do-dia/SKILL.md`.

### Calibragem do dígito de versão

A `v7.0.4` saiu como **Z** e provavelmente era **Y**: trouxe três serviços
novos, a infraestrutura de snapshot de FOW, um parâmetro novo no `PodeMirar` e
uma ferramenta — 5.635 inserções. Z é *"salvamento de fim de trabalho"*; isto foi
*"pega uma parte e trabalha ela e os filhos dela"*.

A tag publicada **fica como está** — mover referência já publicada custa mais que
o erro. É calibragem para a próxima.

---

## Armadilhas que já custaram tempo

| armadilha | lição |
|---|---|
| **`dotnet build --no-restore` com `Temp/obj` limpo** | a Unity apaga a pasta, o build aborta em `NETSDK1004` e imprime **"0 Warning(s)"** porque nada compilou. Já produziu "0 avisos" falso no relatório da `v7.0.4` e um falso "não compila" na revisão. **Sempre com restore quando for afirmar número** |
| **um commit para o lote inteiro** | `eb47d08` juntou quatro frentes em 27 arquivos: hoje não dá para reverter a mudança do `PodeMirar` sem derrubar Melhor Combate, FOW cozido e Melhor Captura |
| **posição hipotética criando conhecimento** | mover no cálculo não permite detectar e atirar antes do compromisso; reuse o snapshot confirmado |
| **foco tratado como gate** | `FocusCells` hoje só soma pontos; missão obrigatória precisa de admissibilidade explícita |
| **recalcular percepção por candidato** | snapshot/bake já possui conhecimento e contribuições; sensor por origem serve apenas à projeção daquela unidade |
| **mudar inicializador de `EditorWindow`** | campo serializado preserva o valor antigo; default novo exige migração versionada |
| **ocupado = inalcançável** | a unidade pode chegar ao entorno sem poder terminar no hex; reporte chegada e ocupação separadamente |
| **runtime-only por hipótese** | `UnitManager` já tinha sliders suficientes para simular combate no Scene Edit |
| **classificar antes de unificar o órgão** | primeiro extraia a fonte única; depois a matriz descreve quem a consome |
| **skill que se declara** | se renomear a etiqueta quebra, o poder está no lugar errado |
| **cobertura aliada sem filtro** | um observador comum pode satisfazer por engano uma missão especializada |
| **troca de tipo em lista serializada** | Unity preserva a contagem e deixa conteúdo nulo; volta com fantasma |
| **gate inaplicável** | separar “não satisfeito” de “impossível/desconhecido” |
| **otimizar por hipótese** | medir antes; cortar chamadas pode não mover o gargalo |
| **`FindObjectsByType` dentro de laço** | se o chamador já possui o objeto, passe-o |
| **`git add .`** | só usar no passo de churn do fechamento |
| **tag antes do commit final** | obriga a mover referência publicada; tag é a última coisa da versão |

---

## Critério de retomada

O próximo incremento está pronto quando o Melhor Visão consegue receber um
snapshot/bake sem recalcular o time, continua funcionando sem fotografia e não
altera FOW, ocupação, memória ou recursos durante a consulta.
