# Conectar um agente de IA ao Unity Editor (Unity CLI)

Este guia ensina a ligar um agente de código, como Claude Code, Cursor ou Codex, a um projeto Unity aberto. Com a conexão feita, o agente consegue recompilar o projeto, ler os erros, rodar testes e executar comandos dentro do editor, sem você precisar copiar e colar o que aparece no Console.

Foi escrito em 2026-11-03, com o Unity CLI v1.0.0-beta.15 no Windows 11 e o Unity 6000.5. Como a ferramenta ainda está em beta, os nomes dos comandos podem mudar. Na dúvida, confira em `unity help`.

## Custa alguma coisa?

Não. O Unity CLI é oficial, gratuito e separado da Unity AI. Ele não pede assinatura nem gasta créditos de IA, e o servidor MCP não tem limite de conexões. Os planos pagos da Unity AI cobrem as ferramentas de IA da própria Unity, não este CLI.

O que você precisa:
- **Unity 6.0 ou mais novo.**
- **Uma conta Unity**, a mesma do Hub. A conta gratuita serve.
- **O pacote Unity Pipeline instalado no projeto.** Ele é instalado por um comando, mais abaixo.

O MCP que vinha embutido no editor foi descontinuado. O CLI é o caminho que a Unity recomenda agora.

## Passo a passo (Windows)

Use o PowerShell normal, sem abrir como administrador.

### 1. Instalar o CLI

```
winget install Unity.CLI
```

**Feche o PowerShell e abra outro**, porque o terminal antigo não reconhece o comando novo. Para conferir se instalou:

```
unity --version
```

Na primeira vez, o CLI pergunta se você quer ligar o "usage analytics". Isso manda para a Unity os comandos que você usa, junto com o seu Unity ID. É opcional e não muda o funcionamento.

Mesmo respondendo "No", o CLI continua mandando relatórios de falha e um sinal anônimo por execução. Para desligar esses dois também, defina as variáveis de ambiente `UNITY_NO_CRASH_REPORT` e `UNITY_NO_CLI_INVOKED_TELEMETRY`.

### 2. Entrar com a conta

```
unity auth login
```

Esse comando abre o navegador. O login do Hub pode não valer para o CLI, então faça mesmo que o Hub já esteja logado. Para conferir:

```
unity auth status
```

### 3. Instalar o pacote no projeto

Deixe o projeto aberto no Unity e, no terminal, entre na pasta dele:

```
cd "C:\caminho\do\seu\projeto"
unity pipeline install
```

Esse comando adiciona o `com.unity.pipeline` ao `Packages/manifest.json`. Vale commitar a mudança, para quem clonar o projeto já receber o pacote.

**Depois, clique na janela do Unity.** O editor só importa um pacote novo quando ganha foco. Espere a importação e a compilação terminarem. Você sabe que deu certo quando o `Packages/packages-lock.json` muda e o `unity status` mostra o editor com estado `ready`.

### 4. Ligar no agente

```
unity mcp configure claude
```

Troque `claude` pelo nome do seu cliente. Para ver quais clientes estão configurados:

```
unity mcp configure --list
```

Reinicie o agente, ou o editor de código onde ele roda, para ele carregar a conexão.

Se o agente consegue rodar comandos de terminal, este passo é opcional, porque ele pode chamar o `unity` direto.

## Conferir se está tudo ligado

```
unity status             # editores conectados: porta, projeto, versão, estado
unity pipeline editors   # se cada editor aberto tem o pacote e se o servidor responde
unity command            # lista os comandos que o editor aceita
```

Se o `unity pipeline editors` mostrar `hasPipelinePackage true` mas `serverReachable false`, o pacote está no projeto e o editor ainda não o carregou. Clique na janela do Unity e espere a compilação terminar.

## O que o agente passa a fazer

| comando | para quê |
|---|---|
| `unity recompile` | recompila o editor aberto e devolve os erros de compilação |
| `unity test` | roda os testes de Edit Mode e Play Mode e gera um relatório |
| `unity command <nome>` | executa um comando do editor (a lista sai de `unity command`) |
| `unity build` / `unity run` | gera e roda builds em batch mode |
| `unity docs <classe>` | abre a documentação certa para a versão do projeto |
| `unity skill show` | guia do CLI escrito para agentes; `unity skill install <cliente>` instala no seu |

### Comandos do editor que mais servem para testar o jogo

Rodar `unity command <grupo>` lista os comandos de um grupo. Os comandos abaixo vêm da lista do Pipeline 0.10.0-exp.1. Até agora só o `console status` e o `capture game_view` foram testados, e os valores dos parâmetros dos outros (nome da tecla, `--action`) são exemplos que ainda precisam ser conferidos:

| comando | para quê |
|---|---|
| `unity command editor.playmode start` / `stop` / `pause` | entra e sai do Play |
| `unity command console status` | erros, avisos e se a compilação falhou, sem puxar as mensagens |
| `unity command console get --tail 50 --level error` | lê o Console |
| `unity command capture game_view --output-path tela.png` | foto da Game view, inclusive a UI |
| `unity command runtime simulate_key --key Space --action press` | aperta uma tecla no jogo rodando (Input System) |
| `unity command runtime simulate_pointer --x 640 --y 360 --action click` | clica na tela |
| `unity command runtime set_time_scale --scale 2` | acelera o jogo para testar mais rápido |
| `unity command script eval --code "..."` | executa C# dentro do editor |

Com esses comandos, o agente consegue dar Play, jogar apertando teclas, tirar foto da tela e ler os erros, sem nenhum código novo no projeto.

## Segurança

- **A conexão é local:** o editor só aceita comandos vindos da própria máquina.
- **O agente executa código dentro do editor.** Use controle de versão e faça commit antes de deixar ele mexer na cena.
- **O login fica na sua máquina.** Não commite arquivos de configuração do CLI.

## Fontes

- [Unity CLI substitui o MCP do editor](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli)
- [Usar o Unity CLI](https://docs.unity.com/en-us/unity-cli/use-unity-cli)
- [Referência do Unity CLI](https://docs.unity.com/en-us/unity-cli/unity-cli-reference)
- [Unity Pipeline package](https://docs.unity.com/en-us/unity-cli/unity-pipeline/unity-pipeline-package)
- [Unity AI: ferramentas e FAQ](https://unity.com/features/ai)
- [Anúncio no fórum da Unity](https://discussions.unity.com/t/announcing-the-unity-cli-a-new-way-to-connect-your-tools-and-agents/1731104)
