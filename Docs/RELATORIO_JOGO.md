# Base Militar: Sobreviva à Noite — Relatório completo e pendências

Estado em **25/10/2026**, branch `fase6-multiplayer`, commit `de51e09`.

O levantamento foi feito direto do projeto: código, assets, documentos das fases e testes registrados. Este documento substitui a versão de 24/10 (que continua no histórico do Git).

**Como ler:** as seções 1 a 3 dão o panorama e a parte técnica. As seções 4 a 16 descrevem o que existe no jogo. As seções 17 a 19 dizem o que falta, **o que eu preciso que você me mande** e a ordem que sugiro.

---

## 1. Panorama

| Área | Situação | Comentário |
|---|---|---|
| Combate em terceira pessoa | ✅ pronto | 6 armas, 7 melhorias, IK procedural, recuo, recarga em etapas, armas no corpo |
| Campanha "Protocolo Aurora" | ✅ pronta (10 capítulos) | só legendas, **sem vozes** |
| Modo Infinito | ✅ pronto | ondas sem fim, loja entre ondas, recorde |
| Inimigos | ✅ 5 infectados + soldados + 2 chefes | animações limitadas; os 4 inimigos novos da Fase 5 **não começaram** |
| Terror e atmosfera | ✅ pronto | clima, apagões, 12 eventos, diretor de tensão, lanterna tática |
| Interface | ✅ pronta | HUD, loja, leitor de documentos, opções em 3 páginas, legendas |
| Minimapa e mapa tático | ✅ pronto (Fase 5) | névoa de exploração, marcadores |
| Mochila e fabricação | ✅ pronto (Fase 5) | 18 espaços, 6 receitas, 1 bancada **provisória** |
| Opções gráficas | ✅ pronto (Fase 5) | qualidade, resolução, FOV, sombras, FPS, VSync |
| Expansão da base (salas novas) | ⛔ não feita | depende de props e de medir o desempenho |
| Animações extras (strafe, ré, prone…) | 🟡 infraestrutura pronta | **faltam os clipes** (Mixamo) |
| Vozes / dublagem | 🟡 infraestrutura pronta | **falta um TTS autorizado ou gravação** |
| Multiplayer cooperativo | 🟡 **MVP funcionando** | 2 jogadores testados no mesmo PC; falta testar em 2 PCs e sincronizar a maior parte do jogo |
| Desempenho | 🟡 só medido no editor | ~28 ms por quadro no editor (iGPU, 1440p); **sem Profiler em build** |
| Localização | ⛔ só PT-BR | as chaves `dlg.*` estão prontas |
| Testes automatizados | ⛔ não existem | Unity Test Framework não instalado |

Legenda: ✅ pronto e testado · 🟡 parcial ou dependente de algo · ⛔ não feito.

---

## 2. Visão técnica

| Item | Valor |
|---|---|
| Gênero | tiro em terceira pessoa + sobrevivência/terror, numa base militar infestada à noite |
| Motor | Unity **6000.6.7f1** (Unity 6.6), URP 17.6 (Forward+, HDR) |
| Plataforma | Windows (a build de teste existe); Mac e Linux não foram testados |
| Cena | `Assets/Scenes/MilitaryBase_Combat.unity`: mapa, sistemas e menus numa cena só |
| Modos | Campanha (10 capítulos), Modo Infinito e **Multijogador cooperativo** (Infinito, de 2 a 4 vagas) |
| Dificuldades | Fácil, Normal, Difícil, Pesadelo |
| Código próprio | **117 scripts C#, ~28.500 linhas** (namespace `Combat`). Com Starter Assets e template, são 123 scripts e ~30.000 linhas |
| Idioma | português (sem pacote de localização) |
| Salvamento | `PlayerPrefs`: progresso (JSON), recordes, traje, opções, névoa do mapa, nome e IP do multiplayer |
| Build Windows | `Builds/Win64/BaseMilitar.exe`, cerca de 421 MB, sem erros (fora do Git) |

### 2.1 Branches e histórico

| Branch / tag | Conteúdo |
|---|---|
| `main` | checkpoint **antes da Fase 3** (desatualizado) |
| `fase3-imersao` | Fase 3 completa |
| `fase5-mundo` (tag `fase5-checkpoint` = antes dela) | Fase 3 + Fase 5 (entrega 1) |
| **`fase6-multiplayer`** (tag `fase6-checkpoint` = antes dela) | **tudo**: Fases 3, 5 e o MVP da 6. É o estado atual. |

