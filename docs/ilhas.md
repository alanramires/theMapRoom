# Ilhas informativas no BeachManager

O BeachManager cataloga massas terrestres independentemente da existencia de praias. Conecta os seis vizinhos do grid hexagonal e conta cada celula uma vez. Por padrao aceita os TerrainTypeData com IDs plains, forest, mountain e beach; a lista Terrenos das ilhas permite configurar outros terrenos. Agua, lacunas e overlays de estrada/ponte nao conectam ilhas. Isto descreve geografia, nao acessibilidade de uma unidade.

O Inspector lista nome, ID, total de tiles, contagem por terreno, praias e setores. Com praia, o nome deriva da primeira praia militar na ordem existente. Sem praia, usa o primeiro setor por valor numerico (campanha antes de Base0+). Sem setor, usa coordenadas. Homonimos recebem coordenadas para diferenciar. Posse e ordem de captura nao mudam o nome.

API: Islands, TryGetIslandAtCell(cell, out island), GetIslandsForSector(sector). Setores pertencem por celula de cada construcao, nao pelo centroide; um setor pode aparecer em varias ilhas. Os IDs derivam do mapa e das celulas ordenadas. O catalogo e isolado por cena e reconstruido sob demanda; mudancas do fingerprint topologico, das referencias de terreno ou das posicoes/setores das construcoes invalidam o levantamento. A reconstrução manual existente do BeachManager tambem invalida as ilhas. Para editar a geografia, reconstrua primeiro o BoardTopologyIndex, se presente.

Nenhuma regra de compras, reserva de elite, defesa ou transporte foi alterada. A futura compra de tanque ainda precisa verificar caminhos com o perfil real de movimento.

Validacao: tools/ai_test/verificar_ilhas.cs, executado fora do Play, usa uma cena temporaria descartada e verifica conectividade hexagonal, quatro terrenos, exclusao de agua, ilhas sem praia, nome pelo setor Alpha (valor zero), setor em varias ilhas, nomes distintos, IDs deterministas e limpeza do cache. No recorte A_IA_Q3 do fixture de teste foram encontrados quatro componentes terrestres: 37, 82, 57 e 31 tiles. A cena de autoria inteira contem outros quadrantes; sua contagem global nao e a contagem do mapa recortado da partida.
## Map Helper e cores na autoria

O Inspector do BeachManager possui Abrir Map Helper, mostra o quadrante ativo compartilhado com Axis Editor e permite Colorir ilhas no Scene View. Cada ilha tem cor e identificador I1, I2 etc. iguais na lista e no mapa; Focar enquadra a ilha e destaca seus tiles. A pintura e apenas um overlay de editor, sem alterar os tiles ou sujar a cena.

O recorte e aplicado ANTES da descoberta das ilhas, separando ligacoes por terra que existam fora do quadrante. Praias desenhadas/listadas tambem respeitam o recorte. A consulta GetIslandsInRegion(RectInt) usa cache separado e preserva o catalogo global. BeachManager apenas acompanha o recorte, sem disputar selecao com Map Helper ou Axis Editor. Em Play, o Inspector usa o mapa da partida inteira.

Teste adicional: tools/ai_test/verificar_ilhas_recorte_fixture.cs valida Q3 (4 ilhas e 207 tiles), nomes locais Lima/Delta/Alpha/Golf, troca de recorte e preservacao do catalogo global.

