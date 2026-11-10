# Base Militar: Sobreviva à Noite

Jogo de tiro em terceira pessoa com sobrevivência e terror, feito na Unity. Uma base militar foi tomada por infectados durante a noite. O jogador precisa cumprir missões, se defender de ondas de inimigos e sobreviver até o resgate.

| | |
|---|---|
| **Aluno** | Cesar Otavio da Silva Boiani |
| **Disciplina** | Desenvolvimento de Jogos Digitais |
| **Professor** | Luis Filipe Grael Tinós |
| **Trabalho** | Trabalho individual — jogo digital completo |
| **Engine** | Unity 6000.6.7f1 (Unity 6.6), Universal Render Pipeline |
| **Plataforma** | Windows 10/11 (64 bits) |
| **Documento de design** | [`Docs/GDD.md`](Docs/GDD.md) |

---

## 1. Como jogar (sem instalar nada)

O jogo pronto para rodar está na página de **Releases** do repositório.

1. Abra **[Releases → versão mais recente](https://github.com/Cesar-Otavio/jogo-de-zumbis/releases/latest)**.
2. Em **Assets**, baixe o arquivo **`BaseMilitar-Windows.zip`** (cerca de 160 MB).
3. Clique com o botão direito no arquivo → **Extrair tudo…**. Não rode o jogo de dentro do zip.
4. Na pasta extraída, abra **`BaseMilitar.exe`**.
5. Se o Windows mostrar "O Windows protegeu o computador" (SmartScreen), clique em **Mais informações → Executar assim mesmo**. O aviso aparece porque o executável não tem assinatura digital paga; não é vírus.
6. No menu, escolha **CAMPANHA** ou **MODO INFINITO**.

**Requisitos**:
- Windows 10 ou 11, 64 bits;
- placa de vídeo com DirectX 11 ou 12 (uma GPU integrada recente basta);
- ~500 MB livres em disco e 4 GB de RAM.

**Desempenho**: num notebook com Ryzen 7 5700U e GPU integrada, o jogo roda a ~95 FPS em 1080p na qualidade Média, com 30 inimigos na tela. A qualidade (Baixo, Médio, Alto) pode ser trocada em **OPÇÕES ▸ Gráficos**.

> Para sair: **Esc → Sair** (ou Alt+F4).

---

## 2. Controles

| Tecla | Ação |
|---|---|
| **W A S D** | andar |
| **Shift** | correr (gasta estamina) |
| **Espaço** | pular (pulo duplo no ar) |
| **C** | agachar (mais silencioso) |
| **Ctrl** | rolar |
| **Mouse** | olhar e mirar |
| **Botão esquerdo** | atirar |
| **Botão direito** | mirar (zoom) |
| **Q / E** (mirando) | inclinar para os lados |
| **Q** (sem mirar) | trocar de arma |
| **R** | recarregar |
| **E** | interagir: pegar armas e itens, abrir baús e portas, ligar geradores, ler documentos, usar a bancada |
| **V / B** | golpe corpo a corpo / empurrar |
| **F** | lanterna |
| **G** | largar a arma |
| **T** | trocar o ombro da câmera |
| **1–6**, **Z / X** | escolher e usar itens rápidos (kit médico, granada, molotov, sinalizador, barricada, distração) |
| **Tab** | mochila e fabricação |
| **M** | mapa tático |
| **L** | loja (no intervalo entre ondas) |
| **N** | começar a próxima onda antes do tempo |
| **Esc** | pausa |

Também há mapeamento para **controle** (Xbox ou compatível). A tela **CONTROLES** do menu lista os comandos.

---

## 3. O jogo

### 3.1 Modos

- **Campanha "Protocolo Aurora"** (10 capítulos):
  - cada capítulo tem missões (ligar geradores, recuperar itens, investigar, defender pontos) e termina numa onda de inimigos;
  - há dois chefes, no capítulo 5 e no capítulo 10;
  - **vitória**: concluir o capítulo 10, sobrevivendo até o helicóptero de resgate pousar.
- **Modo Infinito**: ondas sem fim, cada vez mais difíceis, com loja entre elas. O objetivo é bater o próprio recorde.
- **Multijogador cooperativo** (opcional): o Modo Infinito para 2 a 4 jogadores, por IP na rede local ou via Radmin VPN/Hamachi. Guia em `Docs/FASE6_RADMIN_HAMACHI.md`.

### 3.2 Vitória, derrota e pontuação

| Elemento | Como funciona |
|---|---|
| **Vida** | barra de vida com regeneração lenta; o colete absorve parte do dano |
| **Vidas** | 5, 3, 2 ou 1, conforme a dificuldade (Fácil, Normal, Difícil, Pesadelo). Morrer gasta uma vida e o jogador volta |
| **Derrota** | acabar as vidas → tela de fim de jogo |
| **Vitória** | campanha: concluir o último capítulo. Infinito: sobreviver ao maior número de ondas |
| **Pontuação** | pontos por abate (mais por tiro na cabeça e por inimigos especiais), por onda e por missão; recorde salvo |
| **Créditos** | moeda para comprar armas, munição, melhorias e itens na loja |

### 3.3 Inimigos

- **Infectados**: zumbi comum, corredor (rápido), tanque (resistente, quebra portas), gritador (chama outros) e rastejante (embosca no escuro).
- **Soldados** armados, que atacam o jogador e os infectados.
- **2 chefes**: o Experimento 07 e Aurora, a chefe final, com duas fases.
- Os inimigos **enxergam e escutam**: correr e atirar fazem barulho, e agachar é mais silencioso.

---

## 4. Conceitos de desenvolvimento aplicados (requisitos do trabalho)

| Requisito do professor | Onde aparece no jogo | Onde está no código |
|---|---|---|
| **Controle de entradas** (teclado, mouse, controle) | todos os controles da seção 2, com teclado/mouse e controle | `Assets/SourceFiles/InputSystem/StarterAssetsInputs.cs`, `InputSystem/StarterAssets.inputactions` (Input System) |
| **Movimentação de personagens** | jogador anda, corre, pula, rola e escala; inimigos andam por navegação (NavMesh) | `Scripts/ThirdPersonController.cs`, `Scripts/ClimbingSystem.cs`, `Combat/ZombieAI.cs`, `Combat/EnemyShooterAI.cs` |
| **Transform e física** | `CharacterController`, gravidade, granadas e cápsulas com `Rigidbody`, ragdoll dos inimigos, empurrões | `ThirdPersonController.cs`, `Combat/Campaign/ThrownItem.cs`, `Combat/ShellCasing.cs`, `Combat/RagdollController.cs` |
| **Colisões e interações** | tiros por raycast com hitbox (cabeça dá mais dano), coleta de itens, portas, baús, geradores, barricadas, fogo | `Combat/WeaponHolder.cs` (`FireRay`), `Combat/Hitbox.cs`, `Combat/Health.cs`, `Combat/LootCrate.cs`, `Combat/Horror/InteractiveDoor.cs`, `Combat/Inventory/WorldItem.cs` |
| **Pontuação, vidas, vitória/derrota** | pontos, créditos, vidas por dificuldade, fim de jogo, campanha concluída, recorde | `Combat/GameFlow.cs`, `Combat/Campaign/RunState.cs`, `Combat/Campaign/CampaignManager.cs`, `Combat/GameDifficulty.cs` |
| **Estados e comportamentos** | estados do jogo (Menu, Jogando, Pausado, Fim de jogo); IA com Patrulha → Investigar → Procurar → Perseguir → Atacar → Voltar; fases dos chefes; estados das missões; energia (normal, emergência, apagão) | `GameFlow.State`, `Combat/Horror/EnemyPerception.cs` (`AwarenessState`), `Combat/Campaign/BossAI.cs`, `Combat/Campaign/MissionManager.cs`, `Combat/Horror/PowerGridManager.cs` |
| **Interface e feedback** | HUD (vida, munição, pontos), mira dinâmica, marcador de acerto, setas de ameaça, minimapa, mapa tático, loja, mochila, legendas, menus, sons e partículas de impacto | `Combat/CombatHUD.cs`, `Combat/Campaign/CampaignHUD.cs`, `Combat/Exploration/MinimapView.cs`, `Combat/Campaign/ShopManager.cs`, `Combat/CombatEffects.cs` |

---

## 5. Abrir o projeto na Unity (opcional)

O jogo **não precisa da Unity** para rodar: use a build da seção 1. Esta parte é para quem quiser ver ou editar o projeto.

1. Instale pelo **Unity Hub** a versão **6000.6.7f1**, com o módulo *Windows Build Support (Mono)*.
2. Clone o repositório:
   ```bash
   git clone https://github.com/Cesar-Otavio/jogo-de-zumbis.git
   ```
3. No Unity Hub: **Add → Add project from disk** → a pasta clonada. A primeira abertura importa tudo e demora.
4. Abra a cena **`Assets/Scenes/MilitaryBase_Combat.unity`** e aperte **Play**.

### 5.1 Pacotes de terceiros (não estão no repositório)

Quatro pacotes usados no jogo **não podem ser redistribuídos** pelas licenças deles. Por isso estão fora do repositório público (estão dentro da build, o que as licenças permitem):

| Pacote | Autor / origem | Pasta esperada |
|---|---|---|
| Military Base Pack (o mapa) | Tiny Teacup Studio | `Assets/Tiny Teacup Studio/` |
| Survivalist (personagem e trajes) | Unity Asset Store | `Assets/Survivalist/` |
| Low Poly Weapons VOL.1 (armas) | Unity Asset Store | `Assets/Low Poly Weapons VOL.1/` |
| Zombie Animations | Kevin Iglesias (Unity Asset Store) | `Assets/Kevin Iglesias/` |

- **Sem eles**, o projeto abre e o código compila, mas o mapa, o personagem, as armas e as animações dos zumbis aparecem faltando.
- **Para ter tudo**: baixe cada pacote pela Asset Store (*Window ▸ Package Manager ▸ My Assets*) e importe-o na pasta indicada.

### 5.2 Gerar a build

Menu **Tools ▸ Combat ▸ Phase 7 ▸ 2 - Release Build**. A build sai em `Builds/Win64/`.

---

## 6. Estrutura do repositório

| Pasta | Conteúdo |
|---|---|
| `Assets/Scenes/` | `MilitaryBase_Combat.unity`, a cena do jogo (mapa, menus e sistemas) |
| `Assets/SourceFiles/Scripts/Combat/` | **o código do jogo** (~127 scripts C#): jogador, armas, inimigos, campanha, terror, interface, rede |
| `Assets/SourceFiles/Scripts/` | controlador em terceira pessoa e escalada |
| `Assets/SourceFiles/SoundFX/`, `Resources/` | sons, dados de itens, receitas, falas, mixer de áudio |
| `Docs/` | **GDD** (`GDD.md`), relatório completo do jogo (`RELATORIO_JOGO.md`) e documentação de cada fase |
| `Tools/` | scripts Python que geraram ícones e sons |

---

## 7. Créditos

- **Desenvolvimento, design e programação**: Cesar Otavio da Silva Boiani.
- **Apoio de IA**: o desenvolvimento contou com o apoio de IA generativa (Claude, da Anthropic) na programação, nos testes e na documentação, como permitido na disciplina.
- **Assets de terceiros**:
  - Military Base Pack (Tiny Teacup Studio);
  - Survivalist;
  - Low Poly Weapons VOL.1;
  - Zombie Animations (Kevin Iglesias);
  - Starter Assets e template de exemplo (Unity Technologies, Unity Companion License).
- **Sons**: os de terror, campanha e impactos foram sintetizados por código para este projeto (`Tools/AudioGen`).