> Não há "Fase 4" no histórico do projeto: os pedidos foram da Fase 3 direto para a Fase 5.
> **Recomendação**: quando você aprovar, juntar `fase6-multiplayer` na `main`, para a `main` deixar de ficar 9 commits atrás.

### 2.2 Organização do código (`Assets/SourceFiles/Scripts/Combat/`)

| Pasta | Scripts | O que tem |
|---|---|---|
| (raiz) | ~30 | jogador, armas, vida, inimigos, efeitos, HUD, fluxo do jogo (`GameFlow`) |
| `Campaign/` | 28 | campanha, missões, chefes, loja, inventário, progressão, Infinito, UI (`UiKit`) |
| `Horror/` | 20 | percepção, ruído, energia, portas, eventos, clima, passos, opções |
| `Network/` | 10 | multiplayer (sessão, lobby, jogadores, bonecos, réplicas) |
| `Editor/` | 10 | ferramentas de montagem (*Tools ▸ Combat*) |
| `Exploration/` | 6 | minimapa, mapa tático, névoa, marcadores |
| `Inventory/` | 5 | mochila, ações, itens no mundo, telas |
| `Crafting/` | 4 | receitas, catálogo, bancada |
| `Narrative/` | 4 | biblioteca de falas, diretor, legendas, rádio |
| `Items/` | 4 | catálogo de itens, feed de coleta |
| `Animation/`, `Lighting/`, `Effects/` | 4 | inclinação, reações a impacto, lanterna, impactos por material |

---

## 3. Bibliotecas e assets

### 3.1 Pacotes Unity (`Packages/manifest.json`)

| Pacote | Versão | Uso |
|---|---|---|
| Universal Render Pipeline | 17.6.0 | renderização e pós-processamento (`NightProfile`) |
| Input System | 1.20.1 | todos os controles (teclado, mouse e gamepad) |
| Cinemachine | 6.6.0 | câmera em terceira pessoa, troca de ombro, mira |
| uGUI + TextMesh Pro | 2.6.0 | toda a UI, construída por código |
| **Netcode for GameObjects** | **2.13.3** | multiplayer (Fase 6) |
| **Unity Transport** | **6.6.0** | UDP direto por IP:porta (Fase 6) |
| AI Assistant | 2.21.0-pre.1 | ferramenta de editor; não vai para o jogo |
| AI Inference (Sentis) | 2.6.1 | **não usado**: deixa shaders na build e gera centenas de avisos |
| IET Framework (Learn) | 6.0.7 | tutoriais do template, **não usado** |
| Rider / Visual Studio | 3.1.0 / 2.0.27 | integração com IDE |

**Não instalados** (podem ser úteis): Animation Rigging, Timeline, Localization, Unity Test Framework, AI Navigation (o NavMesh usa o módulo embutido).

### 3.2 Pacotes de terceiros

| Pacote | Uso |
|---|---|
| Tiny Teacup Studio — Military Base Pack | o mapa: hangares, torres, muros, portões, contêineres |
| Survivalist | o jogador, os trajes e os bonecos do multiplayer |
| Low Poly Weapons VOL.1 | as 6 armas do jogo (**10 modelos ainda sem uso**) |
| Kevin Iglesias — Zombie Animations | animações dos zumbis |
| Starter Assets (Third Person) | base do movimento |
| Template "Get Started" | **sobras não usadas**: robô Timmy, estrela, plataformas, `GetStarted_Scene`, Tutorials |

### 3.3 Conteúdo produzido no projeto

- **Ícones**: 37 de itens e 6 de armas, desenhados por código (`Tools/IconGen/make_icons.py`) ou renderizados dos modelos.
- **Dados**: 30 itens, 6 receitas e 2 catálogos (itens, fabricação), mais a biblioteca de 54 falas.
- **Áudio**: 225 arquivos.
  - Armas: 87.
  - Terror: 93 (sintetizados).
  - Campanha: 26 (sintetizados).
  - Música e ambiente.
- **Mapa da base**: imagem 512×512 gerada a partir dos colisores da cena.

---

## 4. Como se joga

