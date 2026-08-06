# v7.0.4 — A partida começou a caber no Scene View

Fecha o dia 2026-08-03, a partir da `v7.0.3`.

O fio do trabalho apareceu numa observação do autor enquanto as ferramentas
ganhavam regras de percepção:

> *Aos poucos vai parecendo com uma partida jogada offline.*

Foi exatamente o salto desta versão. O Scene View deixou de mostrar apenas
alcances geométricos e começou a cruzar **estado da peça, combate executável,
conhecimento do time, ocupação, captura e fonte da visão**. Não é uma segunda
partida: são consultas puras sobre a mesma autoridade do jogo, usando uma
fotografia perceptiva confirmada.

---

## 1. Melhor Combate — a luta ficou auditável antes de virar política

O plano começou como extração de três peças escondidas nos controladores:

- `Attack Decision` devolvia `bool`, apagando por que aceitou ou recusou;
- o DPQ geral de posição morava dentro do arquivo do Capturer;
- a simulação de HP tinha um resumo privado do `AIController`.

As três saíram como contratos compartilhados: `AttackDecisionResult`,
`PositionDpqResolver` e `CombatEvaluationService`. Os consumidores antigos
continuam chegando pela mesma porta booleana, mas o resultado já preserva
`SimulationUnavailable`, arma canônica, dano, perda, sobrevivência e DPQ.

Sobre eles nasceu o `MelhorCombateService`: recebe uma unidade, origens e
preferências da ficha, cruza `PodeMirar`, simula cada combate e devolve rankings
separados para tiro estacionário e combate após movimento. A nota deixou de ser
um `float` que cada papel pode reescalar e virou `CombatRankKey`, comparada pelo
próprio consumidor.

`Tools > Hotzone > Melhor Combate` tornou isso brincável. A janela usa os
sliders do `UnitManager` para HP, munição e autonomia, mostra arma realmente
escolhida pelo resolver, Attack Decision, dano, sobrevivência, preferência de
alvo, DPQ e rejeições de LoS/LdT. Spotters aparecem em tracejado, inclusive para
combate corpo a corpo, mas a própria unidade atacante não é contada como
contribuição externa.

### A hipótese desmentida

O plano afirmou que a ferramenta precisava ser **runtime-only**, porque em Edit
Mode não haveria HP, munição e autonomia atuais. Estava errado: os sliders já
existiam no `UnitManager`. A medição desmentiu o desenho, e a ferramenta passou a
funcionar justamente no tapete onde o autor monta as peças.

### A correção mais importante

A primeira simulação perceptiva também estava errada. Ela deixava a unidade
hipoteticamente movida detectar inimigos e, no mesmo cálculo, atirar neles. Isso
antecipa o compromisso: na partida real, **mover sem ter visto antes não concede
um ataque retroativo**.

O Melhor Combate passou a receber o conhecimento já confirmado do time. Origens
hipotéticas alteram alcance e geometria do ataque, mas não inventam novos
contatos. Essa separação virou a fundação da frente seguinte.

---

## 2. FOW cozido — uma fotografia, muitos consumidores

Recalcular `PodeDetectar`, `PodeEnxergar` e `Alguém Me Vê` para cada origem e
alvo fazia as ferramentas repetirem a parte mais cara da partida e ainda
facilitava respostas divergentes.

Entraram `FogKnowledgeSnapshot` e `FogKnowledgeSnapshotBuilder`. A fotografia
carrega, por slot:

- visibilidade geográfica atual;
- cobertura de sensores e células conhecidas;
- inimigos efetivamente visíveis;
- contribuições de visão por célula;
- contribuições de detecção por alvo.

No runtime, a ferramenta copia o snapshot confirmado já calculado pelo jogo. No
Scene Edit, o `MatchController` ganhou o comando manual **Cozinhar FOW da Rodada
0 para todos os slots** e persiste o resultado na cena. Melhor Combate e Melhor
Captura possuem atalho para o mesmo comando.

A escolha manual é deliberada. Pintar, remover ou mover uma peça no tabuleiro
não dispara um cozimento global. O autor monta o cenário livremente e fotografa
a rodada zero quando estiver satisfeito; depois disso, a fotografia permanece
estável até o próximo comando.

Essa implementação respeita a fronteira transacional: cozinhar no Edit Mode não
publica FOW runtime, não troca o slot ativo, não pinta tilemap e não registra
inteligência.

---

## 3. Melhor Captura — chegar, agir e ser elegível deixaram de ser sinônimos

O Melhor Captura já sabia ordenar construções. Com FOW, a resposta binária
“captura/não captura” ficou pequena demais.

Cada alvo agora pode explicar até três posições independentes:

```text
chegada/ocupação   alcançável, fora do alcance ou ocupado
fow/ação           visível e liberado, ou encoberto e pedindo spotting
cap/reconquista    elegível para captura ou reconquista
```

Isso preserva um prédio conhecido, vazio e encoberto como o **melhor destino de
captura da próxima rodada**. A IA pode conhecer a geografia do objetivo sem
ganhar o direito de capturá-lo através da névoa.

Ocupação também deixou de ser descrita como “não consegue chegar”. Se o prédio
está ocupado, o serviço informa que está no envelope e encontra a melhor célula
adjacente onde a unidade pode terminar. Se o ocupante inimigo estiver visível,
o resultado informa que ele pode ser engajado.

