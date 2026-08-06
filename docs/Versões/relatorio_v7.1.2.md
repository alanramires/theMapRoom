# v7.1.2 — A reta virou uma só

Fecha a terceira sessão do dia 2026-08-04, a partir da `v7.1.1`.

A `v7.1.0` separou enxergar de detectar. A `v7.1.1` descobriu que detectar tinha
**duas implementações** que discordavam. Esta versão terminou o serviço: tirou do
`PodeDetectarSensor` tudo que nunca foi regra de detecção, e fez os três sensores
usarem a mesma reta.

O fio apareceu numa pergunta do autor no meio do trabalho:

> *É impressão minha ou a cada função envelopada o sistema vai ficando mais
> magro?*

Não era impressão, e o número explica por quê.

---

## 1. O que saiu do `PodeDetectar`, e por que estava lá

```text
antes da serie            ~2830 linhas
- ObservationCellService     −207   fato da celula
- HexGridGeometry            −133   geometria da grade
- codigo morto               −193   duplicata e orfao
- ObservationLineService     −259   a reta
agora                     ~2000 linhas
```

**830 linhas**, e só 193 foram apagadas por serem inúteis. As outras 637 mudaram
de casa.

O arquivo estava gordo porque acumulou três assuntos que **qualquer** pergunta
sobre observação precisa — fato de terreno, forma da grade e traçado de reta — e
que moravam ali só porque foi ali que apareceram primeiro.

O teste de que a divisão é honesta: nenhum dos três serviços menciona alcance,
chave, método, especialização, furtividade ou time. Se algum precisasse, estaria
no lugar errado.

---

## 2. `PodeEnxergar` ganhou laço próprio

Era a dívida que o autor tinha cravado como princípio:

> *O `PodeEnxergar` não pode usar regras que pertençam ao `PodeDetectar` para
> liberar hexágonos.*

Ele montava a resposta chamando `CollectVisibleCells` e desligando regra por
regra com flags. Foi assim que o mar de um submarino sumiu na `v7.1.0` — uma
flag de detecção descartando célula antes de qualquer conta de linha.

Agora o laço é dele: disco de raio `visao` pela vizinhança real do tilemap,
célula sem tile fora, a própria célula sem traçar linha contra si mesma, e o
resto pela reta compartilhada. **O arquivo não menciona `PodeDetectar` em lugar
nenhum.**

Com isso morreu o `ignoreDetectSpecializations` — parâmetro que eu havia criado
na `v7.1.0` para desligar regra alheia. Era andaime, não conceito.

**A janela também parou de remontar.** Ela reconstruía a resposta com flags em
vez de chamar o sensor; era essa reconstrução que permitia ferramenta e jogo
discordarem.

---

## 3. A terceira cópia da reta, e o erro que ela escondia

O `PodeMirarSensor` tinha cópia própria de `HasValidStraightObservationLine`,
`ResolveOriginEvForLos`, do lerp e do `ToWorld2` — 12 ocorrências. Comparadas
linha a linha, a geometria era **idêntica**, inclusive o épsilon de raspão.

O que divergia era só **de onde a linha parte**. E aqui eu errei duas vezes.

**Erro 1.** O autor disse *"a unidade herda o EV para liberar hex e detectar
unidades apenas"*, e eu pus o `PodeMirar` inteiro em "não herda". Ele corrigiu:

> *Uma bazuca de range 2 straight na montanha deve ser capaz de acertar o
> oponente atrás da floresta.*

A LdT **precisa** da herança. É ela que faz o tiro partir de EV 2 e passar por
cima do EV 1 da árvore. Sem isso, tiro de terreno alto perdia a linha.

**Erro 2, que a correção revelou.** O `PodeMirar` usa **duas** retas — a de tiro
e a de observação (*"o atirador vê o alvo"*) — e eu tinha posto as duas na mesma
regra. A de observação segue a regra de observação, que é justamente o que
alinha o `PodeMirar` com o `PodeDetectar`, como o comentário antigo dela já
pedia.

O resultado é uma regra nomeada em vez de uma segunda implementação:

```text
InheritTerrain                    revelar hexagono, detectar unidade
ShooterInheritsWhenTerrainAllows  linha de tiro
```

E o `shooterInheritsTerrainEv` — que eu cheguei a declarar dado órfão — **volta a
ter leitor**. Ele não é resto: é o botão que decide de qual terreno se atira de
cima. A Montanha é o único com ele ligado.

---

## 4. O que ficou confirmado sobre EV e combate

Duas verificações que o autor pediu e que valem ficar escritas:

**EV nunca foi fator de combate.** `TurnStateManager.Combat.cs` — que herdou o
papel do antigo `CombatResolver.cs`, hoje inexistente — não tem uma ocorrência
de EV. O `CombatModifierResolver` também não. EV existe em **um** lugar: a linha
de visada.

**Spotter empresta olho, não trajetória.** Para tiro reto, a ordem em
`PodeMirarSensor` é dura: sem LdT, a opção morre num `continue` **antes** de o
observador avançado ser sequer cogitado. O spotter só entra quando a LdT já
passou e o problema é o alvo estar além do alcance de observação do atirador.

---

## 5. O que não terminou

**A partida de teste não aconteceu.** É o item mais importante da lista, e é o
único que não posso fazer. Acumularam mudanças de comportamento que ninguém viu
rodar juntas: tiro de montanha, a linha de observação do `PodeMirar` herdando
EV, a detecção com fonte única, e o FOW recalculando inteiro no commit.

**Perf continua sem número.** Três trocas de filtro por varredura na `v7.1.1`,
nenhuma medida. O `FrameSpike` é o instrumento e um turno de IA com muitas
unidades é o teste.

**`skipLosForCurrentTarget` espera decisão.** Vem do `DPQAirHeightConfig` e fala
do **meio** — se a camada bloqueia linha —, não do sensor. Fica propriedade do
meio ou vira método declarado na ficha como o `Propagated`?

**A linha de quem detectou não aparece no resultado.** Pedido do autor, adiado
duas vezes. Agora está barato: o `TryTrace` já devolve `evPath`,
`intermediateCells` e `blockedCell`.

**O alerta sonoro precisa de um gancho.** O certo é "passou a detectar" — o delta
entre o conjunto anterior e o novo no publish. O `radar.MP3` está no repositório
sem nada o referenciando.

**`KnownCells` continua um balde só** e o `FogKnowledgeSnapshot` segue sem eixo
de camada. O Melhor Spotting depende disso.

---

## 6. Uma armadilha de processo que apareceu duas vezes

Editar arquivo por script no PowerShell 5.1 exige `ReadAllLines`/`WriteAllLines`
com UTF-8 explícito. `Get-Content` num arquivo sem BOM lê como ANSI e corrompe
acentos; `Set-Content -Encoding utf8` escreve BOM em mensagem de commit e ele
aparece no `git log`.

As duas vezes o erro foi pego pelo mesmo instrumento: **o diffstat**. Uma deleção
pura mostrando inserções é sinal de que algo foi reescrito sem intenção.