### 4.1 Controles (teclado e mouse)

| Tecla | Ação |
|---|---|
| WASD / Shift | andar / correr (gasta estamina) |
| Espaço | pular (pulo duplo no ar) |
| C | agachar (silencioso). Segurar = deitar (desligado até haver clipes) |
| Ctrl | rolar (invulnerável no início) |
| Botão esquerdo / direito | atirar / mirar |
| Q / E mirando | inclinar |
| Q (sem mirar) | trocar de arma |
| V / B | coronhada ou faca / empurrar |
| T | trocar o ombro da câmera |
| R | recarregar |
| E | usar (armas, baús, portas, geradores, painéis, documentos, bancada, materiais) |
| G | largar arma |
| F | lanterna |
| 1–6 ou Z / X | escolher item rápido / usar (segurar X mira o arremesso) |
| **Tab** | **mochila e fabricação** (Fase 5) |
| **M** | **mapa tático** (Fase 5) |
| L / N / K | loja (no intervalo) / próxima onda / abandonar missão opcional |
| Esc | pausa (fecha a mochila ou o mapa primeiro) |

O gamepad tem mapeamento completo; LB/RB inclinam enquanto você mira. **Tab e M ainda não foram confirmados num teclado físico, e o gamepad não foi testado com um controle real.**

### 4.2 Menu principal

1. CAMPANHA
2. MODO INFINITO
3. DIFICULDADE (‹ ›)
4. **MULTIJOGADOR**
5. PERSONAGEM (traje)
6. PROGRESSO
7. CONTROLES
8. OPÇÕES
9. SAIR

O fundo do menu passeia por planos de câmera dos locais da história. Também há pausa (com MOCHILA e MAPA), fim de jogo e campanha concluída.

### 4.3 Opções (3 páginas)

- **Imagem e conforto**: brilho, granulação, tremor de câmera, flashes, efeitos de ferimento.
- **Acessibilidade**: legendas de sons, setas de inimigos, marcador de objetivo, legendas de diálogo e o tamanho delas.
- **Volumes**: geral, música, efeitos, diálogo e rádio, ambiente, interface.
- **Minimapa**: ligar, orientação (norte fixo ou girando), zoom.
- **Gráficos**:
  - qualidade, resolução, modo de janela, FOV;
  - distância de sombras (30–90 m), qualidade de efeitos;
  - limite de FPS, VSync, intensidade do pós-processamento.

### 4.4 Dificuldades

| | Fácil | Normal | Difícil | Pesadelo |
|---|---|---|---|---|
| Vidas | 5 | 3 | 2 | 1 |
| Dano inimigo | ×0,6 | ×1 | ×1,4 | ×1,8 |
| Vida inimiga | ×0,8 | ×1 | ×1,25 | ×1,5 |
| Quantidade de inimigos | ×0,75 | ×1 | ×1,3 | ×1,6 |
| Regeneração do jogador | ×1,5 | ×1 | ×0,75 | ×0,4 |
| Suprimentos | ×1,3 | ×1 | ×0,85 | ×0,7 |
| Percepção inimiga | ×0,85 | ×1 | ×1,15 | ×1,3 |
| Infectados especiais | ×0,7 | ×1 | ×1,25 | ×1,5 |
| Pontuação | ×0,75 | ×1 | ×1,5 | ×2 |

---

## 5. Campanha — "Protocolo Aurora"

O jogo começa com um prólogo de ~45 s (Espaço pula), e o jogador começa desarmado. Cada capítulo segue o ciclo: missões → uma missão dispara a onda → intervalo com a loja (50–80 s).

| # | Capítulo | Missões principais | Extras | Onda |
|---|---|---|---|---|
| 1 | Primeiro contato | pegar arma, investigar, recuperar, sobreviver | — | 5 zumbis |
| 2 | Sem energia | fusível, gerador, restaurar energia, defender | — | 7 Z |
| 3 | Mensagem desconhecida | sinal perdido | lateral "Último suprimento" | 8 Z + 1 soldado |
| 4 | Arsenal | cartão, porta, recuperar | **escolha**: defender o arsenal / restaurar a energia | 9 Z + 2 S |
| 5 | Experimento 07 | **chefe E-07** | — | 6 Z + chefe |
| 6 | Comunicação comprometida | torre de vigilância | lateral "Portões comprometidos" | 10 Z + 2 S |
| 7 | Laboratório | caixa-preta | lateral "Código vermelho" | 10 Z + 3 S |
| 8 | Falha de contenção | válvulas em sequência | — | 12 Z + 2 S |
| 9 | Protocolo de evacuação | códigos, rádio, heliponto | — | 13 Z + 3 S |
| 10 | Sobreviva à noite | farol, **chefe Aurora**, defender até o pouso | — | 14 Z + 2 S + chefe |

