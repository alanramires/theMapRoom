# Save and Load Game Refactor (V1.4.8)

## Objetivo
Reduzir o tempo de resposta de Save/Load e separar responsabilidades de dados pesados (replay, metadata e cache de FoW), mantendo o fluxo de jogo estável.

## Principais mudanças

### 1) Separação de Replay do save principal
- `SaveGameData` não carrega mais o bloco completo de replay dentro do `.sav`.
- O replay agora é gravado em arquivo lateral `*.replay` quando a gravação está ativa.
- No load normal de partida, o replay não é importado automaticamente (runtime segue independente).

Impacto:
- Redução de tamanho do save principal.
- Menor tempo de deserialização no carregamento de partida.

### 2) Metadata leve por slot
- Cada slot pode ter `*.meta.json` com campos mínimos para UI/listagem:
  - `sceneName`
  - `savedAtUtcTicks`
- Leitura de metadata na tela de slots evita abrir/descomprimir/deserializar o save inteiro.
- Fallback para saves antigos sem `.meta.json` com recuperação e regeneração da metadata.

Impacto:
- Abertura do painel de save/load mais rápida e previsível.

### 3) Logs de performance no LoadRoutine
- Instrumentação com timestamps por etapa relevante do carregamento.
- Logs adicionados antes/depois de blocos críticos (deserialize, restore de entidades, restore de match state, etc.).

Impacto:
- Diagnóstico objetivo de gargalos reais no pipeline de load.

### 4) Estado de Fog of War no save
- Captura de cache de FoW no save principal:
  - contribuições por célula
  - visibilidade por unidade (índice de cache)
- Na restauração, tentativa de aplicar cache diretamente quando válido para o time ativo.
- Fallback para recálculo padrão quando cache não for aplicável.

Impacto:
- Menos trabalho no pós-load em cenários compatíveis.
- Mantém segurança funcional com fallback.

### 5) Limpeza e descoberta de arquivos de slot
- `ClearSlot` remove também sidecars (`*.meta.json`, `*.replay`).
- Descoberta de arquivo legível ignora sidecars para evitar colisões no fluxo de slot.

## Resultado esperado da versão
- Menor latência percebida entre abrir painel e interação de save/load.
- Redução de carga de CPU/memória durante load de partida.
- Telemetria prática para evolução contínua.
- Arquitetura mais modular para evolução do sistema de replay.

## Observações
- Saves antigos podem operar via fallback em metadata.
- Como projeto em protótipo, alterações estruturais foram priorizadas por performance e manutenibilidade.
