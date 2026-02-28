ok vamos seguir.

proximo sensor > serviços do comando ("X")

o jogador pode acionar quantas vezes quiser, contanto que o cursor esteja em neutro

o serviço do comando nao consome has act da unidade, mas cada unidade beneficiada ganha o "recevie supply this turn (true)" e não pode se beneficiar de outro supridor avulso

crie tools > logistica > serviço do comando, similar a pode suprir, com ordem de execução

cada unidade que estiver em cima de qualquer construção aliada que seja supridor, tenha estoque e serviços disponivel é considerada elegivel para o serviço do comando

exemplo: soldado na cidade, navio no porto, avião (pousado) no aeroporto --> as regras de suprir são as mesmas do pode suprir, todas as validações devem bater (entao caças b com STOVL por exemplo, pousam pra receber o serviço da cidade por exemplo)

só vale para construções. trate-as como se fosse uma unidade fake de alcance 0 para validações, force landing, etc...

as vezes o caça está em cima da construção cidade, mas como nao consegue pousar, fica na lista dos candidatos invalidos (tal qual seria se um caminhao tentasse suprir um caça q voa na planicie, ele naoconsegue pousar la entao fica invalido)

ou seja, esse serviço é quase q uma copia do pode suprir

todas as unidades validas sobre construção vao pra ordem de execução quando clicar em "Iniciar ordem do comando [debug] e são atendidas uma por uma (agente faz a animação depois)