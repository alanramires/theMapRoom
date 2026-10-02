kkkkk esse foi o **verdadeiro build de publicação**: “funciona no PC” ? pega o celular ? *a Campanha explode*. ??

Pesquisei como o pessoal costuma testar isso antes de mandar pro Unity Play, e o caminho mais prático é usar **duas camadas de teste**, porque uma não substitui a outra.

Primeiro, para pegar problema de **layout/resolução/touch**, usa o **Device Simulator** dentro do próprio Unity. Ele simula diferentes celulares, orientação, safe area e toque, então é ótimo para descobrir painel saindo da tela, botão impossível de alcançar, mapa ficando espremido etc. [Documentação do Unity](https://docs.unity3d.com/cn/2022.3/Manual/device-simulator-introduction.html?utm_source=chatgpt.com)

Mas ele tem uma limitação importante: ele continua renderizando pelo Editor. Ele **não reproduz exatamente o runtime Web do navegador do celular**, nem memória/performance real. A própria Unity diz que para isso você precisa rodar um build real no aparelho. [Documentação do Unity](https://docs.unity3d.com/cn/2022.3/Manual/android-device-simulator.html?utm_source=chatgpt.com)

E aqui vem o jeito que eu acho ideal pra você:

1. Faz um **Web Development Build** normal, o mesmo tipo de build que vai para o Unity Play.
2. Em vez de publicar, você serve aquela pasta localmente no seu PC.
3. Celular e PC ficam na mesma Wi-Fi.
4. No celular, abre o IP do PC no navegador.

A documentação atual da Unity recomenda exatamente testar Web usando **Build and Run** ou um servidor web local; ela sugere inclusive `python -m http.server` ou `npx http-server`. [Documentação Unity](https://docs.unity.com/en-us/engine/6000.5/manual/platform-specific/webgl/intro/gettingstarted?utm_source=chatgpt.com)

Como você já tem Node instalado, pra você seria ridiculamente simples. Dentro da pasta gerada pelo build:

```bash
npx http-server -a 0.0.0.0 -p 8000
```

Aí descobre o IP do seu PC no Windows:

```bash
ipconfig
```

Supondo que dê algo tipo `192.168.0.15`, no celular você abre:

```text
http://192.168.0.15:8000
```

E pronto: **Sala de Mapas rodando no navegador do celular sem precisar republicar no Unity Play a cada tentativa**.

Só tem uma pegadinha: compressão Web. Para o build de desenvolvimento isso é menos problemático porque a Unity informa que Development Builds não são comprimidos. Para releases, gzip/Brotli precisam de headers corretos; se o servidor não estiver configurado, pode usar `Decompression Fallback` ou ajustar a compressão. [Documentação Unity](https://docs.unity.com/en-us/engine/6000.5/manual/platform-specific/webgl/building-distribution/deploying?utm_source=chatgpt.com)

Então eu faria teu ciclo assim:

> **Editor normal** ? lógica  
> **Device Simulator** ? layout e touch  
> **Web Development Build no celular pela rede local** ? navegador real  
> **Unity Play** ? só quando esses três passaram

Isso vai te poupar aquele ritual delicioso de:

> corrigir ? build ? upload ? esperar ? pegar celular ? “ah não, quebrou outra coisa” ??

E tem uma informação importante porque encontrei documentação antiga e nova contradizendo isso: **Unity Web antiga realmente não suportava mobile oficialmente**, daí você vai achar muita resposta velha dizendo “WebGL mobile não é suportado”. Mas no **Unity 6**, a documentação atual já lista suporte Web para **Safari iOS 15+ e Chrome Android 58+**. [Documentação do Unity](https://docs.unity3d.com/6000.0/Documentation/Manual/system-requirements.html?utm_source=chatgpt.com)

Então o fato de você estar mirando o Unity Play no celular não é uma maluquice hoje. É uma plataforma válida no Unity 6 — só que a UI e o runtime mobile precisam ser testados de verdade.

No seu caso, como **a Tela de Entrada funcionou e a Campanha quebrou**, eu começaria pelo Device Simulator na resolução/orientação exata do teu celular. Minha primeira suspeita seria mais **Canvas/anchors/resolução/aspect ratio/painel da Campanha** do que alguma falha estrutural do sistema de campanha.

