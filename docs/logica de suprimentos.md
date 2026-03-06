Vamos corrigir o relatório e **fechar a lógica de suprimento completa** do MBT, incluindo:

* **peças de manutenção**
* **galões de combustível**
* a regra de recuperação por categoria da unidade

Como o **MBT é Heavy**, então:

* **1 galão = 1 fuel**
* **1 peça = 1 HP**

Abaixo vai o relatório revisado, já editado.

---

# Relatório de Serviço — MBT

## Dados-base da unidade

* **Unidade:** MBT
* **Categoria logística:** **Heavy**
* **Custo de compra:** **$18.000**
* **HP máximo:** **10**
* **Autonomia máxima:** **70**
* **Arma primária:** **Cannon Heavy** — 6 disparos
* **Arma secundária:** **Chain Gun Medium** — 9 disparos

## Regras do modelo de custo

A composição do custo da unidade é dividida em três blocos:

* **Bloco HP:** 65% do custo total
* **Bloco Ammo:** 25% do custo total
* **Bloco Fuel:** 10% do custo total

Pesos de custo por categoria de munição:

* **Light = 1**
* **Medium = 2**
* **Heavy = 3**

Recuperação por 1 unidade de suprimento usada:

### Ammo

* **Light = 3 tiros**
* **Medium = 2 tiros**
* **Heavy = 1 tiro**

### Fuel

* **Heavy = 1 fuel por galão**

### Repair

* **Heavy = 1 HP por peça**

Arredondamento operacional:

* consumo de caixas é **sempre round up**
* consumo de galões e peças segue recuperação inteira da categoria

---

# 1) Bloco de HP

## Cálculo do bloco

65% de $18.000 = **$11.700**

## Custo unitário por HP

$11.700 / 10 HP = **$1.170 por HP**

## Lógica de serviço

Como o MBT é **Heavy**:

* **1 peça = 1 HP**

## Resultado do serviço de reparo

* Recuperar **1 HP** do MBT:

  * consome **1 peça**
  * custa **$1.170**
* Recuperar **10 HP** do MBT:

  * consome **10 peças**
  * custa **$11.700**

---

# 2) Bloco de Fuel

## Cálculo do bloco

10% de $18.000 = **$1.800**

## Custo unitário por autonomia

$1.800 / 70 = **$25,71 por autonomia**

Arredondamento operacional adotado:

* **$26 por fuel**

## Lógica de serviço

Como o MBT é **Heavy**:

* **1 galão = 1 fuel**

## Resultado do serviço de combustível

* Recuperar **1 fuel**:

  * consome **1 galão**
  * custa **$26**
* Recuperar **70 fuel**:

  * consome **70 galões**
  * custa **$1.800**

---

# 3) Bloco de Ammo

## Cálculo do bloco

25% de $18.000 = **$4.500**

Agora o bloco de munição será distribuído por **média ponderada**, respeitando o peso das categorias de munição.

---

## 3.1) Pontos ponderados de munição

### Cannon Heavy

* 6 disparos
* peso Heavy = 3

6 × 3 = **18 pontos ponderados**

### Chain Gun Medium

* 9 disparos
* peso Medium = 2

9 × 2 = **18 pontos ponderados**

### Total de pontos ponderados

18 + 18 = **36 pontos ponderados**

---

## 3.2) Valor de cada ponto ponderado

$4.500 / 36 = **$125 por ponto ponderado**

---

## 3.3) Custo unitário por disparo

### Cannon Heavy

Peso 3

3 × $125 = **$375 por disparo heavy**

### Chain Gun Medium

Peso 2

2 × $125 = **$250 por disparo medium**

---

## 3.4) Checagem de consistência do bloco

### Cannon Heavy

6 × $375 = **$2.250**

### Chain Gun Medium

9 × $250 = **$2.250**

### Total do bloco de ammo

$2.250 + $2.250 = **$4.500**

Bloco de munição fechado corretamente.

---

# 4) Relatório de suprimento — munição por arma

