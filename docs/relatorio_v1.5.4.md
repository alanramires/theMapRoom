# Relatorio de Atualizacao - v1.5.4

## Em uma frase
A versao v1.5.4 consolida o comportamento de unidades na IA, com captura mais consistente por tipo de infantaria, ataque oportunista no caminho e telemetria de distancia corrigida para leitura real de alcance.

## O que isso trouxe na pratica
- Soldado ficou com perfil principal de captura.
- Bazooka ficou com perfil de beliscada (skirmish), capturando quando estiver sem alvo relevante.
- Em rota de captura, a unidade pode interromper para atacar alvo no corredor quando a troca for favoravel.
- O log de score passou a exibir distancia bruta e distancia efetiva de engajamento (`dist=bruta->efetiva`).
- Fluxo de reparo foi mantido com fallback seguro quando construcao propria estiver bloqueada.

## Principais entregas

### 1. Perfil de comportamento por unidade de infantaria
- `Soldado/Fuzileiro`: prioridade de captura.
- `Bazooka/Lanca-foguetes`: prioridade de combate oportunista.
- Heuristica por id/displayName para separar papel de captura vs beliscada sem quebrar compatibilidade atual.

### 2. Ataque oportunista durante protocolo de captura
- Antes e depois do movimento, se existir alvo no corredor da captura com score suficiente, a IA interrompe captura e ataca.
- Mantem captura como objetivo, mas sem ignorar trocas boas no caminho.
- Log dedicado para auditoria: `captura interrompida por alvo no caminho`.

### 3. Telemetria de alcance corrigida
- Score agora mostra `dist=bruta->efetiva`.
- Remove ambiguidade do caso em que alvo parece "fora de alcance" pela distancia bruta, mas esta valido apos deslocamento.

### 4. Reparo com robustez em construcao bloqueada
- Se construcao propria estiver ocupada/bloqueada, IA busca alternativa.
- Mantem `AIForcedToRepair` com fallback defensivo quando nao houver destino livre.

## Bloco tecnico
- Scripts modificados (principais):
  - `Assets/Scripts/AI/AIPlayerController.cs`
- Ajustes combinam decisao tática, filtro de alvo em corredor e melhoria de log para leitura operacional.

## Pendencias conhecidas (proxima versao)
- Externalizar thresholds de skirmish para `AIData` (hoje hardcoded).
- Refinar prioridade de alvo por custo/valor estrategico sem perder contexto de captura.
- Revisar texto com caracteres corrompidos em alguns logs legados.

## Resultado
A v1.5.4 melhora a coerencia da IA em campo: captura quando deve capturar, morde quando a troca compensa, e registra distancia de combate de forma clara para depuracao.
