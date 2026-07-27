# v4.7.1 — Refactor da AI Asssault and Fire Support 1/4

## Objetivo

Executar a primeira parte do refactor de transporte de Assault e FireSupport,
separando o embarque genérico de combatentes da política especializada de
reboque e segurança da artilharia.

O alcance permanece adjacente nesta etapa. `QueroCaronaService` e
`MelhorEmbarqueService` serão integrados na parte 2/4.

## Fundação comum

Foi criada a entrada:

`TryDecideCombatPassengerTransportAction`

Ela preserva temporariamente:

- consulta adjacente do `PodeEmbarcarSensor`;
- destino de entrega legado;
- distância mínima existente;
- preferência por transportador logístico já avançado;
- construção do mesmo batch de embarque.

A entrada recebe uma política explícita e não interpreta todo passageiro como
artilharia rebocada.

## Políticas

Foram criadas:

- `CombatPassengerTransportPolicy.Assault`;
- `CombatPassengerTransportPolicy.FireSupport`.

### Assault

Usa o embarque genérico e não aplica segurança especializada de artilharia.

### FireSupport

Aplica adicionalmente:

- destino hot;
- retaguarda;
- aliados de suporte;
- rendezvous conservador;
- zona segura de desembarque;
- progressão compatível com posicionamento de fogo.

## FireSupportTransportOutcome

Foi criado o resultado:

- `Handled`;
- `NoAction`;
- `TransportRejected`.

Isso permite distinguir:

- operação de transporte materializada;
- ausência de necessidade ou oportunidade;
- necessidade bloqueada por contexto ou segurança.

Nesta etapa, rejeição do transporte FireSupport ainda libera provisoriamente o
fallback Assault do híbrido. A criticidade será refinada nas próximas partes.

## Artilheiro Combatente

O roteador passa a tratar explicitamente o híbrido:

1. tenta ataque FireSupport;
2. tenta transporte com política FireSupport;
3. encerra quando o resultado é `Handled`;
4. registra `TransportRejected`;
5. libera provisoriamente o fallback Assault.

Antes, o híbrido tentava apenas o ataque FireSupport e caía diretamente no
Assault, sem tentar o transporte especializado.

## Segurança TOW

As regras:

- `CanFireSupportTowEmbarkSafely`;
- drop seguro na rota;
- rendezvous conservador;
- apoio aliado;
- retaguarda;

foram retiradas do antigo fluxo universal do Assault e colocadas no partial de
FireSupport.

O termo reboque deixa de descrever o embarque comum de tanque, trem, navio ou
outro passageiro combatente.

## Compatibilidade preservada

O comportamento físico existente continua usando:

- `PodeEmbarcarSensor`;
- regras de `transportSlots`;
- classe;
- domínio e camada;
- skills;
- bloqueios;
- capacidade;
- exclusividade;
- movimento restante.

Não foram criados branches específicos para trem, navio, APC ou helicóptero.

## Arquitetura transacional

- A fundação apenas consulta opções e constrói o batch existente.
- Nenhum scan altera posição, recursos, ocupação, FOW ou detecção.
- A política FireSupport apenas filtra oportunidades.
- Nenhuma consulta marca `HasActed`.
- O compromisso permanece no fluxo explícito que retorna a
  `CursorState.Neutral`.

## Arquivos principais

- `Assets/Scripts/Match/AI/AIController.Router.cs`
- `Assets/Scripts/Match/AI/Units/Assault/AIController.Assault.cs`
- `Assets/Scripts/Match/AI/Units/Assault/AIController.Assault.Embark.cs`
- `Assets/Scripts/Match/AI/Units/Fire Support/AIController.FireSupport.Embark.cs`
- `Assets/Scripts/Match/AI/Units/Logistics/AIController.Logistic.Shuttle.cs`

## Próxima etapa

A parte 2/4 deve:

- consultar `QueroCaronaService` uma única vez;
- consumir `MelhorEmbarqueService`;
- preservar passageiro, transportador, LZ, slot, envelope, custos e nota;
- aplicar a política Assault ou FireSupport sobre as opções;
- manter a materialização adjacente para a parte 3/4.

## Verificação

- auditoria dos chamadores do embarque antigo;
- auditoria do branch do Artilheiro Combatente;
- auditoria da localização das regras FireSupport;
- auditoria do contrato transacional;
- `dotnet restore Assembly-CSharp.csproj`;
- `dotnet restore Assembly-CSharp-Editor.csproj`;
- `dotnet build Assembly-CSharp.csproj --no-restore`;
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`;
- `git diff --check`;
- resultado: runtime e editor concluídos com 0 erros.
