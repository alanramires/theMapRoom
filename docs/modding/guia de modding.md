# Boas práticas para desenvolver jogos com suporte a mods

## Objetivo

Este guia propõe uma arquitetura e um processo de desenvolvimento que tornem mods mais fáceis de criar, combinar, testar e manter. Serve tanto para um jogo novo quanto para a evolução gradual de um jogo existente.

As recomendações partem da nossa experiência com o Expanded Tournament Brackets e o Tournament Bracket View: leitura de scripts, chaves persistentes, eventos, interface, localização, saves e migração após uma atualização grande do jogo.

Os exemplos abaixo são propostas de design e pseudocódigo; não representam APIs reais do CK3. Os comportamentos observados nos nossos testes não permitem concluir como todo o motor do jogo funciona internamente.

## 1. Defina o que significa oferecer suporte a mods

Antes de abrir arquivos para edição, decida quais partes do jogo serão extensíveis:

- Conteúdo: personagens, itens, mapas, missões e textos.
- Regras: seleção de participantes, combate, recompensas e progressão.
- Apresentação: painéis, filtros, atalhos, retratos e informações adicionais.
- Integrações: novos sistemas que precisam guardar dados e reagir ao jogo.

Para cada área, documente o que o mod pode consultar, modificar ou substituir. Diferencie APIs públicas estáveis de detalhes internos sujeitos a mudanças.

**Regra prática:** uma operação importante para o jogador deve ter uma representação clara para os mods. Se o jogo sabe quem venceu uma luta, o mod não deveria precisar reconstruir isso a partir de notificações ou listas temporárias.

## 2. Mantenha uma fonte única para o estado do jogo

A simulação deve guardar os fatos; eventos e telas devem consultá-los.

No nosso caso, uma chave mostrou que uma lista de classificados não basta para representar a história de um torneio. Também precisamos saber quem enfrentou quem, em qual rodada e por qual motivo avançou.

Um modelo possível:

```text
Confronto {
    id
    rodada_id
    participante_a_id
    participante_b_id
    estado: pendente | agendado | resolvido | cancelado
    vencedor_id: opcional
    motivo: combate | desistencia | desclassificacao | morte | ausencia
    resolvido_em: opcional
    revisao
}
```

Recomendações:

- Use identificadores estáveis, independentes de nomes e posições em listas.
- Guarde o motivo do resultado junto com o resultado.
- Preserve referências históricas a personagens mortos ou ausentes.
- Faça listas de classificados derivarem dos confrontos, ou atualize ambas na mesma operação atômica.
- Trate correções administrativas como operações explícitas e auditáveis.

**Invariante** é uma regra que deve permanecer verdadeira. Exemplos: um confronto resolvido tem no máximo um vencedor; um eliminado não reaparece em outro ramo sem uma regra explícita de repescagem; um mesmo participante não ocupa duas vagas da mesma rodada.

## 3. Defina políticas para exceções antes de escolher substitutos

Desistência, morte, expulsão e abandono são transições do sistema, não apenas mudanças de texto.

Documente o que acontece quando:

- Um participante sai antes do sorteio.
- Ele sai depois do sorteio, antes da luta.
- Ele vence e sai antes da rodada seguinte.
- Ambos os participantes ficam indisponíveis.
- Um resultado já anunciado precisa ser corrigido.

Para uma chave eliminatória fixa, uma política coerente é preservar o resultado anterior e conceder avanço por ausência ao próximo adversário. Outros jogos podem preferir substituição ou repescagem, mas precisam definir critérios, prioridade e limites.

**Evite preencher silenciosamente uma vaga com qualquer personagem elegível.** Isso pode manter o fluxo funcionando e, ao mesmo tempo, quebrar a história da competição.

Se uma modalidade exige um número mínimo de participantes, a política de preenchimento inicial deve ser separada da política de substituição após o início.

## 4. Trate eventos atrasados como pedidos que precisam ser revalidados

Encontramos um caso concreto: a sabotagem desclassificou um competidor e classificou seu adversário. Dias depois, uma luta automática já agendada ainda executou, trocou o vencedor e deixou nove personagens no grupo das quartas.

A correção local acrescentou verificações de desclassificação para os dois participantes. Em um motor projetado para mods, a proteção mais robusta ficaria na própria operação de resolução:

```text
resolver_confronto(id, revisao_esperada, resultado, comando_id):
    iniciar_transacao()
    confronto = buscar_para_atualizacao(id)

    se comando_id ja foi processado:
        retornar resultado_anterior

    exigir confronto.revisao == revisao_esperada
    exigir confronto.estado == agendado
    exigir participantes_validos_para_esta_resolucao(confronto)

    validar_resultado(resultado, confronto)
    gravar_resultado_e_classificacao(confronto, resultado)
    registrar_comando_processado(comando_id)
    registrar_evento_para_publicacao_apos_commit()
    confirmar_transacao()
```

Isso combina três proteções:

- **Revalidação:** o estado ainda permite a operação?
- **Controle de revisão:** o pedido foi criado para uma versão antiga do confronto?
- **Idempotência:** repetir o mesmo comando não repete seus efeitos.

Cancelar eventos pendentes ajuda, mas não substitui essas verificações. O evento pode já estar em execução ou ter sido restaurado de um save.

Também centralize a resolução usada por IA, jogador e scripts. Caminhos separados tendem a acumular regras diferentes.

## 5. Separe o fato ocorrido do momento de anunciá-lo

O jogo pode resolver um confronto antes de mostrar a cena correspondente. Isso não obriga toda interface a revelar imediatamente o resultado.

Ofereça uma camada de apresentação que considere:

- O resultado já foi calculado?
- O anúncio já foi apresentado?
- Esse observador pode conhecer o resultado?
- O jogador precisa confirmar seu próprio resultado antes de receber notícias de outra luta?

Uma consulta poderia ser:

```text
obter_confronto_para_observador(confronto_id, jogador_id)
```

Ela retorna somente as informações disponíveis àquele jogador naquele momento. Assim, um painel de mod não precisa inventar uma segunda cronologia para evitar antecipações.

Notificações devem referenciar o fato registrado. O processamento de uma notificação não deve aplicar novamente a punição ou o avanço que ela anuncia.

## 6. Ofereça pontos de extensão pequenos e explícitos

Copiar um arquivo enorme para alterar poucas linhas aumenta conflitos e o custo de cada atualização.

Prefira extensões com contrato definido:

| Necessidade do mod | Ponto de extensão sugerido |
| --- | --- |
| Exibir uma chave | Consulta pública de rodadas e confrontos |
| Adicionar um botão | Ýrea de ações extensível na interface |
| Ajustar o tamanho da competição | Política configurável de composição |
| Reagir a uma desclassificação | Evento publicado após a alteração confirmada |
| Alterar a escolha de substitutos | Estratégia de substituição registrada por modalidade |
| Trocar uma descrição | Substituição de uma chave de localização |

Cada ponto de extensão deve informar o contexto recebido, os dados disponíveis, as operações permitidas e o tratamento de falhas.

Separe ganchos de consulta, validação, alteração e observação. Um observador que apenas monta uma tela não precisa receber permissão para alterar a simulação.

## 7. Resolva conflitos entre mods de forma previsível

Uma ordem de carregamento, sozinha, não explica como duas alterações devem interagir.

Defina regras por tipo de extensão:

- Listas de ações podem aceitar vários itens.
- Validadores podem combinar restrições, com regras explícitas para rejeições.
- Transformações precisam de ordem documentada.
- Uma política exclusiva, como o algoritmo de pareamento, deve detectar duas substituições concorrentes.

Use namespaces, como `autor.mod.recurso`, para IDs públicos. Declare dependências e incompatibilidades no manifesto.

Quando houver conflito, informe quais mods disputam qual recurso. Não deixe uma substituição silenciosa parecer um defeito aleatório no jogo.

## 8. Construa a interface para receber extensões

Nossa experiência com o botão mostrou que aparência, tamanho ocupado no layout e área clicável podem divergir. Mover um elemento visualmente não garante que a região que recebe cliques acompanhe a mudança.

Boas práticas:

- Exponha áreas próprias para ações de mods.
- Use layout com restrições e prioridades, não apenas coordenadas fixas.
- Defina comportamento quando faltar espaço: quebra de linha, rolagem ou menu de ações adicionais.
- Considere escala da interface, resolução, nomes longos e traduções.
- Mantenha desenho, recorte e área clicável sincronizados.
- Disponibilize componentes nativos reutilizáveis para retratos, tooltips e botões.
- Documente quais propriedades cada componente aceita.

Um inspetor de interface deveria mostrar a árvore de componentes, dimensões calculadas, limites de recorte, área clicável e o elemento que interceptou um clique.

Para uma chave, exponha dados suficientes para o mod calcular o layout. Evite exigir centenas de referências individuais a variáveis internas.

## 9. Faça do contexto de scripts uma interface tipada

Scripts de eventos frequentemente dependem de um personagem, anfitrião, atividade ou adversário disponível no contexto. Essa dependência deve ser explícita.

```text
ContextoDeDesclassificacao {
    atividade: AtividadeId
    confronto: ConfrontoId
    infrator: PersonagemId
    autoridade: PersonagemId
    vitima_da_sabotagem: PersonagemId opcional
}
```

A vítima da sabotagem pode ser diferente do adversário na chave. Nomes e tipos claros evitam que um parâmetro seja usado como se fosse o outro.

O validador deve detectar referências ausentes, tipos incompatíveis, funções desconhecidas e argumentos obrigatórios antes de iniciar uma campanha. Contextos fornecidos pela interface também precisam participar dessa validação.

## 10. Trate localização como parte da API

Ofereça substituição por chave, preservando as demais entradas do jogo.

- Use parâmetros nomeados e documentados: vencedor, derrotado, rodada e motivo.
- Evite frases montadas por concatenação que dificultem plural, gênero e ordem das palavras.
- Separe motivos de resultado: perder uma luta, desistir e morrer antes dela são fatos diferentes.
- Valide chaves, parâmetros e expressões embutidas.
- Disponibilize fallback por idioma e uma forma clara de localizar traduções ausentes.

O texto deve refletir o estado registrado. Um fallback pode impedir uma tela vazia, mas deveria deixar um diagnóstico quando transforma uma morte em “foi derrotado”.

## 11. Faça saves evoluírem junto com o jogo e os mods

Cada mod que persiste dados deve declarar uma versão de esquema, além da versão do pacote.

O carregamento precisa considerar:

- Instalação do mod no meio de uma campanha.
- Atualização de uma versão antiga.
- Dados opcionais ausentes.
- Remoção do mod.
- Personagens mortos e entidades removidas.
- Eventos agendados antes da atualização.

Forneça migrações explícitas, backup e mensagens de incompatibilidade compreensíveis. Evite apagar dados desconhecidos silenciosamente.

Sempre que possível, ofereça uma ferramenta oficial para inspecionar saves. Ela deve mostrar o estado e os eventos pendentes sem exigir que o autor do mod descubra o formato inteiro.

## 12. Torne bugs reproduzíveis e observáveis

Disponibilize:

- Logs estruturados com data do jogo, evento, mod de origem, entidade e cadeia de execução.
- Histórico das transições importantes, incluindo resultado anterior e novo.
- Consulta dos eventos agendados, seus participantes e horário previsto.
- Identificação de qual mod forneceu a versão efetiva de uma função ou tela.
- Exportação de um pacote de diagnóstico com versões, ordem de carregamento e cenário mínimo.

Separe erro de execução, aviso de compatibilidade e informação de depuração. Um parâmetro de layout ignorado merece um aviso específico; uma falha de estado precisa mostrar a operação rejeitada e o motivo.

Para aleatoriedade, permita salvar e restaurar os estados dos geradores. Considere fluxos separados por sistema, de forma que uma chamada aleatória puramente visual não altere o sorteio de uma competição. Isso ajuda a repetir bugs sem exigir que todas as decisões do jogo sejam previsíveis ao jogador.

## 13. Teste contratos e transições, além da aparência

Uma tela correta confirma a apresentação daquele estado. Não prova que a rodada seguinte respeitará as regras.

Uma matriz mínima para o nosso exemplo:

| Cenário | O que verificar |
| --- | --- |
| 8 e 16 participantes | Número de rodadas e composição corretos |
| Rodada normal | Cada vencedor ocupa uma única vaga seguinte |
| Desclassificação antes da luta | Evento atrasado não resolve novamente o confronto |
| Jogador e IA como anfitrião | Mesma regra de avanço, sem efeitos duplicados |
| Desistência e morte | Resultado, motivo e histórico coerentes |
| Ambos indisponíveis | Política explícita, sem laço infinito ou substituição arbitrária |
| Save e reload | Estado e fila de eventos preservados |
| Espectador e participante | Informação e notificações adequadas ao observador |
| Dois mods alterando o mesmo ponto | Composição ou conflito previsível |

Combine testes unitários das regras, testes de integração de eventos e saves, e testes visuais/interativos. Inclua testes que repitam comandos e executem eventos fora da ordem esperada.

Comparar duas capturas ajudou a suspeitar do problema; comparar dois saves mostrou a troca do vencedor, o evento pendente e o nono classificado. Ferramentas oficiais deveriam tornar essa investigação simples.

## 14. Prepare atualizações para não quebrar o ecossistema às cegas

- Versione a API pública de mods separadamente, quando possível.
- Publique mudanças de assinaturas, dados e comportamento, com exemplos de migração.
- Mantenha compatibilidade temporária para interfaces depreciadas quando isso for viável.
- Ofereça uma versão de teste antes de mudanças grandes.
- Rode mods de referência e cenários de regressão no processo de lançamento.
- Distinga “carrega nesta versão” de “foi testado nesta versão”.

Se uma função ganha um argumento obrigatório, o autor precisa de um diagnóstico claro e de uma ferramenta para localizar chamadas antigas. Um mod carregar sem erro de sintaxe não garante que continue correto.

## 15. Defina permissões e limites de execução

O suporte a mods também deve prever falhas acidentais e código não confiável.

- Scripts comuns não precisam de acesso irrestrito ao sistema de arquivos ou à rede.
- Diferencie mods de dados, scripts em ambiente limitado e extensões nativas.
- Estabeleça limites para loops, eventos por atualização e tamanho de dados persistidos.
- Informe erros de um mod com sua origem, sem ocultar a falha.
- Em multiplayer, defina quais alterações precisam ser iguais entre clientes e quem tem autoridade para alterar o estado.

Permissões devem acompanhar a capacidade oferecida. Uma interface de consulta para desenhar uma chave tem necessidades muito menores que uma extensão nativa.

## Como aplicar em um jogo novo

1. Modele estados, transições e invariantes dos sistemas principais.
2. Crie operações centrais para mudanças de estado, com validação e proteção contra repetição.
3. Faça a interface oficial consumir as mesmas consultas públicas disponíveis aos mods.
4. Exponha pontos de extensão e componentes de interface documentados.
5. Construa manifestos, diagnósticos e migração de saves desde cedo.
6. Desenvolva um pequeno mod de referência usando somente a API pública.
7. Teste combinações de mods e atualizações antes de lançar.

Se o mod de referência precisar copiar um arquivo inteiro ou adivinhar uma variável interna para realizar uma tarefa comum, isso é um sinal para melhorar a interface de extensão.

## Como aplicar em um jogo existente

Não é necessário reescrever o motor inteiro.

1. Identifique os arquivos mais sobrescritos e os conflitos mais frequentes.
2. Exponha consultas para os estados que os mods hoje precisam reconstruir.
3. Acrescente pontos de extensão nos locais de maior demanda.
4. Centralize operações críticas duplicadas entre IA, jogador e eventos.
5. Adicione validação de estado aos eventos atrasados.
6. Introduza componentes de UI extensíveis e substituição de textos por chave.
7. Migre gradualmente os sistemas internos para essas interfaces.
8. Deprecie os caminhos antigos com prazo, avisos e exemplos de conversão.

## Critério de sucesso

Um bom suporte a mods permite ao autor **entender o estado, alterar uma regra de forma localizada e descobrir rapidamente por que algo falhou**.

A experiência do torneio resume o objetivo: criar uma tela deveria exigir acesso aos confrontos e um lugar para inserir o botão; alterar uma regra deveria exigir uma política bem definida; atualizar o jogo deveria produzir uma lista clara de adaptações. Quanto menos reconstrução de fatos, cópia de arquivos e tentativa de posicionamento, mais tempo sobra para criar conteúdo e experiências novas.