**Missões**:
- 14 tipos de objetivo: interagir, coletar, matar, defender, sobreviver, sequência, energia, porta, investigar, recuperar, extrair, onda, fortificar, estar armado.
- Bônus opcionais, inimigos de pressão, falas de rádio e marcadores no mundo e no minimapa.
- "Pular depois de X s", para a partida nunca travar.
- Efeitos no mundo: energia, portas, portões, holofotes, alarme, itens.

**Narrativa**:
- 54 falas com prioridade e validade, legendas com o nome de quem fala, efeito de rádio e ducking.
- Personagens: Tenente Duarte (Controle Aurora), o piloto, Dr. Valença, Dra. Nogueira, Cabo Moraes, Sgt. Arantes, Eng. Siqueira.
- 9 documentos colecionáveis com leitor.
- Cinemáticas com letterbox.

---

## 6. Modo Infinito

- Ondas sem fim (`EndlessDirector`), com intervalo e loja entre elas.
- A dificuldade cresce a cada onda, e o recorde fica salvo.
- É o único modo disponível no multiplayer por enquanto.

---

## 7. Jogador

| Sistema | O que faz |
|---|---|
| Movimento | andar, correr com estamina, pulo duplo, rolagem, agachar com IK, giro suave |
| Escalada | detecta bordas, pendura, sobe com IK das mãos |
| Armas | pose procedural com IK, mira com zoom, recuo por molas, dispersão, recarga em etapas, ferrolho/bomba visível, cápsulas, low-ready na corrida |
| Armas no corpo | 2 armas em sockets (quadril, costas, peito), sacar e trocar sem teletransporte, pegar do chão abaixando |
| Inclinação | Q/E mirando, com limite de parede |
| Corpo a corpo | coronhada/faca e empurrão |
| Lanterna tática | no peito, cookie, preenchimento, balanço, auto-íris |
| Vida | regeneração, colete (absorve metade, até 60), vidas, efeitos de ferimento |
| Ruído | correr, atirar e pisar em metal atraem inimigos |
| Passos | som por superfície (concreto, terra, grama, metal, água, madeira) |
| Traje | 7 peças do Survivalist; algumas liberadas por nível; o traje aparece no multiplayer |
| Deitar (prone) | **infraestrutura pronta, faltam os clipes** |

---

## 8. Armas

| Arma | Classe | Dano | Cadência | Pente/Reserva | Alcance | Loja |
|---|---|---|---|---|---|---|
| M1911 | pistola .45 | 28 | semi | 12 / 48 | 120 m | inicial |
| Uzi | SMG 9 mm | 14 | auto ~920/min | 32 / 128 | 120 m | 400 (nv 1) |
| Benelli M4 | escopeta 12 | 13 × 9 bagos | bomba | 7 / 28 | 45 m | 600 (nv 1) |
| AK-74 | fuzil 5,45 | 26 | auto 600/min | 30 / 90 | 120 m | 650 (nv 1) |
| M4 | fuzil 5,56 | 22 | auto 750/min | 30 / 90 | 120 m | 650 (nv 2) |
| M107 | precisão .50 | 110 | ferrolho | 5 / 15 | 300 m, luneta | 900 (nv 3) |

**Melhorias**:

| Melhoria | Efeito | Preço | Nível |
|---|---|---|---|
| Cano reforçado | +15% de dano | 350 | — |
| Carregador estendido | +40% no pente | 300 | — |
| Empunhadura vertical | −30% de recuo | 300 | — |
| Compensador | −20% de recuo, −10% de dispersão | 250 | — |
| Silenciador | furtividade | 400 | 3 |
| Mira holográfica | mais zoom | 250 | 2 |
| Recarga rápida | recarga 35% mais rápida | 300 | 2 |

