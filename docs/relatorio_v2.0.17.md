# Relatorio de Atualizacao - v2.0.17

## AI Transporter

Esta versao introduz a camada de transporte da IA, permitindo que transportadores busquem capturadores, carreguem passageiros e pressionem objetivos distantes com mais intencao.

## Em uma frase

A IA agora consegue usar transportadores como parte do plano de captura, levando capturadores aos setores certos e evitando que capturas oportunistas atrapalhem quem ja consegue cumprir o objetivo no turno.

## O que isso trouxe na pratica

- Transportadores com slot formal no plano passam a buscar o capturador atribuido ao mesmo setor.
- Transportadores livres podem agir como shuttle, escolhendo um passageiro util e se posicionando para pickup.
- Transportadores com carga passam a decidir desembarque com base no objetivo do passageiro.
- Capturadores adjacentes a transportadores validos podem embarcar antes de seguir a marcha a pe.
- A iniciativa da Fase 2 considera transportadores com pickup valido para agir antes dos capturadores quando isso destrava o plano.
- Capturas oportunistas agora cedem para capturador do plano apenas quando ele realmente consegue capturar aquele predio no turno.
- Quando um predio oportunista esta reservado, o capturador continua procurando outro predio alcancavel em vez de desistir da oportunidade.

## Principais melhorias

1. Transportador designado
- Transportadores atribuidos a um objetivo procuram o capturador daquele setor.
- Quando o passageiro existe e ainda pode agir, o transportador tenta se posicionar adjacente para pickup.
- Se nao houver passageiro valido, o transportador pressiona o alvo do setor para continuar contribuindo.

2. Shuttle livre
- Transportadores sem slot formal e vazios procuram candidatos de embarque dentro do alcance pratico.
- A escolha do passageiro usa o alvo de objetivo da unidade para evitar viagens sem valor.
- Quando ja esta adjacente ao passageiro, o transportador segura posicao para permitir o embarque.

3. Courier com carga
- Transportadores carregados avaliam celulas de desembarque para aproximar o passageiro do objetivo.
- O desembarque considera o alvo do passageiro, nao apenas a posicao do transportador.
- Quando nao existe desembarque util, o transportador continua se movendo em direcao ao objetivo.

4. Embarque do capturador
- Capturadores checam transportadores adjacentes antes de decidir avanco normal.
- A preferencia fica com transportador atribuido ao mesmo setor, quando existe.
- Isso permite que o plano use transporte sem depender de comportamento manual ou de coincidencia de ordem.

5. Iniciativa e prioridade
- Transportadores com pickup valido entram em grupo de iniciativa antecipado.
- Essa ordenacao evita que o capturador ande embora antes do transportador conseguir preparar o embarque.
- Capturadores, batedores e reparo continuam usando as regras existentes de corredor e bloqueio.

6. Captura oportunista com respeito ao plano
- Oportunismo nao rouba predio de um capturador atribuido que consegue capturar naquele turno.
- Se o capturador atribuido esta longe demais, o predio continua liberado para oportunismo.
- Quando o primeiro predio oportunista esta reservado, a IA pula aquele predio e testa os proximos alcancaveis.

## Bloco tecnico curto

- Adicionados `AIController.Transportador.cs`, `AIController.Transportador.Assigned.cs`, `AIController.Transportador.Courier.cs` e `AIController.Transportador.Shuttle.cs`.
- Adicionado `AIController.Capturer.Embark.cs` para decisao de embarque de capturadores.
- Ajustado `AIController.Router.cs` para rotear unidades transportadoras para a nova camada de decisao.
- Ajustado `AIController.Initiative.cs` para antecipar transportadores com pickup valido.
- Ajustado `AIController.Capturer.cs` e `AIController.Capturer.Helpers.cs` para busca de captura oportunista com reserva por capturador do plano.
- Ajustados dados/editor de unidade para suportar configuracoes relacionadas ao papel de transportador.

## Resultado

Versao preparada como pacote `AI Transporter`, focada em transformar transporte em comportamento de plano: buscar, carregar, entregar e liberar capturadores sem quebrar a logica de captura dos setores.