Construção aliada só é descartada como concluída quando a captura atual atingiu
o máximo. Se estiver parcialmente perdida, permanece na fila como reconquista.
As contribuições do snapshot desenham quem está iluminando o prédio sem chamar
novamente os sensores.

O resultado já possui informação suficiente para um Capturer escolher entre
aproximar, pedir spotting, engajar e capturar — mas o serviço não tomou essa
decisão. Ele entrega as peças; o papel ainda será o organizador.

---

## 4. Ferramentas de visão — o default também pode mentir

`Pode Enxergar` abria restringindo a consulta ao time ativo. Para uma ferramenta
de auditoria isso invertia a regra do terminal burro: inspecionar o que um
inimigo vê é uma pergunta legítima. O default passou a aceitar qualquer time.

Trocar apenas o inicializador não funcionou. `EditorWindow` serializa o valor e
restaurou a escolha antiga em instalações que já tinham aberto a janela. Foi
necessária uma migração versionada de settings, aplicada também ao `Hex
Enxergado`.

O `Hex Enxergado` ainda escondia uma segunda trava: construções continuavam
filtradas pelo time ativo em código, mesmo com o toggle desligado. O mesmo filtro
agora governa unidades e construções.

O achado vale além destas duas janelas: **mudar o valor inicial de campo
serializado não muda o default de quem já usou a ferramenta.**

---

## 5. Papéis secundários — a explosão era de preferências

A revisão de papéis registrou a pergunta que desmonta a taxonomia combinatória:
um futuro Field Medic precisaria de um `UnitRole` novo apenas para dizer que
logística pesa mais que captura?

Capacidade, essência e sensores já existem. O conceito ausente é preferência.
O teste que separa trait de chave ficou explícito: a chave é lida pelo mundo; o
trait é lido pela própria unidade.

A auditoria corrigiu dois erros do próprio documento:

- `Antiaereo` não sobrevive por ser capacidade de arma — esse é precisamente o
  motivo para morrer, pois `WeaponData` já declara os domínios atingíveis;
- `TransportadorAereo` também não precisa sobreviver — a demanda de shopping já
  pode carregar domínio.

Outra correção preserva a arquitetura: só a agenda de ações pode virar dado. Os
gates invariantes do router continuam acima de qualquer preferência. O campo
futuro deve apontar para um perfil compartilhado, não para uma combinação de
enums em cada ficha.

Nenhuma dessas mudanças de papéis foi implementada nesta versão. Foi avaliação
e correção de rumo.

---

## 6. O que não terminou

**O `MelhorCombateService` não possui consumidor runtime.** Os controladores
reusam o novo `CombatEvaluationService`, mas continuam com suas próprias
políticas e escalas. Migrá-los é o projeto maior, deliberadamente fora do MVP.

**O batch de ataque ainda não carrega índice de arma.** Por isso o Melhor
Combate só promete e ranqueia a opção canônica que o executor realmente usaria;
alternativas permanecem fora do contrato.

**O Melhor Captura ainda não governa o `AIController.Capturer`.** A nova resposta
tripla está pronta para ser consumida, mas pedir e coordenar um spotter ainda
não existe.

**O Melhor Spotting foi apenas planejado.** O plano está em
`docs/implementar_melhor_spotting.md`. A primeira etapa é levar o snapshot/bake
ao Melhor Visão; hoje `FocusCells` é peso, não requisito obrigatório.

**O bake é manual e pode ficar obsoleto.** Essa é a política desejada para
editar livremente a cena, mas a ferramenta precisa sempre dizer qual fotografia
está usando.

**A Vigilância da `v7.0.3` continua sem validação registrada no Unity.** Ainda
faltam os testes da iniciativa da fragata, filtro de cobertura aliada submarina
e devolução de autoridade ao Melhor Visão quando não existe tiro legal.

**Não houve suíte de regressão de gameplay.** O código fecha com
`dotnet build Assembly-CSharp.csproj` em **0 erros e 264 avisos** (todos
pré-existentes: `CS0618` de API obsoleta da Unity e `UAC1009` de serialização),
e as ferramentas foram exercitadas visualmente no Scene View, mas a partida
completa não foi percorrida após todo o lote.

> **Correção pós-tag.** Esta linha dizia "0 erros e 0 avisos". A contagem de
> avisos estava errada — foram medidos 264, contra 258 antes do lote. A causa
> provável é a armadilha registrada no `resumo.md`: com `Temp/obj` limpo pela
> Unity, `dotnet build --no-restore` aborta em `NETSDK1004` e imprime
> **"0 Warning(s)"** porque nada chegou a compilar. O número de erros não mudou.

---

## 7. O que este dia ensinou

**Uma ferramenta de previsão precisa de duas verdades, não uma.** Geometria
hipotética responde “o que aconteceria dali”; snapshot confirmado responde “o
que o time sabe agora”. Misturá-las deixa uma unidade descobrir e agir antes de
ter se movido.

**O Scene Edit não é um runtime inferior.** É o laboratório deliberado do
autor, com peças e estados editáveis. Quando o estado necessário já mora no
`UnitManager`, proibir a ferramenta ali reduz auditabilidade sem aumentar
correção.

**Resultado estruturado vem antes da IA.** Melhor Combate e Melhor Captura já
expõem decisões que os controladores ainda fazem em blocos monolíticos. O ganho
imediato não é a IA jogar melhor; é conseguir apontar exatamente onde e por que
ela diverge da partida que o autor jogaria.

