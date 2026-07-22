# Catálogo de Armas

*Uma ficha por arma. Separar arma de unidade é coerente com a arquitetura do jogo: a plataforma define onde você vai e como sobrevive; a arma define o que você destrói e como.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

> **Catálogo incompleto.** As fichas abaixo ainda não foram preenchidas. Campo ausente não significa "sem valor" — na falta de ficha, a autoridade é o asset do jogo.

## Campos da ficha

Cada arma deve declarar:

nome · classe (antiaérea, antitanque, antiinfantaria, antinavio) · potência · classe de potência derivada · munição · alcance mínimo · alcance máximo · trajetória (reta ou parabólica) · domínio do operador · domínios de alvo

**Revide e uso após deslocamento não são campos da ficha.** Os dois são **derivados** dos valores acima e não admitem exceção por arma: revida quem tem alcance mínimo 1, munição e domínio compatível; usa após mover quem tem alcance mínimo 0 ou 1. Não existe override no sistema, e inventar um campo para isso sugeriria uma escolha que o autor da ficha não tem.

Mesma lógica para a exigência de observador avançado: ela decorre da distância e do alcance de observação do atirador, não de uma marcação na arma.

## Notas já canônicas

Três fatos sobre armas já estão declarados na doutrina e o catálogo não pode contradizê-los:

**Alcance mínimo 1 é o padrão, e alcance 0 é recurso legítimo** — reservado a armamento lançado sobre o próprio setor contra alvo em outro andar, como a carga de profundidade. Ver `06_combate.md`.

**Revide exige alcance mínimo exatamente 1.** Uma arma de 2 a 4 nunca revida, mesmo com o inimigo adjacente.

**A trajetória pertence à arma como montada naquela unidade**, não ao tipo de arma. O mesmo foguete pode ser parabólico de longo alcance numa plataforma de artilharia e reto de alcance curto num helicóptero.

## Fichas

*A preencher.*
