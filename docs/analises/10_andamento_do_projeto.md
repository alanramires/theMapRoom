# 10 - Andamento do Projeto (Revisado)

Este documento consolida o estado atual do **The Map Room** com foco em: o que está correto, o que está incompleto e o que deve virar prioridade.

## 1) Estado geral

O projeto já passou da fase de protótipo. O núcleo sistêmico está funcional e integrado:

- loop tático por turno com estados claros;
- combate determinístico com regras explicáveis;
- sensores como camada de validação de ações;
- economia/logística impactando decisão tática;
- FoW/visão como parte real da estratégia.

Resumo: o jogo já existe como sistema. O gargalo principal deixou de ser "fazer funcionar" e passou a ser **ensinar, estabilizar e escalar conteúdo**.

## 2) O que está correto

### 2.1 Arquitetura de domínio

- Separação entre dados, validação (sensores), estado de turno e execução.
- Base orientada a regras, com baixo acoplamento de mecânicas centrais.
- Decisões de combate e movimentação são rastreáveis (bom para debug e balanceamento).

### 2.2 Filosofia de gameplay

- Predomínio de informação/posicionamento sobre aleatoriedade.
- Sistemas se conectam (terreno, altura, visão, logística, alcance, etc.).
- Custos e restrições (ações, suprimento, economia) geram trade-off real.

### 2.3 Ferramental de desenvolvimento

- Já existem utilitários e janelas de suporte técnico para análise.
- Boa base para diagnosticar comportamento sistêmico.

## 3) O que está errado ou inconsistente

### 3.1 Onboarding ainda frágil para jogador novo

- O sistema é profundo, mas a entrada ainda depende demais de tentativa/erro.
- Faltam trilhas pedagógicas mais explícitas para primeira partida.

### 3.2 Estado documental desalinhado em alguns pontos

- Há relatórios antigos descrevendo estruturas já migradas/refatoradas.
- Terminologia e responsabilidades mudaram (ex.: tutorial e diálogo), mas parte da documentação não foi atualizada.

### 3.3 Escopo de automação pode crescer sem governança

- Banco de automata tende a inchar se não houver estratégia por cenário.
- Sem convenção de organização, manutenção vira custo alto cedo.

## 4) O que falta fechar

### 4.1 Pipeline de tutorial

- Definir padrão oficial para: tutorial com automata, tutorial sem automata e casos híbridos.
- Fechar contrato de objetivos (IDs, parâmetros, critérios de sucesso/falha).
- Garantir consistência entre texto tutorial, regras e comportamento em partida.

### 4.2 Critérios de balanceamento

- Sair de validação ad-hoc para uma rotina mínima de calibração.
- Definir checklist de cenários obrigatórios por mudança de unidade/regra.

### 4.3 UX de leitura de estado

- Melhorar feedback para ações bloqueadas/permitidas sem depender de conhecimento prévio do sistema.
- Priorizar clareza de decisão no turno (o que posso fazer agora e por quê).

## 5) Prioridades sugeridas (curto prazo)

1. **Onboarding primeiro**: tornar o jogo ensinável sem perder profundidade.
2. **Contrato de tutorial**: padronizar objetivos e variações com/sem automata.
3. **Higiene documental**: atualizar docs que ficaram defasados após refactors.
4. **Rotina de balanceamento**: checklist fixo por sprint para evitar regressão silenciosa.

## 6) Conclusão

O projeto está em fase de **consolidação de produto**, não de descoberta técnica.

A base sistêmica já sustenta um jogo forte.
O próximo salto de qualidade vem de:

- onboarding melhor;
- conteúdo/tutorial organizado;
- documentação e processo de balanceamento mais disciplinados.

Se esse tripé for fechado, o risco do projeto cai bastante e a evolução passa a ser incremental e previsível.
