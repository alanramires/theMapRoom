# Briefing — cena `Campanha` (seleção de quadrante)

Para quem vai construir a cena **em paralelo**, sem contexto da conversa que gerou
o resto. Leia inteiro antes de escrever a primeira linha: metade daqui é sobre o
que **não** fazer, e é essa metade que evita colisão com a frente que está aberta.

> **A outra frente está mexendo em:** `QuadranteController`, o bake, o
> `MapHelperWindow` e as classes de dado. **Não toque nesses arquivos.**

---

## 1. O que a cena é

O jogador escolhe **onde vai lutar**. Ela mostra o mundo, o jogador clica num
quadrante, e o jogo carrega a cena `Batalha` já configurada para aquele pedaço.

```text
MUNDO           um asset, uma cena de autoria (não é esta cena)
 └─ BLOCO           Auridia · Europeu · Rússia
     └─ CAMPANHA        Feijão Torto · Terra Firme
         └─ QUADRANTE       ← o jogador clica AQUI, e isso vira uma partida
```

Depois de ganhar ou perder, volta para cá. **Dentro da partida não há travessia**:
o quadrante é fechado, e a única saída é o fim do jogo. Não desenhe setas nem
indicadores de "vá para o quadrante vizinho" — prometeriam um mecanismo que não
existe.

---

## 2. O dado — leitura apenas

Tudo vive num único `ScriptableObject` do tipo **`MundoData`**
(`Assets/DB/Campanha/Mundo Fixture.asset` é o que existe hoje).

```csharp
MundoData
  string mundoId, displayName, descricao
  Sprite foto                       // pensada para o menu, pode servir aqui
  string authoringSceneName         // documentação; NÃO carregue essa cena
  List<BlocoData> blocos

BlocoData      : INoDoMapa
  string blocoId, displayName, descricao
  int originX, originY, width, height          // retângulo, em célula
  List<string> destravadoPor;  bool exigeIrmaos
  List<CampanhaData> campanhas

CampanhaData   : INoDoMapa    // mesmos campos + List<QuadranteData> quadrantes
QuadranteData  : INoDoMapa    // mesmos campos +
  List<TileBase> bakedTiles              // row-major: índice = (y * width) + x
  List<ConstrucaoAssada> bakedConstrucoes
  bool HasBake                           // use isto antes de confiar no bake
  TileBase GetBakedTile(int localX, int localY)
```

`INoDoMapa` dá acesso uniforme a `Id`, `Nome`, `Descricao`, `OriginX/Y`,
`Width/Height`, `DestravadoPor`, `ExigeIrmaos` — **use a interface** se for
escrever código que serve aos três níveis. Foi para isso que ela existe.

### Busca

```csharp
mundo.TryGetBloco(id, out BlocoData bloco)
mundo.TryGetCampanha(campanhaId, out BlocoData bloco, out CampanhaData campanha)
mundo.TryGetQuadrante(campanhaId, quadranteId, out bloco, out campanha, out quadrante)
mundo.AllCampanhas() / mundo.AllQuadrantes()      // enumeração de todo o mundo
```

⚠️ **Ids são únicos no mundo inteiro** e a busca casa por string, devolvendo
sempre **o primeiro**. Não invente chave composta com o bloco: o endereço de uma
partida é `(campanhaId, quadranteId)`, deliberadamente sem o bloco — assim mover
uma campanha de bloco não invalida save.

---

## 3. O contrato de saída — como lançar uma partida

**Este é o ponto de fronteira. Não invente outro.**

O canal é o `PartidaConfig`, um `static` que já existe e já é usado pelo menu
principal. Ele tem semântica de **consumo único**: quem produz chama `Set`, quem
consome chama `Apply`/`TryConsume`, e depois `Clear`.

```csharp
PartidaConfig.Set(
    playerCount, teams, isAI, flipX, preset, commandServiceAutomatic,
    targetScene: "Batalha");

PartidaConfig.SetDifficulty(dificuldade);

// ⚠️ ESTE MÉTODO AINDA NÃO EXISTE — a outra frente vai criá-lo.
// Programe contra esta assinatura; ela está acordada.
PartidaConfig.SetQuadrante(campanhaId, quadranteId);

SceneManager.LoadScene("Batalha");
```

O `QuadranteController` da cena `Batalha` lê o endereço no `Awake` e pinta. **Você
não precisa saber como** — e não deve chamar nada dele.

Se `SetQuadrante` ainda não existir quando você chegar aqui, **pare e avise** em
vez de contornar. Contornar cria um segundo canal, e dois canais divergem.

---

## 4. O que desenhar — e o que NÃO existe ainda

### ⚠️ Não existe bake do mundo inteiro

O bake é **por quadrante**. Não há um tilemap assado do mundo todo, então **não dá
para pintar o terreno do mundo** hoje.

Duas saídas honestas, nesta ordem de preferência:

