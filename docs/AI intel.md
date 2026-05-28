# A Hierarquia de Decisão da IA

A IA não age por reflexo. Ela passa por camadas de raciocínio antes de mover qualquer unidade. Cada camada responde a uma pergunta diferente.

---

## 1. Intel — O que eu sei?

A primeira camada é a consciência situacional. Antes de decidir qualquer coisa, a IA monta um retrato do mundo: quais unidades inimigas estão visíveis, quais setores estão sob controle de quem, quanto dinheiro tem disponível, em que turno está e qual é a postura geral da partida (agressiva, defensiva, equilibrada).

Esse retrato é chamado de **Snapshot** e é reconstruído do zero a cada vez que uma unidade vai agir. A IA nunca age com informação velha.

O **AI Intel Analyzer** transforma esse snapshot em um relatório mais interpretado: quais setores estão ameaçados, quais operações estão ativas, quais papéis estão sem cobertura, qual é a saúde geral do plano.

---

## 2. Plano — O que eu quero?

Com o intel em mãos, a IA decide seus objetivos. Ela olha para o mapa e escolhe quais setores capturar, quais defender e quais ignorar por ora. Para cada setor escolhido, ela reserva vagas: um capturador, talvez um escolta de assalto, talvez um transportador se o setor for distante.

Esse conjunto de setores e vagas é o **Plano**. Ele dura o turno inteiro e guia todas as decisões seguintes. Se o plano tem uma vaga aberta — por exemplo, um setor sem capturador designado — isso alimenta diretamente as compras.

---

## 3. Operações — Como eu vou executar?

O plano diz *o quê*. As operações dizem *como*. A IA organiza grupos de unidades em torno de objetivos concretos: uma operação de captura terrestre tem um capturador avançando e possivelmente um assalto cobrindo o flanco. Uma operação de captura aérea coordena o helicóptero com o passageiro que ele vai transportar.

As operações também controlam **coesão** — a ideia de que unidades que operam juntas devem se manter razoavelmente próximas. Um capturador não deveria avançar sozinho para o meio do território inimigo sem apoio. A operação detecta esse desequilíbrio e pode segurar uma unidade ou acelerar outra para reequilibrar.

---

## 4. Compras — O que eu preciso recrutar?

Antes de agir com as unidades existentes, a IA verifica o estado do plano e detecta vagas não preenchidas. Se faltam capturadores para cobrir os setores escolhidos, ela compra. Se os capturadores já estão em boa cobertura (acima de 60%), ela libera a compra de unidades de elite — assalto pesado, fogo indireto.

Transportadores só são comprados quando os setores-alvo estão suficientemente longe do QG. Não faz sentido gastar em transporte para cruzar dois hexes.

---

## 5. Iniciativa — Quem age primeiro?

Dentro de um turno, a ordem em que as unidades agem importa. A IA usa uma hierarquia de prioridade:

- **Primeiro** agem unidades em situação crítica: alguém que precisa sair do caminho para liberar um hex importante, ou que está bloqueando o avanço de outro capturador.
- **Segundo** agem unidades com momentum: capturador em corredor ativo, transportador com candidato de embarque próximo, unidade em reparo dentro de um setor relevante.
- **Terceiro** agem unidades com objetivo designado no plano.
- **Por último** agem unidades sem objetivo claro — os "rogue", que farão o melhor que puderem sem instrução direta.

Essa ordem garante que uma unidade não bloqueie outra por má sincronização.

---

## 6. Decisão por Papel — O que cada unidade faz?

Cada unidade tem um papel primário. Quando chega sua vez de agir, a IA consulta esse papel e o plano e decide a ação mais adequada:

- **Capturador** avança para setores, captura, eventualmente engaja inimigos no caminho, embarca em transporte se houver um disponível.
- **Assalto** pressiona inimigos, defende flancos, apoia capturadores.
- **Transportador** busca passageiros, entrega nos setores certos, sai do caminho quando vazio.
- **Fogo Indireto** se posiciona para cobrir o avanço, só atua quando há escolta.

Se nenhum papel tem uma resposta clara para a situação atual, um avaliador genérico (o **HexEvaluator**) entra como último recurso e age com base em oportunidade pura: atacar se puder, capturar se estiver perto, avançar se não houver nada melhor.

---

## 7. Ação — O movimento físico

A decisão vira uma instrução atômica: mover para tal hex, atacar tal unidade, capturar tal construção, embarcar, desembarcar. Essa instrução é executada pelo motor do jogo e registrada no histórico de jogadas.

---

## Resumo visual

```
Intel (o que eu sei)
  ↓
Plano (o que eu quero)
  ↓
Operações (como vou executar)
  ↓
Compras (o que preciso recrutar)
  ↓
Iniciativa (quem age primeiro)
  ↓
Decisão por Papel (o que cada unidade faz)
  ↓
Ação (movimento físico no tabuleiro)
```

Cada camada informa a próxima. A IA não pula etapas — uma unidade nunca age sem que o plano tenha sido consultado, e o plano nunca é montado sem que o intel tenha sido lido.
