---
name: consolidar-doutrina
description: Transforma em documento de doutrina uma regra de IA que já assentou no The Map Room — junta relatórios, commits e memória, confere tudo no código e escreve ou atualiza o doc em docs/AI Behavior/ ou docs/manual/. Use quando o autor disser "consolida", "consolidar doutrina", "escreve a doutrina de X", ou escolher um item da seção "Prontos para consolidar" de um relatório.
---

# Consolidar doutrina

O fechamento do dia **aponta** docs desatualizados e regras prontas para
consolidar; esta skill **escreve**. Ela roda fora do fechamento, quando o autor
quiser, de cabeça fresca. Escrever doutrina cansado, ou com a regra ainda
mudando, produz doc errado — e doc errado é pior que doc nenhum, porque a
próxima sessão acredita nele.

## 0. Qual regra, e se ela está pronta

O autor diz qual regra (ou aponta um item de "Prontos para consolidar" no
relatório mais recente). Confirme os três sinais antes de escrever:

1. **estável:** nenhum commit mexeu nela nos últimos 2 fechamentos;
2. **vista rodando:** algum relatório a registra como "visto em Play";
3. **sem marca aberta:** nenhuma marca do AI Step sobre ela depois da última
   mudança (`ai_step_marcas.jsonl`, ler com PowerShell `ConvertFrom-Json`).

Se algum sinal falhar, **diga qual** e pergunte se ele quer consolidar mesmo
assim. Não decida sozinho que "está bom o suficiente".

## 1. Juntar as fontes

Nesta ordem, porque cada uma corrige a anterior:

- **relatórios** (`docs/relatorio_v*.md`, e `docs/Versões/` para majors fechados)
  — o porquê de cada mudança e os erros registrados;
- **commits** das frentes (`git log --grep` pelo conceito, e `git log -- <arquivo>`)
  — o que entrou, e o que ficou de fora de propósito;
- **memória do agente** — decisões do autor e frases dele ("o de trás paga",
  "você não vai fazer dinheiro não");
- **o doc atual**, se existir — o que ele afirma hoje.

Procure o conceito por sinônimo, não só pelo nome do método. Busca vazia não
prova ausência.

## 2. Conferir no código — obrigatório

**Toda afirmação do doc é conferida no código antes de ir para o papel.** É
regra do projeto ("verificar antes de documentar"): notas de design e
mensagens de commit descrevem a intenção, e o código pode ter divergido.

- Para cada regra: ache o ponto que a decide (`arquivo:linha`) e leia.
- Siga a delegação: um sensor que "não olha X" pode delegar a quem olha.
- Se o código contradiz o relatório ou a memória, **o código manda** — e a
  contradição vira uma linha no doc ou uma pergunta ao autor, nunca um silêncio.
- O que é decisão do autor e ainda não tem código fica marcado como **decidido,
  não implementado**.

## 3. Escrever

Onde:

- regra de **comportamento da IA** → `docs/AI Behavior/` (molde: `Transporte.md`,
  `Capturador.md`); papel novo ganha arquivo próprio;
- regra de **jogo** que a IA só consome → `docs/manual/` (leia
  `01_principios_e_vocabulario.md` antes: ele decide onde uma regra pode morar).

Como:

- **o porquê antes do como**, e na língua do projeto (Tático, Operacional,
  âncora, capitão, banda);
- as frases do autor entram como estão, entre aspas;
- cada regra cita o `arquivo:método` que a decide;
- os **erros que custaram tempo** ficam no doc, com o que os desmentiu;
- o que **não** está coberto ou ficou de fora de propósito tem seção própria.

Não reescreva o doc inteiro se só uma parte mudou: atualize a seção e diga, no
topo, de que versão ela é.

## 4. Fechar o ciclo

- Remova o item da lista "Docs de doutrina" no próximo relatório (anote no
  `docs/resumo.md` que foi consolidado, para o próximo fechamento não reapontar).
- Commit próprio, só com os docs: `Doutrina: <regra> consolidada (vX.Y.Z)`,
  dizendo quais fontes foram conferidas no código e o que ficou como
  "decidido, não implementado".

Termine a mensagem com a linha de co-autoria que o sistema indicar.

## Critério de pronto

- [ ] os três sinais conferidos (ou o autor aceitou consolidar sem algum)
- [ ] toda regra do doc tem `arquivo:método` conferido no código
- [ ] contradições entre fontes resolvidas pelo código ou perguntadas ao autor
- [ ] seção do que não está coberto
- [ ] commit próprio só com docs
