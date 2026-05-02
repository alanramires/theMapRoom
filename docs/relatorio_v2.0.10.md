# v2.0.10 - AI Capturador - Partial (antes)

## Snapshot Antes da Separacao

- Registrado o estado atual do `AIController.Capturer.cs` antes da divisao em partial files por papel.
- O capturador concentra hoje comportamentos de reparo, rogue, defensor, ponta de lanca, explorador, perseguidor e captura oportunista no mesmo fluxo decisorio.
- A versao serve como ponto de retorno antes da reorganizacao em arquivos como `AIController.Capturer.Defender.cs`, `AIController.Capturer.PontaLanca.cs`, `AIController.Capturer.Explorer.cs` e `AIController.Capturer.Pursuer.cs`.

## Direcao Tecnica

- Manter `AIController.Capturer.cs` como entrada principal e roteador dos papeis do capturador.
- Extrair comportamentos por responsabilidade sem alterar a logica primeiro, para facilitar comparacao e debug.
- Isolar helpers compartilhados de scoring, DPQ, threat, prioridade de alvo, captura oportunista e busca de construcoes.

## Objetivo

- Criar um marco limpo antes da refatoracao estrutural.
- Preservar o comportamento atual como baseline para validar a reorganizacao em partials.
