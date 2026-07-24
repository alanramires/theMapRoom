# Pendências Técnicas

*Divergências entre a regra canônica e o comportamento implementado.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

Esta seção não é regra. É a lista de pontos onde o comportamento implementado e a intenção declarada neste manual podem estar em desacordo, registrados aqui para que ninguém os descubra por acidente e os tome por doutrina.

**Sobre os identificadores.** Cada pendência tem um ID estável no formato `ÁREA-NNN`. O ID nunca é reaproveitado, mesmo depois de resolvido, e é por ele que uma entrada de auditoria em `92_auditoria.md` e um parágrafo canônico se referem à mesma regra sem depender de o texto permanecer idêntico. Áreas em uso: `LOG` logística, `COM` combate, `FOW` visão e informação, `AIR` operações aéreas, `IA` inteligência artificial.

Dois itens que já foram desta lista — a isenção de consumo por presença no aeroporto e o alcance do Serviço do Comando aos passageiros — saíram porque **não são divergências**: são design deliberado. Migraram para `91_decisoes_de_design.md`. Os IDs `AIR-001` e `LOG-001` ficam aposentados e não serão reaproveitados.

---

### LOG-002 — Peso de munição com valor global escondido

**Regra canônica.** O peso por classe de munição é atributo da ficha de serviço.

**Comportamento atual.** Quando a ficha não declara o peso, o sistema aplica 3/2/1 por conta própria. Hoje é o caso de Reabastecimento e Reparos.

**Evidência.** `ServiceData.cs:173-181`.

**Impacto.** Baixo em comportamento, alto em previsibilidade: quem edita a ficha não vê o valor que está em vigor.

**Status.** Aberta.

---

### FOW-001 — Duas fontes para a duração da emersão forçada

**Regra canônica.** O submarino fica exposto por dois turnos do proprietário, tanto ao atacar quanto ao ser atingido (`05_visao_deteccao_e_nevoa.md`).

**Comportamento atual.** A emersão por ataque próprio lê a duração da ficha do submarino; a emersão por dano recebido usa um valor fixo do sistema. Hoje ambos valem 2 e o comportamento é coerente.

**Evidência.** `UnitData.emergeAfterAttackTurns` contra `ScannerPrompt.cs:4118` (`const int forcedTurns = 2`).

**Impacto.** Latente. Mudar a ficha desalinharia os dois sem aviso nenhum.

**Status.** Aberta.

---

### IA-001 — A inteligência artificial trata alcance 0 como 1

**Regra canônica.** Alcance 0 é recurso suportado, para armamento lançado sobre o próprio setor contra alvo em outro andar (`06_combate.md`).

**Comportamento atual.** O simulador de combate da IA recusa distância 0 de saída, e o chamador compensa enviando 1 no lugar. A IA avalia o ataque como se fosse adjacente, supõe um contra-ataque que nunca vai acontecer, e **subestima** a arma.

**Evidência.** `AICombatHpSimulator.cs:80` e `TurnStateManager.Automation.cs:391`.

**Impacto.** Uma fragata sob controle da IA não caça submarino com carga de profundidade.

**Status.** Aberta, adiada por decisão — a IA naval ainda não foi construída.

---

### FOW-002 — A divisão C/D/T contra E/F/S em terreno explorado não tem princípio declarado

**Regra canônica.** Com destino provisório em terreno apenas explorado, ficam liberados ataque, desembarque, captura e transferência; embarque, fusão e suprimento seguem suprimidos (`04_ciclo_de_acao_e_comprometimento.md`).

**Comportamento atual.** É exatamente isso que o código faz. O problema não é divergência — é que **nenhum princípio conhecido explica a linha**.

O argumento anti-oráculo justificaria bloquear o que revela presença inimiga. Mas os seis verbos miram **aliados ou construções**: transferência só alcança unidades do próprio time (verificado, FOW-020), embarque é sobre transportador aliado, fusão sobre unidade aliada, suprimento sobre unidade aliada, captura sobre construção estática e lembrada pela fotografia. Nenhum deles vaza inimigo. Logo, o critério que separa os liberados dos bloqueados não é o anti-oráculo — e não está escrito em lugar nenhum qual é.

**Evidência.** `TurnStateManager.Sensors.cs:460-499` (`RunExploredTerrainContextSensors`). O único comentário diz "Em terreno apenas memorizado, E/F/S continuam sob a barreira anti-oraculo", sem justificar por que E/F/S e não C/D/T.

**Impacto.** Baixo em jogo, alto em manutenção. Sem princípio declarado, a próxima pessoa que mexer nesses sensores não tem como decidir de que lado um verbo novo cai — e o manual declara em `00` que cada regra deve ter um endereço e um motivo.

**Decisão necessária.** Uma das três: (a) existe um motivo que não foi registrado, e basta escrevê-lo; (b) a linha foi traçada por instinto e deve ser refeita sob um princípio único; (c) a linha está certa e o princípio é outro que ainda não nomeamos.

**Status.** Aberta, sem prioridade.

---

### FOW-003 — Recusa de desembarque nomeia unidade não detectada

**Regra canônica.** O menu nunca filtra pela verdade oculta, e nenhuma recusa deve ensinar o que o time não sabe (`04_ciclo_de_acao_e_comprometimento.md`).

**Comportamento atual.** A checagem de ocupação do desembarque lê a ocupação crua, sem filtro de névoa, e devolve o **nome** do bloqueador: `"Hex ocupado por {blocker.name}"`. Isso entrega a identidade de uma unidade que o jogador nunca detectou.

**Evidência.** `PodeDesembarcarSensor.cs:295-299`.

**Impacto.** Vazamento real, e de categoria pior que os demais: presença já seria informação, mas identidade não tem equivalente em nenhum outro sensor. Acontece hoje em terreno visível e explorado, onde o desembarque roda normalmente.

**Decisão necessária.** Recusar sem nomear, e sem distinguir "ocupado" de "inválido" quando a unidade não é conhecida pelo time — o padrão de motivo neutro que a exceção de ataque no escuro já usa.

**Status.** Aberta. Consertar independe da decisão de FOW-002.
