# v3.0.7 - AI Rally Point

Esta versao cria a primeira camada jogavel do Rally Point como fase de montagem: o setor conquistado deixa de ser apenas mais um predio capturado e passa a funcionar como ponto de concentracao antes do avanco final.

## Rally como montagem

- Adicionado estado tatico de rally: `WaitHold`, `Assembling`, `Ready`, `GoGreen` e `Expired`.
- O planner cria objetivos `RallyAssembly` quando o rally do slot foi conquistado e ainda nao esta pronto para liberar o ataque.
- O readiness do rally conta hold local, capturadores, assalto/blindados, artilharia real, intel, logistica e ameacas visiveis.
- `GO_GREEN` libera o rally como linha de partida e remove a montagem do plano ofensivo.
- O log agora mostra `rallyState`, `goGreen`, `timeout`, `missing`, `cap`, `armor`, `art`, `intel`, `log` e `threat`.

## Semaforo do Rally

- O prefab de construcao ganhou o icone `rally` no HUD.
- Construcoes com `isRallyPoint` mostram semaforo apagado quando ainda nao pertencem ao slot dono do rally.
- Rally conquistado mostra vermelho quando ainda nao esta seguro.
- Rally em montagem/ready mostra amarelo.
- Rally liberado para invasao mostra verde.
- `ConstructionManager` atualiza o HUD do rally acompanhando mudanca de dono, setor, flag `isRallyPoint` e estado de readiness.
- Instancias antigas sem o filho `rally` podem criar o icone em runtime, usando as referencias serializadas no HUD.

## Comportamentos que leem Rally

- Capturadores reconhecem `RallyAssembly` e seguram/defendem o setor em vez de abandonar a posicao cedo demais.
- Assalto usa comportamento proprio de perimetro em rally, varrendo ameacas e ocupando borda funcional.
- Fire Support monta cobertura de retaguarda do rally e evita embarque quando a cabeca de ponte ainda nao existe.
- Intel desloca cobertura para o rally durante montagem, iluminando aproximacoes sem ir para a vanguarda.
- Logistica usa o rally como anchor quando existe montagem ativa.
- Transporte trata rally como staging/drop quando vinculado ao plano.

## Retaguarda comum

- A ferramenta `Tools/Utils/Retaguarda` foi alinhada com a leitura usada pela AI.
- Foi criado helper comum de retaguarda para evitar cada comportamento reinventar "ficar atras".
- Fire Support e Intel passaram a usar uma leitura mais conectada a linha aliada, evitando celulas isoladas que eram geometricamente atras, mas taticamente ruins.
- A vanguarda passou a ser puxada principalmente por capturadores e assalto, deixando suporte, intel e logistica como elementos de retaguarda.

## Fire Support e coordenacao de ataque

- Fire Support recebeu mais protecoes para nao virar peca de vanguarda por acidente.
- Reposicionamento passou a considerar melhor apoio vivo e anchor real do grupo.
- Ataques de assalto/capturador podem ceder a vez quando existe "martelo" melhor chegando no mesmo alvo, incluindo ataque aereo pesado.
- Logs de range-step, screen e bloqueios foram enriquecidos para explicar por que uma artilharia ficou, moveu ou nao achou celula.

## Repair e Logistica

- Repair de aeronaves ganhou fallback inedito para aeronaves sem VTOL/SVTOL quando aeroporto nao esta viavel.
- Aviao em reparo sem condicao de pouso pode procurar estrada proxima ou estrada com supridor por perto, ficando em espera para resgate logistico.
- Servico de suprimento/manutencao voltou a tratar pousar, atender e decolar como permissao/feedback visual quando a aeronave pode decolar.
- O supridor passou a consultar regras de suprimento antes de montar batch, evitando prometer servico impossivel para aeronave sem condicao de atendimento.

## Shopping e composicao

- Bombardeiro e tanque elite receberam mais espaco como pecas de pressao contra linha inimiga travada.
- Shopping recebeu ajustes para nao deixar cacas preventivos ocuparem toda a fatia quando a ameaca aerea real e baixa.
- Pressao de artilharia inimiga e stalemate aumentam incentivo por ruptura: assalto pesado, bombardeiro e fire support adequado.
- A demanda de Intel aerea segue limitada para evitar EWACS duplicado em mapa pequeno, com Radar Movel como complemento barato.

## HUD e ferramentas

- A camada de visao com tecla `L` separa exibicao geral, aerea, superficie e submarina.
- O painel `Panel_remaining/text_camada` mostra a camada atual.
- As camadas aparecem de acordo com capacidade produtiva do mapa: aeroporto libera ar, porto libera sub.
- A ferramenta de FoW validada (`Pode Detectar`) segue como fonte de verdade para deteccao especializada.

## Resultado esperado

O Rally Point agora comunica estado visualmente e influencia a AI como fase de concentracao. Antes do verde, a AI deve juntar capturador, assalto e suporte ao redor do ponto. Depois do verde, o rally deixa de ser destino final e vira ponto de partida para o ataque.

Ainda falta uma ponte mais explicita no shopping: hoje os slots do planner ja puxam demanda indiretamente, mas o shopping ainda nao le diretamente `missing=cap/assalto/support` para abrir compras direcionadas por readiness do rally.

## Validacao

- `dotnet build Assembly-CSharp.csproj`
- Resultado: 0 erros.
- Avisos: warnings obsoletos de Unity ja existentes no projeto.
