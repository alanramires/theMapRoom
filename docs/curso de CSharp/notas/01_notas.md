# Notas — Aula 01: Anatomia de um arquivo

Aula: [01_anatomia_de_um_arquivo.md](../01_anatomia_de_um_arquivo.md)

---

## 📍 Ponto de parada

Parei na declaração da classe — `public class QuadranteData : INoDoMapa`.
O próximo trecho é a implementação explícita de interface.

---

## Linha 1-3 — `using`

>[!important]
>## `Tilemap` ? `TileBase`
>
>**Tilemap:**    o COMPONENTE na cena. A grade em si, que guarda "célula (4,7) tem tal tile".
>           É o objeto que você pinta.
>           
>**TileBase:**   o PINCEL. Um asset da paleta — "Floresta", "Montanha", "Oceano".
>           É com o que você pinta.

> [!note] Namespace
> Namespace é como um “grupo do WhatsApp” das classes. :D

## Linha 5-21 — o comentário XML

>[!note] Sobre o atalho 
>CTRL+SHIFT+P na opção >"Developer: Reload Window" é muito útil para dar reload no projeto
>F12: leva ate o arquivo
>Shift+F12: quem usa esse **símbolo** (não o arquivo — ele lista cada chamada)

## Linha 22-23 — `[System.Serializable]`

>[!note] **O que serializar significa**
>O valor é gravado **dentro** do arquivo da cena (ou do prefab / do asset). Um valor por cena. 25 cenas com o mesmo script = 25 valores independentes.
> Serializar não é compartilhar — é o oposto. É dar a cada cena a sua própria cópia.

## Linha 22-23 — `public class`

>[!note] Private vs Public
>Responde **quem no código** pode ler e escrever.
>```csharp
private float musicVolume;   // só esta classe
public  float musicVolume;   // qualquer arquivo do projeto
>```
>E só isso. `public` **não** torna nada global, compartilhado ou permanente. É uma porta, não um cofre.
