# 10 - Andamento do Projeto (Revisado)

Data base: 2026-05-25 (revisado; base original: 2026-04-xx)

Este documento consolida o estado atual do **The Map Room** com foco em: o que esta correto, o que esta incompleto e o que deve virar prioridade.

## 1) Estado geral

O projeto ja passou da fase de prototipo. O nucleo sistemico esta funcional e integrado:

- loop tatico por turno com estados claros;
- combate deterministico com regras explicaveis;
- sensores como camada de validacao de acoes;
- economia/logistica impactando decisao tatica;
- FoW/visao como parte real da estrategia;
- **IA completa com todos os papeis de unidade implementados.**

Resumo: o jogo ja existe como sistema jogavel com IA funcional. O gargalo principal passou a ser **ensinar, estabilizar e escalar conteudo**.

## 2) O que esta correto

### 2.1 Arquitetura de dominio

- Separacao entre dados, validacao (sensores), estado de turno e execucao.
- Base orientada a regras, com baixo acoplamento de mecanicas centrais.
- Decisoes de combate e movimentacao sao rastreaveis (bom para debug e balanceamento).

### 2.2 Filosofia de gameplay

- Predominio de informacao/posicionamento sobre aleatoriedade.
- Sistemas se conectam (terreno, altura, visao, logistica, alcance, etc.).
- Custos e restricoes (acoes, suprimento, economia) geram trade-off real.

### 2.3 Ferramental de desenvolvimento

- Ja existem utilitarios e janelas de suporte tecnico para analise.
- Boa base para diagnosticar comportamento sistemico.

### 2.4 Sistema de IA

- `AIController` cobre todos os papeis taticos: Capturador, Assault, Transportador, Artilharia, Defesa.
- Suporte a unidades aereas: gestao de altitude, reabastecimento em voo, helicopteros de transporte, combate aereo.
- Arquitetura por `partial class` com responsabilidades claras por arquivo — governanca definida por convencao (ver `CLAUDE.md`).
- Planejamento por objetivos (`TeamObjectivePlan`, `SectorObjective`) coordena movimentos entre unidades do mesmo time.
- Shopping automatico (`AIShoppingPlanner`) ajusta compras ao estado do plano.

## 3) O que esta errado ou inconsistente

### 3.1 Onboarding ainda fragil para jogador novo

- O sistema e profundo, mas a entrada ainda depende demais de tentativa/erro.
- Faltam trilhas pedagogicas mais explicitas para primeira partida.

### 3.2 Estado documental desalinhado em alguns pontos

- Revisao em andamento (docs 01-09 atualizados nesta sessao).
- Terminologia e responsabilidades evoluiram com o crescimento da IA e dos novos sistemas (FoW, Autonomy, PodeEmergir, etc.).

### 3.3 Balanceamento de IA ainda empirico

- A IA funciona, mas calibracao de agressividade, prioridade de objetivos e decisoes de compra ainda e feita por observacao de partidas.
- Sem metricas formais para avaliar qualidade de decisao por turno.

## 4) O que falta fechar

### 4.1 Pipeline de tutorial

- Definir padrao oficial para: tutorial com automata, tutorial sem automata e casos hibridos.
- Fechar contrato de objetivos (IDs, parametros, criterios de sucesso/falha).
- Garantir consistencia entre texto tutorial, regras e comportamento em partida.

### 4.2 Criterios de balanceamento

- Sair de validacao ad-hoc para uma rotina minima de calibracao.
- Definir checklist de cenarios obrigatorios por mudanca de unidade/regra.
- Para IA: definir metricas minimas de avaliacao por papel (ex.: taxa de captura por turno, perdas relativas).

### 4.3 UX de leitura de estado

- Melhorar feedback para acoes bloqueadas/permitidas sem depender de conhecimento previo do sistema.
- Priorizar clareza de decisao no turno (o que posso fazer agora e por que).

## 5) Prioridades sugeridas (curto prazo)

1. **Onboarding primeiro**: tornar o jogo ensinavel sem perder profundidade.
2. **Contrato de tutorial**: padronizar objetivos e variacoes com/sem automata.
3. **Higiene documental**: continuar atualizando docs defasados (docs 10-12 em andamento).
4. **Rotina de balanceamento de IA**: checklist fixo por sprint para evitar regressao silenciosa.

## 6) Conclusao

O projeto esta em fase de **consolidacao de produto**, nao de descoberta tecnica.

A base sistemica ja sustenta um jogo forte, com IA funcional cobrindo todos os papeis de unidade incluindo operacoes aereas.

O proximo salto de qualidade vem de:

- onboarding melhor;
- conteudo/tutorial organizado;
- documentacao e processo de balanceamento mais disciplinados.

Se esse tripe for fechado, o risco do projeto cai bastante e a evolucao passa a ser incremental e previsivel.
