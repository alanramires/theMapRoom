# Relatorio de Atualizacao - v2.0.11

## AI Partial (depois)

Esta versao registra a reorganizacao estrutural da IA em partial files menores, com foco em separar roteamento, iniciativa, fases, debug, batches e papeis do capturador sem alterar a intencao central do comportamento.

---

## Em uma frase

A IA foi fatiada por responsabilidade para tornar o capturador e o controlador principal mais legiveis, testaveis e prontos para ajustes finos de comportamento.

---

## Principais pontos revisados

### 1. Capturador dividido por papel

- `AIController.Capturer.cs` passou a atuar como roteador do capturador.
- O comportamento foi separado em partials: `Defender`, `Rogue`, `PontaLanca`, `Pursuer`, `Opportunist`, `Explorer` e `Helpers`.
- O objetivo foi reduzir acoplamento dentro do antigo arquivo monolitico e deixar claro qual papel decide cada tipo de acao.

### 2. Controller principal dividido por responsabilidade

- `AIController.cs` ficou concentrado em configuracao, estado compartilhado e propriedades publicas.
- Foram criados partials para lifecycle, fases, roteamento generico, iniciativa, debug step e construcao de batches.
- A separacao deixa mais claro onde mexer quando o problema for ordem de unidade, execucao de fase, decisao generica ou montagem de `PlayerAction`.

### 3. Roteador do capturador mais explicito

- A decisao de captura oportunista foi puxada para o roteador do capturador.
- O partial oportunista ficou responsavel apenas por montar a acao, nao por decidir politica de prioridade.
- Capturas oportunistas agora podem ser cedidas quando outra unidade restante, mais proxima ou ja alocada ao alvo, consegue assumir a construcao.

### 4. Iniciativa isolada

- A ordenacao das unidades restantes passou a ficar em partial proprio.
- Isso evidencia que a IA reconstrói o snapshot depois de cada batch, mas preserva o plano sticky durante a fase de unidades.
- Essa separacao prepara ajustes futuros na iniciativa sem misturar logica com debug, batches ou lifecycle.

---

## Bloco tecnico curto

- Arquivos novos principais: `AIController.Router.cs`, `AIController.Initiative.cs`, `AIController.Phases.cs`, `AIController.Debug.cs`, `AIController.Batches.cs` e `AIController.Lifecycle.cs`.
- Arquivos novos do capturador: `AIController.Capturer.Defender.cs`, `Explorer.cs`, `Helpers.cs`, `Opportunist.cs`, `PontaLanca.cs`, `Pursuer.cs` e `Rogue.cs`.
- Foram criados `.meta` para os novos scripts.
- O `.csproj` local foi atualizado apenas para permitir build fora do Unity; ele continua ignorado pelo Git.

---

## Validacao

Build C# executado:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
```

Resultado: 0 erros. Permanecem warnings antigos de APIs obsoletas do Unity.
