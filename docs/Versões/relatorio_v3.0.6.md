# v3.0.6 - AI Intel parte 2

Esta versao consolida a segunda rodada de Intel aerea e corrige varios efeitos colaterais que apareceram quando EWACS, Radar Movel, Fire Support e shopping passaram a interagir com o mesmo campo de batalha.

## Intel em retaguarda

- O EWACS deixou de usar apenas a media geral dos aliados para decidir retaguarda.
- O calculo agora identifica a faixa de vanguarda formada pelos combatentes aliados mais proximos do objetivo.
- A posicao ideal de Intel fica cerca de 2 hexes atras dessa faixa, proxima da linha aliada.
- Posicoes muito isoladas recebem penalidade mesmo quando estao "atras" do ponto de vista geometrico.
- Aeronaves de Intel evitam terminar sobre construcoes aliadas quando existe alternativa boa sobre terreno.
- O log de Intel agora mostra `gap`, `ally` e `iso` para explicar retaguarda, proximidade aliada e isolamento.

## Vacate de base

- O libera-producao passou a respeitar melhor postura conservadora.
- Unidades de Fire Support e Intel tentam primeiro cumprir seu papel antes de simplesmente sair da construcao.
- SAM/Fire Support podem procurar ataque ou reposicionamento util antes do fallback generico de liberar base.
- O reposicionamento conservador usa coesao e linha de retaguarda, reduzindo casos em que a unidade "da no pe" sem necessidade.

## Fire Support e apoio de setor

- Reposicionamento de Fire Support atribuido passou a considerar melhor o apoio vivo no setor.
- O fallback para apoiar objetivo tenta se aproximar do grupo que esta de fato executando a captura, nao apenas do nome do setor.
- Foram adicionados logs mais ricos para entender bloqueios de range-step, screen e reposicionamento.
- Comportamentos conservadores ficaram menos propensos a atravessar a vanguarda quando existe linha aliada utilizavel.

## Shopping e composicao

- A demanda de EWACS foi limitada para evitar compra duplicada desnecessaria em mapas pequenos.
- Radar Movel continua fazendo parte de `Air Intel`, como fallback barato para revelar o ceu quando EWACS nao e viavel.
- A reserva de combate aereo passou a considerar custos por papel, evitando que um custo barato de caca bloqueie bombardeiro ou Apache indevidamente.
- O bombardeiro ganhou espaco como peca ofensiva de pressao, nao apenas como luxo depois de uma frota pronta.
- Tanque elite recebeu incentivo maior quando a AI tem economia e plano ofensivo para romper stalemate.

## Iniciativa e transporte

- EWACS e Radar Movel receberam prioridade alta de iniciativa para iluminar alvos cedo.
- Helicoptero/transportador carregado evita lutar antes de concluir transporte quando a carga e relevante.
- O evac aereo continua util, mas foi tratado com mais cuidado para nao pousar no meio da pressao sem necessidade.

## Ferramenta de retaguarda

- Criada ferramenta de editor `Tools/Utils/Retaguarda`.
- A janela ajuda a visualizar a faixa de vanguarda e a area de retaguarda esperada.
- A ferramenta serve para comparar o comportamento da AI com o desenho tatico visto no mapa.

## Resultado esperado

Intel aerea deve parecer menos solta e mais conectada ao corpo principal. O EWACS continua evitando vanguarda, mas agora prefere uma retaguarda util: atras da linha aliada, perto o suficiente para cobrir o grupo, e sem ficar parado em HQ ou isolado na borda do mapa quando existe espaco melhor.

## Validacao

- `dotnet build Assembly-CSharp.csproj`
- Resultado: 0 erros.
- Avisos: warnings obsoletos de Unity ja existentes no projeto.
