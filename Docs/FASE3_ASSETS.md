# Fase 3 — Assets

## 1. Assets criados nesta fase (todos originais)

| Asset | Local | Origem / licença | Uso |
|---|---|---|---|
| 22 ícones de itens (kit médico, granada, molotov, sinalizador, barricada, distração, fusível, cartão, módulo de rádio, documento, códigos, caixa-preta, maleta, 5 munições, créditos, vida, colete, "desconhecido") | `Assets/SourceFiles/UI/Icons/Items/*.png` | Desenhados por código (SDF, `Tools/IconGen/make_icons.py`). Arte original do projeto, sem restrição | Barra rápida, feed de coleta, HUD, catálogo |
| 6 ícones de armas (M1911, Uzi, AK-74, M4, Benelli M4, M107) | `Assets/SourceFiles/UI/Icons/Weapons/*.png` | Silhueta renderizada **a partir dos próprios modelos 3D do projeto** (menu *Phase 3 / 2*), mesmo estilo dos itens | HUD da arma, feed |
| Catálogo de itens (19 definições) | `Assets/SourceFiles/Combat/Items/Item_*.asset` + `Assets/SourceFiles/Resources/ItemCatalog.asset` | Dados | Nome, descrição, categoria, ícone, pilha máxima |
| Biblioteca de diálogos (50 falas) | `Assets/SourceFiles/Resources/DialogueLibrary.asset` | Roteiro original (ver FASE3_NARRATIVA.md) | DialogueDirector |
| Cookie da lanterna | gerado em tempo de execução (`TacticalFlashlight.Cookie`) | Procedural | Feixe com centro forte e borda suave |
| Sons de rádio (squelch de abertura/fechamento, chiado) | gerados em tempo de execução (`RadioSounds`) | Procedural | Transmissões |
| 9 sons de impacto: madeira, terra e água (3 variações cada) | `Assets/SourceFiles/SoundFX/Weapons/Impact_{Wood,Dirt,Water}_*.wav` | Sintetizados por `Tools/AudioGen/make_impacts.py` (ruído filtrado e ressonadores). **Provisórios**: trocar por foley gravado com licença livre quando houver | `SurfaceImpactLibrary` |
| Biblioteca de impactos por superfície | `Assets/SourceFiles/Resources/SurfaceImpactLibrary.asset` | Dados (menu *Phase 3 / 7*) | Partículas, furo e som por material |

Para regerar os ícones de itens: `python Tools/IconGen/make_icons.py Assets/SourceFiles/UI/Icons/Items` (só precisa de `numpy`), depois *Tools ▸ Combat ▸ Phase 3 ▸ 2 - Item Icons and Catalog*.

Nenhum pacote foi instalado e nada foi baixado da internet.

## 2. Assets existentes reaproveitados

| Asset | Local |
|---|---|
| Animações do jogador (Starter Assets: Idle, Walk_N, Run_N, Run_S, JumpStart, InAir, JumpLand, *_Land) | `Assets/SourceFiles/StarterAssets/.../Animations` |
| Pendurar, subir, rolar | `Assets/SourceFiles/Animation/Climbing` |
| Zumbis (Kevin Iglesias: Idle01, Idle01_Action01, Walk01, Walk01 RM, Attack01, Damage01, Death01_A/B) | `Assets/Kevin Iglesias/Zombie Animations` |
| Rastejar (gerado no Prompt 1) | `Assets/SourceFiles/Combat/Horror/Zombie_Crawl.anim` |
| Modelos de armas | `Assets/Low Poly Weapons VOL.1` |
| Sons (93 de terror + 26 da campanha, sintetizados nas fases anteriores) | `Assets/SourceFiles/SoundFX/Horror`, `.../Campaign` |

## 3. Assets FALTANTES (infraestrutura pronta, asset pendente)

### 3.1 Animações humanoides — fonte recomendada: Adobe Mixamo (gratuito, conta Adobe)

Não consigo baixar do Mixamo: exige login na conta do usuário. Como baixar:
1. Selecione **Y Bot** (ou suba `SK_Military_Survivalist`).
2. Opções: *Format: FBX for Unity*, *Skin: Without Skin*, *Frames per second: 30*, **In Place** marcado nas animações de locomoção.
3. Salve em `Assets/SourceFiles/Animation/Mixamo/` com os nomes abaixo.
4. No import: *Rig ▸ Animation Type: Humanoid*, *Avatar Definition: Copy From Other Avatar ▸ SK_Military_SurvivalistAvatar*. Em locomoção: *Loop Time*, *Root Transform Rotation/Position(Y) ▸ Bake Into Pose*.
5. Retargeting: confira posição das mãos (arma) e contato dos pés. Ser Humanoid **não garante** compatibilidade.