## 4.1) Cannon Heavy

### Dados

* munição máxima: **6**
* custo unitário por disparo: **$375**
* recuperação logística: **1 disparo por caixa**
* arredondamento: round up

### Serviço unitário

* recuperar **1 disparo heavy**:

  * consome **1 caixa**
  * custa **$375**

### Serviço completo

* recuperar **6 disparos heavy**:

  * consome **6 caixas**
  * custa **6 × $375 = $2.250**

### Resultado

**Cannon Heavy**

* **$375 por disparo**
* **1 caixa por disparo**
* **6 caixas para carga completa**
* **$2.250 para carga completa**

---

## 4.2) Chain Gun Medium

### Dados

* munição máxima: **9**
* custo unitário por disparo: **$250**
* recuperação logística: **2 disparos por caixa**
* arredondamento: round up

### Serviço unitário de referência

* 1 caixa medium recupera até **2 disparos**
* valor logístico por caixa medium:

  * 2 × $250 = **$500 por caixa**

### Serviço completo

* recuperar **9 disparos medium**
* pela regra de recuperação:

  * 9 / 2 = 4,5 caixas
* com round up:

  * **5 caixas**

Essas 5 caixas têm capacidade teórica para recuperar **10 disparos**, mas a arma só comporta **9**.

### Custo do serviço completo

* 9 × $250 = **$2.250**

### Resultado

**Chain Gun Medium**

* **$250 por disparo**
* **1 caixa recupera 2 disparos**
* **5 caixas para carga completa**
* recupera **9 de 10 possíveis**
* **$2.250 para carga completa**

---

# 5) Relatório consolidado de munição

## Ammo Heavy

* 6 disparos máximos
* **$375 por disparo**
* **6 caixas** para recuperar tudo
* **$2.250** para recuperar tudo

## Ammo Medium

* 9 disparos máximos
* **$250 por disparo**
* **5 caixas** para recuperar tudo
* recupera **9 de 10 possíveis**
* **$2.250** para recuperar tudo

## Total consolidado de munição

* total de disparos: **15**
* total de caixas consumidas:

  * 6 heavy
  * 5 medium
  * **11 caixas no total**
* custo total para rearmar tudo:

  * $2.250 heavy
  * $2.250 medium
  * **$4.500 no total**

---

# 6) Relatório consolidado de reparo

## Reparo unitário

Como o MBT é **Heavy**:

* **1 peça = 1 HP**

### Serviço unitário

* recuperar **1 HP**

  * consome **1 peça**
  * custa **$1.170**

### Serviço completo

* recuperar **10 HP**

  * consome **10 peças**
  * custa **$11.700**

## Total consolidado de reparo

* **10 peças** para recuperar tudo
* **$11.700** para recuperar tudo

---

# 7) Relatório consolidado de combustível

## Reabastecimento unitário

Como o MBT é **Heavy**:

* **1 galão = 1 fuel**

### Serviço unitário

* recuperar **1 fuel**

  * consome **1 galão**
  * custa **$26**

### Serviço completo

* recuperar **70 fuel**

  * consome **70 galões**
  * custa **$1.800**

## Total consolidado de combustível

* **70 galões** para recuperar tudo
* **$1.800** para recuperar tudo

---

# 8) Relatório consolidado de serviço completo da unidade

## Reparo total

* 10 HP
* **$1.170 por HP**
* **10 peças**
* **$11.700** para recuperar tudo

## Reabastecimento total

* 70 fuel
* **$26 por fuel**
* **70 galões**
* **$1.800** para recuperar tudo

## Rearmamento total

### Cannon Heavy

* 6 caixas
* **$2.250**

### Chain Gun Medium

* 5 caixas
* **$2.250**

### Total ammo

* **11 caixas**
* **$4.500**

---

# 9) Resumo executivo

**MBT — custo de manutenção por bloco**

* **HP:** $1.170 por ponto
* **Fuel:** $26 por autonomia
* **Heavy ammo:** $375 por disparo
* **Medium ammo:** $250 por disparo

