# Changelog

Índice das versões do projeto. Cada versão tem um relatório próprio explicando
**o porquê**, não só o que mudou.

## Como ler a versão — `vX.Y.Z`

| dígito | significa |
|---|---|
| **X** | grande mudança de arquitetura. Jogar uma IA fora e fazer outra, alterar regra de sensor já validada, mexer na velocidade do jogo |
| **Y** | mudança localizada importante. Pegar uma parte — o capturador, por exemplo — e trabalhar nela e nos filhos dela |
| **Z** | salvamento pontual de fim de trabalho |

**Onde vivem os relatórios:** o major **corrente** fica em `docs/`; quando o
major fecha, os relatórios dele são arquivados em `docs/Versões/`.

---

## v7 — Terminais burros e desacoplamento da IA *(em curso)*

A arquitetura da IA deixa de ser "cada papel resolve o seu alcance" e passa a ser
uma **escada**: sensores `PodeX` → serviços de área → consumidores `Melhor*` →
papéis → variações de papel. Cada degrau só começa depois de o de baixo estar de
pé.

O impacto no jogo é mínimo por construção: a IA é **consumidora** dos sensores,
não dona deles. O tabuleiro, os `PodeX`, o FoW e o ciclo transacional seguem
intactos.

| versão | título | relatório |
|---|---|---|
| v7.0.0 | Fundação, desacoplamento e generalização do uso dos sensores | [relatório](docs/relatorio_v7.0.0.md) |
| v7.0.1 | O alvo de captura tem um dono, e o desembarque tem um preço | [relatório](docs/relatorio_v7.0.1.md) |
| v7.0.2 | A habilidade é uma chave, e o capitão virou dado | [relatório](docs/relatorio_v7.0.2.md) |
| v7.0.3 | A camada virou parâmetro, e a taxonomia destrancou | [relatório](docs/relatorio_v7.0.3.md) |

### Documentos normativos criados neste major

| documento | papel |
|---|---|
| [AI Behavior/governanca.md](docs/AI%20Behavior/governanca.md) | **norma** acima de todos os papéis: ordens, ciclo, ações, sensores de sistema, visão |
| [AI Behavior/governanca_entre_papeis.md](docs/AI%20Behavior/governanca_entre_papeis.md) | as arestas: os três tipos de governo e o Comportamento Magnético |
| [refactor/plano_de_trabalho.md](docs/refactor/plano_de_trabalho.md) | a escada e a fila das pendências, ordenada por dependência |

---

## v6 — Envelope de alcance e contratos de IA *(fechado)*

A Hotzone deixou de ser ferramenta de inspeção e virou **serviço**: uma única
fonte de alcance, consultada por intenção e banda. A doutrina de cada papel
passou a existir por escrito.

14 versões, de `v6.0.0` a `v6.1.5`. Relatórios arquivados em
[`docs/Versões/`](docs/Versões/).

### Documentos normativos criados neste major

| documento | papel |
|---|---|
| [contrato_envelope_alcance.md](docs/AI%20Behavior/contrato_envelope_alcance.md) | **norma** das bandas de alcance |
| [AI Behavior/Capturador.md](docs/AI%20Behavior/Capturador.md) | doutrina do capturador |
| [AI Behavior/Assalto.md](docs/AI%20Behavior/Assalto.md) | doutrina do assalto e da marinha |
| [AI Behavior/FireSupport.md](docs/AI%20Behavior/FireSupport.md) | doutrina do fogo indireto |
| [AI Behavior/Transporte.md](docs/AI%20Behavior/Transporte.md) | doutrina do transporte |
| [hotzone e bandas de alcance.md](docs/AI%20Behavior/hotzone%20e%20bandas%20de%20alcance.md) | apresentação do conceito (não-normativo) |
| [refactor/ai_sem_plano.md](docs/refactor/ai_sem_plano.md) | plano do refactor de unidade sem plano |
| [implementar_logistica.md](docs/implementar_logistica.md) | investigação parqueada da fome da artilharia |

---

## Majors anteriores

Relatórios em [`docs/Versões/`](docs/Versões/) — 311 arquivos.

| major | versões | tema |
|---|---|---|
| v5 | 26 | — |
| v4 | 137 | o maior ciclo do projeto |
| v3 | 11 | — |
| v2 | 47 | — |
| v1 | 90 | primeiro ciclo, do zero ao jogo |

---

## Como gerar a lista de uma versão

Com commits pequenos, o changelog de uma versão se escreve sozinho:

```bash
git log --oneline v6.1.4..v6.1.5
```

Um commit grande devolve uma linha inútil; doze commits pequenos devolvem doze
linhas prontas para o relatório.
