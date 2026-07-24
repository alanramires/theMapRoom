# v4.1.6 - Separar Slot de TeamID

Esta versão é um point save antes da migração arquitetural que separará a identidade do participante (`SlotIndex`) de sua cor/facção visual (`TeamId`).

## Diagnóstico: Slot versus TeamID

Um stress test com quatro participantes expôs a colisão:

- slot 0: azul;
- slot 1: amarelo;
- slot 2: vermelho;
- slot 3: vermelho.

Os dois slots vermelhos ainda são tratados como um único participante em partes importantes do runtime. A causa é o uso histórico de `TeamId` como chave simultânea de identidade, propriedade, turno e apresentação.

O contrato desejado para a próxima etapa é:

- `SlotIndex` identifica exclusivamente jogador, IA, economia, turno, unidades, construções, planos, memória, FOW e vitória;
- `TeamId` define cor/facção visual e pode se repetir entre slots;
- dois slots com o mesmo `TeamId` continuam sendo participantes independentes e potencialmente inimigos.

## Pontos confirmados para refatoração

- O início de turno compara somente o `TeamId`; slots consecutivos com a mesma cor podem não disparar uma nova ativação.
- A liberação de unidades e o reset de `HasActed` filtram por `TeamId`, podendo ativar unidades de ambos os slots.
- Os Stages 0–2 da IA selecionam unidades e constroem snapshots por `TeamId`.
- O `ObjectiveManager` e caches relacionados armazenam planos por `TeamId`.
- A retomada de stages da IA identifica o participante atual por `TeamId`.
- Diversas regras de aliado/inimigo, FOW, detecção, construções, captura, logística, economia e derrota ainda usam `TeamId` como identidade.

Esses pontos devem ser migrados de forma coordenada. Uma correção isolada somente no Stage 2 deixaria o jogo em estado inconsistente.

## Correções já incluídas neste checkpoint

- O contador `Panel_remaining` passou a contar unidades pelo slot ativo e por `UnitManager.SlotIndex`.
- `FOW PARTIAL` não desliga mais a opção `IA Rápida`.
- O Map Generator limita sua detecção à cena ativa, substitui corretamente uma geração completa anterior e comprime bounds vazios.
- O Auto Mirror continua autorizado a expandir o Tilemap quando novas células reais são pintadas.

## Demais trabalhos consolidados

- Ajustes em dados de unidades, construções, serviços, transporte, terreno e infraestrutura.
- Evolução dos presets e ferramentas de inspeção da IA.
- Atualizações nos mapas Hot Seat e Playground.
- Revisões em sensores, regras de movimento, documentação técnica e manual do jogo.

## Próxima etapa

Executar a migração “Separar Slot de TeamID” por fronteiras:

1. turno e ativação;
2. propriedade de unidades e construções;
3. snapshots, planos e stages da IA;
4. economia, compra e limites;
5. relações aliado/inimigo, combate e sensores;
6. FOW, detecção e memória;
7. captura, derrota, vitória, save/load e replay;
8. auditoria do stress test com `slot2 = vermelho` e `slot3 = vermelho`.

## Validação

- Os projetos `Assembly-CSharp` e `Assembly-CSharp-Editor` compilaram sem erros nas alterações de código deste checkpoint.
- Avisos preexistentes de APIs obsoletas e serialização do Unity permanecem sem bloquear a compilação.
