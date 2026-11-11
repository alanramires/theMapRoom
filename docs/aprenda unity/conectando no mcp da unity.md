# Conectando o Claude ao editor da Unity (Unity CLI)

Nota de método, para reusar em outra máquina ou com outra pessoa.

A **instalação** (winget, login, `unity pipeline install`, `unity mcp configure`) está em
[`docs/indie/unity_cli_mcp.md`](../indie/unity_cli_mcp.md) e não se repete aqui. O que este
trecho guarda é o que aprendemos **usando**: o que roda em que terminal, o que é rotina e o que
já derrubou uma partida. Vale para outra máquina e para outra pessoa com Claude.

## Os cinco comandos (o autor roda, no Windows Terminal, com o projeto aberto na Unity)

```powershell
winget install Unity.CLI          # 1. instala; fechar e reabrir o terminal depois
unity auth login                  # 2. abre o navegador; o login do Hub NAO vale para o CLI
cd "C:\caminho\do\projeto"
unity pipeline install            # 3. adiciona com.unity.pipeline ao manifest; clicar na Unity para importar
unity mcp configure claude        # 4. opcional para o Claude Code (ele chama o `unity` direto)
unity status                      # 5. editor com serverReachable true = pronto
```

Daí em diante o Claude completa o serviço sozinho: `unity command` para listar o que o editor
aceita, `unity recompile`, `console get`, `script eval`. Quem só tem este arquivo não precisa
do guia para chegar até aqui; o guia é para quando algo dos cinco falhar.

## Máquina nova, em ordem

1. Clonar o projeto. O `com.unity.pipeline` já vem no `Packages/manifest.json`; abrir o projeto
   na Unity e **clicar na janela** para o pacote importar.
2. `winget install Unity.CLI`, fechar e reabrir o terminal, `unity auth login` (o login do Hub
   não vale para o CLI), `unity --version`.
3. `unity status` tem de mostrar o editor com `serverReachable true`. Se mostrar
   `hasPipelinePackage true` e `serverReachable false`, é só a Unity que ainda não compilou.
4. Para o Claude Code **não precisa de MCP**: ele chama o `unity` direto pelo terminal. O
   `unity mcp configure claude` é para clientes que não rodam comando de terminal.
5. Abrir a Unity uma vez antes de confiar no `bash tools/compilar.sh`: os `.csproj` que ele lê
   são gerados pelo editor, e um `.cs` criado depois fica fora da lista (o script acusa).
6. Navegação no VS Code (F12 entre assemblies) exige um `.sln` de verdade apontado em
   `.vscode/settings.json`; o `.slnx` que a Unity gera não serve. Ver `docs/resumo.md`, armadilhas.

## Qual terminal para quê

| tarefa | terminal | por quê |
|---|---|---|
| `bash tools/compilar.sh` | Git Bash | é um script sh; compila por fora, com a Unity aberta ou fechada |
| `unity ...` (CLI) | PowerShell | o Bash do Windows às vezes engasga com o `.exe` da WindowsApps; no PowerShell 5.1 aparece uma linha de `NativeCommandError` inofensiva |
| ler `ai_step_marcas.jsonl` e `step-NNNNNN.json` | PowerShell `ConvertFrom-Json` | o Python desta máquina quebra no `import json` (conflito de `enum`) |
| git, grep, sed | Git Bash | caminhos com espaço: sempre entre aspas |

## Rotina

```powershell
unity status                                  # editor conectado?
unity recompile                               # recompila DENTRO do editor; tabela vazia = ok
unity command console status                  # erros/avisos sem colar o Console
unity command console get --tail 50 --level error
unity command script eval --file tools/ai_test/<script>.cs   # fixtures e ensaios, FORA do Play
```

- **Fixture sempre por arquivo** (`--file`). Código inline quebra nas aspas do PowerShell.
- O método de ensaio da IA (estado, plano, unidade, compra, agir, conferir) está em
  [`docs/ai_test.md`](../ai_test.md). Fixtures de regressão ficam em `tools/ai_test/`, fora de
  `Assets/` para a Unity não compilar.
- Compilar prova que os tipos batem. Comportamento só se prova em Play, pelo autor.

## O que já custou uma partida

- **`unity recompile` com o jogo em Play recarrega o domínio e derruba a partida** (voltou ao
  turno 1). **Salvar um `.cs` faz o mesmo**: a Unity recompila sozinha. Com alguém em Play,
  deixe o código pronto, avise, e só salve ou recompile quando a pessoa disser que parou. Para
  conferir, `unity status` mostra o estado do editor.
- `Debug.Log` de Play não cai no `Editor.log` nesta máquina. Diagnóstico sem depender de colar
  o Console: `unity command console get`, ou instrumentar com `File.AppendAllText` na raiz e
  apagar depois.
- `unity command editor.playmode start|stop`, `capture game_view` e `runtime simulate_key`
  existem e ainda não foram usados em rotina; os parâmetros estão no guia como exemplo, conferir
  com `unity command <grupo>` antes de confiar.
