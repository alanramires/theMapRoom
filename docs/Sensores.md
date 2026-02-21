# Sensores

Documento de referencia e roadmap dos sensores taticos do scanner.

## Visao geral

No fluxo de scanner, as acoes disponiveis hoje sao:

- `A`: ataque (`PodeMirar`)
- `E`: embarque (`PodeEmbarcar`)
- `L`: operacao aerea (pouso/decolagem; nao e sensor classico, mas entra no mesmo painel)

Orquestrador:

- `Assets/Scripts/Sensors/SensorHandle.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Sensors.cs`

---

## Status macro (metade para release de sensores)

- Sensores implementados hoje: `PodeMirar`, `PodeEmbarcar`
- Fluxo especial ja existente (nao sensor): `OperacaoAerea (L)`
- Metade: painel e contrato de "validos + invalidos com motivo"
- Proximo lote: `Desembarcar`, `Capturar`, `Reparar`, `Fundir`, `Suprir`, `Transferir`, `ServicosCidade`, `MoverSomente`

---

## 1) PodeMirar (`A`)

Arquivos:

- `Assets/Scripts/Sensors/PodeMirarSensor.cs`
- `Assets/Scripts/Sensors/PodeMirarTargetOption.cs`
- `Assets/Scripts/Sensors/PodeMirarInvalidOption.cs`

Objetivo:

- listar alvos validos para ataque
- explicar invalidacoes (municao, layer, LoS/LdT, etc)
- prever revide do defensor

Checklist de validacao (pode mirar?):

- quem: alvo em unidade inimiga
- tudo validado: atacante apto e acao permitida no estado atual
- `LoS` / `LdT`: precisa linha de visao e linha de tiro conforme regra da arma
- arma usada: selecionar arma com prioridade e alcance valido
- dominio: compatibilidade `ground/sea/air` atacante x defensor
- municao: municao suficiente e tipo de municao compativel

Saida:

- validos: `PodeMirarTargetOption`
- invalidos: `PodeMirarInvalidOption`

Debug:

- `Tools/Sensors/Simular Pode Mirar`

---

## 2) PodeEmbarcar (`E`)

Arquivos:

- `Assets/Scripts/Sensors/PodeEmbarcarSensor.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarOption.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarInvalidOption.cs`

Objetivo:

- listar transportadores adjacentes elegiveis
- validar contexto, slot, custo e restricoes
- explicar invalidacoes por motivo

Checklist de validacao (pode embarcar?):

- transportadores proximos: range 1 adjacente
- vagas validadas e disponiveis: capacidade livre no slot
- aliado e transportador: `isTransporter=true` e mesmo time
- contexto de embarque: `allowedEmbarkTerrains` / `allowedEmbarkConstructions`
- compatibilidade passageiro x slot: camada, classe e skills
- inclui porta-avioes: carrier naval entra como transportador elegivel para unidade aerea compativel

Saida:

- validos: `PodeEmbarcarOption`
- invalidos: `PodeEmbarcarInvalidOption`

Debug:

- `Tools/Sensors/Pode Embarcar`

---

## 3) PodeDesembarcar

Objetivo:

- listar transportadores com carga apta a desembarcar
- validar local propicio e regras da unidade descarregada

Checklist de validacao (pode desembarcar?):

- transportador com carga
- ao menos uma unidade na carga com estado desembarcavel
- tile de destino propicio para a unidade descarregada
- bloqueios de ocupacao, dominio e custo de movimento respeitados

Status:

- roadmap (proximo lote)

---

## 4) PodeCapturar

Objetivo:

- detectar oportunidade de captura em construcao inimiga ou neutra

Checklist de validacao (pode capturar?):

- unidade ativa e infantaria (ou classe com permissao de captura)
- unidade posicionada sobre construcao inimiga ou neutra capturavel
- estado da partida permite acao de captura neste turno

Status:

- roadmap (proximo lote)

---

## 5) PodeReparar

