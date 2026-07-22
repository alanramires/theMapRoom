# Pendências Técnicas

*Divergências entre a regra canônica e o comportamento implementado.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

Esta seção não é regra. É a lista de pontos onde o comportamento implementado e a intenção declarada neste manual podem estar em desacordo, registrados aqui para que ninguém os descubra por acidente e os tome por doutrina.

**Sobre os identificadores.** Cada pendência tem um ID estável no formato `ÁREA-NNN`. O ID nunca é reaproveitado, mesmo depois de resolvido, e é por ele que uma entrada de auditoria em `92_auditoria.md` e um parágrafo canônico se referem à mesma regra sem depender de o texto permanecer idêntico. Áreas em uso: `LOG` logística, `COM` combate, `FOW` visão e informação, `AIR` operações aéreas, `IA` inteligência artificial.

---

### AIR-001 — Isenção de consumo não verifica se a aeronave pousou

**Regra canônica.** Aeronave **pousada** em instalação aeronáutica não paga consumo de autonomia (`07_logistica_e_servicos.md`).

**Comportamento atual.** A isenção é concedida pela presença sobre o hexágono da instalação, sem verificar a camada. Uma aeronave **sobrevoando** o próprio aeroporto também deixa de pagar.

**Evidência.** `OperationalAutonomyRules.cs:75-102`.

**Impacto.** Contraria a razão de design da arremetida automática, que existe para manter a aviação operacional dentro do alcance da antiaérea. Permite estacionar no ar de graça sobre a própria base.

**Status.** Aberta. Decisão necessária: corrigir o código ou aceitar como regra.

---

### LOG-001 — Caminhão atende passageiros pelo Serviço do Comando

**Regra canônica.** Apenas construções atendem os passageiros de um transportador; o caminhão atende só a unidade que encostou (`07_logistica_e_servicos.md`).

**Comportamento atual.** Vale para o suprimento prestado em campo, mas não para o lote do Serviço do Comando, onde o caminhão alcança quem está embarcado.

**Evidência.** `ServicoDoComandoSensor.cs:412-421` ignora `serviceRange`; `PodeSuprirSensor.cs:183-276` respeita.

**Impacto.** Dois caminhos do mesmo serviço com regras diferentes. Um dos dois está errado.

**Status.** Aberta.

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
