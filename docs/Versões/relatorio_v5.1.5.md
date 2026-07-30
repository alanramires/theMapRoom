# Embarque naval de unidades militares

## Versão

`v5.1.5`

## Objetivo

Consolidar o primeiro ciclo de testes em escala da IA com aproximadamente
70 unidades, com foco na coordenação entre passageiros militares,
transportadores navais, objetivos de captura e progressão pelo tabuleiro.

O princípio central deste checkpoint é:

> O passageiro decide se precisa de transporte; a Melhor LZ casa passageiro,
> vaga, encontro e transportador; Caminhos Válidos materializa o deslocamento;
> Pode Embarcar continua sendo a fonte final da legalidade física.

## Embarque naval de papéis combatentes

Assault, Fire Support e Vigilância Aérea passaram a compartilhar o fluxo de
passageiro militar:

1. `Quero Carona` determina se a unidade realmente precisa de transporte;
2. `Melhor LZ de Embarque` escolhe transportador, slot, LZ e encontro do
   passageiro;
3. a unidade progride até o encontro usando a Progressão oficial;
4. quando o transportador já está na LZ e existe movimento suficiente, o
   passageiro move e embarca no mesmo batch;
5. quando a operação ainda não pode ser materializada, a unidade apenas
   progride ou aguarda sem fabricar ocupação.

Isso corrige o caso observado em que o Obuseiro Móvel alcançava o hex
adjacente ao navio, conservava pontos de movimento, mas encerrava sua ação sem
embarcar.

## Mover e embarcar no mesmo batch

`PodeEmbarcarSensor` ganhou uma consulta pura para a posição projetada do
passageiro. Ela reutiliza as mesmas regras oficiais de:

- aliança e validade do transportador;
- contexto de terreno, estrutura ou construção;
- domínio, altura, classe e skills aceitos pelo slot;
- exclusividade e capacidade restante;
- custo oficial de entrada no transportador;
- movimento restante depois do caminho;
- adjacência real entre passageiro e transportador.

Quando a projeção é válida, a IA produz uma única `PlayerAction` com:

```text
MoveTo = encontro do passageiro
SensorAction = Embark
Target = transportador na LZ
```

O executor normal realiza o movimento provisório e confirma o embarque pelo
sensor. A chegada ao encontro não é mais materializada como uma ação isolada
quando o embarque já é legal.

## Passageiro é a autoridade da carona

O transportador não impõe uma viagem a uma unidade que declarou não precisar
dela. Capturadores recusam embarque quando já possuem oportunidade livre,
reservada 1:1 e alcançável a pé dentro do envelope Operational.

Quando os objetivos próximos já pertencem a outros capturadores, a unidade
remanescente pode declarar `QueroCarona=SIM`, permitindo que o transportador
atenda quem realmente ficou sem destino terrestre útil.

As reservas de captura ganharam representação persistente em `UnitManager` e
no save/load:

- existência de destino designado;
- instance ID da construção;
- célula do objetivo;
- reconstrução e limpeza da reserva quando o alvo deixa de ser válido.

Isso reduz a troca oportunista de objetivos após load e evita que vários
capturadores façam o transporte perseguir a mesma oportunidade.

## Capitães, magnets e progressão

Papéis combatentes sem objetivo melhor podem seguir capturadores como
capitães, mantendo um respiro visual desejável e preservando construções já
declaradas para captura.

O seguimento usa `Tools > Transporte > Caminhos Válidos > Progressão` como
fonte de verdade para contornar montanhas e outros obstáculos. A posição do
capitão é uma direção operacional, não um destino que autorize atravessar
terreno ilegal.

Unidades com domínio nativo preferencial, como submarinos, fazem primeiro uma
progressão não regressiva que termina no próprio domínio. Praia ou superfície
continuam disponíveis somente como fallback quando nenhum avanço nativo pode
ser materializado.

## Coordenação aérea

O ciclo também registra o refinamento inicial das patrulhas em torno do
capitão:

- ataque aéreo e bombardeiros preferem flancos e retaguarda;
- interceptadores preferem flancos e vanguarda;
- aeronaves ofensivas não reabrem a vanguarda como fallback apenas porque a
  zona segura não cabe no movimento da rodada;
- sem posição de flanco/retaguarda materializável, podem manter a posição.

Essa política evita que aeronaves de ataque se comportem como unidades de
linha ou permaneçam girando diretamente sobre o capitão.

## Conteúdo adicional do checkpoint

Como solicitado com `git add .`, o checkpoint inclui todo o estado atual do
workspace, entre ele:

- reorganização dos assets de skills em subpastas;
- ajustes atuais em Rodovias;
- assets de fontes serializados pelo Unity;
- cena presente em `Assets/_Recovery`;
- refinamentos de Capturer, Rebel, Assault, Air Combat, Quero Carona,
  reservas de captura e save/load desenvolvidos durante o teste.

## Contrato transacional

A consulta projetada de `Pode Embarcar` é somente leitura. Ela não:

- move o passageiro;
- ocupa a LZ;
- consome pontos de movimento;
- altera passageiros ou vagas;
- atualiza FOW, detecção ou inteligência;
- modifica `HasActed`.

Movimento e embarque permanecem provisórios até o compromisso explícito do
batch normal. Somente após a confirmação e o retorno a
`CursorState.Neutral` o estado definitivo e seus caches podem ser
recalculados.

## Validação

- projeto compilado com 0 erros;
- legalidade projetada centralizada em `PodeEmbarcarSensor`;
- batch combinado de movimento e embarque construído pelo fluxo normal;
- fallback preservado quando transportador, LZ, slot, MP ou contexto deixam
  de ser válidos;
- crash nativo do índice de busca do Unity isolado de erros de compilação; o
  cache `Library/Search` foi preservado em backup para reconstrução automática.
