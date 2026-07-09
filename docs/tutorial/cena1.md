# História 1 — Aprendendo a Atirar

## Abertura

Sargento:
Bem-vindo, recruta. Já encheu o bucho com a comida da caserna e tomou o chá brochante do tonel de óleo diesel?

<o sistema revela/spawna o Soldado do jogador>

Sargento:
Recruta Ryan, certo? Muito bem. Você é o número 17. Em fila, soldado.

<dois outros soldados aparecem ao lado, com nomes aleatórios. Eles já estão marcados como "já agiram". A barra de passar turno permanece desativada.>

Sargento:
Esses dois aí já cumpriram a parte deles. Agora é você.

Enquanto eu estiver falando, ninguém passa turno, ninguém dá uma de turista e ninguém aperta botão por curiosidade. Entendido?

---

## Step 1 — Conhecer a unidade

Objetivo do sistema: fazer o jogador observar a unidade e inspecionar um aliado.
Validação: inspecionar uma unidade aliada (evento de inspeção — o zoom é só ambientação, não valida).

Sargento:
Antes de puxar o gatilho, primeiro é preciso conhecer a si mesmo.

Meu Velho falava algo nessa linha, mas ele provavelmente não precisava lidar com recruta sonolento.

Olhe bem para você e seus companheiros.
Aproxime o zoom usando a bolinha do mouse ou o toque do celular.

<o sistema aguarda a bolinha do mouse?>

Sargento:
Essa informação é persistente. Ela mostra os dados importantes do combatente.

Está vendo o coração?

Ele indica sua capacidade de luta.
Quando está cheio, o esquadrão combate com força total.
Quando esvazia, sua força de tiro cai junto.

A sua figura em campo não representa um soldado sozinho.
Ela representa 10 soldados lutando juntos no mesmo setor.

Menos soldados atirando, menos chumbo indo para o outro lado.

Sargento:
Agora olhe a barra laranja.

Essa é sua autonomia, sua capacidade de marcha.
Se isso zerar, você não morre, mas fica estacionário no campo, feito poste com capacete.

Sargento:
E essa barra azul é munição.

Se ela zerar, você não atira.
E soldado sem munição vira decoração de cemitério.

Caixão e vela preta.

Sargento:
Mas olhar por cima não é conhecer.

Inspecione um dos seus companheiros de fila. Abra a ficha dele.

<o sistema espera o jogador inspecionar uma unidade aliada>

Sargento:
Essa é a ficha completa do combatente: arma, alcance, capacidade de marcha. Tudo o que ele é está aí. Note que o soldado move 3, e o rifle tem alcance de 1, por isso tem esse simbolo de mira na quarta casa, indica o alcance maximo de onde ele combate em 1 rodada. mas você pode atacar qualquer alvo estando parado. Isso é útil pra vc saber onde se posicionar com segurança em relação ao inimigo que vc está caçando ok?

E repare: esses dois já agiram neste turno, mas a ficha continua aberta para leitura.
Informação não tira folga, recruta.

Sargento:
Muito bem. Você já sabe olhar para uma unidade sem babar no mapa.

É um começo.

---

## Step 2 — Selecionar Ryan

Objetivo do sistema: jogador clicar/tocar no Soldado Ryan.

Sargento:
Agora selecione você mesmo, recruta Ryan.

<o sistema espera o jogador selecionar o Soldado Ryan>

<ao selecionar, aparece a área de movimento>

Sargento:
Está vendo essa área marcada?

Ela mostra até onde você pode ir neste turno.

Você anda 3 casas em terreno normal.
Mas cada movimento consome autonomia.

Marchar cansa, soldado. Até no treinamento de fuga e evasão da AMAN.

---

## Step 3 — Movimento e terreno

Objetivo do sistema: mover Ryan até a bandeira no morro.

<o sistema marca uma bandeira em um hex de montanha ou terreno elevado>

Sargento:
Está vendo aquela bandeira?

Vá até lá.

Mas preste atenção: terreno diferente cobra preço diferente.

Planície é fácil.
Floresta atrasa veiculos, mas não influencia a infantaria. Aqui é CIGS!
Montanha cobra mais da perna.

Para a infantaria, subir montanha custa mais esforço.
Uma casa de montanha consome 2 de autonomia.

É só uma casa no mapa, mas suas pernas discordam.

Sargento:
Agora mova até a bandeira.

Um, dois! Um, dois!
Acelerado, recruta!

<o sistema espera o jogador mover Ryan até o hex indicado>

Ao chegar:

Sargento:
Bom. Você chegou.

Não foi bonito, mas foi útil.

---

## Step 4 — Manter posição

Objetivo do sistema: ensinar que confirmar sem mover também é ação válida.

Sargento:
Agora aprenda uma coisa importante: nem toda ordem é avanço.

Às vezes, a melhor decisão é manter posição.
Você já está onde precisa estar. Não invente moda.

Fique aí parado e aprenda: soldado que anda demais morre cansado.
Soldado que segura o ponto obriga o inimigo a pensar.

Confirme sua posição atual.

<o sistema espera o jogador usar MANTER POSIÇÃO ou confirmar sem mover>

Sargento:
Muito bem.

Manter posição não é ficar parado por medo.
É segurar o ponto certo até a hora certa.

---

## Step 5 — Primeiro alvo

Objetivo do sistema: revelar/spawnar inimigo simples em alcance válido.

<um inimigo simples aparece em um hex próximo, em alcance válido>

Sargento:
Contato à frente e ele ainda não te viu.

Esse alvo está no seu alcance.

Não precisa fazer discurso.
Não precisa odiar o sujeito.
Você só precisa confirmar se sua arma alcança.

Abra o comando de ataque.

<o sistema destaca o botão/atalho de ataque>

<o sistema espera o jogador abrir o comando de ataque>

---

## Step 6 — Escolher alvo

Objetivo do sistema: jogador alternar/selecionar o alvo válido.

Sargento:
O scanner mostrou um alvo válido.

Isso significa que você tem alcance, munição e condição de disparo.

Escolha o alvo.

<o jogador seleciona o inimigo>

Sargento:
Alvo selecionado.

Agora leia antes de confirmar.
Depois do tiro, não existe "foi sem querer".

---

## Step 7 — Confirmar ataque

Objetivo do sistema: executar o primeiro combate.

Sargento:
Confirme o ataque.

<jogador confirma>

<combate executa>

Sargento:
Resultado registrado.

Aqui não tem dado escondido, sorte milagrosa ou desculpa de azar.

Se acertou, havia motivo.
Se falhou, também.

Você estava em posição, tinha munição, tinha alcance e recebeu ordem de fogo.

Isso é combate.

---

## Fechamento
Objetivo do sistema: Revisar as lições aprendidas, já que para executar diversas das tarefas, o jogador terá q passar a vez varias vezes.

Sargento:
Primeira lição concluída.

Você aprendeu a olhar uma unidade, inspecionar um companheiro, selecionar, mover, entender terreno, manter posição, escolher alvo, atacar e encerrar o turno.

Ainda não é estratégia.

É alfabetização de comando.

Amanhã a gente descobre se você consegue fazer isso sem eu gritar no seu ouvido.

No chão, recruta! Paga 20!

<toca vitoria.mp3>
