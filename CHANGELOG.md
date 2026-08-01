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

## v6 — Envelope de alcance e contratos de IA *(em curso)*

A Hotzone deixa de ser ferramenta de inspeção e vira **serviço**: uma única
fonte de alcance, consultada por intenção e banda. A IA migra papel por papel
para consumi-la, e a doutrina de cada papel passa a existir por escrito.

| versão | título | relatório |
|---|---|---|
| v6.1.5 | Contratos de AI: Capturador, Assault, Fire Support | [relatório](docs/relatorio_v6.1.5.md) |
| v6.1.4 | Unificação de AI Capturador: Constantes | [relatório](docs/relatorio_v6.1.4.md) |
| v6.1.3 | Unificação de AI Capturador: Planos e Atribuição | [relatório](docs/relatorio_v6.1.3.md) |
| v6.1.2 | Fix — Rebel: Capturador de volta pro AI Controller | [relatório](docs/relatorio_v6.1.2.md) |
| v6.1.1 | Táxi e Carona | [relatório](docs/relatorio_v6.1.1.md) |
| v6.1.0 | Táxi e Carona — antes do refactor | [relatório](docs/relatorio_v6.1.0.md) |
| v6.0.7 | Game start tunning | [relatório](docs/relatorio_v6.0.7.md) |
| v6.0.6 | Hotzone como Serviço: Progressão | [relatório](docs/relatorio_v6.0.6.md) |
| v6.0.5 | Hotzone como Serviço: Logística Terrestre | [relatório](docs/relatorio_v6.0.5.md) |
| v6.0.4 | Hotzone como Envelope de Serviço | [relatório](docs/relatorio_v6.0.4.md) |
| v6.0.3 | Vigilância Aérea | [relatório](docs/relatorio_v6.0.3.md) |
| v6.0.2 | Desembarque parcial e novo Hotzone tool | [relatório](docs/relatorio_v6.0.2.md) |
| v6.0.1 | Start and loading optimization | [relatório](docs/relatorio_v6.0.1.md) |
| v6.0.0 | Board baking e loading optimization | [relatório](docs/relatorio_v6.0.0.md) |

### Documentos normativos criados neste major

| documento | papel |
|---|---|
| [contrato_envelope_alcance.md](docs/contrato_envelope_alcance.md) | **norma** das bandas de alcance |
| [AI Behavior/Capturador.md](docs/AI%20Behavior/Capturador.md) | doutrina do capturador |
| [AI Behavior/Assalto.md](docs/AI%20Behavior/Assalto.md) | doutrina do assalto e da marinha |
| [AI Behavior/FireSupport.md](docs/AI%20Behavior/FireSupport.md) | doutrina do fogo indireto |
| [AI Behavior/Transporte.md](docs/AI%20Behavior/Transporte.md) | doutrina do transporte |
| [hotzone e bandas de alcance.md](docs/hotzone%20e%20bandas%20de%20alcance.md) | apresentação do conceito (não-normativo) |
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
