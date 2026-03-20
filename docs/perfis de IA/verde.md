Hardening do replay — 3 correções
1. Bloquear save durante replay ativo

No SaveGameManager, antes de executar o save, checar ReplayManager.IsReplaying
Se true: cancelar save e logar [Save] Bloqueado: replay ativo
Opcional: mostrar mensagem na UI avisando o jogador

2. Pausar replay ao fechar painel F9

No ReplayPanelUI, ao fechar o painel (OnDisable ou botão de fechar):
Se replay estiver rodando (IsReplaying && IsPlaying): chamar Pause() automaticamente
Replay continua existindo mas para de executar

3. Validar UnitInstanceId antes do HandleConfirm()

Antes de chamar HandleConfirm() no hex da unidade, verificar se a unidade no hex tem o InstanceId esperado
Se não bater: logar warning [Replay] UnitInstanceId divergiu — esperado X, encontrado Y e abortar batch
Não crashar — abortar graciosamente e parar o replay

Regras:

Não quebrar fluxo normal de gameplay
Build limpo obrigatório nos dois projetos