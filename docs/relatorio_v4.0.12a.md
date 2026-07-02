# v4.0.12a - AI Retaguarda, Vanguarda e Pode Enxergar

Esta revisao consolida a classificacao dinamica de vanguarda e retaguarda da IA, amplia a ferramenta de diagnostico tatico e melhora a estabilidade da linha de visao em casos de rasante sobre obstaculos.

## Retaguarda e vanguarda da IA

- A direcao da frente em runtime passa a usar o centro da massa das unidades inimigas conhecidas.
- O HQ ou objetivo recebido pelo chamador permanece como fallback quando nao ha contato inimigo disponivel.
- Artilharia, suporte, intel e verificacoes de retaguarda segura compartilham a mesma referencia dinamica.
- A linha de frente continua formada por unidades com papel de Capturador ou Assalto, excluindo unidades de apoio da geometria principal.

## Ferramenta Retaguarda

- A janela `Tools > Utils > Retaguarda` ganhou camadas separadas para retaguarda, vanguarda, flancos, linha de combate, unidades da linha, inimigos e referencia de direcao.
- O Tilemap do tabuleiro pode ser detectado a partir das unidades e construcoes da cena, evitando o uso acidental do mapa de ameacas.
- A ferramenta usa a massa inimiga como referencia dinamica e permite referencia manual como alternativa.
- A leitura da cena usa a posicao atual dos objetos no Edit Mode, permitindo recalcular depois de reposicionar unidades.
- Avancos isolados podem ser distinguidos da cabeca principal da linha durante a simulacao.

## Forward Observer Spots

- Construcoes marcadas como `Forward Observer Spot` podem ser selecionadas diretamente na janela ou na Scene View.
- O spot selecionado usa a visibilidade real da unidade posicionada nele por meio de `CollectVisibleCellsForFogOfWar`.
- A area visivel considera alcance, terreno, elevacao e LoS do mesmo sensor usado pelo jogo.
- O HQ inimigo define a direcao local da frente para aquele spot.
- A ferramenta identifica a borda avancada da area visivel e informa quando uma unidade terrestre de linha ja cobre essa borda, tornando o observador liberavel.

## Pode Enxergar e LoS

- A verificacao de rasante ganhou uma pequena tolerancia numerica para evitar bloqueios instaveis quando o topo do obstaculo coincide praticamente com a linha de visao.
- A altura da LoS em cada hex intermediario usa a distancia real projetada sobre a reta entre origem e alvo.
- Hexes empatados geometricamente deixam de receber alturas diferentes apenas por sua ordem na lista de intersecoes.
- O resultado preserva a visibilidade sobre obstaculos no limite sem permitir que terrenos efetivamente acima da linha deixem de bloquear.

## Assets

- O arquivo de design de unidades em PSD foi atualizado junto com esta revisao.

## Validacao

- `Assembly-CSharp.csproj`: build sem erros.
- `Assembly-CSharp-Editor.csproj`: build sem erros.