Objetivo:

- detectar reparo por infantaria em construcao aliada danificada

Checklist de validacao (pode reparar?):

- unidade ativa com perfil de reparo
- construcao alvo aliada e danificada
- recursos/custos e travas de turno validos

Status:

- roadmap (proximo lote)

---

## 6) PodeFundir

Objetivo:

- detectar unidades similares proximas para fusao

Checklist de validacao (pode fundir?):

- mesma faccao, tipo e classe da unidade
- unidade parceira adjacente e elegivel
- fusao recupera hp/movimento/municao dentro dos limites maximos
- nenhuma das duas unidades bloqueada por estado de turno

Status:

- roadmap (proximo lote)

---

## 7) PodeSuprir

Objetivo:

- detectar supridores ao lado de aliados enfraquecidos

Checklist de validacao (pode suprir?):

- unidade ativa com papel de supridor
- aliado adjacente abaixo de limite de municao/combustivel/hp configurado
- acao de suprimento permitida no estado atual

Status:

- roadmap (proximo lote)

---

## 8) PodeTransferir

Objetivo:

- detectar transferencia em unidades hub dentro de construcoes hub

Checklist de validacao (pode transferir?):

- unidade ativa marcada como hub (ex: navio tanque)
- unidade sobre construcao hub compativel (ex: porto)
- recurso de transferencia disponivel (ex: oleo diesel)
- regra de capacidade/estoque nao excedida

Status:

- roadmap (proximo lote)

---

## 9) PodeUsarServicosCidade

Objetivo:

- detectar servicos de cidade para unidade sobre construcao aliada

Checklist de validacao (pode usar servicos da cidade?):

- unidade posicionada sobre construcao aliada
- construcao oferece servico: reparo, rearmamento, reabastecimento
- unidade precisa de pelo menos um servico ofertado
- custo/restricao de turno validos

Status:

- roadmap (proximo lote)

---

## 10) PodeAterrissarOuDecolar (`L`)

Arquivos:

- `Assets/Scripts/Units/Rules/AircraftOperationRules.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Sensors.cs`

Objetivo:

- validar pouso/decolagem por tipo de unidade aerea e tipo de tile

Checklist de validacao (pode aterrissar / decolar?):

- VTOL: estradas e planicie (ou tiles permitidos por dados)
- VTOSL: praia e tiles marinhos permitidos para sea landing
- aeroporto e construcoes que aceitam pouso (ex: plataforma de petroleo com heliponto)
- em aeroportos, pouso como opcao preferida no `Enter`

Observacao:

- `L` nao vem de `SensorHandle`, mas participa do mesmo painel de scanner.
- pouso/decolagem e decidido por contexto do hex (construction/structure/terrain).

Status:

- fluxo existente no painel, fora do conjunto de sensores implementados

---

## 11) MoverSomente

Objetivo:

- permitir reposicionamento quando nenhum sensor de acao for desejado

Checklist de validacao (apenas mover?):

- unidade com movimento restante
- destino navegavel e dentro do custo de movimento
- acao explicita de reposicionamento sem abrir painel de acao

Status:

- roadmap (proximo lote)

---

## Padrao de contrato para novos sensores

Todos os novos sensores devem seguir o mesmo contrato:

- lista de validos com payload acionavel
- lista de invalidos com motivo explicito
- debug tool para simular contexto isolado
- integracao no `TurnStateManager.Sensors` sem quebrar `A/E/L`

---

## Checklist rapido de troubleshooting

1. Confirme `CursorState` (`MoveuParado` / `MoveuAndando`), pois muda sensores e custo.
2. Para `A`, veja a lista de invalidos do `PodeMirar` e motivo.
3. Para `E`, veja a lista de invalidos do `PodeEmbarcar` e motivo.
4. Para `L`, verifique restricoes de classe/skill/modo nos dados de construction/structure/terrain.
5. Para novos sensores, valide primeiro o contrato `validos + invalidos + motivo`.
