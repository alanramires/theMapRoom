# Governança entre papéis — as arestas

Os contratos desta pasta descrevem **papéis**. O `governanca.md` descreve o que
vale **acima** de todos eles — as duas ordens e os sensores. Este descreve o que
existe **entre** eles.

Nasceu de uma observação do autor, escrevendo o quarto contrato:

> *"cada papel lê todos os sensores, mas de maneira diferente, e alguns governam
> como outros papéis se comportam"*

A primeira metade dessa frase está respondida na §5 do `governanca.md`: papel é
uma consulta, não um conjunto. Este documento é a segunda metade.

---

## 1. Três tipos de governo

"Governar" não é uma coisa só. São três, e confundi-las é o que produz o mesmo
bug repetido.

| tipo | governa | mecanismo |
|---|---|---|
| **magnético** | o **onde** | o papel A vira a **âncora** do papel B |
| **por agenda** | o **para quê** | o papel A **adota o objetivo** do papel B |
| **por exclusão** | o **onde não** | o papel A é definido por onde o papel B **está** |

### 1.1 Magnético — governa o onde

O papel governado orbita o governante. Não precisa ficar colado: fica entre 1 e
o **Tactical do capitão**.

| capitão | orbita | estado |
|---|---|---|
| Capturador | Assalto | ✅ `AIController.Assault.cs:213` resolve o capitão e loga o id |
| Assalto marítimo (`isMaritime`) | Marinha (fragata, submarino) | ⚠️ **hoje a marinha segue o capitão terrestre** — hardcode para o jogo de testes continuar rodando. É o item M3 do `Assalto.md` |
| Radar / EWACS | Antiaéreo | ver `FireSupport.md` |

### 1.2 Por agenda — governa o para quê

O papel governado **não tem objetivo próprio**: adota o de quem ele serve.

O transporte é o caso puro. *"O passageiro mais antigo assume o volante e leva o
transportador até onde quer ir."* O destino do veículo **é** o destino da carga —
e por isso o `AIDesignatedMission` dele deveria herdar o objetivo do passageiro
(pendência T11 do `Transporte.md`).

É o único papel governado por **todos** os outros. Isso não é acidente de
implementação, é a definição da função dele: alavancar.

### 1.3 Por exclusão — governa o onde não

O papel governado é definido, em parte, por uma região **proibida**, e essa
região é a posição de outro papel.

Fire Support não pode estar na vanguarda. Mas "vanguarda" não é uma constante do
mapa: é **onde o assalto está**. Mover o assalto move a proibição do fire
support, sem ninguém tocar em fire support.

O mesmo vale, invertido, para o transporte: ele não pode largar âncora em cima de
capturável porque essa célula é do capturador (§6.5 do `Transporte.md`).

---

## 2. Por que isso importa: ordem de refactor

**As arestas determinam a ordem, e a ordem não é negociável.**

O caso que já foi esbarrado, e que motivou este documento:

> A lógica de camada nativa do submarino mora **dentro** do fluxo de perseguir o
> capitão. O M3 remove esse fluxo. Fazer M4 antes de M3 apagaria a camada nativa
> junto; fazer M3 antes de M4b apagaria sem ter onde recolocá-la. Daí a ordem
> obrigatória **M4b → M3 → M4**.

Ninguém deduziu isso do plano. Descobriu-se batendo. Com o grafo escrito, a
regra é mecânica:

> **Antes de arrancar um fluxo de governo, verifique o que está morando dentro
> dele.** Um fluxo magnético é um lugar tentador para pendurar lógica que não tem
> nada a ver com o capitão — e ela morre junto quando o fluxo sai.

---

## 3. O bug que este documento explica

Os quatro contratos produziram tabelas de pendência que **rimam**. Sempre uma
destas três formas:

| forma | exemplos |
|---|---|
| **número fixo onde devia ter banda** | `TransportDropOffRange = 4`, `FireSupportDropOffRange = 3`, `ShuttlePickupRange = 2`, `AirDropOffRange = 2` |
| **âncora congelada onde devia ter parâmetro** | o funil do QG no capturador rogue — derrubado na v6.1.2/6.1.3 e **ainda vivo** na sua cópia dentro do transporte (T2) |
| **cópia paralela onde devia ter parâmetro** | `Rebel.cs` como espelho do capturador; e a segunda cópia do mesmo funil no desembarque |

Não são quatro problemas. É um, aparecendo quatro vezes — e é o que a regra
geradora do `CLAUDE.md` já dizia antes de existirem os casos:

> *banda, âncora e camada são sempre parâmetro da unidade avaliada — nunca
> constante do papel.*

Este documento acrescenta a razão de a violação ser **tão fácil de cometer**:
quando o papel A governa o papel B, a tentação é escrever em B a constante que
descreve A hoje, em vez de perguntar a A. Funciona, e continua funcionando até A
mudar — aí B fica com a doutrina velha, e ninguém percebe, porque B compila e
roda.

---

## 4. Aviso de método

Contrato escreve mais rápido do que código implementa. Quatro contratos já
produziram dezenas de pendências, e boa parte é doutrina do zero.

Uma lista grande, organizada e marcada **parece progresso**. O antídoto é o ritmo
que o autor já usava por instinto: **uma classe por vez — mexe, compila, roda no
jogo, comita antes da próxima.** Não emenda fases.
