Pesquisei. E tem uma coisa muito interessante: **vocês chegaram intuitivamente a várias das práticas mais recomendadas para tutorial design**, especialmente para jogo de estratégia.

A fonte mais “oficial” que encontrei foi a própria Apple, na documentação de onboarding para jogos. Ela recomenda exatamente: ensinar o **core loop primeiro**, objetivos que se constroem uns sobre os outros, instruções curtas, deixar o jogador demonstrar competência antes de avançar e, em jogos com muitos sistemas, distribuir o ensino em vários tutoriais curtos em vez de despejar tudo de uma vez. As primeiras lições podem orientar mais; as posteriores devem exigir que o jogador use sozinho o que já aprendeu. [Apple Developer](https://developer.apple.com/app-store/onboarding-for-games/?utm_source=chatgpt.com)

A literatura acadêmica sobre *scaffolding* chega numa conclusão parecida: suporte demais transforma a experiência em execução dirigida; suporte de menos pode produzir frustração. O importante é **qual ajuda é dada e em que momento**, e essa ajuda deve ser retirada conforme o jogador passa a dominar aquilo. [ScienceDirect](https://www.sciencedirect.com/science/article/abs/pii/S0360131517301343?utm_source=chatgpt.com)

Para mim, as melhores práticas que realmente importam para **The Map Room** se condensam nestes princípios:

1. **Ensine pela necessidade, não pela explicação.**  
O jogador aprende uma mecânica porque apareceu um problema que aquela mecânica resolve. É exatamente a sua ideia do Chinook sem combustível: primeiro existe o problema; depois o caminhão faz sentido. Tutoriais implícitos, integrados ao gameplay, tendem a distribuir melhor a carga cognitiva do que blocos prévios de regras. [ScienceDirect](https://www.sciencedirect.com/science/article/abs/pii/S1566253526000308?utm_source=chatgpt.com)

2. **Cada aula precisa de um objetivo pedagógico principal.**  
Isso não significa “só pode aparecer uma mecânica”. Significa que, ao terminar, deve ser possível responder: **o que eu queria que o jogador aprendesse aqui?** Game Developer recomenda que cada lição peça ao jogador para realmente realizar a ação, não apenas clicar “próximo”. [Game Developer](https://www.gamedeveloper.com/design/important-tips-for-effective-tutorial-game-design?utm_source=chatgpt.com)  
No seu caso:
   - Soldado 1: **conquistar uma posição usando uma pequena força combinada**.
   - Soldado 2: **resolver um problema logístico e extrair a força**.
   
   Há movimento, combustível, embarque etc. envolvidos, mas existe uma ideia central.

3. **Introduza ? pratique ? combine ? teste sem ajuda.**  
Esse é provavelmente o padrão mais útil para sua estrutura de patentes. A Apple inclusive cita tutoriais curtos em sequência, começando com mais orientação e depois exigindo que o jogador demonstre o aprendido. [Apple Developer](https://developer.apple.com/app-store/onboarding-for-games/?utm_source=chatgpt.com)  
Então não precisa ser:
   
   `Tutorial de movimento ? tutorial de captura ? tutorial de combustível`
   
   Pode ser:
   
   **Aula 1:** nova ideia + suporte.  
   **Aula 2:** ideia nova + reutilização da anterior.  
   **Aula 3:** combinação.  
   **Prova de Soldado:** problema aberto, quase nenhuma orientação.

4. **Just-in-time: explique quando a informação se torna útil.**  
Não explique Galões quando o helicóptero ainda está cheio. Não explique captura quando não existe prédio a tomar. Não explique SAM quando não existe ameaça aérea. O conceito aparece quando o jogador tem um motivo para querer entendê-lo. Essa abordagem é consistentemente recomendada em onboarding justamente para evitar sobrecarga. [Wayline](https://www.wayline.io/blog/designing-effective-game-tutorials-and-onboarding?utm_source=chatgpt.com)

5. **Feedback deve ser imediato e legível.**  
Fez algo ? o jogo responde claramente. O jogador precisa associar ação e consequência. Isso é particularmente importante em estratégia, porque muita coisa acontece de maneira abstrata. Game Developer usa justamente jogos de estratégia como exemplo: isolar uma tarefa importante e deixar o jogador executar e observar a resposta. [Game Developer](https://www.gamedeveloper.com/design/how-to-make-game-tutorials-better-immediate-feedback?utm_source=chatgpt.com)  
Se capturou: resistência `30 ? 20 ? 10 ? 0`.  
Se abasteceu: autonomia sobe.  
Se perdeu o helicóptero: motivo compreensível.  
Vocês já começaram a fazer isso com os contadores das tarefas. relatorio_v9.3.1

6. **Não confunda ensinar com impedir erro.**  
Essa apareceu várias vezes na pesquisa. Uma boa área introdutória pode ser segura, mas não deveria virar um teste de obediência. Uma análise de tutorial design resume bem: colocar o jogador numa situação em que ele precise aprender, oferecer ambiente controlado e **respeitar o jogador**. [Game Developer](https://www.gamedeveloper.com/design/disorder-dev-diary-2-to-teach?utm_source=chatgpt.com)  
Sua solução para “desembarquei só um soldado” é ótima sob essa ótica: você não diz “AÇÃO INVÁLIDA”. O cara perdeu um turno. Continua jogando. relatorio_v9.3.1

7. **Ensine também o “quando” e o “por quê”.**  
Encontrei uma formulação boa do Game Developer: para uma mecânica, o tutorial precisa eventualmente responder **o quê, como, por quê e quando** usar. [Game Developer](https://www.gamedeveloper.com/design/the-not-so-simple-secrets-to-making-good-tutorials?utm_source=chatgpt.com)  
Isso é especialmente importante na Sala de Mapas. Saber clicar em “Suprir” vale pouco. O jogador precisa começar a perceber:
   
   **“Quando minha operação passa a depender deste caminhão?”**
   
   Isso já é pensamento estratégico.

8. **A orientação deve desaparecer.**  
Esse é o ponto que eu colocaria como princípio formal da Academia. A primeira vez pode existir fala do Sargento, contador, trava e dica. Na segunda vez, menos. Na terceira, só objetivo. Depois, nada.  
O objetivo do tutorial é tornar-se desnecessário.

E uma prática que vocês já descobriram na marra é talvez a mais importante de todas:

**playtest de tutorial não é jogar do jeito que o designer espera. É tentar quebrá-lo.**

A v9.3.1 inteira nasceu disso: desembarcar um só, capturar cedo, ficar voando, passar turno em momentos estranhos. relatorio_v9.3.1 Isso bate diretamente com recomendações de designers de tutorial: observar alguém jogando é indispensável porque o designer conhece demais o próprio jogo para enxergar as suposições que está fazendo. [Game Developer](https://www.gamedeveloper.com/design/the-not-so-simple-secrets-to-making-good-tutorials?utm_source=chatgpt.com)

### Onde eu acho que sua Academia está particularmente forte

Ela não é uma “sala branca”.

O primeiro cenário é uma operação de tomada de posição. O segundo já é uma operação completamente diferente — combustível, caminhão, embarque, extração e uma ameaça que pode aparecer conforme o tempo ou a ação do jogador. relatorio_v9.3.2

Isso significa que você está ensinando **mecânicas dentro de significado operacional**.

E isso é provavelmente mais valioso em The Map Room do que seguir dogmaticamente “uma mecânica por missão”.

Eu usaria uma regra um pouco diferente:

> **Uma nova pergunta por aula. Quantas mecânicas antigas forem necessárias para respondê-la.**

Essa frase combina muito mais com o seu jogo.

Soldado 1 pergunta:

> **Como estabeleço e seguro uma cabeça de ponte?**

Soldado 2:

> **Como recupero e retiro minha força quando a logística falha?**

Depois você pode ter:

> **Como atravesso uma posição defendida?**  
> **Como descubro um inimigo que não consigo ver?**  
> **Como sustento um avanço longe da base?**

E progressivamente o Sargento cala a boca.

### A única cautela grande que a pesquisa me trouxe para você

Não confunda **complexidade interessante** com **quantidade de coisas novas simultâneas**.

Sua Aula 2 já tem combustível, caminhão, serviço, embarque, extração, Apaches e pressão temporal. Ela pode funcionar muito bem **se várias dessas coisas já forem intuitivas ou reutilizadas**. Mas, em playtest com alguém que nunca viu The Map Room, vale observar onde ele começa a perguntar “pera, o que eu faço agora?”.

Esse momento é um indicador melhor que qualquer teoria.

Se ele está pensando:

> “Será que levo o caminhão primeiro ou junto os soldados?”

ótimo. **Decisão.**

Se ele está pensando:

> “Eu nem sei o que esse caminhão faz.”

aí falta **ensino**.

Essa é uma distinção excelente para você usar durante os playtests:

**confusão estratégica é desejável.  
confusão de interface/regra não é.**

E acho que isso resume quase toda a pesquisa aplicada à Sala de Mapas.

As boas práticas modernas não dizem para deixar o jogo fácil ou conduzir o jogador pela mão. Elas dizem para **reduzir a dificuldade de compreender o sistema, sem reduzir a dificuldade de tomar boas decisões**. [Apple Developer](https://developer.apple.com/app-store/onboarding-for-games/?utm_source=chatgpt.com)

Isso é praticamente a filosofia que você já estava tentando alcançar com a Academia.