| Prioridade | Nome do arquivo | Clipe Mixamo sugerido | Onde entra |
|---|---|---|---|
| Alta | `Strafe_Left`, `Strafe_Right`, `Walk_Back`, `Walk_FwdLeft/Right`, `Walk_BackLeft/Right` | "Walking Strafe Left/Right", "Walking Backwards" (versões *Rifle* para o estado armado) | Blend Tree 2D de locomoção |
| Alta | `Run_Strafe_Left/Right`, `Run_Back` | "Running Strafe", "Running Backward" | Blend Tree 2D (correr) |
| Alta | `Crouch_Idle`, `Crouch_Walk_Fwd/Back/Left/Right` | "Crouch Idle", "Crouched Walking" | Agachar (hoje: IK no quadril) |
| Alta | `Rifle_Idle`, `Pistol_Idle` | "Rifle Aiming Idle", "Pistol Idle" | Camada de tronco armado (Avatar Mask) |
| Média | `Prone_Idle`, `Prone_Crawl`, `Stand_To_Prone`, `Prone_To_Stand` | "Prone Idle", "Prone Forward", "Standing To Prone" | **Prone** (não ativado sem esses clipes) |
| Média | `Roll_Left`, `Roll_Right`, `Roll_Back` | "Sprinting Forward Roll" etc. | Rolagem direcional |
| Média | `Hit_Front`, `Hit_Back`, `Hit_Left`, `Hit_Right` | "Hit Reaction" | Reações do jogador |
| Média | `Melee_RifleButt` | "Rifle Butt" | Golpe corpo a corpo |
| Baixa | `Run_Start`, `Run_Stop`, `Turn_180` | "Run To Stop", "Running Turn 180" | Partida e parada |
| Baixa | `Land_Heavy`, `Jump_Flip` | "Falling To Roll", "Front Flip" | Aterrissagem pesada, pulo duplo |
| Baixa | `Interact_Low`, `Interact_Panel`, `Use_Medkit` | "Picking Up", "Button Pushing", "Treating Wounded" | Interações físicas |

### 3.2 Animações de inimigos

| Família | Faltando | Sugestão |
|---|---|---|
| Zumbi comum | 2ª variação de caminhada, ataque com braço esquerdo, mordida, tropeço/levantar | Kevin Iglesias *Zombie Animations* completo (o projeto tem só o lite) ou Mixamo "Zombie Walk/Attack/Biting/Stumble" |
| Corredor | corrida própria (hoje usa `Run_N` humana) | Mixamo "Zombie Running" |
| Tanque | passo pesado, golpe de chão com antecipação | Mixamo "Mutant Walking", "Mutant Swiping", "Mutant Jump Attack" |
| Gritador | grito com expansão do corpo | Mixamo "Zombie Scream" |
| Soldados | andar/correr armado, mirar, recarregar | Mixamo "Rifle Walk/Run", "Rifle Aiming Idle", "Reloading" |

### 3.3 Áudio

| Asset | Situação | Como resolver |
|---|---|---|
| Vozes do narrador, do Tenente Duarte e do piloto (50 falas) | **Pendente.** Hoje: legenda + sons de rádio procedurais | Gerar com TTS licenciado (ElevenLabs, Azure Speech) ou gravar. Salvar como `Assets/SourceFiles/SoundFX/Dialogue/pt-BR/<id_da_fala>.wav` e rodar *Phase 3 / 4*: os clipes entram sozinhos |
| AudioMixer | Não criado (não há API pública para criar o asset por código) | Criar `Assets/SourceFiles/Audio/MainMixer.mixer` com os grupos Master/Music/SFX/Dialogue/Ambient/UI. Hoje os volumes por grupo são aplicados por código (`GameSettings.*Volume`) |
| Foley de roupa/equipamento, respiração, esforço | Pendente | Pacotes gratuitos de Foley com licença CC0 (ex.: Sonniss GDC bundle) |

### 3.4 Ícones profissionais (opcional)

Os ícones atuais são originais e consistentes. Para substituir por um pacote gratuito (Kenney *Game Icons* / *UI Pack Sci-Fi*, CC0):
1. Baixe o pacote no site do autor e confira a licença.
2. Troque o `Icon` de cada `Item_*.asset`; nada mais precisa mudar.
