# Fase 3 — Auditoria inicial

Data: 24/10/2026 · Branch: `fase3-imersao` · Checkpoint: commit `25b0b31` na `main` (estado após o Prompt 2).

## 1. Ambiente

| Item | Valor |
|---|---|
| Unity | 6000.6.7f1 (Unity 6.6) |
| Render pipeline | URP 17.6, Forward+, HDR, `PC_RPAsset` |
| Input | Input System 1.20.1, `StarterAssets.inputactions`, `PlayerInput` em modo SendMessages |
| Câmera | Cinemachine 6.6 (`CinemachineThirdPersonFollow`) |
| UI | uGUI 2.6 + TextMesh Pro (todo o HUD é construído por código) |
| Pacotes ausentes relevantes | Animation Rigging, Timeline, Localization, Unity Test Framework (não instalados) |
| Git | não existia — repositório criado nesta fase, commit inicial na `main` |
| Ferramentas MCP | `Unity_RunCommand` (C# no editor), `Unity_GetConsoleLogs`, capturas de cena/câmera, geração de assets (sem assinatura: `NoSubscription`) |

## 2. Sistemas encontrados

| Sistema | Onde | Observação |
|---|---|---|
| Movimento | `Scripts/ThirdPersonController.cs` | CharacterController, sprint com estamina, pulo duplo, rolagem por código (0,65 s, i-frames), agachar com cápsula e IK |
| Escalada | `Scripts/ClimbingSystem.cs` | detecção de borda, pendurar, subir com IK de mãos |
| Armas | `Combat/WeaponHolder.cs`, `PlayerCombat.cs`, `WeaponData` | **pose 100% procedural**: a arma é posicionada no mundo e as mãos seguem por IK; já existe "low ready" no sprint, recarga em etapas, coice por molas e uma arma visível nas costas |
| Corpo a corpo | `Horror/PlayerCloseCombat.cs` | golpe e empurrão por código |
| Inimigos | `ZombieAI.cs` (+ variantes), `EnemyShooterAI.cs`, `BossAI.cs`, `EnemyPerception.cs` | NavMesh, percepção por estados |
| Ragdoll | `RagdollController.cs` | troca seca animação → física (sem blending) |
| HUD | `CombatHUD.cs`, `Campaign/CampaignHUD.cs`, `Campaign/UiKit.cs` | só texto e caixas; **nenhum sprite** |
| Inventário | `Campaign/InventoryManager.cs` | barra rápida com 6 itens, nomes curtos em texto |
| Missões / narrativa | `MissionManager`, `Story.cs`, `CampaignManager`, `CinematicPlayer` | rádio e documentos só em texto; legendas e closed captions já existem (`Subtitles`) |
| Áudio | `CombatEffects` (48 vozes), `SoundFX/Horror` (93) e `SoundFX/Campaign` (26) sintetizados | **sem AudioMixer**; volume de música controlado no `GameFlow` |
| Iluminação | `NightProfile`, `PowerGridManager`, `LightFlicker`, lanterna em `PlayerCombat` | ver seção 5 |

## 3. Diagnóstico de animação — jogador

Animator: `StarterAssetsThirdPerson.controller` (1 camada, Humanoid `SK_Military_SurvivalistAvatar`, root motion desligado).
Parâmetros: `Speed, Jump, Grounded, FreeFall, MotionSpeed, Hanging, ClimbUp, Roll`.

| Ação | Clipe encontrado | Rig | Estado atual | Problema | Correção | Status |
|---|---|---|---|---|---|---|
| Idle | `Idle` (Starter Assets) | Humanoid OK | Blend 1D | — | manter | OK |
| Caminhada | `Walk_N` | OK | Blend 1D | só para frente | 2D quando houver clipes | asset pendente |
| Corrida | `Run_N`, `Run_S` | OK | Blend 1D | idem | idem | asset pendente |
| Strafe / trás / diagonais | **nenhum** | — | corpo gira para a direção | não há como fazer strafe real | importar Mixamo (lista em FASE3_ASSETS) | asset pendente |
| Parada / mudança de direção | nenhum | — | — | — | Mixamo | asset pendente |
| Agachar / andar agachado | **nenhum** | — | IK desce o quadril | pose aceitável parado, pernas "duras" andando | Mixamo + infraestrutura | asset pendente |
| Pulo / ápice / queda / aterrissagem | `JumpStart`, `InAir`, `JumpLand`, `*_Land` | OK | OK | sem aterrissagem pesada | camada procedural de impacto | parcial |
| Pulo duplo | nenhum | — | reaproveita `InAir` | — | Mixamo (flip) | asset pendente |
| Rolagem | `Roll` (só frente) | OK | trigger | rola sempre para frente, o corpo gira | rolar na direção do movimento | parcial |
| Escalada | `Hang`, `ClimbUp` | OK | OK | — | manter | OK |
| Equipar / guardar / trocar arma | **nenhum** | — | arma some e reaparece | "teletransporte" | sequência com sockets + IK (procedural, sem clipe) | infraestrutura |
| Recarga | nenhum | — | procedural (carregador sai e volta) | — | manter | OK procedural |
| ADS | — | — | procedural | — | manter | OK |
| Inclinar (lean) | nenhum | — | não existe | — | pose de coluna por código (ação simples) | a fazer |
| Deitar (prone) | **nenhum** | — | não existe | sem clipes não é aceitável | só infraestrutura, desligado | asset pendente |
| Ferimento / reação | nenhum | — | tranco procedural na arma | — | Mixamo `Hit Reaction` | asset pendente |
| Corpo a corpo | nenhum | — | procedural | — | Mixamo `Rifle Butt` | asset pendente |
| Morte | — | — | ragdoll | sem blending | — | OK |

## 4. Diagnóstico de animação — inimigos

| Família | Controller | Clipes | Problemas |
|---|---|---|---|
| Zumbi comum | `Zombie.controller` | Kevin Iglesias: Idle01, Idle01_Action01, Walk01, Attack01, Damage01, Death01_A/B | 1 ataque, 1 reação, sem corrida; já tem `CycleOffset` e `WalkSpeedScale` |
| Corredor | `Zombie_Runner.controller` | Walk01 → `Run_N` (do jogador) | corrida "humana" demais |
| Rastejante | `Zombie_Crawler.controller` | `Zombie_Crawl` (gerado) | idle = crawl a 15% |
| Tanque / Gritador | Zombie / Runner + `ZombieVariant` | mesmos clipes | identidade só por escala, velocidade e som |
| Soldados | `StarterAssetsThirdPerson` | os do jogador | sem clipes de mira, tiro ou recarga (arma procedural, como o jogador) |
| Chefes | Runner | idem | sem animação própria |

## 5. Diagnóstico visual

1. **Lanterna**: existe (`PlayerCombat.UpdateFlashlight`, tecla F lida direto do teclado, fora do Input System).
   - Ela é filha da **câmera**, que fica atrás do personagem. Resultado: o corpo do jogador projeta uma sombra grande no meio da tela, e o chão perto do jogador estoura (intensidade 30, alcance 35).
   - Não há luz periférica: fora do cone o mundo fica preto.
2. **Atlas de sombras saturado**: 153 luzes na cena, 142 ativas e 54 com sombra. A própria Unity avisa que reduziu a resolução das sombras adicionais em 4× ("34 shadow maps") — a lanterna também perde qualidade.
3. **Pós-processamento** (`NightProfile`):
   - Granulação 0,45 e vinheta 0,45 apagam detalhe nas sombras.
   - Saturação −35 e contraste +18 deixam os escuros "esmagados".
   - A exposição +0,6 tenta compensar.
4. **Ambiente**: trilight com chão quase preto (0,035); a lua (direcional 0,5) está ok.
5. **Apagão**: o `PowerGridManager` apaga as lâmpadas do setor e acende só as de emergência. Isso é correto, mas sem lanterna boa o setor fica ilegível.
6. **HUD**: nenhum sprite; itens são siglas (KIT, GRAN…). Tudo é texto + caixa.
7. **Áudio**: sem AudioMixer, então não há grupos (Music/SFX/Dialogue…) nem ducking por snapshot.

## 6. Riscos e dependências

- **Clipes de animação são o gargalo principal.** Locomoção 2D, agachar, prone, equipar e reações de inimigos de qualidade TPS moderna exigem clipes que o projeto não tem.
  - A fonte recomendada é a Adobe Mixamo, que exige a conta do usuário: não posso baixar nem fazer login por ele.
  - Tudo que depende disso é entregue como "infraestrutura pronta, asset pendente".
- **Dublagem**: não há credencial de TTS disponível. Será entregue roteiro + banco de falas + pipeline de importação; o jogo funciona sem os áudios (legenda + bip de rádio).
- **AudioMixer**: não é possível criar o asset de mixer por código no editor sem API interna. O ducking e os volumes por grupo serão feitos por código (canais), com instruções para o mixer manual.
- **Edição durante o Play** recompila e reinicia o domínio. Os testes são feitos em lotes.
