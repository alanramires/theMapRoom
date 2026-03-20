# Guia do Jogador

Guia rapido e canonico para jogar `The Map Room` no fluxo atual.

## Objetivo da partida

Voce vence controlando espaco, informacao e economia:
- capturando construcoes relevantes
- neutralizando capacidade operacional inimiga
- sustentando seu exercito (combustivel, municao, dinheiro)

## Loop basico de jogo

Em cada turno do seu time:
1. selecione uma unidade aliada
2. mova para um hex valido (ou confirme parado)
3. escolha uma acao de sensor quando disponivel
4. confirme e aguarde a execucao da acao
5. repita com outras unidades
6. encerre o turno

## Acoes taticas principais (sensores)

Atalhos mais comuns no scanner:
- `A`: atacar (mirar)
- `E`: embarcar
- `D`: desembarcar
- `C`: capturar
- `F`: fundir
- `S`: suprir
- `T`: transferir

Nem toda unidade tera todas as opcoes em todo momento. O jogo valida contexto (range, camada, alvo, servicos, regras do hex, etc).

## O que mais decide partida

- Informacao: FoW, deteccao e linha de visada importam tanto quanto dano.
- Posicionamento: chegar no hex certo antes do inimigo muda o turno inteiro.
- Logistica: unidade sem combustivel/municao para de projetar poder.
- Economia: captura e manutencao de construcoes sustentam compras e servicos.

## Erros comuns de iniciante

- gastar todas as unidades em ofensiva e esquecer suprimento
- lutar sem cobertura de visao/deteccao
- ignorar custo operacional por dominio/camada
- abrir combate sem plano de reposicionamento no turno seguinte

## Leituras complementares

- `docs/turnState.md` (FSM micro da unidade)
- `docs/sensors.md` (sensores do runtime)
- `docs/FOW.md` (fog of war)
- `docs/Combat.md` (combate)
- `docs/Logistica/logistica.md` (logistica)
