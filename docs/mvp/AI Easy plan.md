# AI Easy Mode — Plano

## Objetivo

Criar uma IA fácil que continue jogando pelas mesmas regras do jogador, mas seja mais imediatista, previsível e menos eficiente estrategicamente.

O modo Easy não deve alterar HP, dano ou regras das unidades. A dificuldade será reduzida por limitações de informação, planejamento, economia e coordenação.

## Identidade do modo Easy

- Não consulta o `JogadasManager` para prever ou interpretar o comportamento do jogador.
- Não compra unidades Elite.
- Usa avaliações locais do `HexEvaluator` para a maior parte das decisões.
- Não mantém reserva estratégica: tenta gastar todo o saldo disponível.
- Trabalha com dois eixos operacionais em vez de três.
- Respeita integralmente o Fog of War.
- Só considera como alvos unidades atualmente detectadas por alguma unidade aliada.
- Mantém as mesmas regras de combate, movimento, logística, HP e dano do modo Normal.

## Comparação inicial

| Sistema | Easy | Normal |
|---|---|---|
| Inteligência | Visibilidade atual | Jogadas, memória e inteligência acumulada permitida |
| Planejamento | Avaliação local por hex | Planos, objetivos e contexto estratégico |
| Eixos | 2 | 3 |
| Compra de Elite | Desativada | Permitida |
| Economia | Gasta o saldo disponível | Mantém reservas estratégicas |
| Seleção de alvos | Somente contatos detectados | Conhecimento permitido pelo sistema de inteligência |
| Coordenação | Local e imediatista | Combinada entre unidades e objetivos |

## Arquitetura sugerida

Criar uma dificuldade centralizada, preferencialmente como enum:

```csharp
public enum AIDifficulty
{
    Easy,
    Normal,
    Hard
}
```

As diferenças devem ser expostas por uma política única, por exemplo `AIDifficultyRules`, evitando espalhar verificações como `if (easyMode)` por toda a IA.

Exemplos de propriedades da política:

- `CanReadJogadasManager`
- `CanPurchaseElite`
- `UsesStrategicReserves`
- `MaximumOperationalAxes`
- `UsesStrategicPlanner`
- `RequiresCurrentDetectionForTargets`
- `IncomeMultiplier`

O valor inicial de `IncomeMultiplier` deve permanecer em `1.0`. Uma redução para aproximadamente `0.85` só deve ser considerada se as limitações comportamentais ainda não forem suficientes.

## Regras por subsistema

### Inteligência e Fog of War

- A IA Easy não pode acessar diretamente unidades ocultas.
- Um inimigo só pode entrar na lista de alvos quando estiver atualmente detectado.
- Perder contato deve remover o inimigo das decisões táticas atuais.
- Não consultar o `JogadasManager` significa perder antecipação e memória estratégica, não desativar as regras do Fog of War.

### Planejamento e HexEvaluator

- Priorizar decisões locais baseadas no `HexEvaluator`.
- Evitar planejamento de longo prazo, leitura macro do mapa e coordenação sofisticada.
- Não reativar diretamente código legado que possa estar desconectado das regras atuais.
- Manter o roteador e os executores modernos, mas fornecer a eles decisões simplificadas pela política Easy.

### Eixos operacionais

- Limitar a IA Easy a dois eixos.
- Preservar estabilidade de eixo para evitar alternância caótica entre turnos.
- Reduzir a capacidade de pressionar várias regiões simultaneamente.

### Shopping

- Excluir unidades Elite da lista de candidatos.
- Não criar ou preservar reserva estratégica.
- Continuar respeitando composição mínima, disponibilidade da construção e demais regras de compra.
- Escolher compras usando avaliação local e necessidades imediatas.
- Tentar consumir todo o saldo possível sem realizar compras inválidas.

Observação: gastar tudo pode tornar o começo da partida agressivo. O desperdício deve surgir da baixa qualidade estratégica das compras, não da violação de regras ou da compra aleatória de itens inúteis.

### Combate

- Atacar somente alvos atualmente detectados.
- Preferir confrontos claramente favoráveis.
- Reduzir coordenação de combos entre múltiplas unidades.
- Não adicionar erros aleatórios inicialmente: as limitações de informação e planejamento já devem produzir erros naturais e compreensíveis.

### Logística e serviços

- Manter abastecimento, reparo e serviços funcionais.
- Usar prioridades mais locais e imediatas.
- Não comprometer regras básicas que poderiam deixar a IA travada ou incapaz de operar.

## Implementação incremental

1. Introduzir `AIDifficulty` e `AIDifficultyRules` sem alterar o comportamento Normal.
2. Persistir a dificuldade na configuração da partida e no save.
3. Limitar o Easy a dois eixos.
4. Bloquear compras Elite no Easy.
5. Desativar reservas estratégicas e permitir gasto integral do saldo.
6. Desativar leitura do `JogadasManager` no Easy.
7. Filtrar snapshots e candidatos para incluir apenas inimigos atualmente detectados.
8. Direcionar o planejamento Easy para avaliações locais do `HexEvaluator` mantendo os executores atuais.
9. Avaliar força real em partidas repetidas antes de aplicar qualquer penalidade de renda.

## Critérios de aceitação

- O modo Normal mantém exatamente o comportamento atual.
- A IA Easy nunca compra Elite.
- A IA Easy nunca toma decisão usando uma unidade inimiga oculta.
- A IA Easy não consulta dados do `JogadasManager`.
- A IA Easy cria no máximo dois eixos operacionais.
- A IA Easy não mantém reserva voluntária quando existe uma compra válida.
- A IA continua capaz de comprar, mover, atacar, capturar, abastecer, reparar e passar o turno.
- Save/load preserva corretamente a dificuldade escolhida.
- Batch e apresentação visual executam as mesmas decisões do modo Easy.

## Testes recomendados

- Easy contra jogador em mapa com Fog of War Total.
- Easy perdendo e recuperando contato visual com um alvo.
- Shopping com saldo suficiente para Elite, confirmando sua exclusão.
- Shopping com saldo residual, confirmando tentativa de gasto sem loop infinito.
- Mapa grande, confirmando o limite de dois eixos.
- Save/load durante o turno da IA Easy.
- Comparação determinística Easy versus Normal usando a mesma seed e configuração inicial.
- Partida longa para verificar travamentos causados pela simplificação do planejamento.

## Ajustes posteriores

Somente depois dos testes comportamentais:

- Reduzir a renda da IA Easy para 85%, se ela ainda mantiver pressão excessiva.
- Limitar objetivos ofensivos simultâneos.
- Aumentar a preferência por confrontos seguros.
- Introduzir atraso de um turno para reagir a informações recém-descobertas, caso necessário.

