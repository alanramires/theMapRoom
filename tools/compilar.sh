#!/usr/bin/env bash
# Compila o projeto SEM abrir a Unity, usando os .csproj que ela gera.
#
# Serve para pegar "nao compila" antes de alguem abrir o editor. Funciona com a
# Unity aberta ou fechada: o dotnet so le os .csproj e escreve em Temp/, que e a
# mesma pasta que Rider e Visual Studio usam.
#
# O QUE ELE NAO FAZ: rodar o jogo. Compilar prova que os tipos batem, nao que o
# comportamento esta certo.
#
# A ARMADILHA QUE ELE GUARDA: o .csproj e uma LISTA de arquivos que a Unity
# escreve quando o editor esta aberto. Um .cs criado depois disso fica de fora, e
# o build passa SEM TER COMPILADO O ARQUIVO — falso verde. Por isso a primeira
# coisa aqui e comparar o disco com a lista, e falhar se algo estiver faltando.
#
# ESTE ARQUIVO TEM BARRA INVERTIDA. Edite pela ferramenta de arquivo, nunca por
# heredoc em shell: a ferramenta de comando engole "\" antes de gravar, e o sed
# abaixo quebra calado (ja aconteceu na primeira versao deste script).
#
# Uso:  bash tools/compilar.sh
# Saida: 0 = compila e nada ficou de fora; 1 = erro de compilacao ou arquivo fora.

set -u
cd "$(dirname "$0")/.."

falhou=0

# 1. Todo .cs de Assets/ esta em algum .csproj?
listados=$(grep -ho 'Compile Include="[^"]*"' ./*.csproj \
    | sed -e 's/Compile Include="//' -e 's/"$//' \
    | tr '\\' '/' | sort -u)

if [ -z "$listados" ]; then
    echo "✗ Nao consegui ler a lista de nenhum .csproj. Abra a Unity para gerar."
    exit 1
fi

# A Unity ignora de proposito pasta que TERMINA em "~" e que COMECA com ".":
# o AI_Legacy~ e codigo aposentado que nao deve compilar. Nao e arquivo fora.
no_disco=$(find Assets -name '*.cs' -not -path '*~/*' -not -path '*/.*' | sort -u)
fora=$(comm -23 <(echo "$no_disco") <(echo "$listados"))

if [ -n "$fora" ]; then
    echo "✗ ARQUIVOS FORA DO .csproj — nao foram compilados:"
    echo "$fora" | sed 's/^/    /'
    echo "  Abra a Unity uma vez para ela regenerar a lista, e rode de novo."
    echo
    falhou=1
fi

# 2. Compila cada assembly, so erros na saida.
for proj in ./*.csproj; do
    case "$proj" in *_canario*) continue ;; esac
    nome=$(basename "$proj" .csproj)
    saida=$(dotnet build "$proj" -nologo -v q -clp:ErrorsOnly 2>&1)
    if echo "$saida" | grep -q " error "; then
        echo "✗ $nome"
        echo "$saida" | grep " error " | sed 's/ \[.*\]$//' | sort -u | sed 's/^/    /'
        falhou=1
    else
        echo "✓ $nome"
    fi
done

exit $falhou
