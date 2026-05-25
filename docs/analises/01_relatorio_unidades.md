# Relatorio das Unidades

Data base: 2026-05-25 (revisado; base original: 2026-03-06)

## Resumo
- Total de unidades analisadas: 31
- Custo medio: $11690.32
- Menor custo: Soldado ($1000)
- Maior custo: Destroyer ($30000)

## Alteracoes desde a revisao anterior (2026-03-06)
- **Sea Hawk removida** do roster (asset não existe mais)
- **Caminhão de Carga** adicionada (arquivo: `18Wheels.asset`)
- **Navio Transporte** adicionada (arquivo: `MA Desembarque.asset`)
- Reajustes de custo: Obus Leve (6200→4000), Avião Tanque (12500→14000), Super Tucano (8500→9000), Porta Aviões (20000→22500), Navio Tanque (11500→8000), Hidroavião (3000→2000)
- Reajustes de visao: Caça A/B (3→4), Destroyer (3→4), Super Tucano (3→5), Hidroavião (3→4)
- Reajustes de FD: Apache (14→15), Chinook (11→14)
- Reajuste de autonomia: Soldado (99→70), Tanque Pesado (80→50), Hidroavião (50→30), Obus Leve (movimento 5→3)
- Reajuste de elite: Bombardeiro (0→1)
- Reajuste de movimento: Obus Leve (5→3 Motor), Trem de Carga (4→6 Motor)
- Renomeacoes internas de asset (displayName permanece igual): Tanque A/B/Z, Obus Médio, Astros II, AAA, SAM

