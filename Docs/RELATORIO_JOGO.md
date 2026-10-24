# Base Militar: Sobreviva à Noite — Relatório completo

Estado em 24/10/2026, branch `fase3-imersao`, commit `938a337`. Levantado direto do projeto: código, assets e dados lidos na Unity.

## 1. Visão geral

| Item | Valor |
|---|---|
| Gênero | tiro em terceira pessoa + sobrevivência/terror, noite numa base militar infestada |
| Motor | Unity 6000.6.7f1 (Unity 6.6), URP 17.6 (Forward+, HDR) |
| Plataforma alvo | Windows, Mac, Linux (desktop) |
| Cena do jogo | `Assets/Scenes/MilitaryBase_Combat.unity` (mapa, sistemas e menus numa cena só) |
| Modos | **Campanha** (10 capítulos, "Protocolo Aurora") e **Modo Infinito** (ondas sem fim) |
| Dificuldades | Fácil, Normal, Difícil, Pesadelo |
| Código próprio | 89 scripts C#, ~24.500 linhas (namespace `Combat`) |
| Idioma | português (textos no código; sem pacote de localização) |
| Salvamento | `PlayerPrefs` (JSON de progresso, recordes, traje, opções) |
| Controle de versão | Git: `main` (checkpoint pré-Fase 3) e `fase3-imersao` (4 commits) |

## 2. Bibliotecas e pacotes

### 2.1 Pacotes Unity (`Packages/manifest.json`)

| Pacote | Versão | Uso no jogo |
|---|---|---|
| Universal Render Pipeline | 17.6.0 | renderização, pós-processamento (perfil `NightProfile`) |
| Input System | 1.20.1 | todos os controles (`StarterAssets.inputactions`), teclado/mouse e gamepad |
| Cinemachine | 6.6.0 | câmera em terceira pessoa (`CinemachineThirdPersonFollow`), troca de ombro, mira |
| uGUI | 2.6.0 | toda a interface (construída por código) |
| TextMesh Pro | (embutido) | textos da UI |
| AI Assistant | 2.21.0-pre.1 | ferramenta de editor (MCP), não vai para o jogo |
| AI Inference | 2.6.1 | instalado, **não usado** pelo jogo |
| IET Framework (Learn) | 6.0.7 | tutoriais do template, **não usado** |
| IDE Rider / Visual Studio | 3.1.0 / 2.0.27 | integração com editor de código |
| Módulos padrão | 1.0.0 | física, áudio, animação, partículas, IA (NavMesh), terreno, etc. |

**Não instalados** (relevantes para próximos passos): Animation Rigging, Timeline, Localization, Unity Test Framework, AI Navigation (o NavMesh atual usa o módulo embutido).

### 2.2 Pacotes de assets de terceiros

| Pacote | Conteúdo | Uso |
|---|---|---|
| **Tiny Teacup Studio — Military Base Pack** | 20 modelos, 19 prefabs | o mapa: hangares, torres, muros, portões, contêineres |
| **Survivalist** | personagem militar modular, 98 materiais, 70 texturas | o jogador e as peças de traje |
| **Low Poly Weapons VOL.1** | 16 armas (prefabs) | as 6 armas do jogo (10 modelos ainda sem uso) |
| **Kevin Iglesias — Zombie Animations** | 7 FBX, 5 controllers | animações dos zumbis (idle, andar, atacar, dano, morte) |
| **Starter Assets (Third Person)** | controlador, animações, input | base do movimento do jogador e dos soldados |
| Template "Get Started" da Unity | robô Timmy, estrela coletável, plataformas, tutoriais | **sobras do template, não usadas** pelo jogo |

## 3. Como se joga

### 3.1 Controles (teclado e mouse)

