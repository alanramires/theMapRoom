# AI Versao 1 Exercito

## Visao geral

A versao 3.0.0 marca a primeira IA que passa a se comportar como um exercito coordenado, nao como unidades isoladas correndo para o alvo. O foco desta versao foi consolidar pressao organizada: blindados na frente, soldados capturando e explorando oportunidades, artilharia em apoio, suprimentos na retaguarda e pontos de rally reunindo forca antes da invasao.

## Principais avancos

- Progressao passou a considerar melhor congestionamento aliado, evitando jogar unidades no meio de engarrafamentos quando existem rotas equivalentes mais limpas.
- Fire support foi unificado no padrao de progressao, com melhor uso de posicionamento e rotas validas.
- Rally assembly ganhou papel claro no plano: o marcador `+` indica "junta aqui", evitando confusao com nomes de setores.
- Transportadores e APCs respeitam melhor rendezvous e preparacao antes de invasao, incluindo criterio minimo de assalto proximo ao rally.
- Unidades em repair podem atirar quando estao no local seguro de reparo e possuem alvo valido.
- Logistica ganhou decisao de restock, usando estoque de galao, municao e ferramentas para voltar a bases/cidades aliadas seguras.
- Suprimentos conseguem usar transferencia em modo receber depois do movimento, sem precisar gastar um turno so para chegar.
- Shopping passou a reagir melhor a stalemate e pressao por elite, mantendo a composicao do exercito mais coerente.
- Bazooka permanece como compra defensiva, reservada para defesa de base/ameaca defensiva, enquanto paridade blindada estrategica favorece tanques.

## Resultado observado

A IA agora forma uma linha de frente mais convincente:

- tanks e assaltos sustentam a frente;
- soldados aproveitam abertura para capturar;
- artilharia apoia por tras sem se expor de forma gratuita;
- suprimentos recuam e reabastecem em seguranca;
- rally points concentram tropas antes do avanco;
- o exercito pressiona de forma continua em vez de dispersar unidades.

## Nota de design

Esta versao estabelece a base da "AI Versao 1 Exercito": uma IA que entende composicao, retaguarda, frente, apoio e acumulacao de forca. Ainda existem ajustes finos a fazer, mas o comportamento central ja mudou de corrida individual para operacao coordenada.