## Tabela consolidada
| Unidade | Custo | HP | Autonomia | Dominio | Movimento | Visao | Armor | FD base | Elite | Armas | Modificadores de combate |
|---|---:|---:|---:|---|---:|---:|---|---:|---:|---|---|
| Anti Arcraft Artilery | 5500 | 10 | 50 | Land/Surface | 5 (Motor) | 3 | Light | 10 | 0 | W1: Auto Gun (Heavy/AntiInfantaria, maxAmmo=5) | Artilharia Frágil |
| Apache | 7500 | 10 | 70 | Air/AirLow | 5 (Helice) | 3 | Medium | 15 | 0 | W1: Chain Gun 30mm (Medium/AntiAerea, maxAmmo=8); W2: Hydra (Light/AntiTank, maxAmmo=4) | - |
| APC | 4200 | 10 | 70 | Land/Surface | 6 (Motor) | 3 | Medium | 12 | 0 | W1: Roof Gun (Light/AntiAerea, maxAmmo=9) | - |
| Artilharia de Campanha | 20000 | 10 | 10 | Land/Surface | 1 (Motor) | 3 | Heavy | 16 | 2 | W1: Projétil 155mm (Heavy/AntiTank, maxAmmo=3) | AntiVehicle; Anti Tank; Explosão Forte; Artilharia a prova de Balas |
| Avião Tanque | 14000 | 10 | 120 | Air/AirHigh | 7 (Jato) | 3 | Medium | 12 | 0 | - | - |
| Bazooka | 2000 | 10 | 70 | Land/Surface | 2 (Marcha) | 3 | Medium | 12 | 0 | W1: LAW (Light/AntiTank, maxAmmo=3); W2: Stinger (Light/AntiInfantaria, maxAmmo=3) | - |
| Bombardeiro | 22000 | 10 | 90 | Air/AirHigh | 7 (Jato) | 3 | Medium | 13 | 1 | W1: TOW II (Light/AntiTank, maxAmmo=6) | Chafflir |
| Caça A | 26000 | 10 | 80 | Air/AirHigh | 9 (Jato) | 4 | Heavy | 15 | 1 | W1: Seeker Missile (Medium/AntiInfantaria, maxAmmo=5); W2: Vulcan (Light/AntiAerea, maxAmmo=8) | Dog Fight; Manobrabilidade Aérea |
| Caça B | 16000 | 10 | 70 | Air/AirLow | 8 (Jato) | 4 | Medium | 12 | 0 | W1: Seeker Missile (Medium/AntiInfantaria, maxAmmo=4); W2: Vulcan (Light/AntiAerea, maxAmmo=8) | - |
| Caminhão de Carga | 2000 | 10 | 60 | Land/Surface | 6 (Motor) | 1 | Light | 8 | 0 | - | - |
| Chinook | 4500 | 10 | 60 | Air/AirLow | 6 (Helice) | 3 | Light | 14 | 0 | W1: M60 (Light/AntiAerea, maxAmmo=8) | - |
| Destroyer | 30000 | 10 | 90 | Naval/Surface | 5 (Naval) | 4 | Heavy | 16 | 0 | W1: Cruise Missile (Light/AntiNavio, maxAmmo=4); W2: Spartan Missile (Heavy/AntiInfantaria, maxAmmo=4) | - |
| EWACS | 9000 | 10 | 90 | Air/AirHigh | 7 (Jato) | 3 | Medium | 12 | 0 | - | - |
| Fragata | 16500 | 10 | 90 | Naval/Surface | 5 (Naval) | 3 | Heavy | 15 | 0 | W1: Deck Gun (Light/AntiInfantaria, maxAmmo=8) | - |
| Hidroavião | 2000 | 10 | 30 | Air/AirLow | 4 (Helice) | 4 | Light | 8 | 0 | - | - |
| Lança Foguetes | 6200 | 10 | 50 | Land/Surface | 4 (Motor) | 3 | Light | 10 | 0 | W1: Katiusha (Medium/AntiAerea, maxAmmo=4) | - |
| MBT (Main Battle Tank) | 18000 | 10 | 70 | Land/Surface | 6 (Motor) | 3 | Heavy | 16 | 1 | W1: Canhão 105mm (Heavy/AntiTank, maxAmmo=6); W2: Chain Gun 30mm (Medium/AntiAerea, maxAmmo=9) | Infantrry Killer; Tanque Rápido; Battle Tank; Anti Artillery |
| Navio Tanque | 8000 | 10 | 120 | Naval/Surface | 5 (Naval) | 3 | Medium | 12 | 0 | - | - |
| Navio Transporte | 12000 | 10 | 90 | Naval/Surface | 4 (Naval) | 3 | Light | 10 | 0 | - | - |
| Obus Leve | 4000 | 10 | 30 | Land/Surface | 3 (Motor) | 3 | Light | 11 | 0 | W1: Projétil 115mm (Medium/AntiTank, maxAmmo=4) | Artilharia Frágil |
| Obuseiro Móvel | 14500 | 10 | 50 | Land/Surface | 4 (Motor) | 3 | Medium | 14 | 1 | W1: Projétil 150mm (Heavy/AntiTank, maxAmmo=6) | AntiVehicle; Anti Tank; Artilharia Frágil; Artilharia a prova de Balas |
| Porta Aviões | 22500 | 10 | 90 | Naval/Surface | 5 (Naval) | 3 | Heavy | 15 | 0 | W1: Spartan Missile (Heavy/AntiInfantaria, maxAmmo=4) | - |
| Radar Móvel | 5000 | 10 | 50 | Land/Surface | 4 (Motor) | 3 | Medium | 12 | 0 | - | - |
| Soldado | 1000 | 10 | 70 | Land/Surface | 3 (Marcha) | 3 | Light | 10 | 0 | W1: Rifle (Light/AntiAerea, maxAmmo=9) | - |
| Submarino | 24000 | 10 | 90 | Submarine/Submerged | 4 (Naval) | 3 | Light | 10 | 0 | W1: Torpedo (Light/AntiNavio, maxAmmo=6) | - |
| Super Tucano | 9000 | 10 | 80 | Air/AirLow | 6 (Helice) | 5 | Light | 12 | 0 | W1: Vulcan (Light/AntiAerea, maxAmmo=8); W2: Torpedo (Light/AntiNavio, maxAmmo=4) | - |
| Suprimentos | 3500 | 10 | 60 | Land/Surface | 5 (Motor) | 3 | Light | 8 | 0 | - | - |
| Surface Air Missile | 12000 | 10 | 40 | Land/Surface | 4 (Motor) | 3 | Medium | 13 | 1 | W1: Spartan Missile (Heavy/AntiInfantaria, maxAmmo=4) | Artilharia Frágil; Missile shot Heli |
| Tanque Leve | 6000 | 10 | 32 | Land/Surface | 4 (Motor) | 3 | Medium | 14 | 0 | W1: Canhão 100mm (Medium/AntiTank, maxAmmo=4); W2: Roof Gun (Light/AntiAerea, maxAmmo=9) | - |
| Tanque Pesado | 28000 | 10 | 50 | Land/Surface | 4 (Motor) | 3 | Heavy | 17 | 2 | W1: Canhão 110mm (Heavy/AntiTank, maxAmmo=9); W2: Chain Gun 30mm (Medium/AntiAerea, maxAmmo=9) | Carapara Dura; Artilharia a prova de Balas; Infantrry Killer; Battle Tank; Tank Superiority; Anti Artillery |
| Trem de Carga | 7500 | 10 | 120 | Land/Surface | 6 (Motor) | 3 | Medium | 12 | 0 | - | - |

