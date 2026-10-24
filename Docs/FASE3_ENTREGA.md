# Fase 3 — Entrega

Branch `fase3-imersao`, sobre o checkpoint `25b0b31` (`main`, estado após o Prompt 2). Para voltar ao estado anterior: `git checkout main`.

## 1. Resumo

| Área | Entregue | Status |
|---|---|---|
| Iluminação | lanterna tática no peito (feixe com cookie, preenchimento, balanço, íris, ignora o jogador), pós-processamento mais legível, ambiente mais claro, atlas de sombras 4096 | concluído e testado |
| Ícones | 25 ícones de itens + 6 de armas (renderizados dos modelos), catálogo de itens (ScriptableObjects), barra rápida, loja e leitor de documentos com ícones e descrições | concluído e testado |
| Feed de coleta | ícone + nome + quantidade, agrupado, animado | concluído e testado |
| Armas | guardar/sacar/trocar/pegar do chão com sockets (quadril, costas, peito) e IK, sem teletransporte nem duplicata; troca enfileirada; recarga cancelada sem dar munição | concluído e testado |
| Inclinação | Q/E mirando, com limite de parede; sem conflito com trocar/interagir | concluído e testado |
| Rolagem | giro suave para a direção | concluído |
| Locomoção 2D, agachar animado, prone | parâmetros, controlador, ferramenta de Animator, regras de prone | **infraestrutura pronta, asset pendente** |
| Inimigos | reações direcionais e cambaleio (`HitReactor`), limite de ragdolls, anti-explosão | concluído (teste visual limitado) |
| HUD | cartões de vida, arma e pontuação, missão com cartão, ícones, avisos menores, opções em 2 colunas | concluído e testado (exceto as opções em Play) |
| Narrativa | biblioteca de 50 falas, diretor com prioridades e validade, prólogo de ~45 s, operador Duarte, piloto, legendas com nome, rádio processado, ducking | concluído e testado; **vozes pendentes** |
| Áudio | volumes por grupo (geral, música, efeitos, diálogo, ambiente, interface) | concluído; **AudioMixer pendente** (manual) |
| Documentação | 7 documentos em `Docs/` | concluído |

## 2. Arquivos criados

```
Docs/FASE3_AUDITORIA.md, FASE3_ANIMACOES.md, FASE3_ASSETS.md, FASE3_NARRATIVA.md, FASE3_UI_UX.md, FASE3_TESTES.md, FASE3_ENTREGA.md
Tools/IconGen/make_icons.py
.gitignore, .gitattributes
Assets/SourceFiles/Scripts/Combat/Lighting/TacticalFlashlight.cs
Assets/SourceFiles/Scripts/Combat/Animation/LeanController.cs
Assets/SourceFiles/Scripts/Combat/Animation/HitReactor.cs
Assets/SourceFiles/Scripts/Combat/WeaponHolder.Gear.cs
Assets/SourceFiles/Scripts/Combat/Items/ItemDefinition.cs, ItemCatalog.cs, PickupFeed.cs, PickupFeedView.cs
Assets/SourceFiles/Scripts/Combat/Narrative/DialogueLibrary.cs, DialogueDirector.cs, DialogueSubtitleView.cs, RadioSounds.cs
Assets/SourceFiles/Scripts/Combat/Editor/CombatSetup.Phase3.cs, CombatSetup.Narrative.cs, CombatSetup.Animation.cs
Assets/SourceFiles/UI/Icons/Items/*.png (25), UI/Icons/Weapons/*.png (6)
Assets/SourceFiles/Combat/Items/Item_*.asset (19)
Assets/SourceFiles/Resources/ItemCatalog.asset, DialogueLibrary.asset
```