| Tecla | Ação |
|---|---|
| WASD | andar |
| Shift | correr (gasta estamina) |
| Espaço | pular; pulo duplo no ar |
| C | agachar (silencioso); segurar = deitar (infraestrutura, desligado sem clipes) |
| Ctrl | rolar (invulnerável no início) |
| Botão esquerdo / direito | atirar / mirar |
| Q / E mirando | inclinar para a esquerda/direita |
| Q (sem mirar) | trocar de arma |
| V / B | coronhada ou faca / empurrar |
| T | trocar ombro da câmera |
| R | recarregar |
| E | usar (armas, baús, portas, geradores, painéis, documentos) |
| G | largar arma |
| F | lanterna |
| 1–6 ou Z | escolher item rápido |
| X | usar item (segurar mira o arremesso) |
| L / N / K | loja (no intervalo) / próxima onda / abandonar missão opcional |
| Esc | pausa |

Também há mapeamento para gamepad (LB/RB inclinam enquanto mira).


### 3.2 Menu principal

- CAMPANHA (continua do capítulo salvo)
- MODO INFINITO
- DIFICULDADE (‹ ›, com descrição)
- PERSONAGEM (guarda-roupa)
- PROGRESSO (nível, conquistas, estatísticas, documentos)
- CONTROLES
- OPÇÕES (2 colunas)
- SAIR

Além do menu: pausa, tela de fim de jogo e tela de campanha concluída.

### 3.3 Opções

- **Imagem e conforto**: brilho, granulação, tremor de câmera, flashes (raios e refletores), efeitos de ferimento.
- **Acessibilidade**: legendas de sons (closed captions), setas de inimigos, marcador de objetivo, legendas de diálogo, tamanho das legendas.
- **Volumes**: geral, música, efeitos, diálogos e rádio, ambiente, interface.

### 3.4 Dificuldades

| | Fácil | Normal | Difícil | Pesadelo |
|---|---|---|---|---|
| Vidas | 5 | 3 | 2 | 1 |
| Dano inimigo | ×0,6 | ×1 | ×1,4 | ×1,8 |
| Vida inimiga | ×0,8 | ×1 | ×1,25 | ×1,5 |
| Quantidade de inimigos | ×0,75 | ×1 | ×1,3 | ×1,6 |
| Regeneração do jogador | ×1,5 | ×1 | ×0,75 | ×0,4 |
| Suprimentos no mapa | ×1,3 | ×1 | ×0,85 | ×0,7 |
| Percepção inimiga | ×0,85 | ×1 | ×1,15 | ×1,3 |
| Chance de infectados especiais | ×0,7 | ×1 | ×1,25 | ×1,5 |
| Pontuação | ×0,75 | ×1 | ×1,5 | ×2 |

A dificuldade também muda a mira e o tempo de reação dos inimigos.

## 4. Campanha — "Protocolo Aurora"

- **Prólogo** de ~45 s com legendas; Espaço pula.
- **Início**: o jogador começa desarmado no capítulo 1.
- **Ritmo de cada capítulo**:
  1. missões de história;
  2. uma missão dispara a onda;
  3. intervalo com a loja aberta (50–80 s).

| # | Capítulo | Missões principais (objetivos) | Extras | Onda |
|---|---|---|---|---|
| 1 | Primeiro contato | pegar arma, investigar, recuperar, sobreviver à onda | — | 5 zumbis |
| 2 | Sem energia | gerador principal: achar fusível, ligar, restaurar energia, defender | — | 7 Z |
| 3 | Mensagem desconhecida | sinal perdido: investigar, coletar, 2 interações | lateral "Último suprimento" | 8 Z + 1 soldado |
| 4 | Arsenal | arsenal bloqueado: cartão, porta, recuperar | **escolha**: Defender o arsenal / Restaurar a energia | 9 Z + 2 S |
| 5 | Experimento 07 | **chefe E-07** | — | 6 Z + chefe |
| 6 | Comunicação comprometida | torre de vigilância: 2 interações + defender | lateral "Portões comprometidos" | 10 Z + 2 S |
| 7 | Laboratório | caixa-preta: investigar, recuperar, interagir | lateral "Código vermelho" | 10 Z + 3 S |
| 8 | Falha de contenção | protocolo: válvulas em sequência, sobreviver | — | 12 Z + 2 S |
| 9 | Protocolo de evacuação | recuperar códigos, rádio, ir ao heliponto | — | 13 Z + 3 S |
| 10 | Sobreviva à noite | evacuação: acender o farol, **chefe Aurora**, defender até o pouso | — | 14 Z + 2 S + chefe |

