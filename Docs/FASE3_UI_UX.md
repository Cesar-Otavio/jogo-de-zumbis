# Fase 3 — UI / UX

O projeto usa **uGUI + TextMesh Pro**, com todo o HUD construído por código (`CombatHUD`, `CampaignHUD`, `UiKit`). Não houve migração para UI Toolkit. O redesenho trabalha sobre os mesmos componentes, então mouse, gamepad, pausa e escala por resolução continuam iguais (`CanvasScaler` em 1920×1080, *match* 0,5).

## 1. Layout

| Região | Conteúdo | Comportamento |
|---|---|---|
| Superior esquerdo | missão ativa + objetivo + progresso (cartão que cresce com o texto); estado de energia | sempre durante a partida |
| Superior central | onda e inimigos restantes, banner de onda, avisos de chefe | só quando há onda ou evento |
| Superior direito | abates, pontos, créditos (cartão) | sempre |
| Centro | mira, marcador de acerto, aviso de interação "[E] …", barra de percepção | contextual |
| Centro-baixo | **legendas de diálogo** (nome + fala); legendas de sons acima | só quando alguém fala |
| Inferior esquerdo | barra rápida com **ícones**; cartão de vida (ícone, valor, barra, estamina, vidas, estado de ferimento) | estamina some quando cheia |
| Inferior direito | cartão da arma (**ícone**, nome, munição, reserva, recarga), arma reserva com ícone e "[Q]"; **feed de coleta** acima | cartão some sem arma; o ícone esmaece enquanto a arma é trocada |

## 2. Paleta e tipografia

| Uso | Cor |
|---|---|
| Fundo dos cartões | grafite `#0E0F12` a 58% |
| Borda dos cartões | branco a 22% (3 px) |
| Texto principal | branco |
| Destaque / aviso | âmbar `#FFC74D` |
| Perigo | vermelho `#FF4033` |
| Confirmação / vida | verde `#59F28C` |
| Itens por categoria | arma cinza-claro · munição âmbar · médico verde · tático laranja · missão azul-claro · documento bege · construção madeira · moeda dourado |

Fonte: Liberation Sans (TMP padrão), com contorno escuro de 15% para legibilidade sobre a cena.

## 3. Ícones

- Os sprites ficam em `UI/Icons/Items` e `UI/Icons/Weapons`. Detalhes em `FASE3_ASSETS.md`.
- Tamanho padronizado: 256×256 para itens e 512×256 para armas. Fundo transparente, silhueta clara e contorno escuro: legíveis a 34 px.
- Resolução de ícones:
  - `ItemCatalog.IconOf(id)` para itens;
  - `ItemCatalog.IconOf(QuickItem)` para a barra rápida;
  - `WeaponData.Icon` / `AmmoIcon` para armas e munição;
  - **fallback** "?" quando faltar o sprite, para que um sprite ausente fique evidente mas sem quebrar nada.
- Onde aparecem:
  - barra rápida (ícone, tecla e quantidade; acinzentado sem estoque);
  - feed de coleta;
  - cartão da arma e da arma reserva.
- **Pendente**: ícones nas linhas da loja e no leitor de documentos. Os dados já existem no catálogo; falta só ligar à UI da loja.

## 4. Feed de coleta (`PickupFeed` + `PickupFeedView`)

- Cada linha mostra ícone, nome e quantidade, por exemplo "Munição .45 ACP +16". Uma faixa colorida à esquerda indica a categoria.
- A linha entra deslizando da direita, cresce um pouco quando a quantidade aumenta e some após 3,4 s.
- Coletar o mesmo item em até 2,5 s **soma na mesma linha** em vez de empilhar avisos. No máximo 5 linhas.
- A falta de espaço aparece em vermelho ("Granada — sem espaço").
- Origens:
  - pegar arma (com animação de pegar);
  - munição de caixa ou de arma repetida;
  - kit médico e vida;
  - itens rápidos;
  - itens de missão;
  - créditos de baú.

## 5. Opções novas (Opções e Acessibilidade, 2ª coluna)

- Legendas de diálogo (liga/desliga)
- Tamanho das legendas (80% a 150%)
- Volumes: geral, música, efeitos, diálogos e rádio, ambiente, interface

## 6. Não feito nesta fase

- **Hub e menu principal**: mantidos como estavam. O fluxo inicial não foi alterado, para não arriscar os menus que já funcionam.
- **Tela de inventário dedicada**: o jogo não tem uma; o inventário é a barra rápida.
- **Animações de UI**: foram feitas com código simples (lerp/fade), sem bibliotecas novas.