**(a) Esquemático.** Retângulos dos blocos, campanhas e quadrantes, desenhados a
partir de `origin` + `width/height`, com nome e descrição. Sem terreno. É o que
funciona hoje, e é suficiente para escolher.

**(b) Mosaico dos bakes.** Cada quadrante tem `bakedTiles` **e** o `origin` de
autoria. Pintando cada quadrante no seu `origin`, você reconstrói o mundo menos os
buracos entre quadrantes. Funciona com o dado que existe, e fica bonito.

**Não faça:** carregar a cena de autoria em runtime. Ela não entra no build, de
propósito.

### ⚠️ Não existe arquivo de progresso

O tint de domínio — quadrante na cor de quem conquistou — está **desenhado mas não
implementado**. Não existe onde ler "quem venceu o quê".

Então: **mostre a estrutura, não o progresso.** Deixe a cor como costura (uma
função `Color CorDoQuadrante(QuadranteData)` que hoje devolve neutro), para que
ligar o progresso depois seja trocar o corpo dela.

**Não invente um modelo de progresso.** Ele já está especificado em
[`plano_campanha.md`](plano_campanha.md) e é da outra frente.

### O destrave também não avalia

`destravadoPor` e `exigeIrmaos` estão nos dados e **ninguém os avalia** ainda. Pode
mostrar como informação ("requer X"), mas não bloqueie o clique por conta própria.

---

## 5. Regras da casa que valem aqui

**A lei transacional.** Nada é definitivo antes do compromisso. Clicar num
quadrante **abre os detalhes** (construções iniciais, renda projetada, posição dos
QGs) e o jogador **aceita ou recusa**. Só o aceite carrega a cena.

⚠️ **A renda projetada é um preview, e preview tem histórico ruim neste projeto.**
Se você precisar mostrar renda, **não escreva uma segunda continha**. A regra de
renda vive no `MatchController` e opera sobre objetos de cena; ou você reusa a
mesma regra, ou não mostra o número. Preview que diverge da partida destrói a
confiança em todos os números da tela.

**Cor de time nunca sai do slot direto.** Sempre
`slot → MatchController.GetTeamIdForSlot → TeamUtils.GetColor`. Assumir a cor pelo
índice pinta errado quando o POV não é o slot 0.

**Tint ≠ névoa.** Se um dia pintar domínio, use a **arte** dos overlays (miolo
branco tintável + outline preto), nunca o sistema de FoW. São perguntas diferentes:
névoa é "o que vejo agora", domínio é "o que conquistei".

**Grade hexagonal serrilha.** Um retângulo limpo em coordenada de célula **não é**
um retângulo na tela: linhas ímpares saem meio hexágono deslocadas. Se desenhar
contorno de quadrante, ligue os **centros das células da borda** — sai serrilhado,
e é o certo. Retângulo liso mente sobre onde o recorte cai.

**Sobreposição entre quadrantes é recurso, não bug.** Dois quadrantes podem
compartilhar células — é a faixa de fronteira. Se pintar por cima, use **ordem
determinística de id** (nunca ordem de iteração), senão a mesma tela pinta
diferente entre duas aberturas.

**Menu é painel, não cena.** A tela de escolher *mundo* mora na `Tela de Entrada`,
via `MainMenuState`. **Esta** cena é a única do fluxo que é cena, porque é mapa.

---

## 6. O que NÃO tocar

```text
Assets/Scripts/Campanha/*.cs          dado e controller — frente aberta
Assets/Editor/MapHelperWindow.cs      a bancada de autoria — frente aberta
Assets/Scenes/Batalha.unity           a cena de execução — frente aberta
Assets/Scenes/Autoria/*               cenas de autoria, fora do build
Assets/DB/Campanha/*.asset            editado pela bancada, não à mão
```

Precisa de um campo novo no dado? **Peça.** Acrescentar campo em `QuadranteData`
pelo seu lado colide com o bake da outra frente.

---

## 7. Como saber que funcionou

1. A cena abre com o `MundoData` atribuído e lista **1 bloco, 1 campanha, 2
   quadrantes** (é o conteúdo do `Mundo Fixture` hoje).
2. Um quadrante sem bake (`HasBake == false`) aparece marcado — hoje o `A_IA_Q2`
   está assim, e é bom que apareça.
3. Clicar num quadrante mostra os detalhes e **não** carrega nada.
4. Aceitar chama `PartidaConfig.SetQuadrante(...)` + `LoadScene("Batalha")`.
5. A `Batalha` desenha aquele quadrante. Se desenhar **outro**, o endereço passou
   errado — confira que você mandou `campanhaId` e não `blocoId`. Isso já aconteceu
   uma vez.

---

## 8. Perguntas que valem interromper por

- **`PartidaConfig.SetQuadrante` não existe ainda** → avise, não contorne.
- **Precisa de campo novo no dado** → peça, não acrescente.
- **Quer mostrar progresso/conquista** → não existe fonte; peça o modelo.
- **Quer mostrar renda projetada** → precisa acordar como, para não virar segunda
  continha.
