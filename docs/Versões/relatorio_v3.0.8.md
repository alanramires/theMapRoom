# v3.0.8 - Reformulação do Shopping e Novos Papeis

Esta versão estabiliza problemas de composição observados depois do Rally Point e prepara a substituição estrutural do shopping por um sistema orientado a papéis. Também melhora repair de transportes aéreos e a leitura visual de carga no HUD.

## Composição e shopping

- Rallys antigos deixam de reter vagas ilimitadas de Fire Support quando perdem o estado de montagem.
- Objetivos comuns mantêm no máximo uma escolta de fogo; rallys usam as três vagas explícitas de artilharia.
- Artilharias excedentes são liberadas e redistribuídas para outros objetivos antes de abrir novas compras.
- Economia madura pode priorizar assalto elite em vez de continuar comprando suporte básico saturado.
- MBT permanece elegível quando o Tanque Pesado da próxima cadeia elite ainda não cabe no orçamento.
- Reservas aéreas não reduzem o orçamento do produtor que pode executar uma compra elite prioritária.
- SAM deixou de contar como artilharia elite terrestre.
- Somente blindados terrestres de assalto contam para a progressão elite de tanks.

## Novos papéis

O enum `UnitRole` ganhou papéis explícitos para a futura reformulação:

- `Antiaereo`
- `RaidAntiSub`
- `CapturadorAgressivo`
- `ArtilheiroCombatente`
- `AntiaereoCombatente`
- `LogisticaMovel`
- `LogisticaEstoque`

Os valores foram adicionados ao final do enum, preservando a serialização dos papéis existentes. A migração dos assets e a compatibilidade controlada serão implementadas na próxima etapa.

## Direção da reformulação

- Criado `docs/Reformular Shopping.md` com a classificação das unidades de Exército e Aeronáutica.
- A composição terrestre planejada permanece em dois capturadores, dois assaltos e um Fire Support.
- Papéis híbridos deverão atender apenas uma demanda por unidade por meio de compatibilidade controlada.
- O novo shopping será orientado por operações, composição, stance, foco de alvo e progressão elite, sem hardcode por nome.
- A Marinha permanece provisória e fora desta etapa.

## Rally e planner

- Readiness do rally passou a considerar força conhecida, pacotes de ruptura, ataque aéreo e mínimo real de artilharia.
- Slots excedentes de rally são removidos mesmo quando já estavam preenchidos.
- Unidades liberadas têm a atribuição limpa para poderem entrar novamente no solver do plano.
- Fire Support livre é distribuído entre frentes em vez de recriar vagas ilimitadas no objetivo de maior prioridade.

## Repair e transporte aéreo

- Helicópteros de transporte em manutenção liberam passageiros ao chegar a uma instalação segura de reparo.
- Movimento e desembarque podem ser preparados no mesmo batch de chegada.
- A escolha da instalação consulta `PodeDesembarcar` e rejeita destinos que prenderiam toda a carga.
- Instalações capazes de liberar mais passageiros recebem preferência no score de repair.

## HUD de transporte

- O indicador de transporte diferencia uma carga única de múltiplos passageiros.
- `UnitManager` expõe a contagem atual de passageiros embarcados.
- O prefab de unidade recebeu referências para os sprites de transporte simples e múltiplo.

## Conteúdo e editor

- Atualizados prefab de construção, prefab de unidade e Battle Map 2 - Air.
- Adicionado o ícone `transportando2` e seus metadados.
- Atualizados assets de fonte usados pelo HUD.

## Validação

- `dotnet build Assembly-CSharp.csproj`
- Resultado: 0 erros.
- Permanecem apenas avisos obsoletos já existentes nas APIs Unity.

