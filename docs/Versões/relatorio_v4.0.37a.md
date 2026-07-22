# v4.0.37a — Performance Fixes II

Data: 17/07/2026

## Visão geral

Esta revisão complementar consolida correções encontradas durante os testes posteriores à v4.0.37. O foco foi remover uma restrição indevida de combate, melhorar o fluxo de confirmação do Serviço do Comando e garantir que perdas ocorridas durante o turno adversário sejam registradas no Jornal do Comandante.

## Layer Lock e uso de armas

- `Layer Lock` passa a cumprir somente sua responsabilidade: manter a unidade no domínio e altura forçados durante a duração configurada.
- Uma unidade com trava de camada ativa não perde mais todas as opções de ataque automaticamente.
- O submarino forçado a permanecer em `Naval/Surface` pode usar o Torpedo, que possui essa camada entre seus modos operacionais permitidos.
- Restrições reais de disparo continuam sendo decididas pelos dados da unidade e da arma, incluindo `cantUseWeaponsOnTheFollowDomain` e `canBeFireOnlyAtDomainHeigh`.
- Caças e demais unidades que possuem bloqueios explícitos por camada continuam respeitando suas configurações.
- O `PodeMirarSensor` permanece como autoridade para validar munição, alcance, camada do alvo, camada do atacante, trajetória, linha de tiro, visibilidade e stealth.

## Serviço do Comando

- A nova tela de preview passa a abrir com o foco em **EXECUTAR**.
- Nenhuma unidade começa selecionada ou destacada automaticamente.
- A câmera não é mais deslocada para a primeira unidade ao abrir o painel.
- As unidades previstas e ignoradas continuam disponíveis na mesma navegação para inspeção opcional.
- **CANCELAR** permanece acessível depois de Executar, preservando o fluxo de teclado e controle.

## Jornal do Comandante

- Corrigida a ausência de unidades destruídas durante o turno adversário no resumo do proprietário.
- A rotina de destruição definia o HP como zero antes de chamar `MarkDead`; a sincronização de HP já marcava `isDead`, fazendo a auditoria interpretar a morte como previamente processada.
- A morte agora é registrada antes da sincronização final do HP, garantindo uma única publicação no ledger do Jornal.
- Quando o time vermelho destrói uma unidade verde, o evento fica pendente para o início do próximo turno verde.
- A identificação do atacante continua fog-honesta: ele só é nomeado quando estava visível para o proprietário da unidade destruída.
- A mesma correção cobre unidades embarcadas destruídas junto com o transportador, respeitando o destinatário de cada perda.

## Contrato transacional

- A trava de camada continua sendo aplicada somente sobre estado comprometido.
- A liberação do ataque não antecipa Fog of War nem transforma posição provisória em estado definitivo.
- O evento de destruição é publicado no momento em que o combate confirmado aplica a morte.
- O Serviço do Comando mantém o preview cancelável; a mudança afeta apenas o foco inicial da interface.

## Validação

- Build de `Assembly-CSharp.csproj`: **0 erros**.
- `git diff --check` executado sem erros.
- O Torpedo continua configurado para `Submarine/Submerged` e `Naval/Surface`.
- A navegação opcional pelas unidades do Serviço do Comando foi preservada.
