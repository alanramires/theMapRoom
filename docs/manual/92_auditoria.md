# Auditoria da Doutrina

*Registro do que já foi verificado contra o código, com evidência e commit. Existe para que o manual distinga a frase conferida da frase que ninguém nunca checou — hoje as duas soam igualmente confiantes.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

## Como funciona

Uma sessão de auditoria toma **um documento canônico**, extrai as afirmações normativas dele e resolve cada uma em um de quatro veredictos:

**Confirmada** — o código faz o que o documento diz.
**Manual errado** — o texto foi corrigido para descrever o comportamento real.
**Código suspeito** — o documento expressa a intenção certa e a implementação diverge; entra em `90_pendencias_tecnicas.md`.
**Decisão em aberto** — nem texto nem código estão claramente certos; a escolha é de design e precisa de decisão humana.

**Padrão de evidência:** vale o `if`, a fórmula ou o valor de asset que **decide** o comportamento. Comentário e nome de variável servem para achar o lugar, nunca para fechar a questão — nesta sessão um comentário dizia "HP do oponente" enquanto a linha abaixo usava o HP de quem ataca.

Uma verificação vale para o **commit** em que foi feita. O código se move; a linha citada pode ter mudado de lugar ou de conteúdo.

## Armadilhas conhecidas

Os padrões de erro que já apareceram de verdade, para serem procurados ativamente:

Comentário que contradiz o código. Regra que existe nos dados mas não é imposta pelo motor. Regra imposta num caminho e esquecida no caminho gêmeo. Valor duplicado em dois lugares que podem divergir. Campo de configuração que nunca é consultado. Padrão silencioso aplicado quando o asset está vazio. Documento declarando lei universal onde o código tem exceção.

---

## Verificado

Sessão de 21/07/2026 · commit `2c80c09`

Os IDs seguem o mesmo esquema de `90_pendencias_tecnicas.md` e não são reaproveitados.

| ID | Afirmação | Veredicto | Evidência |
|---|---|---|---|
| LOG-010 | Custo de serviço: 5% reabastecer, 10% rearmar, 40% reparar, pro-rata do recuperado | Confirmada | `Assets/DB/Logistic/Services/*.asset` · `ServiceData.cs:76-158` |
| LOG-011 | Reparo devolve 2 HP em construção e 1 com caminhão | Confirmada | `Reparaos.asset` / `Reparos Leves.asset` (`serviceLimitPerUnitPerTurn`) |
| LOG-012 | Caminhão atende 1 unidade; só avião-tanque e porta-aviões atendem 2 | Confirmada | `Suprimentos.asset:94` · `UnitData.maxUnitsServedPerTurn` |
| LOG-013 | Rendimento por classe 3/2/1 (combustível e munição) e 2/1/1 (reparo) | Confirmada | `ServiceData.serviceEfficiency` nos assets de serviço |
| LOG-014 | Projétil pesado pesa 3× o leve | Confirmada | `Rearmamento.asset` (`costWeight`) |
| COM-010 | Revide só a distância 1, e a arma precisa de alcance mínimo 1 | Confirmada | `PodeMirarSensor.cs:1317` e `:1362` |
| COM-011 | Teto de eliminações é o HP do atacante no início da troca | Confirmada | `TurnStateManager.Combat.cs:337-347` |
| COM-012 | "Teto do alvo" não existe como regra — é truncamento no piso 0 | Manual errado | mesma referência; texto corrigido em `06` |
| COM-013 | Penalidade de ferido: −1 desfalcado, −2 com HP ≤ 5 | Confirmada | `TurnStateManager.Combat.cs:684` |
| COM-014 | Escala de posição 0–4 e bônus −1/0/+2/+4/+6 | Confirmada | `DPQData.cs:55-79` + assets DPQ |
| COM-015 | Alcance 0 é suportado pelo motor | Confirmada | `PodeMirarSensor.cs:100` e `:971`; mover-e-atirar corrigido em `:959-968` nesta sessão |
| FOW-010 | Montanha concede elevação 2 e bloqueia como 2,25 | Confirmada | `TerrainTypeData.cs:95-108` · `Montanha.asset` |
| FOW-011 | Construção revela terreno no raio e detecta unidade só no próprio hex | Confirmada | `MatchController.cs:4674-4713` e `:6265` |
| FOW-012 | Exposição de furtivo dura até o próximo turno do dono | Confirmada | `UnitManager.cs:977-994` |
| FOW-013 | Submarino exposto por 2 turnos jogáveis do dono; lock pendente não conta tempo | Confirmada | `UnitManager.cs:1376-1398` |
| FOW-014 | Air/Low bloqueia linha de visão / unidade projeta sombra | **Manual errado** | `TerrainVisionResolver.cs:57` só compõe a camada aérea com ocupante; `PodeMirarSensor.cs:1595` passa `null` nas células intermediárias. **Unidade nunca é obstáculo** |
| FOW-015 | Do destino provisório no escuro só o ataque é liberado | **Manual errado** | são três estados; terreno explorado libera também desembarque, captura e transferência — `Sensors.cs:119-136` e `:460-499` |
| AIR-010 | Aeroporto é a única construção que isenta consumo aéreo | **Manual errado** | são três: Aeroporto, Aeroporto Avançado e Hidrobase (`aircraftUnitsPaysUpkeep: 0`) |
| AIR-011 | Submarino nasce emerso no porto e mergulha após o primeiro movimento | Confirmada | `ConstructionShopping.cs:332-370` · `Movement.cs:319-355` |
| TRA-010 | Passageiros morrem recursivamente com o transportador | Confirmada | `ScannerPrompt.cs:4250-4293` |
| TRA-011 | Dano proporcional a passageiros: fração do transportador, mínimo 1, piso de 1 HP, mesma fração por toda a cadeia | Confirmada | `ScannerPrompt.cs:4216-4247` |
| TRA-012 | Unidades embarcadas contam contra a derrota por eliminação total | **Manual impreciso** | a contagem ignora embarcados (`MatchController.cs:2734`), mas o caso é inalcançável porque a morte do transportador mata a cadeia |
| ECO-010 | Renda é atributo por construção, não constante global | Confirmada | `ConstructionSiteRuntime.capturedIncoming` |
| ECO-011 | Nomes oficiais: "Fábrica" (não "Fábrica Média"), "Porto Naval", "Logistica Naval" | Confirmada | assets em `Assets/DB/World Building/Construction/` |

## Não auditado

Nada dos documentos `01`, `03`, `09` e `10` foi verificado. Em `06` foram verificados a fórmula, o arredondamento, o teto e o DPQ — mas **não o Elite** (os três filtros, os quatro valores movidos, a soma de especializações). Em `05` foram verificados construções, elevação e ocultação, mas não os alcances de sensor por camada.

Estimativa honesta: cerca de um terço das afirmações normativas da biblioteca passou por verificação.

## Fila sugerida

**`03_movimento_terreno_e_infraestrutura.md`** primeiro — quase todo numérico, mora em assets de terreno, e nenhuma das dezenas de afirmações de custo foi conferida. Alta densidade, verificação barata.

**`06_combate.md`** depois — maior consequência, e o Elite nunca foi olhado.

**`05_visao_deteccao_e_nevoa.md`** em seguida — maior superfície de exceções, e já produziu dois erros num único dia.