## Leitura de balanceamento rapido
- O projeto tem spread de custo amplo (1000 a 30000), com classes navais e blindadas pesadas no topo.
- Visao deixou de ser padronizada em 3: unidades aereas e de reconhecimento agora variam entre 1 e 5, criando diferenciacoes reais de scouting.
- Super Tucano (visao 5) e o maior olheiro aereo da frota atualmente.
- Caminhão de Carga (visao 1) e o ponto cego — pensado para logistica pura, sem capacidade de observacao.
- A escalada de poder bruto combina `basicAttack` das armas + RPS + DPQ + elite modifiers; custo alto nao garante superioridade universal.

## Referencia cruzada: pouso e skills de unidade
- As capacidades de operacao aerea (ex.: VTOL/STOVL/landing) sao modeladas como skills e validacoes de camada no runtime.
- Regras de execucao de pouso/transicao e impacto no fluxo de acao: ver `07_relatorio_turn_state_manager.md`.
- Pre-requisitos por terreno/estrutura (allowed landing skills): ver `03_relatorio_terrenos_dpq.md`.

## Catalogo de Skills e Combat Modifiers por Unidade
- Fonte: `UnitData.skills` e `UnitData.combatModifiers` em `Assets/DB/Character/Unit`.
- Nomes exibidos: `displayName` (quando existe), com fallback para `m_Name`.

| Unidade | Skills | Combat Modifiers |
|---|---|---|
| Anti Arcraft Artilery | - | Artilharia Frágil; Gunner shot Plane; Gunner shot Heli |
| Apache | Aircraft Carrier Landing; VTOL | Helicopter Hunter |
| APC | Off - Road | - |
| Artilharia de Campanha | Precisa de Reboque | AntiVehicle; Anti Tank; Explosão Forte; Artilharia a prova de Balas |
| Avião Tanque | Aircraft Carrier Landing; Aircraft Landing | - |
| Bazooka | Guerrilha; Alpino | - |
| Bombardeiro | Aircraft Carrier Landing; Aircraft Landing | Chafflir |
| Caça A | Aircraft Landing; Aircraft Carrier Landing | Dog Fight; Manobrabilidade Aérea |
| Caça B | Short Vertical Landing Take Off; Aircraft Carrier Landing | - |
| Caminhão de Carga | a verificar | - |
| Chinook | VTOL | - |
| Destroyer | - | - |
| EWACS | Aircraft Carrier Landing; Aircraft Landing | - |
| Fragata | - | - |
| Hidroavião | Aircraft Landing; Aircraft Sea Landing | - |
| Lança Foguetes | - | - |
| MBT (Main Battle Tank) | - | Infantrry Killer; Tanque Rápido; Battle Tank; Anti Artillery |
| Navio Tanque | - | - |
| Navio Transporte | a verificar | - |
| Obus Leve | - | Artilharia Frágil |
| Obuseiro Móvel | - | AntiVehicle; Anti Tank; Artilharia Frágil; Artilharia a prova de Balas |
| Porta Aviões | - | - |
| Radar Móvel | - | - |
| Soldado | Guerrilha; Alpino | - |
| Submarino | Submerse Operations | - |
| Super Tucano | Aircraft Carrier Landing; Aircraft Landing | - |
| Suprimentos | Rebocador | - |
| Surface Air Missile | - | Artilharia Frágil; Missile shot Heli |
| Tanque Leve | - | - |
| Tanque Pesado | - | Carapara Dura; Artilharia a prova de Balas; Infantrry Killer; Battle Tank; Tank Superiority; Anti Artillery |
| Trem de Carga | Linha de Trem | - |