A vida dos inimigos sobe de ×1 a ×1,2 ao longo da campanha. Ao final há a tela "campanha concluída" com estatísticas.

### 4.1 Sistema de missões

- **14 tipos de objetivo**: Interagir, Coletar, Matar (por tipo), Defender zona, Sobreviver X s, Sequência, Restaurar energia, Porta, Investigar, Recuperar, Extrair, Sobreviver à onda, Fortificar (barricadas), Estar armado.
- **Objetivos podem ter**:
  - bônus opcional em créditos;
  - inimigos de pressão;
  - fala de rádio;
  - marcador no mundo;
  - "pular depois de X s", para a partida nunca travar.
- **Efeitos de missão**: ligar ou cortar energia, abrir ou trancar portas, bloquear um portão de spawn, holofotes, alarme, flags, dar itens, dar munição, criar arma no chão.
- **Escolhas**: duas missões incompatíveis no intervalo.

### 4.2 Narrativa

- **Personagens** (por texto e rádio):
  - Tenente M. Duarte, "Controle Aurora", o operador de rádio;
  - o piloto;
  - Dr. H. Valença;
  - Dra. C. Nogueira;
  - Cabo Moraes;
  - Sgt. Arantes;
  - Eng. Siqueira.
- **Biblioteca de diálogos**: 50 falas. Um diretor organiza as falas por prioridade e validade, e há legendas com o nome de quem fala.
- **Efeitos de rádio**: bip, chiado e filtro, com ducking da música.
- **Vozes**: não há áudio gravado ainda. O pipeline de importação está pronto (*Phase 3 ▸ 4*).
- **Documentos** (9 colecionáveis, leitor com ícone do item):

| Documento | Autor |
|---|---|
| Diário de turno — Portão Leste | Cabo R. Moraes |
| Memorando de manutenção | Eng. P. Siqueira |
| Ordem 7-ALFA (interceptada) | Comando Regional |
| Bilhete do intendente | Sgt. L. Arantes |
| Relatório de contenção — E-07 | Dr. H. Valença |
| Notas de laboratório — Aurora | Dra. C. Nogueira |
| Procedimento de contenção | Segurança da base |
| Caixa-preta — registro final | Sistema AURORA |
| Códigos de evacuação | Tenente M. Duarte |

- **Cinemáticas**: `CinematicPlayer` faz tomadas de câmera com letterbox.

## 5. Modo Infinito

- Ondas sem fim (`EndlessDirector`), com intervalo e loja entre elas.
- A dificuldade cresce a cada onda.
- O recorde de onda fica salvo e aparece no menu e no progresso.

## 6. Jogador

| Sistema | O que faz |
|---|---|
| Movimento (`ThirdPersonController`) | andar, correr com estamina, pulo duplo, rolagem com invulnerabilidade, agachar com IK, giro suave |
| Escalada (`ClimbingSystem`) | detecta bordas, pendura, sobe com IK das mãos |
| Armas (`WeaponHolder`, `PlayerCombat`) | pose 100% procedural com IK; mira com zoom; recuo por molas; dispersão (bloom); recarga em etapas (pente ou cartucho a cartucho); ferrolho/bomba visível; cápsulas ejetadas; low-ready no sprint |
| Armas no corpo (`WeaponHolder.Gear`) | 2 armas; sockets no quadril, costas e peito; guardar, sacar e trocar sem teletransporte; pegar do chão abaixando; troca enfileirada |
| Inclinação (`LeanController`) | Q/E mirando, com limite de parede |
| Corpo a corpo (`PlayerCloseCombat`) | coronhada/faca e empurrão |
| Lanterna (`TacticalFlashlight`) | no peito, feixe com cookie, luz de preenchimento, balanço, auto-íris, não ilumina o próprio jogador |
| Vida (`Health`, `PlayerInjuryFeedback`) | regeneração, colete (absorve metade até 60), vidas; efeitos de ferimento (respiração, batimento, tela) |
| Barulho (`NoiseSystem`) | correr, atirar e pisar em metal fazem ruído que os inimigos ouvem |
| Passos (`SurfaceFootstepSystem`) | som por superfície: concreto, terra, grama, metal, água, madeira |
| Traje (`PlayerOutfit`) | camisa, calça, botas, boné, colete (Survivalist); algumas opções liberadas por nível |
| Deitar (prone) | **infraestrutura pronta, asset pendente** |