**Impactos por material**: concreto, metal, madeira, terra, grama e água, cada um com partículas, furo e som. Os sons de madeira, terra e água são **sintetizados e provisórios**.

---

## 9. Itens, mochila, fabricação e loja

### 9.1 Onde ficam as coisas

| Lugar | Capacidade | O que guarda |
|---|---|---|
| Barra rápida | 6 tipos | kit médico, granada, molotov, sinalizador, barricada, distração |
| Armas | 2 | em mãos + nas costas |
| Colete | até 60 | proteção |
| **Mochila** | 18 espaços com pilhas | materiais, bandagens, reforços e o excedente da barra |
| Itens de missão | ilimitado | fusível, cartão, caixa-preta… (nunca bloqueiam a história) |

### 9.2 Materiais e receitas

- **8 materiais**: sucata, tecido, fiação (comuns); material médico, eletrônicos, peças mecânicas, químicos (incomuns); peças táticas (raras).
- **Onde achar**: baús e 12 pontos fixos no mapa.

| Receita | Materiais | Onde |
|---|---|---|
| Bandagem (+20 de vida) | 2 tecido | na mochila |
| Kit médico improvisado | 2 material médico + 1 tecido | bancada |
| Distração | 1 eletrônico + 1 sucata + 1 fiação | bancada |
| Sinalizador | 1 químico + 1 sucata | bancada |
| Kit de barricada | 2 sucata + 1 peça mecânica | bancada |
| Reforço de colete (+30) | 2 sucata + 1 peça tática + 1 tecido | bancada |

A fabricação é atômica (um duplo clique não duplica). A bancada fica no **Depósito de suprimentos**; o visual dela é **provisório**, feito de primitivas.

O item "Pacote de suprimentos" existe, mas **ainda não tem receita nem fonte no mapa**.

### 9.3 Loja e economia

- **25 entradas em 5 abas**:
  - armas;
  - munição;
  - proteção (colete, atendimento);
  - consumíveis;
  - melhorias (inclui estamina e a bolsa tática).
- **Créditos**: vêm de abates, ondas, missões e depósitos.
- **Loot no mapa**: baús, munição, kits, armas no chão.
- **Feed de coleta**: ícone, nome e quantidade.

---

## 10. Inimigos

| Inimigo | Detalhes |
|---|---|
| Zumbi comum | NavMesh, perambula, persegue, ataca |
| Corredor | 60% de vida, ×3,6 de velocidade |
| Tanque | ×4,5 de vida, ×2,6 de dano, quebra portas e barricadas |
| Gritador | grita e chama outros |
| Rastejante | embosca em cantos escuros |
| Soldados (4 visuais) | patrulham, investigam, atiram em rajadas, se reposicionam; lutam contra você **e contra os infectados** |
| Experimento 07 (chefe, cap. 5) | 1500 de vida, investida telegrafada, fica atordoado se bater em algo |
| Aurora (chefe final) | 2600 de vida, 2 fases, onda de choque, invoca corredores, fraca à luz |

**Sistemas compartilhados**:
- percepção por visão e audição, com estados de suspeita, investigação e perseguição;
- reações direcionais e cambaleio;
- ragdoll com limite;
- dano extra na cabeça;
- portões de spawn bloqueáveis por missão.

---

## 11. Mundo, terror e exploração

| Sistema | O que faz |
|---|---|
| Mapa | base militar: portões, hangares, torres, arsenal, laboratório, heliponto, depósito com bancada |
| Clima | chuva, raios, vento, neblina, céu noturno |
| Energia | setores com apagão, luzes de emergência, geradores que exigem fusível |
| Portas e barricadas | trancar com cartão; inimigos arrombam; construir e reforçar |
| Fogo, granadas, sinalizador, distração | física e área de efeito |
| Eventos de terror | 12 tipos (portas batendo, passos, gemidos, silhuetas, lâmpadas…) |
| Diretor de tensão | alterna sustos e calmaria |
| **Minimapa** | redondo, névoa de exploração (salva entre sessões), seta, norte, locais, objetivo com distância |
| **Mapa tático (M)** | legenda, % explorado, marcas por clique |

---

## 12. Interface

