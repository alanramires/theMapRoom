# v4.7.4 — Refactor da AI Asssault and Fire Support 4/4

## Objetivo

Concluir o refactor conjunto de Assault e FireSupport, fechando o roteamento
dos papéis híbridos e impedindo que uma rejeição de segurança seja contornada
por uma segunda política de transporte.

## Roteamento híbrido final

`ArtilheiroCombatente` e `AntiaereoCombatente` seguem a mesma ordem:

1. tentar ataque pelo pipeline FireSupport;
2. tentar transporte pela política FireSupport;
3. quando houver ação, materializar o batch escolhido;
4. quando o transporte for rejeitado, liberar o comportamento Assault sem
   repetir a tentativa de carona;
5. quando não houver ação Assault, continuar pelos demais papéis do roteador.

O fallback continua permitindo combate, escolta, avanço e reposicionamento. A
supressão atinge somente a nova consulta de transporte.

## Segurança sem bypass

Antes desta etapa, o híbrido podia ter a carona recusada pela política
FireSupport e, logo depois, consultar a mesma oportunidade pela política
Assault, que é menos restritiva.

Foi introduzido o controle efêmero `suppressAssaultTransportFallback`.

Quando FireSupport retorna `TransportRejected`:

- o motivo de segurança permanece soberano;
- Assault não chama novamente `QueroCarona` e `MelhorEmbarque`;
- a unidade ainda pode cumprir uma ação normal de Assault;
- nenhuma flag é gravada na unidade ou no estado confirmado do tabuleiro.

## Antiaéreo Combatente

O desvio que encaminhava uma unidade Assault antiaérea para o controller
terrestre especializado foi removido do ponto de entrada.

Com isso, `AntiaereoCombatente` permanece no mesmo pipeline híbrido do
`ArtilheiroCombatente`. Sua diferença continua limitada à seleção de alvos
aéreos, conforme consolidado na parte 3/4.

O `Shopping Pressure` não foi alterado.

## Diagnóstico de transporte

Os logs da decisão passageiro–transportador agora registram, quando há um
vencedor:

- transportador;
- LZ e slot;
- envelope Tactical ou Operational;
- estado da rota do passageiro;
- disposição normal ou emergencial;
- custo do passageiro;
- custo do transportador;
- nota final após a política consumidora.

Quando não há vencedor, o diagnóstico informa:

- quantidade de transportadores compatíveis avaliados;
- quantidade de rendezvous oferecidos por `MelhorEmbarque`;
- quantidade de opções rejeitadas pela política especializada.

Isso permite distinguir ausência de capacidade física, ausência de encontro e
rejeição operacional sem reimplementar as consultas dos serviços.

## Arquitetura transacional

- O controle de fallback existe somente durante a chamada do roteador.
- Nenhuma decisão grava estado persistente na unidade.
- `QueroCaronaService` e `MelhorEmbarqueService` permanecem consultas puras.
- Logs e contadores não alteram posição, ocupação, FOW, detecção ou recursos.
- Somente o controller materializa batches.
- O compromisso permanece no fluxo explícito que retorna a
  `CursorState.Neutral`.

## Arquivos

- `Assets/Scripts/Match/AI/AIController.Router.cs`
- `Assets/Scripts/Match/AI/Units/Assault/AIController.Assault.cs`

## Resultado final do refactor

Ao término das quatro partes:

- Assault e FireSupport consomem `QueroCarona` e `MelhorEmbarque`;
- passageiros e transportadores podem progredir até uma LZ comum;
- slots, domínios e locais válidos continuam definidos pelos dados e sensores;
- FireSupport preserva suas regras próprias de reboque, retaguarda e drop;
- Artilheiro Combatente e Antiaéreo Combatente compartilham o pipeline híbrido;
- Antiaéreo e Antiaéreo Combatente diferem apenas pela seleção de alvo;
- rejeições especializadas não podem ser contornadas por fallback;
- a decisão permanece separada da materialização e do compromisso.

## Verificação

- `dotnet build Assembly-CSharp.csproj --no-restore`;
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`;
- `git diff --check`.
