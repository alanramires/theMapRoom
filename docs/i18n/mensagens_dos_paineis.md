# Mensagens dos painéis: autoria e preparação para i18n

## Onde editar

- `Assets/DB/Messages/Dialog Data`: mensagens curtas do painel inferior esquerdo.
- `Assets/DB/Messages/Helper Data`: títulos, botões, confirmações e corpo do painel de apoio.
- `Dialog Database.asset` e `Helper Database.asset`: registram os assets usados pelos respectivos painéis. Os prefabs `Panel_dialog` e `Panel_helper` já referenciam essas bases.

Abra **Tools > Messages > Catalogo dos paineis**. A busca encontra texto, ID, condição e arquivo de origem. Expanda uma mensagem para abrir os usos no código; **Editar asset** seleciona sua ficha. **Atualizar / validar** relê o conteúdo e aponta IDs duplicados, campos vazios e assets sem cadastro. Não é preciso entrar em Play.

O [índice gerado](panel_messages_index.md) permite consultar as mesmas chaves fora da Unity. Ele inclui referências literais, inclusive constantes usadas como IDs; chaves construídas dinamicamente exigem seguir o código.

## Contrato de autoria

O `id` é estável e não é traduzido. Edite `message` (português) e, opcionalmente, `messageEnglish` (inglês); `condition` explica o contexto e, nos novos assets, os parâmetros recebidos.

Por exemplo, `helper.remove_unit.confirm` contém `A unidade <unit> vai ser removida.`. Uma tradução pode escrever `Remove <unit>?` sem alterar o código. Preserve os nomes dos tokens; mude livremente sua posição. Preserve também as tags de apresentação do TextMeshPro.

Os tokens antigos com ponto ou espaço, como `<transfer_type.selecionado>` e `<transfer type>`, continuam funcionando. Novas mensagens devem preferir nomes simples, como `<unit>`, `<count>` e `<cost>`. A substituição ocorre uma única vez: conteúdo inserido não é interpretado novamente como template.

Para acrescentar uma mensagem:

1. Crie um `HelperData` ou `DialogData` na pasta correspondente, com ID único.
2. Escreva a frase completa e descreva os tokens em `condition`.
3. Registre o asset na respectiva Database.
4. Use `PanelMessage.Helper("helper.…", ("unit", nome))` ou `PanelMessage.Dialog("panel_dialog.…", ("unit", nome))` na apresentação.
5. Execute a auditoria e confira a tela.

As chamadas antigas `ResolveHelperMessage`/`ResolveDialogMessage` continuam válidas. Seus fallbacks preservam contextos sem painel, como ferramentas de Editor. Em runtime, o texto do asset tem prioridade. As novas chamadas `PanelMessage` mostram `[id]` quando a definição não é encontrada, e as bases avisam uma vez por ID ausente ou vazio. Elas devem ser usadas depois da inicialização dos painéis, nunca em inicializadores estáticos.

## O que foi centralizado

Títulos e ações dos painéis, confirmação de ataque/embarque/desembarque/fusão/supply/transferência, dados de visão e estoque, mensagens e categorias do Jornal, campanha e resultados, assistente de nova partida, regras mostradas pelo assistente, Sobre, confirmações de menu, avisos diretos de save/load, movimento e combate, além de mensagens antigas que tinham ID mas não possuíam asset.

Os marcadores `HP:`, `MOV:`, `AUT:`, `SECTION:*` e `||SUPPLIES||` entre TurnState e o helper continuam sendo protocolo interno. Eles não devem ser traduzidos. A apresentação correspondente consulta os assets. O filtro de ganhos zero passou a usar números antes de formatar, e o título do estoque é um parâmetro, sem procurar palavras numa frase traduzida.

IDs duplicados agora geram aviso nas bases. A formatação de tokens é compartilhada entre as duas bases, seus painéis e o catálogo legado. `Dialog Catalog.asset` é legado: não foi encontrado consumidor runtime de `DialogCatalog` além da própria classe; os painéis usam as Databases.

