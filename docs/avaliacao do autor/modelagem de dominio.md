# Modelagem de dominio

## 1) Quais sao os principais conceitos do dominio modelados neste codigo?
Os conceitos centrais modelados no projeto sao:

- Unidades: identidade, atributos taticos (HP, municao, autonomia, movimento, visao), classe militar, armas embarcadas, status de acao no turno e estado de camada.
- Camadas operacionais: `Domain` + `HeightLevel` (terra, naval, submarino, ar; superficie/submerso/alturas de voo), usados em movimento, ocupacao, combate, visao e operacoes aereas.
- Terreno/estrutura/construcao como contexto tatico: custo de entrada, EV, bloqueio de LoS, regras de acesso por skill, captura e oferta de servicos.
- Combate composicional: alcance, tipo de trajetoria, LoS/LdT, spotting, stealth, prioridade/categoria de arma, modificadores (RPS, elite, DPQ e outros).
- Logistica e servicos: suprimento, reabastecimento, reparo, rearme, transferencia de estoque, fornecedores (unidade e construcao), limites por turno e custo economico.
- Fluxo de turno: ciclo de time ativo, economia por turno, upkeep de autonomia no inicio do turno e estados de acao da unidade.
- Sensores taticos: modulos `Pode*` que avaliam elegibilidade de acoes (mirar, embarcar, desembarcar, capturar, suprir, transferir, fundir), incluindo motivos de invalidez.
- Interface de decisao: helper panel e overlays (alcance, linha de tiro, ameaca) para expor estado e justificativas das regras ao jogador.

## 2) O design sugere que o autor pensou primeiro em mecanicas do jogo e depois implementou codigo, ou o codigo cresceu de forma incremental?
Os dois, mas com predominio de desenho orientado a mecanica.

O codigo mostra uma base conceitual forte definida por mecanicas (dominio/altura, sensores, logistica, combate por camadas, fluxo de turno), o que indica modelagem de jogo antes da implementacao final. Ao mesmo tempo, ha sinais claros de evolucao incremental controlada:

- `TurnStateManager` dividido em varios arquivos parciais por feature (movimento, sensores, supply, combat, helper, state machine).
- Campos e anotacoes de compatibilidade/migracao (`FormerlySerializedAs`, regras legadas).
- Camadas de cache e revisao adicionadas para otimizar overlays de ameaca.
- Presenca de modos/presets de setup e flags de validacao (LdT, LoS, spotter, stealth), sugerindo calibracao progressiva.

Conclusao: o projeto nao parece ter crescido de forma ad-hoc; ele evoluiu incrementalmente sobre um modelo de dominio previamente pensado.
