# Transporte — doutrina

Doutrina definida pelo autor. Onde o código divergir dela, o código está errado.

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |

---

## 1. Onde largar o passageiro (`TransportDropOffRange`)

A área de largada **não é um raio em hexes a partir do transportador**. Ela é
derivada por **análise reversa**, a partir do passageiro:

> Teleporta a unidade para cima do objetivo, calcula o **Tactical dela** a partir
> dali, e é essa a área. O passageiro pode ser largado em qualquer célula dentro
> dela — em cima do objetivo ou nas adjacências.

O que isso quer dizer na prática: a largada é boa quando o passageiro, no turno
seguinte, **fecha o objetivo com o próprio movimento**. Um obus de 2 MP e uma
infantaria de 3 MP não aceitam a mesma largada, porque o Tactical delas não é o
mesmo — e é a unidade que define a área, não o veículo.

Isso também explica por que "0" é válido: em cima do alvo é o melhor caso, não
uma exceção.

⚠️ Hoje é constante fixa e propriedade do **veículo**, não do passageiro:

```csharp
TransportDropOffRange   = 4   // entrega terrestre
FireSupportDropOffRange = 3   // artilharia
AirDropOffRange         = 2   // helicóptero voa direto ao alvo
```

Existe uma peça no caminho certo: `BuildPassengerRouteLimits(passengers, turns)`
no `MelhorDesembarque` limita a rota **por passageiro**, em turnos. E há um
comentário no mesmo arquivo registrando que o `TransportDropOffRange` *"era uma
regra de entrega pingada"* que eliminava o segundo passageiro de uma entrega
conjunta mesmo quando a melhor LZ era alcançável no mesmo turno.

O conserto natural é o envelope: `UnitReachEnvelopeService` na banda `Tactical`
do **passageiro**, calculado a partir da célula do objetivo. É a mesma consulta
que o Quero Carona já faz, invertida.

Ver `docs/AI Behavior/Capturador.md` §7 para os limites por papel que o autor
definiu (capturador 0-3, agressivo 0-2) — eles são política **sobre** essa área,
não substitutos dela.

---

## 2. Transporte não pousa em prédio capturável

O veículo **nunca** larga âncora em cima de um capturável. Um transporte parado
sobre o prédio ocupa a célula que o capturador precisa e, pior, trava a captura
que ele mesmo veio viabilizar.

Se não houver jeito — sem célula alternativa —, ele **sobe na iniciativa** para
sair de lá o mais rápido possível na rodada seguinte.

❌ Nenhuma das duas existe. A iniciativa hoje tem a regra espelhada, mas só para
o outro lado: grupo 0 inclui *"blocker sobre o objetivo de captura de outro
capturador"* — e o teste exige que o bloqueador **satisfaça o papel Capturador**
(`CanSatisfy(targetData, UnitRole.Capturador)`), então um transportador parado
em cima do prédio não é reconhecido como bloqueador.

Duas frentes, então:

1. **evitar** — a escolha de LZ precisa rejeitar célula com capturável;
2. **sair** — quando for inevitável, o transportador entra no grupo de
   iniciativa que age primeiro, para liberar o hex antes que o capturador
   precise dele.

> Marcado pelo autor como "a gente vai chegar lá ainda".

---

## 2-PA. O Porta-Aviões é deste papel

Papel primário **Transportador**, com três pernas que ele **consulta**:

| perna | o quê |
|---|---|
| Fire Support | duas armas antiaéreas de longo alcance — tenta o tiro, volta à agenda |
| Logística | suporte |
| Transferência | é **Hub**: recebe do porto naval e repassa ao avião-tanque (perna no controlador de Estoque) |

Ele segue a agenda **courier/pickup** deste documento. Não herda a doutrina de
retaguarda, iniciativa alta nem recusa de embarque do `FireSupport.md`: aquilo é
de quem tem Fire Support como papel primário.

Mesmo mecanismo do Artilheiro Combatente, com os lados trocados — *"tento o
tiro; se não der, volto para a minha agenda"*. A diferença é qual agenda espera
a bola de volta.

❌ A perna de Fire Support do transportador não existe no roteador: hoje o
transporte é consultado antes dos papéis de combate e não volta.

### Quando o Porta-Aviões atira — rascunho do autor

> Anotado como veio, ainda sem lapidar. Fica aqui porque é política **do
> transportador**, e este é o documento dele.

| estado | comportamento |
|---|---|
| **vazio** | atira contra aeronaves no seu raio **Tático** |
| **com carga aérea** | atira **apenas contra ataque aéreo** (bombardeiros), porque deles não dá para fugir — são mais rápidos |
| **com carga aérea, sob caças** | **ergue os caças** — reconhecidamente tarde demais |

A lógica por trás: com o convés cheio, gastar o tiro em caça é desperdício
(caça se evita manobrando), enquanto bombardeiro é ameaça inescapável. E a
resposta contra caça não é a arma do navio, é o próprio convés.

"Erguer os caças tarde demais" é a admissão de que a decisão de decolar deveria
ter acontecido antes — provavelmente ao **detectar** o caça, não ao ser atacado
por ele.

---

## 3. Esteira (implementado nas v6.1.0/v6.1.1)

A doutrina completa está no `docs/relatorio_v6.1.1.md`. Resumo:

| regra | estado |
|---|---|
| quem embarca primeiro dita a rota (FIFO por turno de embarque) | ✅ |
| larga esse passageiro no Tactical do objetivo **dele** | ✅ |
| o próximo da fila vira referência | ✅ |
| fila de espera por antiguidade (urgência cresce, teto na emergência) | ✅ |
| transportador que não alcança o passageiro não promete | ✅ |
| promessa persistente no `AIDesignatedMission` do transportador | ✅ |
| encher a vaga livre no caminho | ❌ (`BuildAttempts` retorna cedo com carga) |
| promessa reserva **uma vaga**, não o veículo | ❌ |
| espera vira pressão de compra de mais transporte | ❌ |

⚠️ **Cascata registrada:** implementar "encher a vaga livre" sozinho **piora** a
fome do passageiro esquecido — o veículo passa a ficar ocupado mais tempo. Só
entra junto com a reserva de vaga ou com a pressão de compra.
