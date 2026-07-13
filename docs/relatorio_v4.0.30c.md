# v4.0.30c - Ajustes no FOW

## Foco

Atualização incremental de desempenho e apresentação, preservando a lei transacional do tabuleiro e refinando ferramentas, dados de unidades e interface da partida hot seat.

## FOW e desempenho

- O recálculo após uma ação comprometida passa a atualizar somente a contribuição da unidade que agiu.
- Remoção do `FullVisual` desnecessário após cada retorno a `Neutral`.
- Unidades que não agiram preservam seus caches de visão.
- Visões especializadas, como as do EWACS, reutilizam os resultados já calculados quando o observador não mudou.
- Refresh completo permanece reservado a mudanças que exigem reconstrução global do FOW.
- A publicação do novo snapshot continua ocorrendo somente depois do compromisso e do retorno a `Neutral`.

## Panel Helper

- Inclusão dos sprites das unidades embarcadas na seção `Transportando`.
- Sprites resolvidos por `UnitData`, `TeamId` e `TeamUtils`, respeitando a cor do time.
- Fallback para o renderer da instância quando não existir variante configurada.
- Ícones com `52x52`, igualando o tamanho visual do hex inspecionado em `LOCAL`.
- Suporte à hierarquia visual de transportes aninhados.

## Ferramentas de unidades

- `Tools > Units > Unit Analysis` passa a priorizar o `UnitDatabase` configurado no `UnitSpawner` da cena.
- Campo manual e busca por asset continuam disponíveis como fallback.
- Novo filtro multisseleção por classe de unidade.
- O filtro limita atacantes, alvos e matrizes produzidas pela análise.
- `UnitSpawner` expõe seu banco por uma propriedade somente leitura.

## Dados e conteúdo

- Ajustes em aeronaves, armas, terrenos, estruturas e catálogo da partida Hot Seat.
- Atualizações no mapa, no `Panel_remaining` e nos assets de fontes utilizados pela interface.

## Estado

- Builds de runtime e Editor verificados durante o ciclo.
- Compilações concluídas sem erros.