- **HUD de combate**: vida, vidas, colete, arma com ícone e munição, pontos/abates/créditos, mira dinâmica, marcadores de acerto, setas de ameaça, legendas de sons.
- **HUD de campanha**: cartão da missão, barra rápida com ícones, feed de coleta, avisos, conquistas, intervalo, leitor de documentos.
- **Telas**: loja, mochila e fabricação, mapa tático, opções, lobby multijogador.
- **Estilo**: toda a UI é gerada por código (`UiKit`), sem prefabs. As telas de mochila e mapa deixam o tempo em câmera lenta (0,25), exceto no multiplayer.

---

## 13. Multiplayer cooperativo (Fase 6 — MVP)

### 13.1 Como funciona

- **Tecnologia**: Netcode for GameObjects + Unity Transport, com conexão **direta por IP:porta** (UDP 7777, configurável). Não há servidor pago, Relay nem VPS.
- **Para jogar pela internet**: Radmin VPN ou Hamachi. O guia está em `FASE6_RADMIN_HAMACHI.md`.
- **Topologia Host-Client**: quem cria a sala é o Host.
  - O Host roda a IA, as ondas e valida o dano.
  - Cada jogador controla o próprio personagem e o publica a 20 Hz.
  - Os outros veem um **boneco com o modelo real**: interpolado, com animação, arma, lanterna, ragdoll e nome.
- **Fluxo de entrada**: menu MULTIJOGADOR → Criar sala (nome, dificuldade, 2–4 vagas, porta, senha) ou Entrar (IP, porta, senha).
- **Lobby**:
  - vagas com estado de pronto, traje e ping;
  - REMOVER, para o host expulsar alguém;
  - os IPs do host, identificados como LAN, Radmin ou Hamachi;
  - INICIAR PARTIDA.
- **Recusas com motivo**: versão diferente, sala cheia, partida já iniciada, senha incorreta.
- **Segurança**:
  - a identidade é o ID dado pela camada de rede;
  - o dano pedido pelo cliente é validado (distância, taxa e valor máximo);
  - nenhuma mensagem de rede executa código;
  - a senha trafega sem criptografia (para salas entre amigos).

### 13.2 O que foi testado de verdade (editor + build, mesmo PC)

- Lobby, início e spawn de 2 personagens reais.
- Zumbis replicados no cliente; o cliente matou zumbis com dano validado pelo host e recebeu os abates e os pontos.
- Zumbi do host feriu o cliente (100 → 82).
- Cliente caiu → o host o removeu depois de ~30 s e seguiu jogando.
- Voltar ao menu e hospedar de novo.
- Recusa por senha.
- Build nova: aviso de entrada com o nome, contador de abates do cliente, boneco sem avisos.

Os detalhes estão em `FASE6_TESTES.md`.

### 13.3 O que **ainda não** é sincronizado

- **Inimigos**: soldados, chefes e o comportamento do Gritador (só zumbis comuns são replicados).
- **Mundo**: portas, energia, geradores, barricadas, baús, itens no chão, materiais.
- **Economia**: munição, créditos e loja continuam locais, sem validação do host.
- **Partida**: campanha e missões (o multiplayer só tem o Infinito), game over cooperativo, queda e revive.
- **Interface**: HUD de ondas e intervalo no cliente, aliados no minimapa.
- **Conexão**: desconexão detectada só depois de ~30 s, sem reconexão.

---

## 14. Progressão e salvamento

- **Nível**: até 30. Libera armas na loja e peças de traje.
- **8 conquistas**: Ainda de pé, Cem a menos, Intocável, Fim do Experimento, Protocolo Aurora, Só o básico, Amanhecer, Arquivista.
- **Estatísticas**: abates, ondas, missões, melhor onda, capítulo salvo.
- **Salvamento**: o jogo salva **por capítulo**. Não há salvamento no meio do capítulo nem vários perfis.

---

## 15. Áudio

- 225 arquivos: armas, terror, campanha, música (Ethereal, Exciting, Suspenseful) e ambiente.
- Volumes em 6 grupos por código, com ducking durante as falas.
- **Sem AudioMixer** (configuração manual opcional).
- **Sem vozes**: as 54 falas só têm legenda. O manifesto `FASE5_DIALOGOS_MANIFESTO.csv` lista id, personagem, texto, arquivo esperado e estado. Quando os `.wav` existirem, a religação é automática (*Phase 5 ▸ 4*).

---

## 16. Desempenho e ferramentas

### 16.1 Desempenho (só no editor)