## 7. Armas

| Arma | Classe | Calibre | Dano | Cadência | Pente/Reserva | Alcance | Preço na loja |
|---|---|---|---|---|---|---|---|
| M1911 | Pistola | .45 ACP | 28 | semi, ~430/min | 12 / 48 | 120 m | inicial |
| Uzi | Submetralhadora | 9 mm | 14 | auto, ~920/min | 32 / 128 | 120 m | 400 (nível 1) |
| Benelli M4 | Escopeta | calibre 12 | 13 × 9 bagos | bomba, 0,85 s | 7 / 28 (cartucho a cartucho) | 45 m | 600 (nível 1) |
| AK-74 | Fuzil | 5,45 mm | 26 | auto, 600/min | 30 / 90 | 120 m | 650 (nível 1) |
| M4 | Fuzil | 5,56 mm | 22 | auto, 750/min | 30 / 90 | 120 m | 650 (nível 2) |
| M107 | Fuzil de precisão | .50 BMG | 110 | ferrolho, 1,25 s | 5 / 15 | 300 m, luneta | 900 (nível 3) |

**7 melhorias de arma (loja)**:

| Melhoria | Efeito | Preço |
|---|---|---|
| Cano reforçado | +15% de dano | 350 |
| Carregador estendido | +40% no pente | 300 |
| Empunhadura vertical | −30% de recuo | 300 |
| Compensador | −20% de recuo, −10% de dispersão | 250 |
| Silenciador | furtividade; nível 3 | 400 |
| Mira holográfica | mais zoom; nível 2 | 250 |
| Mola de recarga rápida | recarga 35% mais rápida; nível 2 | 300 |

## 8. Itens e loja

- **6 itens rápidos**: Kit médico (+50), Granada (2,5 s, fere você também), Molotov (fogo por 8 s), Sinalizador (luz vermelha por 40 s, atrai infectados), Kit de barricada, Distração (apita 10 s).
- **Itens de missão e documentos** (catálogo com ícones): fusível, cartão magnético, chave de válvula, módulo de rádio, caixa-preta, códigos de evacuação, documentos de laboratório, etc. São 19 definições e 25 ícones de itens, mais 6 de armas.
- **Loja**: 25 entradas em 5 abas:
  - Armas;
  - Munição (60/180);
  - Proteção (colete 200, atendimento 150);
  - Consumíveis (40–100);
  - Melhorias, incluindo treino de resistência (+15 de estamina) e bolsa tática (+1 de capacidade).
- **Créditos**: vêm de abates, ondas, missões e depósitos.
- **Loot pelo mapa**: baús (`LootCrate`), munição e kits (`ConsumablePickup`), armas no chão (`WeaponPickup`, `WeaponSpawnPoint`).
- **Feed de coleta**: ícone, nome e quantidade, agrupados.

## 9. Inimigos

| Inimigo | Detalhes |
|---|---|
| Zumbi comum | NavMesh, perambula, persegue, ataca; animações Kevin Iglesias |
| Corredor | 60% de vida, ×3,6 de velocidade |
| Tanque | ×4,5 de vida, ×2,6 de dano, quebra portas e barricadas |
| Gritador | grita e chama outros (`ScreamerBehaviour`) |
| Rastejante | baixo, embosca em cantos escuros |
| Soldados (4 visuais) | `EnemyShooterAI`: patrulham, investigam, atiram em rajadas de posições de tiro e se reposicionam; lutam contra o jogador **e contra os infectados**; armas procedurais como o jogador |
| **Experimento 07** (chefe, cap. 5) | 1500 de vida; investida telegrafada; se bater em algo fica atordoado e vulnerável (×2,5) |
| **Aurora** (chefe final, cap. 10) | 2600 de vida; 2 fases; onda de choque; invoca corredores; fraco à luz (lâmpadas e sinalizadores) |

