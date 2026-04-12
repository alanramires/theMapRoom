# AIPlanEvaluator — Como Funciona

## Em uma frase

O AIPlanEvaluator olha para o mapa no início do turno e decide **quais regiões a IA vai tentar capturar** e **quais unidades ficam responsáveis por cada tarefa**.

---

## O que ele produz

Uma lista de **planos ativos** (`AIPlanIntent`). Cada plano representa uma missão:

> "Capturar o setor Charlie, usando 2 infantarias, 1 escolta e 1 suporte."

Cada plano já vem com as unidades designadas por nome e papel.

---

## De onde vêm as informações

O avaliador recebe um **snapshot** — uma foto do estado do jogo naquele momento:
- Todas as construções do mapa (posição e dono)
- Unidades aliadas disponíveis
- Inimigos visíveis
- Localização do próprio QG

Ele **não executa** nada. Só lê e decide. Por isso pode ser chamado do editor sem afetar o jogo.

---

## O pipeline em ordem

### 1. Plano de Invasão (stance = Invasion)
Se a IA está em modo de invasão, ela primeiro localiza a **base inimiga mais fraca** (menos pontos de captura acumulados pelo inimigo, mais próxima do próprio QG) e cria um plano com prioridade máxima para ela. Este plano rouba um slot dos planos normais.

### 2. Planos de Setor Ativos (até 3 planos)
Esta é a parte principal. O avaliador varre todos os setores do mapa que ainda têm construções não conquistadas e monta uma lista de candidatos. Cada candidato é avaliado por:

| Critério | Peso |
|---|---|
| Distância ao próprio QG | Quanto mais perto, maior prioridade |
| Construções não capturadas | Quanto mais, maior prioridade |
| Pressão inimiga no setor | Mais inimigos = mais urgente |
| Tem QG inimigo? | Bônus de prioridade |

Os **3 melhores setores** viram planos ativos. Para cada um, o avaliador calcula a força necessária:

- **Capture**: quantas infantarias precisam capturar as construções
- **Escort**: quantas unidades de proteção acompanham
- **FireSupport**: quantas artilharias apoiam
- **Logistics**: quantos supridores são necessários

Em seguida, ele percorre as unidades disponíveis e designa cada uma a um plano, evitando que a mesma unidade entre em dois planos ao mesmo tempo.

### 3. Planos de Hold (segurar setor conquistado)
Se a IA já conquistou um setor mas detecta inimigos a 2 hexes das construções aliadas, ela cria um plano de "hold" para manter a presença. Não envia novas unidades — só mantém as já designadas no posto.

### 4. Planos Backup (retomada)
Se um setor próprio está sendo reconquistado pelo inimigo e não entrou nos planos principais, entra aqui como backup. Prioriza os mais contestados e mais próximos do QG.

### 5. Persistência de Planos Anteriores
Planos que estavam ativos no turno anterior e ainda fazem sentido continuam vivos mesmo que não tenham sido selecionados neste turno. Isso evita que a IA "mude de ideia" a cada turno por pequenas flutuações no score.

Exceção: se uma unidade está no mesmo lugar por **2 turnos seguidos sem avançar** (estagnação), o plano é descartado e a unidade é liberada para outra missão.

### 6. Demanda de Transporte
Depois que todos os planos e designações estão resolvidos, o avaliador verifica se alguma infantaria de captura está longe demais para chegar a pé ao objetivo. Nesses casos, sinaliza **demanda de transporte** — um APC ou veículo deverá buscá-la.

---

## Exemplo prático

Estado do mapa: QG Verde ao sul, setores Alpha (perto), Bravo (médio), Charlie (longe com QG inimigo).

```
Candidatos após scoring:
  Alpha  → dist=3, uncaptured=2, pressure=0  → rank 1
  Bravo  → dist=5, uncaptured=3, pressure=1  → rank 2
  Charlie→ dist=9, uncaptured=4, pressure=0, hasEnemyHQ → rank 3

Planos gerados:
  Plano A: "Captura Alpha [CAP 2, ESC 1, ART 0, SUP 0]"
    → Soldado_1 (Capture), Soldado_2 (Capture), Tanque_1 (Escort)
  Plano B: "Captura Bravo [CAP 2, ESC 1, ART 1, SUP 1]"
    → Soldado_3, Soldado_4, Tanque_2, Canhão_1, Supridor_1
  Plano C: "Captura Charlie [CAP 1, ESC 0, ART 0, SUP 0]"
    → Soldado_5 (muito longe → demanda de transporte sinalizada)
```

---

## O que o AIPlanEvaluator NÃO faz

- Não move nenhuma unidade
- Não decide com quem atacar
- Não resolve o caminho até o objetivo
- Não decide se vai usar sensor de combate em vez de seguir o plano

Tudo isso é responsabilidade da **Phase2** (`AIPlayerController`), que recebe os planos prontos e executa turno a turno.

---

## Onde ver isso em ação

- `Tools > AI > Simuladores > AI Pode Comprar` — mostra os planos e demandas de força antes das compras
- `Tools > AI > Simuladores > AI Unit Decision` — mostra qual plano cada unidade recebeu e qual papel ela tem
- `Tools > AI > AI Planner` — visualiza os planos no mapa com setas e setores coloridos
