# Sistema de Logística — The Map Room
### Documento de Design v1.0

---

## 1. Tiers

Todo agente logístico — unidade ou construção — pertence a um tier que define sua capacidade de movimentar estoques.

**Hub**
- Pode doar ou receber recursos de outro Hub
- Pode doar recursos para qualquer Receiver
- Se o Hub for infinito, pode apenas doar para Hub ou Receiver (nunca recebe)

**Receiver**
- Pode apenas receber recursos de um Hub
- Não transfere para Hub
- Não transfere para Receiver

**Regras de direção:**
```
Hub infinito  -> Hub          sim
Hub           <-> Hub         sim
Hub           -> Receiver     sim
Receiver      -> Hub finito   nao
Receiver      -> Receiver      nao
```

**Observações:**
- Acesso é restrito por time — agentes de times diferentes não interagem logisticamente
- Construções não iniciam transferências — a iniciativa é sempre da unidade
- Construções não precisam do serviço Transferir para participar como origem/destino; unidades envolvidas na transferência precisam ser elegíveis no sensor
- Receiver não doa para nenhum Hub (finito ou infinito); “Hub finito” na matriz é apenas para explicitar o caso mais comum.

---

## 2. Reservas

Três tipos de recurso físico armazenados na reserva de unidades e construções:

| Reserva | Convertida em |
|---|---|
| Galões | Autonomia (Serviço de Reabastecimento) |
| Caixas de Munição | Munição (Serviço de Rearme) |
| Peças | HP (Serviço de Reparo) |

**Capacidade:**
- **Unidades** — reserva máxima fixa definida por tipo de unidade. Reserva cheia não aceita mais recursos.
- **Construções** — sem teto de reserva. Acumula indefinidamente conforme agentes Hub chegam e doam. Esvazia conforme unidades consomem serviços.

**Movimentação:**
- Reservas podem ser **transferidas** entre agentes via serviço de Transferir (gratuito)
- Reservas podem ser **consumidas** convertidas em benefícios via serviços pagos

---

## 3. Serviços

Todo agente logístico oferece um ou mais serviços. A combinação de serviços define o papel narrativo do agente. Construções e unidades seguem as mesmas regras.

Todo agente que possui serviços converte seus próprios estoques internos em benefícios para unidades que o visitam. Um Hub com Transferir e Reabastecer é simultaneamente participante da cadeia de movimentação de estoque e provedor de serviços diretos — as duas funções coexistem no mesmo agente usando os mesmos estoques.

---

### Reabastecer
Converte Galões em Autonomia.

| Armor Class | Taxa | Custo cobrado |
|---|---|---|
| Light | 1 galão >> 3 autonomia | 10% do valor de compra |
| Medium | 1 galão >> 2 autonomia | 10% do valor de compra |
| Heavy | 1 galão >> 1 autonomia | 10% do valor de compra |

---

### Rearmar
Converte Caixas em Munição.

| Weapon Class | Taxa | Custo cobrado |
|---|---|---|
| Light | 1 caixa >> 3 munição | 25% do valor de compra |
| Medium | 1 caixa >> 2 munição | 25% do valor de compra |
| Heavy | 1 caixa >> 1 munição | 25% do valor de compra |

*Custo cobrado: média ponderada pelo total de armas carregadas. Ou seja, uma unidade com 2 armas embarcadas (heavy e light): armas heavy tem um custo proporcionalmente maior dentro da fatia dos 25% do que armas lights*

---

### Reparar
Converte Peças em HP.

| Armor Class | Taxa | Custo cobrado |
|---|---|---|
| Light | 1 peça >> 2 HP | 65% do valor de compra por HP |
| Medium / Heavy | 1 peça >> 1 HP | 65% do valor de compra por HP |

---

### Transferir
Move reservas entre agentes logísticos conforme regras de Tier.

- Custo: **gratuito**
- Sempre executado pela unidade ativa
- Direção governada pelas regras de Tier descritas na seção 1

---

## 4. Agentes Logísticos — Unidades

| Unidade | Tier | Reserva máxima | Serviços | Restrição |
|---|---|---|---|---|
| Caminhão de Carga (Optimus) | Hub | 150g / 40c / 40p | Transferir | Estradas + planície limitada |
| Trem de Carga | Hub | 300g / 60c / 60p | Transferir | Trilhos apenas |
| Navio Tanker | Hub | 500g / 120c / 100p | Transferir, Reabastecer | Naval |
| KC-130 | Receiver | 150g / 0c / 0p | Transferir, Reabastecer | Unidades aéreas apenas |
| Fragata | Receiver | 150g / 20c / 20p | Transferir, Rearmar, Reabastecer, Reparar | Apache embarcado apenas |
| Porta-Aviões | Hub | 400g / 100c / 75p | Transferir, Rearmar, Reabastecer, Reparar | Unidades embarcadas apenas |
| Caminhão de Suprimentos | Receiver | 40g / 12c / 10p | Transferir, Rearmar, Reabastecer, Reparar | Até 2 unidades adjacentes |

---

## 5. Agentes Logísticos — Construções

| Construção | Tier | Reserva | Serviços | Observação |
|---|---|---|---|---|
| HQ | Hub | Infinita | Rearmar, Reabastecer, Reparar | Só doa — nunca recebe |
| Fábrica | Hub | Infinita | Rearmar, Reabastecer, Reparar | Idem |
| Aeroporto (base) | Hub | Infinita | Rearmar, Reabastecer, Reparar | Idem |
| Porto (base) | Hub | Infinita | Rearmar, Reabastecer, Reparar | Idem |
| Aeroporto neutro | Hub | Ilimitada sem teto | Rearmar, Reabastecer, Reparar | Esvazia — reabastecido por agentes Hub |
| Porto neutro | Hub | Ilimitada sem teto | Rearmar, Reabastecer, Reparar | Idem |
| Cidade | Hub | Ilimitada sem teto | Rearmar, Reabastecer, Reparar | Idem |
| Construção especial | Receiver | Ilimitada sem teto | Definido por design | Reabastecida por agentes Hub |

---

## 6. Reservas Iniciais

| Agente | Galões | Caixas | Peças | Tipo |
|---|---|---|---|---|
| Caminhão de Suprimentos | 40 | 12 | 10 | Unidade |
| Caminhão de Carga (Optimus) | 150 | 40 | 40 | Unidade |
| Trem de Carga | 300 | 60 | 60 | Unidade |
| KC-130 | 150 | 0 | 0 | Unidade |
| Fragata | 150 | 20 | 20 | Unidade |
| Navio Tanker | 500 | 120 | 100 | Unidade |
| Porta-Aviões | 400 | 100 | 75 | Unidade |
| Cidade | 40 | 20 | 30 | Construção |
| Aeroporto | 200 | 40 | 30 | Construção |
| Porto | 300 | 50 | 40 | Construção |

---

## 7. Regra de Ouro

> **Tier define o que você pode fazer. Reserva define o que você tem. Serviço define o que você entrega.**

Os mesmos conceitos base valem para todos, com diferenças operacionais por tipo de agente (unidade/construção e tier).