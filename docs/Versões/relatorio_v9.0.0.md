# v9.0.0 — ver, detectar e atirar partem da mesma linha

O major muda por causa de uma regra de sensor: a linha de visão. Ela foi definida
pelo autor em quatro mensagens curtas, e cada uma desmontou uma complicação que
o código, ou eu, tinha posto no caminho:

> *"não tem essa salada não cara... a linha de tiro é a mesma da visão poxa..."*

> *"construções precisam de EV, elas não são fantasmas..."*

> *"na verdade a cidade na montanha pega o EV 2, assim como um soldado pegaria"*

> *"um HQ atrás de uma montanha vê atrás da montanha... a construção não deveria
> funcionar como um soldado?"*

O resto do dia continuou o fio da v8.6.1 e deu nome ao major: **auditável**. A
v8.6.1 mostrou cinco casos de "o configurado não é o que está valendo". Daqui em
diante, cada valor tem de saber dizer de onde veio, e cada save tem de ser
legível sem arqueologia. A direção vem do
[guia de modding](modding/guia%20de%20modding.md) do autor.

---

## Frente 1 — a visão vira uma regra só *(o X)*

### A origem: o terreno decide, para tudo

Existiam duas regras de origem (`OriginEvRule`). Para **observar**, a unidade
herdava sempre o EV do terreno. Para **atirar**, só quando o terreno autorizava
(`shooterInheritsTerrainEv`). A consequência apareceu na ferramenta: o soldado
dentro da mata partia de 1, a linha seguia nivelada e o empate passava. **Quem
estava na mata enxergava por cima da mata.**

Agora há uma regra só
([ObservationLineService.ResolveOriginEv](../Assets/Scripts/Sensors/ObservationLineService.cs)):

```text
terreno empresta EV (toggle + override)?  → parte do emprestado
não empresta                              → parte de 0
aeronave / submerso                       → parte do EV da camada (DPQ para Ar)
```

O alvo não mudou, e o autor confirmou que já estava certo:
- **enxergar** chega ao cume do hex (o EV do terreno dele);
- **detectar** chega à camada da unidade (EWACS e caça: linha alta, terreno
  embaixo continua escuro, porque visão 3 ≠ detecção 7).

### A construção é ocupante, com altura

A `ConstructionData` não tinha EV. A altura de uma construção morava numa lista
de exceções **do terreno** (`constructionVisionOverrides`). Só a Planície tinha
lista, com cinco construções, e todo o resto nascia fantasma: a Fábrica Leve na
planície tinha EV 0.

Agora a construção tem **EV Base** e **Block LoS** (padrão 1 e bloqueia; a flag
fica com 0 e não bloqueia). A regra é a mesma do soldado:

```text
terreno empresta?  sim → EV emprestado     (cidade na montanha = 2)
                   não → EV Base           (cidade na planície ou floresta = 1)
```

E com Block LoS ela **ocupa** o hex: a altura dela **substitui** a do terreno.
A cidade na montanha deixa o hex com 2, não 2,25. Nas palavras do autor, *"afinal
você tem que fazer ajustes na infraestrutura"*. Sem Block LoS, a construção é
marcador e o terreno fica como está: uma flag na floresta não derruba a árvore.

### A construção olha como soldado

Para revelar, a construção pintava o disco inteiro de raio `visao`, **sem
linha**. O HQ de visão 2 revelava o hex atrás da montanha vizinha. Agora ela
traça a mesma reta da unidade, partindo da própria altura. A regra mora num lugar
só, `FogKnowledgeSnapshotBuilder.CollectConstructionVisibleCells`, que antes eram
**três cópias** do disco. Usam essa função:
- o FOW de runtime;
- a visualização;
- a validação do cache do save;
- o bake da rodada 0;
- a ferramenta Pode Enxergar.

A detecção continua só no próprio hex. A consequência, que o autor nomeou: **a
cidade na montanha vira um "spotter falso"**. Ela mantém o chão revelado, mas não
faz o inimigo do hex vizinho aparecer e não entrega alvo à artilharia, porque o
tiro parabólico pede detecção.

### Os cenários que o autor conferiu

| quem olha | alvo | resultado |
|---|---|---|
| soldado na floresta (0) | 2ª floresta | a 1ª bloqueia |
| soldado na montanha A (2) | cidade em B (2) | vê |
| soldado na montanha A (2) | montanha C atrás de B (2,25) | **vê**: em B a linha está a 2,125 |
| soldado na montanha A (2) | montanha D (2,25) | não vê: em C a linha está a 2,167 |
| soldado na planície (0) | montanha atrás da floresta | vê o cume: linha a 1,125 sobre 1 |
| HQ (1) atrás da montanha | hex do outro lado | não vê |

### O erro de processo que vale registrar

Quando o autor perguntou por que o soldado na floresta herdava EV, eu respondi
com **três caminhos**: manter, unificar, ou criar um toggle novo só para
observação. A resposta foi *"não tem essa salada não"*. A regra certa era uma só
e estava na cabeça dele. Antes de abrir alternativas para uma regra de jogo,
**perguntar qual é a regra**; não multiplicar campos para acomodar uma divisão que
ninguém pediu.

### Caches