- **Percepção** (`EnemyPerception`): visão, audição, estados de suspeita, investigação e perseguição. A dificuldade escala tudo isso.
- **Reações**: reações direcionais a tiros e cambaleio (`HitReactor`), ragdoll na morte com limite e anti-explosão, multiplicador de dano na cabeça.
- **Spawn** (`EnemySpawner`): portões que podem ser bloqueados por missão; a chance de infectados especiais depende da dificuldade.

## 10. Mundo e terror

| Sistema | O que faz |
|---|---|
| Mapa | base militar (Military Base Pack): portões, hangares, torres, arsenal, laboratório, heliponto |
| Noite e clima (`WeatherController`) | chuva, raios, vento, neblina; skybox noturno; perfil de pós-processamento noturno |
| Rede elétrica (`PowerGridManager`, `PowerGenerator`) | setores com apagão, luzes de emergência, geradores que exigem fusível e fazem barulho |
| Portas (`InteractiveDoor`) | abrir, fechar, trancar (cartão); inimigos podem arrombar |
| Barricadas (`BarricadePoint`) | construir e reforçar; tomam dano |
| Fogo (`FireZone`) | molotov e fogo de cenário, que os inimigos evitam |
| Itens arremessados (`ThrownItem`) | granada, molotov, sinalizador, distração com física e trajetória |
| Obstáculos quebráveis (`Breakables`) | portas fechadas e barricadas que os infectados golpeiam; tanques e chefes quebram, os comuns só danificam |
| Eventos de terror (`EnvironmentalEventDirector`) | 12 tipos: porta batendo, passos distantes, batidas em contêiner, gemidos, interferência no rádio, alarme, lâmpada piscando, objeto caindo, silhueta, movimento no corredor, holofote revelando, metal arranhando |
| Diretor de tensão (`TensionDirector`) | controla o ritmo de sustos e calmaria |
| Luzes (`LightFlicker`, `TimedLight`) | lâmpadas falhando, luzes temporárias |

## 11. Interface

- **HUD de combate** (`CombatHUD`):
  - cartões de vida, vidas e colete;
  - cartão da arma, com ícone, munição e reserva;
  - pontuação, abates e créditos;
  - mira dinâmica, marcadores de acerto, setas de ameaça, avisos e legendas de sons.
- **HUD de campanha** (`CampaignHUD`):
  - cartão da missão com objetivos;
  - barra rápida com ícones e quantidades;
  - feed de coleta;
  - avisos (toasts), conquistas e banner de intervalo;
  - leitor de documentos.
- **Loja** (`ShopManager`): 5 abas, ícones, descrições, níveis exigidos, limites por partida.
- **Legendas de diálogo** (`DialogueSubtitleView`): nome de quem fala, tamanho ajustável.
- **Estilo**: toda a UI é gerada por código (`UiKit`), sem prefabs de UI.

## 12. Progressão permanente

- **Experiência e nível**: até o nível 30. O nível libera armas na loja e peças de traje.
- **8 conquistas**:
  - Ainda de pé (5 ondas);
  - Cem a menos (100 zumbis);
  - Intocável (missão sem dano);
  - Fim do Experimento;
  - Protocolo Aurora (terminar a campanha);
  - Só o básico (uma onda só com a pistola);
  - Amanhecer (derrotar Aurora);
  - Arquivista (todos os documentos).
- **Estatísticas**: abates, ondas, missões concluídas, melhor onda no Infinito, capítulo salvo.

## 13. Áudio

- **205 efeitos sonoros**:
  - armas (78): tiros com variação, pentes, ferrolho, cartuchos, disparo em seco;
  - terror (93): portas, eventos, gerador, alarme, passos por superfície, golpes, ferimento, vozes dos especiais;
  - campanha (26): chefes, explosões, fogo, helicóptero, loja, missão, rádio, música de chefe.
  - Os sons de terror e campanha foram sintetizados nas fases anteriores.
- **Música**: Ethereal, Exciting/Action, Suspenseful, mais ambientes sci-fi e vento.
- **Volumes**: 6 grupos por código, com ducking durante falas.
- **AudioMixer**: não existe ainda (configuração manual opcional).