## Limites desta preparação

Esta entrega centraliza as mensagens extraídas, mas não é uma tradução completa do jogo nem um seletor de idioma em tempo real.

- Nomes e descrições de unidades, armas, terrenos, construções, suprimentos, mapas e histórias continuam nas fichas de conteúdo que os fornecem. Os painéis recebem esses valores como parâmetros.
- Alguns motivos de recusa ainda chegam como frases dos sensores/regras, sem ID. Exemplos: `PodeCapturarSensor`, motivos de transporte/supply e fallbacks de validação de fusão. Para traduzir esses casos, o próximo passo é expor ID e parâmetros junto do resultado, como já ocorre em `PodeMirarInvalidOption` e `PodeFundirInvalidOption`; não decidir regras comparando traduções.
- `TeamUtils.GetName`, nomes de domínio/altura vindos de enums e descrições de camadas ainda podem aparecer como valores externos. Rótulos do assistente e da visão já têm mensagens próprias, mas isso não cobre todas as fontes do jogo.
- O Jornal armazena alguns detalhes já formatados em saves. Editar o catálogo muda eventos novos, mas não reescreve frases antigas salvas. Troca de idioma de históricos exige persistir ID e parâmetros desses detalhes.
- Elementos de outros painéis/HUD, textos serializados em cenas/prefabs, diálogos autorados dos tutoriais e mensagens de diagnóstico do Console não foram tratados como textos desses dois painéis.
- Cultura de números/datas, pluralização e atualização de telas já abertas devem fazer parte da futura seleção de idioma. A formatação monetária existente em pt-BR foi preservada.

## Teste parcial em inglês

O campo **Message (English)** existe em todos os `DialogData` e `HelperData`. Pode ficar vazio: nesse caso, inclusive se contiver apenas espaços, a mensagem usa o português. Os IDs e os nomes dos tokens continuam iguais nos dois idiomas.

Abra **Tools > Messages > Idioma dos paineis**, escolha **English** no campo Language e depois entre em Play. A configuração compartilhada está em `Assets/DB/Messages/Resources/Panel Message Language.asset`; o padrão entregue é **Portuguese Brazil**. Se esse asset estiver ausente, o código também usa português. Para voltar, saia do Play e selecione português.

Há quatro exemplos preenchidos: `aim.invalid.attacker_blocked_at`, `helper.action.cancel`, `helper.action.confirm` e `helper.remove_unit.confirm`. O catálogo pesquisa os dois textos e mostra quantas mensagens têm inglês. Não é necessário traduzir todas para testar.

Selecione o idioma antes de entrar em Play. Textos que menus já armazenaram e detalhes antigos do Jornal não são reformatados retroativamente. Esta configuração não adiciona um menu de idioma ao jogador.

## Verificação

```text
python tools/audit_panel_messages.py
python tools/audit_panel_messages.py --write-index
dotnet run --project tools/tests/MessageTemplateChecks/MessageTemplateChecks.csproj
bash tools/compilar.sh
```

A auditoria verifica IDs, registro por GUID, referências literais, equivalência dos tokens nas traduções preenchidas e argumentos das chamadas `PanelMessage` com ID literal. Não prova a ausência de todo texto hardcoded nem valida valores dinâmicos de sensores. Os testes de formatação cobrem reordenação, tags TMP, tokens antigos, caixa, nulos e substituição sem recursão.

Antes de publicar, conferir em Play:

- nova partida e confirmação de quadrante;
- compra, confirmação de ataque e cancelamento;
- estoque e supply com HP/fuel/ammo iguais a zero;
- transferência, incluindo o token legado com ponto;
- Jornal, save/load e volta à campanha;
- uma mensagem temporariamente mais longa no asset, por teclado, mouse e toque.

Compilação e testes estáticos não substituem essa verificação visual.