O hash de configuração do cache de FOW enxerga mudança de mapa, não de regra.
Então `FogSourceCacheFormatVersion` e `FogRoundZeroSlotBake.CurrentFormatVersion`
foram de 1 para 4: fotografia antiga é descartada e recalculada.

E o bake da rodada 0 **não é necessário para jogar**. No Play, ele é só um atalho
de partida ([MatchController.cs:1482](../Assets/Scripts/Match/MatchController.cs)):
com a versão antiga, o jogo descarta o bake e calcula ao vivo. Na campanha nem se
aplica, porque a Batalha nasce vazia. Ele só faz falta às ferramentas no Edit Mode.

---

## Frente 2 — Save Inspector

`Tools ▸ Auditoria ▸ Save Inspector`. Lê o `.tmrsave` pelo **mesmo caminho do
load** (`SaveGameManager.TryReadSaveForAudit`): container, versão do manifesto,
desserialização e migração do FOW. Se um campo não aparece na janela, o jogo
também não o carregaria.

A lista ordena **por slot** e rotula **pelo manifesto**. O nome do arquivo
engana: o slot 1 se chama `Battle Map 1 - Groud` e é um save da cena Campanha. A
primeira versão ordenava por data, e o autor abriu o slot errado achando que era
o 2.

---

## Frente 3 — o perfil em uso aparece

O Inspector do `AIController` ganhou um quadro "Perfil em uso". No Play, ele
mostra a dificuldade, o perfil e o valor **efetivo** de cada capacidade, pelos
mesmos getters que a IA lê. No Edit Mode, mostra o que cada botão e o Play direto
resolveriam. O Base Preset virou "Reserva (sem catálogo)".

**O Play direto passou a resolver o perfil.** Antes ele rodava sem perfil, com os
flags antigos da cena: o "médio" de teste do editor não era o `AIPreset_Medio`.

---

## Frente 4 — o resultado com motivo

O `VictoryReason` chegava ao `QuadranteController` e era descartado. Agora o
progresso grava **como** a partida acabou, pelo nome (QG capturado, exército
eliminado, rendição, estrelas), e o placar da Campanha ganha a linha `MOTIVO`. Uma
vitória por "exército eliminado" na rodada 2 deixa de ser igual a uma jogada.

---

## Frente 5 — o JOGAR soa como o contrato

O JOGAR da Campanha toca o `done.mp3` e espera ele terminar, como o fim do
contrato da Tela de Entrada. Sem a espera, o som seria cortado na troca de cena.

---

## Frente 6 — o que veio entre a v8.6.1 e o major

- **As três dificuldades poupam para elite (decisão do autor).** O que separa o
  Difícil é o **piso**, não o teto: ele compra o básico em cada fábrica e a sobra
  vai para o elite. Fácil e Médio compram pelo plano e podem deixar produtor
  vazio enquanto poupam.
- O `Papeis.md` foi convertido para UTF-8.
- A pasta órfã `CampaignProgress/` (versão antiga) foi apagada com o autor
  confirmando.

---

## Errata da v8.6.1

> **O relatório v8.6.1 diz que o progresso "nunca foi para o disco" e que o
> `RODADAS: 3` "existiu só naquela sessão". Está errado.**

Eu procurei `campaignProgress` com `grep` direto nos `.tmrsave`, e eles são zip
comprimido: o grep não enxerga o texto. O Save Inspector achou no primeiro uso. O
**slot 1** (cena Campanha, 10/09) guarda o registro falso: Q2 no slot 1 em 3
rodadas, Q1 no slot 0 em 2. Carregar esse save traz o placar falso de volta.

O que continua certo: o progresso só vai para o disco **dentro** de um save, e a
pasta `CampaignProgress/` era mesmo órfã.

---

## O que não terminou

- **O save do slot 1** guarda o `RODADAS: 3` falso. Apagar ou não é decisão do
  autor.
- **No mesmo save**, o `capturedBuildingHistory` traz o slot 0 com `teamId: 2` e
  uma Fábrica Leve numa cena (Campanha) sem construção nenhuma. Parece histórico
  de Batalha atravessando para outra cena. Não investigado.
- **EV padrão das construções:** todas pegaram 1 / bloqueia, exceto a flag.
  Docas, Hidrobase, Estação de Trem e Terminal Rodoviário talvez queiram outro
  valor.
- **O `Planicie.asset` ainda guarda a lista antiga em YAML.** O código não lê, e
  a Unity descarta no próximo save do asset.
- **O runtime de FOW ainda tem a regra de inclusão própria** (quem é dono, HQ
  global) em `ApplyFriendlyConstructionVision`. A parte da linha foi unificada; a
  de quem entra, não.
- **Não visto em partida:**
  - a construção revelando com linha;
  - o Play direto com perfil;
  - a linha MOTIVO no painel (pode não caber);
  - o perfil Fácil e o Difícil.
  
  O autor validou os cenários de visão nos mapas que montou.
- **A sobra do Difícil sem alvo elite:** o tooltip diz "unidades do plano", o
  autor diz "o básico". Conferir quando a fase 2 ligar o `poupaPraElite`.
- **Fase 2 dos números do perfil**, o **teste da blitzkrieg** (casos C, E, F) e o
  **0b**: seguem como no resumo.
