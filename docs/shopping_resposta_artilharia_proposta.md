# Shopping — resposta à parede de artilharia

Estado: **decisões fechadas em conversa, ainda não implementadas**. Registro de 09/11/2026.

A demanda de resposta à artilharia continua existindo. O que muda é quem deve atendê-la;
não deve ficar presa a Ataque Aéreo elite nem confundir essa resposta com Disruptor de Captura.

## Preferência e alternativa viável

| Parede inimiga | Resposta preferida | Alternativa |
|---|---|---|
| Fogo de suporte / artilheiro combatente | Ataque aéreo | Assalto terrestre, como tanques regulares, quando não houver ataque aéreo adequado disponível |
| Antiaéreo / antiaéreo combatente (SAM, AAA) | Assalto terrestre com acesso ao alvo | Ataque aéreo, inclusive Bombardeiro, quando o acesso ou a oferta terrestre não permitir a resposta preferida |

A preferência não é um bloqueio. No mapa ilhado, o autor permite Bombardeiro contra SAM/AAA:
“ele não é a resposta ideal, mas é a que tem”. O inverso também vale: mapa sem Aeronáutica,
ou com Aeronáutica sem oferta de ataque aéreo, continua podendo responder com tanques regulares.

Uma parede mista pode pedir resposta mista. A resposta deve considerar oferta e acesso real;
ter uma produtora aérea, por si só, não prova que exista ataque aéreo adequado disponível.
Disruptor de Captura continua respondendo ao poder de captura, com sua doutrina própria.

## Próxima implementação

A marca T10 mostrou a demanda atual `breakthrough` em `AIShoppingPlanner.Demand.cs`, que
conta a classe `Artillery` e pede `AtaqueAereo` elite com essa presa em prioridade primária.
Essa implementação ainda não foi trocada.

Antes de alterar, reaproveitar os filtros de papel, preferências de alvo, catálogo, acesso
terrestre e cobertura do shopping. A divisão de cobertura entre as respostas, contagem de
unidades com múltiplos rótulos e persistência de compromissos antigos precisam ser conferidas
no código e nos testes; este registro não apresenta essas partes como implementadas.