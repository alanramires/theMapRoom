# v3.0.2 - AI Estavel

Esta versao consolida a AI em um ponto estavel e jogavel. O foco nao foi adicionar uma regra isolada, mas fechar varios casos compostos em que papeis taticos competiam entre si: assalto, captura, artilharia, transporte, logistica, reparo e compra agora conversam melhor dentro do plano.

## AI tatica

- Melhor separacao entre comportamento de assalto, captura, fogo de suporte, transporte, logistica e reparo.
- Refinos no planner para respeitar melhor anchors, rally points, bases, setores recem-capturados e objetivos comuns.
- Unidades passam a ceder espaco e objetivos quando outra peca do plano cumpre melhor a funcao do setor.
- Decisoes rogue foram ajustadas para evitar dispersao desnecessaria sem bloquear oportunidades locais boas.
- O comportamento de fim de jogo e invasao ficou mais consistente quando a forca ja esta proxima do HQ inimigo.

## Artilharia e transporte

- Artilharia passa a ser tratada como ativo especial: procura tiro bom, evita vanguarda desnecessaria e busca posicao coesa quando nao ha alvo util.
- Transportes com carga de fogo de suporte evitam atravessar a frente como se fossem unidades de assalto.
- Courier e shuttle receberam ajustes para soltar carga em retaguarda util quando a entrega direta no objetivo nao e segura.
- O Tanque Z e outras unidades hibridas foram melhor enquadrados entre tiro de suporte e pressao de assalto.

## Reparo e sobrevivencia

- Unidades em reparo agora diferenciam melhor entre recuar, segurar base/HQ sob ameaca e lutar quando nao existe rota segura.
- O modo de combate de unidades danificadas passou a usar simulacao de troca em vez de escolher alvo por heuristica simples.
- Repair considera melhor anchors bloqueadas, ameaca local e valor militar antes de decidir entre fuga e combate.

## Logistica e compras

- Suprimento e restock ficaram mais alinhados com a necessidade real da frente.
- Compra da AI responde melhor a deficits de composicao, falta de suporte, pressao blindada, defesa de base e demanda logistica.
- A AI evita excesso de algumas pecas de apoio quando o mapa ja tem capacidade suficiente.

## Debug e estabilidade

- Logs de decisao ficaram mais uteis para diagnosticar por que uma unidade atacou, moveu, aguardou, embarcou, desembarcou ou entrou em reparo.
- O fluxo de turno ficou mais confiavel com commits de mundo entre acoes da AI.
- Ajustes de menu: `Backspace` agora tambem funciona como alternativa ao `Escape` para abrir, voltar e sair do menu no estado neutro.

## Resultado

A AI chegou a um estado mais coeso: ela ja produz pressao de time, protege ativos caros, usa transporte com mais intencao, recua ou luta com unidades quebradas conforme o contexto e mantem artilharia em papel mais adequado. Ainda ha espaco para evoluir com destacamentos, spotters e ranking unificado de combate, mas a base atual esta estavel o suficiente para servir como marco de versao.