| Situação | Tempo de quadro |
|---|---|
| Onda de 30–34 inimigos | ~27–28 ms (~36 FPS) |
| Pátio sem inimigos, minimapa ligado ou desligado | 28,7 ms (sem diferença) |

- **Hardware**: Ryzen 7 5700U com GPU integrada, Game View em 1440p.
- O tempo **quase não muda com o número de inimigos**. O gargalo provável é a renderização: mais de 40 sombras de luzes pontuais disputam o atlas.
- **Não há medição em build com o Profiler.** Por isso não posso afirmar que o jogo está otimizado.

### 16.2 Ferramentas de editor (*Tools ▸ Combat*)

- **Setups das fases anteriores**: mapa, menu, noite, terror, campanha.
- ***Phase 3* ▸ 1–7**: lanterna, ícones, armas, narrativa, reações, Animator 2D (precisa dos clipes) e impactos.
- ***Phase 5* ▸ 1–4**: itens e receitas, gerar o mapa, montar a cena, manifesto de falas.
- ***Phase 6* ▸ 1–2**: montar a rede, build Windows de teste.
- **Play From Game Scene**.

Fora da Unity: `Tools/IconGen/make_icons.py`, `Tools/AudioGen/make_impacts.py`, `Tools/add_input_actions.py`.

**Teste automático do multiplayer**: `BaseMilitar.exe -net-join IP:porta -net-name Nome -net-log arquivo.txt -net-hit` conecta, fica pronto, registra o estado a cada segundo e tira capturas.

---

## 17. O que está pendente — lista completa

### 17.1 Bloqueado esperando algo **seu** (eu não posso fazer sem isso)

| # | O que falta | Por que depende de você | Destrava |
|---|---|---|---|
| A | **Clipes de animação Mixamo** (~12 de prioridade alta: strafe e andar de ré em 4 direções, partida/parada, agachar andando, prone, pulo duplo, reação a dano) | exige a sua conta Adobe; eu não crio contas nem baixo arquivos sem você | locomoção 2D, prone, inimigos novos com movimento próprio |
| B | **Vozes das 54 falas** | exige escolher e autorizar um serviço de TTS (com chave sua) ou gravar; eu não envio dados a serviços externos sem autorização e não clono vozes reais | dublagem completa; o lote 1 tem 9 falas da missão 1 |
| C | **Pacote de props** gratuito (bancada, armários, prateleiras, camas, macas, mesas, monitores) | escolher o pacote e verificar a licença (CC0 / Asset Store) | bancada definitiva; enfermaria, oficina, alojamentos, sala de controle |
| D | **Teste em 2 PCs** (LAN e depois Radmin VPN ou Hamachi) | só há um computador nesta sessão | validar o multiplayer de verdade, medir ping real e testar o firewall |
| E | **Teste com 3 e 4 jogadores** | precisa de mais máquinas ou pessoas | sala cheia, escala de inimigos, desempenho |
| F | **Confirmar Tab, M e E no teclado físico, e o gamepad** | as teclas simuladas não chegam ao jogo nesta sessão | fechar os testes da Fase 5 |
| G | **Aprovar o merge** de `fase6-multiplayer` na `main` | decisão sua | a `main` passa a ter tudo |
| H | (opcional) **Criar o AudioMixer** | configuração manual no editor | mixagem por grupos |

### 17.2 Trabalho que eu posso fazer **agora**, sem nada seu

**Multiplayer (Fase 6, próximos marcos)**

1. Reduzir o timeout de desconexão (de 30 s para ~10 s) e mostrar "reconectando / jogador caiu".
2. HUD de ondas e intervalo no cliente; aliados no minimapa e no mapa tático.
3. Replicar soldados, chefes e o comportamento do Gritador.
4. Sincronizar o mundo: portas, energia, geradores, barricadas, baús e itens (com IDs estáveis e sem loot duplicado).
5. Economia validada pelo host: munição, créditos, loja, fabricação.
6. Queda, revive e game over cooperativo (todos caídos = fim; espectador).
7. Campanha cooperativa: missões e objetivos sincronizados.
8. Chat de texto ou sinalização (ping no mapa) entre jogadores.

**Mundo e conteúdo (Fase 5, marcos 3–5)**

