# Fase 5 — Entrega (primeira entrega obrigatória: objetivos A–F)

Branch `fase5-mundo`, sobre a tag `fase5-checkpoint` (`1cf548f`). Para voltar: `git checkout fase5-checkpoint`.

## 1. Situação por objetivo

| Objetivo | Entregue | Status |
|---|---|---|
| **A** Minimapa com posição e objetivos | minimapa redondo com mapa gerado no editor, névoa, seta, norte, locais, bancada, marcas, objetivo com distância; opções de ligar, orientação e zoom. Também o **mapa tático (M)** com legenda, % explorado e marcas por clique | **concluído e testado** (marcas por clique não testadas) |
| **B** Inventário completo integrado à barra rápida | mochila de 18 espaços com pilhas, equipamento (armas com melhorias, colete, vida), barra rápida, itens de missão separados, detalhes, ações (usar, mover para a barra, separar, mover, descartar no chão), organizar; excedente da barra vai para a mochila e a repõe | **concluído e testado** |
| **C** Bancada + 3 receitas funcionais | 1 bancada (depósito), **6 receitas** (1 na mochila, 5 na bancada), 8 materiais, baús e 12 pontos fixos com materiais, anti-duplicação | **concluído e testado**; visual da bancada **provisório** |
| **D** Visibilidade sem destruir a atmosfera | página GRÁFICOS: qualidade, resolução, janela, FOV, sombras, efeitos, limite de FPS, VSync, intensidade do pós. Itens e bancada com luz própria. A revisão de luz da Fase 3 foi mantida (nenhum passe novo de iluminação nesta entrega) | **parcial**: opções feitas e testadas na interface; iluminação por setor fica para o Marco 3 |
| **E** Manifesto de falas + 1ª missão pronta para dublagem | manifesto CSV de 54 falas com estado de produção; religação automática das vozes; lote da 1ª missão definido (9 falas) | **infraestrutura pronta — vozes pendentes** (sem TTS autorizado) |
| **F** Área para exploração | Depósito de suprimentos: bancada, materiais, fala, marcador; plano do próximo passo | **concluído** (escolha e primeira ocupação) |

## 2. Scripts criados

```
Scripts/Combat/Inventory/Backpack.cs, ItemActions.cs, WorldItem.cs, InventoryView.cs, GameplayOverlay.cs
Scripts/Combat/Crafting/CraftingRecipe.cs, CraftingCatalog.cs, Crafting.cs, CraftingStation.cs
Scripts/Combat/Exploration/MapDefinition.cs, MapDiscovery.cs, MapMarker.cs, MapSprites.cs, MinimapView.cs, TacticalMapView.cs
Scripts/Combat/Horror/DisplayOptions.cs
Scripts/Combat/Editor/CombatSetup.Phase5.cs
```

## 3. Scripts alterados

| Arquivo | Mudança |
|---|---|
| `Items/ItemDefinition.cs` | categoria `Material`; `Use`, `UseAmount`, `Droppable` |
| `Items/ItemCatalog.cs` | `WorkbenchIcon` |
| `Items/PickupFeed.cs` | cor da categoria Material |
| `Campaign/InventoryManager.cs` | excedente para a mochila, reposição ao usar, mover entre barra e mochila, `CanTake` |
| `Campaign/ItemPickup.cs`, `ConsumablePickup.cs`, `ShopManager.cs` | usam `CanTake` (barra ou mochila) |
| `Campaign/RunState.cs` | zera a mochila; lista os itens de missão |
| `Campaign/Progression.cs` | reiniciar a campanha apaga o mapa |
| `LootCrate.cs` | baús soltam materiais (pesos por raridade × suprimentos da dificuldade) |
| `CombatEffects.cs` | qualidade dos efeitos nas partículas e nos furos |
| `PlayerCombat.cs` | FOV do jogador no tiro sem mira |
| `CombatHUD.cs` | `Visible` |
| `GameFlow.cs` | opções em 3 páginas (minimapa e gráficos), Esc fecha mochila e mapa, MOCHILA e MAPA no menu de pausa, painel de controles, reset das telas, intensidade do pós |
| `Horror/GameSettings.cs` | minimapa (3) e gráficos (9 opções) |
| `InputSystem/StarterAssetsInputs.cs`, `StarterAssets.inputactions` | ações Map (M) e Inventory (Tab) |
| `Editor/CombatSetup.Phase3.cs` | 11 itens novos na tabela, uso dos itens, `Droppable`, ícone da bancada |
| `Editor/CombatSetup.Narrative.cs` | 4 falas novas (bancada, laboratório, depósito, torre) |
| `Tools/IconGen/make_icons.py` | 12 ícones |

## 4. Cena e assets

- **Cena** `MilitaryBase_Combat`:
  - `ExplorationSystems` (Minimap, TacticalMap, Inventory);
  - `Workbench_Depot`;
  - `CraftingMaterials` (12).
- **Assets**: ver `FASE5_ASSETS.md`.
- **Menus** *Tools ▸ Combat ▸ Phase 5*:
  - 1 itens e receitas;
  - 2 gerar mapa;
  - 3 cena;
  - 4 manifesto de falas e religar vozes.
- **Dependências**: nenhuma nova.

## 5. Como verificar

1. Play → Modo Infinito.
2. Minimapa no canto superior direito: ande e veja a névoa se abrir.
3. Perto do início há tecido e sucata: E para pegar.
4. Tab → FABRICAÇÃO → Bandagem.
5. M → mapa tático; clique para marcar.
6. Vá ao Depósito de suprimentos, a oeste. Duarte comenta, e a bancada fica sob uma luz de trabalho: E → kit médico improvisado (precisa de material médico do laboratório ou da torre, ou de baús).
7. Opções ▸ GRÁFICOS.

## 6. Pendências e próximos passos

- **Confirmar no teclado físico** M e Tab (as teclas simuladas não chegaram ao jogo nesta sessão).
- **Vozes**: escolher e autorizar um TTS (ou gravação). Começar pelo lote da missão 1 (`FASE5_NARRATIVA_AUDIO.md` §5).
- **Pacote de props gratuito** (bancada, armários, móveis): libera enfermaria, oficina, alojamentos e sala de controle.
- **Marco 3/4** no depósito:
  - contêiner-sala;
  - armário com código achado num documento;
  - terminal que revela o mapa (`MapDiscovery.Reveal` pronto);
  - missão secundária "Oficina isolada" com desbloqueio de receita (`UnlockFlag` pronto).
- **Marco 5**: inimigos novos (plano em `FASE5_INIMIGOS.md`; dependem de clipes).
- **Build + Profiler** antes de expandir o mapa (`FASE5_PERFORMANCE.md`).
- **Não implementado nesta entrega**:
  - diário (J);
  - áreas seguras;
  - armazenamento em caixa;
  - salvamento no meio do capítulo;
  - eventos dinâmicos novos;
  - multiplayer (não existe no projeto).