## 14. Ferramentas de editor (menu *Tools ▸ Combat*)

| Menu | Função |
|---|---|
| Setup | configuração base de combate |
| Setup Military Base Map | monta o mapa |
| Setup Menu, Waves and Roll | menus, ondas, rolagem |
| Setup Night, Loot and Survivor | noite, loot, personagem |
| Setup Horror Systems | percepção, eventos, energia, portas |
| Setup Campaign and Missions | campanha, missões, chefes, loja |
| Phase 3 ▸ 1–5 | lanterna, ícones e catálogo, movimento e armas, narrativa, reações |
| Phase 3 ▸ 6 | Animator de locomoção 2D (precisa dos clipes Mixamo) |
| Play From Game Scene | sempre dá Play pela cena do jogo |

Ferramentas fora da Unity:
- `Tools/IconGen/make_icons.py` gera os ícones.
- `Tools/add_input_actions.py` adiciona ações de input.

## 15. Lacunas conhecidas (o que falta ou está incompleto)

### Animação (maior lacuna visual)
- **Sem clipes para**:
  - strafe, andar de ré e diagonais;
  - partida e parada;
  - agachar andando;
  - prone;
  - pulo duplo;
  - reações de dano do jogador.
- **A infraestrutura já existe**: falta importar os clipes Mixamo e rodar *Phase 3 ▸ 6*.
- **Inimigos**:
  - o zumbi tem 1 ataque e 1 reação;
  - o corredor usa a corrida do jogador;
  - tanque, gritador e chefes não têm animação própria;
  - soldados sem clipes de arma.

### Áudio e voz
- **Vozes**: as 50 falas só têm legenda; o pipeline de TTS e importação está pronto.
- **AudioMixer**: não existe.

### Conteúdo
- **Mapa**: um só mapa.
- **Armas**: 6. Há 10 modelos de arma do pacote sem uso.
- **Inimigos**: 5 tipos de infectado, soldados genéricos e 2 chefes.

### Técnico
- **Localização**: só PT-BR, sem pacote; as chaves `dlg.*` estão prontas.
- **Testes automatizados**: não existem (Unity Test Framework não instalado).
- **Desempenho**: não há medição em build com Profiler; a cena tem ~150 luzes.
- **Lixo do template**: robô Timmy, estrela, plataformas, `GetStarted_Scene`, pasta Tutorials e o pacote IET.

### Funcionalidades que não existem
- **Modos e interface**:
  - multijogador ou cooperativo;
  - mapa ou minimapa;
  - menu principal com cena 3D própria (o menu é sobre a cena do jogo).
- **Mecânicas**:
  - inventário de mochila (só barra rápida);
  - fabricação (crafting);
  - veículos;
  - stealth kill;
  - companheiros controlados por IA.
- **Salvamento**:
  - salvamento no meio do capítulo (salva por capítulo);
  - vários perfis.
- **Configuração**:
  - remapeamento de teclas;
  - opções gráficas (qualidade, resolução, FOV).

## 16. Ideias para decidir o próximo passo

| Ideia | Esforço | Impacto | Depende de |
|---|---|---|---|
| Importar clipes Mixamo (locomoção 2D, agachar, reações) | médio | **alto** (visual) | você baixar os clipes (conta Adobe) |
| Vozes das 50 falas (TTS) | baixo–médio | alto (imersão) | serviço de TTS escolhido por você |
| Novas armas com os 10 modelos sem uso | baixo | médio | nada |
| Novo tipo de infectado ou chefe intermediário | médio | médio–alto | nada (animação limitada) |
| Opções gráficas, FOV e remapeamento de teclas | baixo–médio | médio | nada |
| Minimapa ou mapa tático com objetivos | médio | médio | nada |
| Stealth kill por trás (ligado à furtividade e ao silenciador) | médio | médio | idealmente um clipe |
| Segundo mapa ou setor novo da base | alto | alto | assets de cenário |
| Modo cooperativo local | alto | alto | grande refatoração |
| Limpeza do template e build com medição de FPS | baixo | médio (qualidade) | nada |
| Localização (EN) | médio | médio | pacote Localization |
