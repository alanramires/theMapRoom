BUG-001 — Merge bloqueia unidades embarcadas mas não verifica carga própria

Arquivo: Assets/Scripts/Sensors/PodeFundirSensor.cs
Problema: A validação checa se a unidade está embarcada (IsEmbarked) mas não verifica se ela própria está transportando passageiros (GetEmbarkedUnits().Count > 0). Um merge entre duas unidades que carregam passageiros dissolveria silenciosamente a carga do mapa.
Correção esperada: Bloquear merge se qualquer uma das duas unidades tiver GetEmbarkedUnits().Count > 0.
Severidade: Alta — perda silenciosa de unidades sem feedback ao jogador.
Origem: Identificado durante revisão de documentação em 24/03/2026.