**MBT — consumo logístico por categoria**

* **Repair:** 1 peça = 1 HP
* **Fuel:** 1 galão = 1 fuel
* **Heavy ammo:** 1 caixa = 1 disparo
* **Medium ammo:** 1 caixa = 2 disparos

**MBT — serviço completo**

* **Reparar 10 HP:** 10 peças / **$11.700**
* **Reabastecer 70 fuel:** 70 galões / **$1.800**
* **Rearmar tudo:** 11 caixas / **$4.500**

**Detalhamento do rearme**

* Cannon Heavy:

  * 6 caixas
  * **$2.250**
* Chain Gun Medium:

  * 5 caixas
  * **$2.250**
  * recupera **9 de 10 possíveis**

-------------------------------------------------------------------
Vamos calcular **exatamente o serviço possível nesta rodada**, respeitando as regras do seu sistema:
-------------------------------------------------------------------

* MBT é **Heavy**
* Caminhão só consegue **recuperar 2 HP nesta rodada**
* Tudo usa **round up**
* Recuperação logística:

  * **1 peça = 1 HP**
  * **1 galão = 1 fuel**
  * **heavy ammo: 1 caixa = 1 tiro**
  * **medium ammo: 1 caixa = 2 tiros**

Valores econômicos que já calculamos:

* **$1170 por HP**
* **$26 por fuel**
* **$375 por tiro heavy**
* **$250 por tiro medium**

---

# Estado atual do MBT

HP: **5 / 10**
Autonomia: **30 / 70**
Cannon: **1 / 6**
Chain gun: **1 / 9**

---

# 1) Serviço de reparo (limitado a 2 HP)

HP faltando: **5**

Mas o caminhão recupera **no máximo 2 HP nesta rodada**

Recuperação realizada:

**2 HP**

Consumo logístico:

* **2 peças**

Custo:

2 × $1170 = **$2340**

Estado após serviço:

HP = **7 / 10**

---

# 2) Serviço de combustível

Fuel faltando:

70 ? 30 = **40**

Recuperação realizada:

**40 fuel**

Consumo logístico:

* **40 galões**

Custo:

40 × $26 = **$1040**

Estado após serviço:

Fuel = **70 / 70**

---

# 3) Serviço de munição — Cannon Heavy

Situação atual:

1 / 6

Faltam:

**5 tiros**

Recuperação logística heavy:

1 caixa = **1 tiro**

Consumo logístico:

**5 caixas**

Custo:

5 × $375 = **$1875**

Estado após serviço:

Cannon = **6 / 6**

---

# 4) Serviço de munição — Chain Gun Medium

Situação atual:

1 / 9

Faltam:

**8 tiros**

Recuperação logística medium:

1 caixa = **2 tiros**

Cálculo:

8 / 2 = **4 caixas**

Consumo logístico:

**4 caixas**

Custo:

8 × $250 = **$2000**

Estado após serviço:

Chain gun = **9 / 9**

---

# Custo econômico desta rodada

Reparar: **$2340**
Reabastecimento: **$1040**
Rearmamento: **$3875**
    Cannon: **$1875**
    Chain gun: **$2000**
Total Serviços: **$7255**

---
# Consumo logístico desta rodada

Peças de manutenção: **2**
Galões de combustível: **40**
Caixas totais de munição: **9**
    Caixas heavy: **5**
    Caixas medium: **4**
---

# Total do serviço

**$7255**

---

# Estado final do MBT após o serviço

HP: **7 / 10**
Fuel: **70 / 70**
Cannon: **6 / 6**
Chain gun: **9 / 9**

---

# Observação interessante de balanceamento

Com renda de **~7000 por turno**, só esse serviço já custa **$7255**.

Ou seja, manter um MBT parcialmente destruído em combate **consome praticamente um turno inteiro de economia**, o que confirma exatamente o comportamento estratégico que você queria:

* combate pesado gera **pressão econômica**
* logística pesa no orçamento
* unidades caras não são triviais de sustentar.
