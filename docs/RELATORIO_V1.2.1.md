# Relatorio de Atualizacao - v1.2.1

## Em uma frase
A v1.2.1 revisa custos e logistica de servicos, unifica as formulas entre runtime e tools e melhora a leitura operacional no helper.

## O que isso trouxe na pratica
- `Pode Suprir` e `Servico do Comando` passam a usar a mesma base de calculo para consumo e custo.
- Simuladores/debug e gameplay mostram os mesmos resultados para o mesmo cenario.
- Custos de HP, fuel e ammo seguem arredondamento operacional consistente.

## Principais melhorias
1. Formula unica de custo e consumo
- Encapsulamento da regra central de economia/logistica em helper compartilhado.
- Reuso da mesma funcao pelos 4 fluxos: runtime/tools de `Pode Suprir` e runtime/tools de `Servico do Comando`.
- Eliminacao de divergencia entre previsao (tool) e execucao (runtime).

2. Service Data revisado
- Renomeacao de `Points recover` para `Points recover per 1 unit of supply used`.
- Inclusao da lista `Cost Weight` (entry + armor/weapon class + value) para ponderacao de custo por arma.
- Ajustes de editor para suportar os novos campos com nomenclatura final.

3. Blocos de custo com arredondamento operacional
- HP: custo unitario arredondado e aplicado por recuperacao efetiva.
- Fuel: custo por autonomia arredondado (ex.: bloco de 10% / autonomia base) e aplicado por unidade recuperada.
- Ammo: media ponderada por arma, custo por disparo arredondado por slot e multiplicado pelos tiros recuperados.

4. Helper de Servico do Comando
- Padronizacao de etiquetas de ganho de municao para `W1+`, `W2+`, `W3+...` (substituindo `P+`/`S+`).
- Leitura mais universal para jogadores e alinhada com convencoes de HUD.

5. Correcoes e consistencia tecnica
- Correcao de referencia ausente `IsAnyAmmoMissing` em janela de debug de Servico do Comando.
- Consolidacao da regra descrita em `docs/logica de suprimentos.md` no fluxo implementado.

## Regras importantes
- `Pode Suprir` e `Servico do Comando` compartilham a mesma logica de custo/consumo.
- O valor final deve bater entre runtime e simuladores para a mesma entrada.
- O custo de municao deve considerar peso por arma (`Cost Weight`) antes do arredondamento por disparo.

## Bloco tecnico curto
- Scripts-chave: `ServiceData`, `TurnStateManager.Supply`, `TurnStateManager.CommandService`, `PodeSuprirSensorDebugWindow`, `ServicoDoComandoDebugWindow`, `PanelHelperController`.
- Documentacao-base da regra: `docs/logica de suprimentos.md`.

## Resultado
A v1.2.1 fecha a revisao de economia de servicos com formula unificada, previsibilidade entre ferramentas e runtime, e helper mais claro para tomada de decisao em partida.
