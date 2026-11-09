# Shopping — resposta à parede de artilharia

Estado: **implementado em 09/11/2026; validado por fixture fora do Play**. Falta observar em partida.

## Doutrina aprovada

| Parede inimiga | Resposta preferida | Alternativa por falta de oferta/acesso |
|---|---|---|
| Fogo de suporte / artilheiro combatente terrestre | Ataque aéreo | Assalto terrestre |
| Antiaéreo / antiaéreo combatente terrestre | Assalto terrestre | Ataque aéreo |

Cada inimigo conta uma vez por InstanceId. Artilheiros e antiaéreos são classificados primeiro
pelo papel principal; os demais Assaltos terrestres entram pela classificação de composição existente. Antiaéreo combatente pertence
ao grupo antiaéreo, mesmo satisfazendo também FogoIndireto. A resposta exige arma antitanque,
papel adequado e Artillery como presa primária ou secundária; havendo oferta primária no domínio
escolhido, ela precede a secundária. Apache com papel principal DisruptorDeCaptura fica fora:
sua demanda continua sendo poder de captura. Bombardeiro com papel principal AtaqueAereo entra.

Oferta considera produtor, regras de compra e progressão disponíveis. O acesso terrestre usa
`SectorManager.TryBuildLandMovementDistanceToTargetMap`, com o perfil da unidade, entre a
produtora (ou unidade existente) e pelo menos um alvo do grupo. Não é distância em hexes nem
promessa de transporte futuro. Mapas de acesso são reutilizados dentro da decisão por perfil/alvo.
A falta de caixa não converte a resposta preferida: permanece a política existente de poupança.

## Escala e cobertura

- Normal: uma resposta a cada **três pontos** no total, inclusive pressão mista; teto global de três.
  Artilharia nível 0/1/2+: **1 / 1,5 / 3** pontos. Assalto terrestre nível 0/1/2+:
  **0,5 / 0,75 / 1,5** pontos, no placar que prefere ataque aéreo. Antiaéreos permanecem no
  placar que prefere Assalto, com os pesos de artilharia. Níveis acima de 2 mantêm o peso de 2.
  ArtilheiroCombatente não soma novamente como Assalto. A soma usa quartos de ponto inteiros.
  Assim uma artilharia elite 2, duas elite 1, dois Assaltos elite 2 ou seis Assaltos básicos
  geram uma vaga, antes de descontar cobertura.
- GoGreen (`snapshot.IsInvading`): uma resposta por peça de artilharia, sem esse teto e sem
  multiplicar pelo nível de elite. Assaltos continuam acrescentando sua pressão normal de pontos.
- As vagas são proporcionais aos pontos dos dois grupos. Resto indivisível segue o maior resto; empate
  favorece o grupo menos coberto e depois o menor custo da resposta preferida ofertada.
- Uma peça existente apta e com acesso desconta no máximo uma vaga. Usa primeiro seu confronto
  preferido; cobertura excedente pode atender o outro grupo. A proporção não força comprar
  outra peça quando uma existente já atende a vaga restante.
- Compras no carrinho para composição/progressão também descontam uma vaga da parede quando
  aptas e produzidas de onde há acesso. A mesma compra não desconta os dois grupos.
- As demandas normais de veículos e elite continuam. O compromisso de elite existente conserva
  seu ciclo de poupança, inclusive quando a ameaça original desaparece; não foi criado cancelamento.

## Implementação e validação

`AIShoppingPlanner.Demand.cs`: `BuildArtilleryWallDemands` classifica, divide as vagas e desconta
cobertura; `MatchesWallOffer` preserva a elegibilidade por produtora nos seletores de compra e
poupança; `AddCandidateToRoleShoppingCart` registra cobertura compartilhada. A origem continua
`breakthrough`, integrada ao compromisso de elite existente. `AIShoppingDemand` carrega os
pares de oferta/acesso apenas durante a decisão, sem novo estado persistido.

`tools/ai_test/verificar_demanda_parede_artilharia.cs`: fixture temporária, sem compra confirmada,
cobre parede mista, teto, invasão, preferências, exclusão do Apache, primária/secundária, conversão
nos dois sentidos, caixa insuficiente, unidade existente, carrinho e produtor separado por ilha.
As checagens de acesso preservam as regras de terreno do SectorManager; não modelam vitória em
combate, sobrevivência da resposta ou transporte futuro. Custo em mapas grandes ainda requer
observação em partida, especialmente com muitos perfis e alvos terrestres desconectados.

Extensão de 09/11/2026: a fixture também cobre os pesos por nível, limiares de Assalto sem
artilharia inimiga, duplicação de InstanceId e ArtilheiroCombatente sem dupla contagem.
