# AI Capturador — Árvore de Decisão

Prioridade padrão da unidade: `[Capture, Attack, Reposition]`

---

## Classificação dos prédios

Antes de decidir, o capturador classifica todos os prédios capturáveis:

- **Livre**: sem nenhuma unidade em cima, pertence ao inimigo ou é neutro
- **Contestado**: tem uma unidade inimiga em cima (capturando ou bloqueando)
- **Aliado ocupado**: tem uma unidade aliada em cima → ignorado completamente

---

## Árvore de decisão

```
Capturador ativado
│
├── Há prédio contestado MAIS PRÓXIMO que o livre?
│   │
│   ├── É o HQ inimigo contestado?
│   │   └── Avança em direção ao HQ (prioridade máxima, ignora prédios livres)
│   │
│   └── É prédio comum contestado?
│       ├── Consegue atacar o ocupante de algum prédio contestado?
│       │   └── Ataca o ocupante (simula posições via PodeMirar)
│       └── Não consegue atacar
│           └── Avança em direção ao prédio contestado mais próximo
│
├── Há prédio LIVRE?
│   │
│   ├── Está em cima do prédio livre?
│   │   └── Captura parado
│   │
│   ├── Consegue chegar ao prédio livre neste turno?
│   │   └── Move até o prédio e captura
│   │
│   └── Prédio fora de alcance
│       ├── Há ocupante de prédio contestado atacável no caminho?
│       │   └── Ataca o ocupante (simula posições via PodeMirar)
│       └── Não há ataque disponível
│           └── Avança um passo em direção ao prédio livre
│
├── Todos os prédios têm inimigo (nenhum livre)?
│   ├── Consegue atacar o ocupante de algum prédio contestado?
│   │   └── Ataca o ocupante (simula posições via PodeMirar)
│   └── Fora de alcance de todos
│       └── Avança em direção ao prédio contestado mais próximo
│
└── Nenhum prédio capturável encontrado (todos aliados ou nenhum)
    ├── Há HQ inimigo conhecido?
    │   └── Rush ao HQ inimigo (avança um passo por turno)
    └── Sem HQ inimigo visível
        └── Retorna null → cai para Attack ou Reposition
```

---

## Regras de ataque a ocupantes

Quando o capturador precisa expulsar um inimigo de um prédio:

1. Testa ataque **parado** (sem mover) contra o ocupante via `PodeMirar`
2. Simula a unidade em **cada célula alcançável** e testa ataque via `PodeMirar`
3. Usa o primeiro destino válido que permita acertar o ocupante específico
4. Se nenhuma posição funcionar → avança em direção ao prédio

> O atacante simulado é sempre o ocupante específico do prédio — não qualquer inimigo em campo aberto.

---

## Exceções e prioridades especiais

| Situação | Comportamento |
|---|---|
| HQ inimigo **livre** | Nunca sacrificado por prédio contestado mais próximo |
| HQ inimigo **contestado** | Avança mesmo com inimigo lá, não tenta atacar antes |
| Prédio com **aliado** em cima | Completamente ignorado |
| Prédio fora de alcance com ataque no caminho | Ataca o ocupante antes de avançar |
| Sem nenhum prédio capturável | Rush ao HQ inimigo conhecido |
