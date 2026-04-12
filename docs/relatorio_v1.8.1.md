# The Map Room - Relatório de Atualização v1.8.1

**Versão:** v1.8.1
**Data:** 11 de Abril de 2026
**Tema:** Revisão da Ficha de Unidades e Arquitetura de Dados

## Resumo das Alterações
A versão `v1.8.1` marca a conclusão do processo de higienização do `UnitData` e do painel de controle de unidades (`UnitDataEditor`) após o encerramento do antigo controlador de Inteligência Artificial do jogo. O foco foi a padronização visual da arquitetura de dados e o desacoplamento de lógicas estáticas que limitavam a escalabilidade das validações de sistemas (como a determinação de classes de voo e náuticas).

## Alterações Tecnológicas Principais

### 1. Refatoração de Domínio (Desacoplamento de Classes Estáticas)
- **Nova Avaliação `isAircraft()`:** Anteriormente amarrado unicamente às visualizações `Jet`, `Plane` e `Helicopter`, a função foi reconstruída para analisar se o `Domain.Air` se encontra presente no domínio nativo da unidade ou em seus domínios adicionais de operação. 
- **Nova Função `isMaritime()`:** Criada com o mesmo princípio conceitual modular. Detecta nativamente navios ou submarinos garantindo que qualquer unidade pertencente aos escopos de navegação marítima possa se aproveitar das mecânicas navais automaticamente, indepentente da tipologia visual.

### 2. Conversão da Estrutura de Equipes (`TeamVariantSprites`)
- **Fim das variáveis estáticas:** As antigas variáveis hardcoded que sustentavam as mudanças de *sprite* dos quatro times principais (`spriteGreen`, `spriteRed`, `spriteBlue` e `spriteYellow`) foram apagadas para sempre do `UnitData` e da struct do `UnitLayerMode` (para as formatações *fallback* e adicionais).
- **Implantação de Listas Dinâmicas (`TeamVariantSprites`):** A identidade visual de equipe agora é controlada através de blocos opcionais de Listas no editor. Times que não possuem visuais distintos não ocupam mais espaço no Inspector.
- As UIs da loja e serviços (`TurnStateManager.ConstructionShopping`) já foram validadas e atualizadas para consumir a nova utilidade central de requisição de cor com *fallback* automático.

### 3. Novo Layout Visual do Unit Data Editor
O Inspector do *UnitData* passou por uma das suas limpezas mais extensas da história, visando melhor visualização, manutenção e hierarquia técnica:
- Os sub-blocos foram todos integrados e categorizados, eliminando atributos soltos e legados obsoletos.
- **Identity:** Aglomera os descritivos, informações militares, e agora também encapsula o subgrupo *Visuals*, mantendo todos os traços de apresentação unificados do topo da página.
- **Native Domain:** Mantém em evidência a trindade espacial da unidade (Domínio, Altura, Camadas Opcionais).
- **Attributes:** Abstrai lógicas soltas, agregando *Max HP*, *Movement Category*, e as referências físicas e matemáticas. Incorpora também com destaque as propriedades da antiga seção separada *Autonomy* (*Upkeep e Consumo* de recursos de movimentação).
- **Aircraft Information & Naval Information:** Exibidos dinamicamente e bloqueados de edição caso a unidade não possua os respectivos escopos nativos (`isAircraft` ou `isMaritime`).
- **Vision and Detection:** Funde e organiza todo o controle de linha de visão, exceções de *Vision Specializations* e detecção condicional do *Stealth Skills*. As variantes e matrizes legadas foram inteiramente eliminadas.
- **Training & Abilities:** Coleta elementos finais passivos importantes do ecossistema, incluindo Nível de Elite, Skills base, e Modificadores de Combate de RPS.

## Impacto Pós-Atualização (Próximos Passos)
A higienização técnica é mandatória para garantir que a transição entre domínios (terrestre, naval, ar), captura de hexágonos e o combate entre entidades seja plenamente robusto sem a presença da IA anterior validando falsamente blocos legados.

A versão se encontra completamente livre de erros de compilação.
O próximo estágio foca totalmente na rodada de testes de mecânicas vitais do Single-Player para validar a fundação antes da construção do novo Master AI da Camada Estratégica.