9. Depósito, marco 3/4: contêiner-sala, armário com código achado num documento, terminal que revela o mapa, missão "Oficina isolada" que desbloqueia receita.
10. Fonte e receita para o "Pacote de suprimentos".
11. Novas armas com os 10 modelos sem uso do Low Poly Weapons.
12. Diário (J), armazenamento em caixa, áreas seguras, eventos dinâmicos novos.
13. Salvamento no meio do capítulo.
14. Iluminação por setor (passe de luz da Fase 5).
15. Remapeamento de teclas.

**Qualidade e técnico**

16. **Build + Profiler** numa onda pesada (CPU × GPU) e, se preciso, cortar sombras de postes distantes ou passar para luz baked. É **pré-requisito** antes de aumentar o mapa.
17. Remover o AI Inference (Sentis) e as sobras do template, para reduzir a build e os avisos.
18. Instalar o Unity Test Framework e criar testes automáticos (mochila, fabricação, dano, aprovação de conexão).
19. Localização em inglês (pacote Localization; as chaves `dlg.*` já existem).

### 17.3 Itens provisórios que precisam de troca

- Bancada de trabalho (primitivas) → modelo real (depende de C).
- Visual dos materiais no chão (caixinha com ícone).
- Sons de impacto em madeira, terra e água (sintetizados).
- Sons das telas de inventário (reaproveitados).

### 17.4 Problemas conhecidos e limitações

- O personagem de cada jogador é decidido pelo próprio dono. O host só registra velocidades impossíveis, sem corrigir; é aceitável para cooperativo entre amigos.
- A senha da sala trafega sem criptografia.
- O ping mostrado no lobby, testado no editor a poucos quadros por segundo, saiu alto (119–369 ms). Falta medir numa build entre dois PCs.
- **Não foram testados em Play**:
  - baús soltando materiais;
  - persistência da névoa ao reabrir o jogo;
  - clique para marcar no mapa tático;
  - um capítulo de campanha com as telas novas;
  - uma regressão completa do single-player depois da Fase 6.
- Os `NullReferenceException` do `PickupFeedView` e do `WeaponHolder` no console aparecem **só quando o código é recompilado durante o Play**. É um artefato do editor, não do jogo.

---

## 18. O que me mandar — checklist

Copie e responda o que puder:

- [ ] **Teste em 2 PCs**: rode a build `Builds/Win64` nos dois, siga `FASE6_RADMIN_HAMACHI.md` e me mande o que aconteceu (conectou? ping? travou?) e, se der, o `Player.log` de cada máquina.
- [ ] **Teclado e controle**: Tab, M e E funcionam? O gamepad navega nos menus?
- [ ] **Clipes Mixamo**: baixe a lista de `FASE3_ASSETS.md` §3.1 (FBX, *Without Skin*, 30 fps) e coloque em `Assets/SourceFiles/Animation/Mixamo/`.
- [ ] **Vozes**: diga qual opção prefere:
  - (a) um TTS que você autoriza, com a chave configurada por você;
  - (b) gravar as falas;
  - (c) deixar só com legendas por enquanto.
- [ ] **Props**: escolha um pacote gratuito (ou peça que eu sugira opções com a licença verificada).
- [ ] **Merge na `main`**: sim ou não.
- [ ] **Prioridade**: qual bloco da seção 17.2 você quer primeiro.

---

## 19. Ordem sugerida

| Ordem | Bloco | Por quê |
|---|---|---|
| 1 | Teste em 2 PCs (você) + timeout e HUD de ondas no cliente (eu) | valida a base do multiplayer antes de construir em cima dela |
| 2 | Build + Profiler e limpeza (Sentis, template) | sem medir, qualquer expansão de mapa ou de jogadores é às cegas |
| 3 | Revive + game over cooperativo + soldados e chefes replicados | é o que torna o cooperativo jogável de ponta a ponta |
| 4 | Mundo sincronizado + economia validada | evita loot duplicado e estados diferentes entre jogadores |
| 5 | Clipes Mixamo (você) → locomoção 2D e inimigos novos | o maior salto visual restante |
| 6 | Vozes (lote da missão 1 primeiro) | imersão |
| 7 | Props → expansão da base (enfermaria, oficina, alojamentos) | conteúdo novo, depois de medir o desempenho |
| 8 | Campanha cooperativa, localização, testes automáticos | acabamento |
