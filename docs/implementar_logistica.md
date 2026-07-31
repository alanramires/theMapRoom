# Logística: fome da artilharia (a implementar)

Anotações de investigação, não plano fechado. Nada aqui foi alterado no código.

## A queixa

> "Artilharia de campanha seca no campo de batalha porque o supridor está
> perdendo tempo reparando tropinhas inúteis."

## O que foi verificado

`ScoreLogisticsTargetNeed` (`AIController.Logistics.Supply.cs:1155`) tem **dois
ramos**, e eles vivem em escalas diferentes.

### Ramo do reparo — só para quem tem `IsUnderRepair`

```csharp
return 10000f
     + hpFaltandoEmPontos * 1800f
     + (7500f se HP <= 50%)
     + custo/100
     + ScoreCriticalLogisticsStrategicBonus(...);
```

O bônus crítico (`:1183`) já tem a política certa:

| termo | peso |
|---|---|
| fogo indireto / artilharia / `longRangeStationary` | +6500 |
| ...com alguma arma em 0 de munição | +9500 |
| ...com alguma arma em ≤1 | +4500 |
| `eliteLevel` | ×3000 |
| infantaria que pode se **fundir** em vez de consumir supridor | **−9000** |

### Ramo comum — quem está na linha

```csharp
score = custo/100
      + déficit de HP % * 18
      + déficit de combustível % * 10
      + ScorePreventiveLogisticsStrategicBonus(...);
```

O bônus preventivo (`:1218`) **também pesa munição** — este ramo não é cego:

| termo | peso |
|---|---|
| `custo/25 + eliteLevel * 900` | base |
| fogo indireto com arma em 0 | +9000 |
| fogo indireto com arma em ≤1 | +4200 |
| fogo indireto com munição ok | +1200 |
| não-fogo-indireto com arma em ≤1 | +1400 |

### A conta que explica a raiva

```text
Obus seco, casco inteiro, tanque cheio, fora de reparo (custo 800):
  8 + 0 + 0 + 32 + 9000                       ≈  9.040

Tropinha com 2 HP faltando, em reparo (custo 100):
  10000 + 3600 + 0 + 1 + estratégico          ≈ 13.600
  (≈ 21.100 se estiver com metade da vida)
```

A tropinha ganha por causa do **piso de 10.000 que a flag `IsUnderRepair`
concede** — não porque munição seja ignorada.

## A doutrina do autor (não mexer)

`repairTriggerAmmoPct = 0` em toda a base de dados **é intencional**: artilharia
atira até secar. Ela não deve sair da linha para se reabastecer.

Consequência direta: **a artilharia nunca vai levantar a mão sozinha.** Ela
nunca entra em `IsUnderRepair` por munição antes de a última granada acabar, e
portanto nunca alcança o ramo dos 10.000.

Nota de leitura, para ninguém "consertar" isso depois achando que é bug: HP e
autonomia usam `> 0` como convenção de desligado (`repairTriggerHpBelow > 0 &&`);
munição tem um bool separado para ligar, então `pct = 0` **não** significa
desligado — significa "só quando zerar". A assimetria é proposital.

## O buraco conceitual

O scorer trata como a mesma coisa duas necessidades diferentes:

| | significado | quem se desloca |
|---|---|---|
| `IsUnderRepair` | saio de combate e busco manutenção | a unidade |
| **preciso de munição** | continuo atirando, tragam até mim | o supridor |

Só a primeira tem piso. A artilharia, por doutrina, só emite a segunda.

## Dois caminhos

**1. Constante mágica.** Subir o degrau de "fogo indireto seco" no ramo comum de
9000 para ~15000, produzindo a ordem:

```text
em reparo com HP crítico (<=50%)   ~17.500+
artilharia seca na linha           ~15.000
em reparo, ferida comum            ~13.600
resto
```

Funciona, mas o número é um chute sobre quanto vale um canhão calado.

**2. Aging (preferido).** Carimbo de "espera por suprimento" na unidade, como o
de carona: o obus que pede munição há 3 turnos sobe sozinho. Não exige adivinhar
o valor de um canhão seco, e resolve o caso que a constante não resolve — dois
obuses secos, um esperando há 1 turno e outro há 6.

É a mesma peça do transporte (`aiRideWaitSinceTurn`). Quando esta parte for
implementada, o campo irmão `aiSupplyWaitSinceTurn` entra no `UnitManager`, no
`SaveDataDtos` e no `SaveDataMapper` seguindo exatamente o mesmo padrão — mais
um `int` no formato do save.

## Pendências relacionadas, ainda não investigadas

- `FindLogisticsServiceTarget` (quem justifica o deslocamento) e
  `TryBuildLogisticsSupplyAction` (quem é atendido na chegada) são duas
  perguntas com dois códigos, e já discordaram em teste — documentado com
  evidência no relatório v6.0.6. Unificá-las na mesma consulta ao envelope
  resolve a divergência e o custo repetido.
- O `−9000` da infantaria fundível só existe no ramo do reparo. Uma tropinha
  fundível **fora** de reparo não é penalizada.