## 3. Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `ThirdPersonController.cs` | offset da câmera (lean), giro suave da rolagem, MoveX/MoveY, Crouched, prone |
| `StarterAssetsInputs.cs`, `StarterAssets.inputactions` | ações Flashlight (F), LeanLeft/Right (Q/E e LB/RB, só mirando), Prone (segurar C / ↑) |
| `PlayerCombat.cs` | lanterna antiga removida (o estado FlashlightOn ficou); lean; troca física; pegar do chão; fila de troca; feed |
| `WeaponHolder.cs` | `partial`; `AdoptVisual`; sockets; disparo bloqueado durante a troca; peso da mão de apoio |
| `WeaponData.cs` | `Class`, `Icon`, `AmmoName`, `AmmoIcon` |
| `WeaponPickup.cs` | `VisualPose` |
| `CombatHUD.cs` | cartões, ícone de arma e de vida, avisos menores |
| `Campaign/CampaignHUD.cs` | quick bar com ícones, feed, cartão da missão, leitor com ícone do item e contagem de documentos |
| `Campaign/ShopManager.cs`, `ShopCatalog.cs` | ícones, cores de categoria e descrições do catálogo nas linhas; painel opaco |
| `Campaign/InventoryManager.cs`, `MissionPickup.cs`, `ConsumablePickup.cs`, `LootCrate.cs` | coletas pelo feed; LB não usa item enquanto inclina |
| `Campaign/MissionDevice.cs` | nomes de itens pelo catálogo |
| `Campaign/Story.cs` | rádio pelo diretor (textos movidos para a biblioteca) |
| `Campaign/CampaignManager.cs` | prólogo novo |
| `GameFlow.cs` | volumes, ducking, reset de diálogo e feed, opções em 2 colunas |
| `CombatEffects.cs`, `Horror/WeatherController.cs` | volumes por grupo (efeitos, interface, ambiente) |
| `Horror/GameSettings.cs` | legendas de diálogo, tamanho, volumes |
| `RagdollController.cs` | limite de simulação, congelamento, anti-explosão |

## 4. Prefabs, cena e assets modificados

- `Prefabs/PlayerRobot.prefab`: + `TacticalFlashlight`, + `LeanController`.
- `Prefabs/Enemies/*.prefab` (Zombie, Soldier_1..4): + `HitReactor`.
- `Scenes/MilitaryBase_Combat.unity`:
  - ambiente;
  - pontos de luz sem sombra;
  - `CampaignSystems` + `DialogueDirector` + `DialogueSubtitleView`.
- `Combat/Environment/NightProfile.asset`: granulação 0,22, vinheta 0,32, saturação −22, contraste 12, aberração 0,08.
- `PC_RPAsset`: atlas de sombras adicionais 4096.
- `Combat/Weapons/Weapon_*.asset`: classe, munição, ícones.

## 5. Como reproduzir e verificar

1. Abra `MilitaryBase_Combat`.
2. Os setups já foram aplicados. Para refazer: *Tools ▸ Combat ▸ Phase 3* ▸ 1 a 5 (o 6 só depois de importar os clipes).
3. Em Play, verifique:
   - **Campanha nova**: prólogo com legendas (Espaço pula).
   - **Pegar a pistola**: o personagem abaixa e pega; o feed aparece; Duarte comenta.
   - **F**: lanterna.
   - **Q** com 2 armas: guarda uma e saca a outra.
   - **Botão direito + Q/E**: inclina.
   - **Opções ▸ 2ª coluna**: legendas e volumes.

## 6. Ajustes manuais necessários

1. **AudioMixer** (opcional): crie `MainMixer` com grupos Master/Music/SFX/Dialogue/Ambient/UI e ligue as fontes. Os volumes já funcionam sem ele.
2. **Clipes Mixamo**: veja `FASE3_ASSETS.md` §3.1 e depois *Phase 3 ▸ 6*.
3. **Vozes**: veja `FASE3_NARRATIVA.md` §8 e depois *Phase 3 ▸ 4*.

## 7. Pendências conhecidas

- **Animações**: strafe, ré, partida/parada, agachar andando, prone, interações físicas com clipe, inimigos especiais com movimento próprio, soldados com clipes de arma (dependem de assets).
- **Hub/menu principal** não foi redesenhado.
- **Localização** (pacote não instalado; chaves `dlg.*` prontas).
- **Efeitos de impacto por material** (metal, concreto, madeira, terra, água): o sistema de marcas e partículas existente foi mantido, sem novas definições por superfície.
- **Medição em build** com Profiler; a meta de 60 FPS não foi verificada fora do editor.
- **Testes automatizados** (Unity Test Framework não instalado).

## 8. Próximas recomendações

1. Importar os ~12 clipes Mixamo de prioridade alta e rodar *Phase 3 ▸ 6*: é o maior salto visual restante.
2. Gerar as 50 falas com TTS e conferir a mixagem do rádio.
3. Fazer um build Windows e medir com o Profiler numa onda de 30+ inimigos; se faltar desempenho:
   - reduzir as sombras dos postes distantes;
   - reduzir a distância de sombra (50 m).
