# Base Militar: Sobreviva à Noite — Relatório completo, pendências e decisões

Estado em **25/10/2026, fim do dia**. Branch `fase7-definitive`, commit `4627dd1`.

O levantamento foi feito direto do projeto: código, assets, builds, documentos das fases e medições registradas. Este documento substitui a versão anterior (commit `debc258`, que continua no histórico do Git).

**Como ler:**
- **Seção 0**: resumo de uma página para decidir rápido.
- **Seção 1**: o que foi feito nas **duas últimas rodadas** de pedidos, em detalhe.
- **Seção 2**: tudo o que a Fase 7 fez desde o relatório anterior.
- **Seções 3–17**: como o jogo está hoje, sistema por sistema.
- **Seções 18–21**: pendências, melhorias, o que preciso de você e as decisões a tomar.

Estados usados: ✅ concluído e validado · 🧪 implementado, aguardando teste · 🟡 parcial · ⛔ não iniciado / bloqueado.

---

## 0. Resumo executivo

| Pergunta | Resposta curta |
|---|---|
| O jogo funciona de ponta a ponta? | **Sim**, no single-player: campanha de 10 capítulos, Modo Infinito, 4 dificuldades. Build release Windows de 416 MB, sem erros |
| O desempenho está bom? | **Sim, na média**: 10,5–10,7 ms por quadro (~95 FPS) no Médio 1080p com 30 infectados, neste notebook com GPU integrada. **Problema aberto**: o **primeiro tiro** da partida trava (55 ms na release, mais 65 ms no quadro seguinte) |
| O multiplayer está pronto? | **Quase**, no Modo Infinito cooperativo: quase tudo sincronizado e validado pelo host, **testado com até 3 jogadores no mesmo PC**. **Nunca foi testado entre dois computadores**, e sem isso não pode ser dado como completo |
| O que mais falta? | Vozes, animações novas, inimigos novos, expansão do mapa, campanha cooperativa e testes automáticos. A maioria **depende de algo seu**: clipes Mixamo, decisão sobre as vozes, pacote de props e um segundo PC |
| Há bug grave conhecido? | Nenhum que quebre o jogo (P0). O mais sério é a trava do primeiro tiro (P1) |
| Decisões suas que destravam mais coisas | 1) fazer o **teste em dois PCs**; 2) **vozes**: TTS autorizado, gravação ou só legendas; 3) baixar os **clipes Mixamo**; 4) escolher um **pacote de props**; 5) aprovar o **merge na `main`** |

---

## 1. As duas últimas rodadas, em detalhe

### 1.1 Rodada A — "marcadores finos no ZombieAI e depois o AudioMixer do Marco 4" (commit `b287d9c`)

**a) Pico residual: investigação no `ZombieAI`**
- O `Update` do `ZombieAI` foi dividido em 6 partes medidas: gemido, movimento, porta, golpe, acerto e animação.
- **Resultado em 20 execuções**: o `ZombieAI` **não** era a causa dos picos daquela série. Ele custa 0,23 ms por quadro no total.
- **Hipótese testada e descartada: compilação JIT do Mono** (o código compilado na primeira vez que roda).
  - Escrevi um aquecimento que compilava os 2.393 métodos do jogo numa thread de fundo, ao abrir o jogo.
  - A primeira versão não compilava nada: o `PrepareMethod` não tem efeito no Mono da Unity. A segunda compilava de verdade, em ~0,9 s.
  - A/B com 6 pares: **nenhum efeito**. O código foi **removido**.

**b) AudioMixer (Marco 4, item 9.4 do seu pedido)** ✅ no editor
- **Arquivo**: `Assets/SourceFiles/Resources/Audio/GameMixer.mixer`, criado pelo menu *Tools ▸ Combat ▸ Phase 7 ▸ 6*.
- **Grupos**: Master ▸ Music, SFX, Dialogue, Radio, Ambient, UI. Cada volume fica exposto como `Vol_<grupo>`.
- **Roteamento** (`AudioRouting.cs`): cada som vai para o seu grupo. Qualquer fonte da cena que nenhum sistema roteie vai para SFX.
- **Volumes das opções preservados**: as 6 barras de volume continuam funcionando como antes, e os grupos começam em 0 dB.
- **Ducking durante falas**:
  - a música cai para 35%, como antes;
  - o **ambiente cai −8 dB**;
  - os **efeitos não caem**, para os passos e ataques dos infectados continuarem audíveis.
- **Testado no editor**: 92 fontes, todas com grupo. Durante uma fala de rádio: ambiente −8 dB, efeitos 0 dB, música 35%, voz no grupo Rádio. Depois da fala tudo volta.
- **Release**: sem perda de desempenho (10,54 ms contra 10,24 antes, dentro da variação).
- **Falta**: você ouvir em jogo se as ameaças continuam audíveis durante o rádio.

### 1.2 Rodada B — "marque os Update restantes e repita a série de 20 execuções" (commit `4627dd1`)

**a) Medição completa**
- **53 marcadores novos**: todos os `Update`, `LateUpdate` e `FixedUpdate` do jogo que faltavam (51 arquivos), incluindo o controlador do jogador.
- **Partes medidas separadamente**: arma, tiro, controlador, golpe do infectado e o `Play()` de áudio.
- **A sonda de desempenho ganhou**:
  - registro de espera por *jobs*;
  - hora do relógio em cada pico;
  - registro da CPU do editor durante as séries;
  - o modo **`-perf-fire`**, em que o jogador atira a cada 1,5 s, para tornar o teste repetível.
- **5 séries de 20 execuções** (100 no total) e mais 18 execuções com tiro.

**b) A própria sonda criava parte dos picos** ✅ corrigido
- Ao anotar um pico, ela montava o texto do relatório no mesmo quadro (5 ms; 30 ms quando passou a ler a hora).
- Isso fazia o quadro seguinte também parecer um pico. Agora ela guarda só os números e escreve no fim.

**c) O que era o "pico raro"** ✅ causa encontrada
- É o **primeiro tiro da partida**: em **10 de 10 execuções** com `-perf-fire`, ele custou **42–64 ms**. Os 12 tiros seguintes custaram ~0,02 ms cada.
- Parecia raro porque a sonda não atirava: os tiros nas séries vinham de cliques do mouse na janela do jogo.
- Também apareceu o **primeiro golpe de um infectado**: 14 ms dentro do `Animator` (`SetTrigger`/`speed`).
- **Descartados com medição**:
  - áudio: o `Play()` custa 0,01 ms;
  - GC: o marcador de coleta nunca aparece num pico;
  - atividade do editor: 0% de CPU durante a série;
  - JIT (rodada A).

**d) Correção comprovada**
- No primeiro impacto de bala, o jogo carregava do disco a biblioteca de impactos e os sons ligados a ela: **10,5–11,7 ms** em todas as medições.
- Agora ela carrega junto com a cena (`CombatEffects.Awake`).
- **Antes → depois** (desenvolvimento):

| Medida | Antes | Depois |
|---|---|---|
| Impacto | 16–19 ms | 5–6 ms |
| Disparo completo | 39–54 ms | 20–28 ms |
| Quadro do primeiro tiro | 42–64 ms | 30–36 ms |

- **Média da release** sem piora: 10,68 ms parado, 10,47 ms atirando.

**e) O que ficou em aberto**
- **Release: o primeiro tiro ainda custa 55 ms**, e o quadro seguinte 65 ms. Não há medição "antes" da release com tiro para comparar.
- O resto parece ser:
  - a **criação de primeira vez** dos efeitos (traçante, clarão, cápsula, marca de bala, partículas);
  - o **primeiro desenho** dos materiais deles.
  - **Hipóteses ainda não medidas peça por peça.**
- **Próximo passo proposto**: aquecer esses efeitos na carga da cena e medir de novo.

---

## 2. Tudo o que a Fase 7 fez desde o relatório anterior

A Fase 7 ("Definitive Edition", Marcos 1 a 10) começou do checkpoint `fase7-checkpoint` (`debc258`), na branch `fase7-definitive`. São 7 commits:

| Commit | O que entrou |
|---|---|
| `e4a0db6` | sonda de desempenho em build (`PerfProbe`); luzes duplicadas dos postes removidas e orçamento de sombras (`LampShadowBudget`) |
| `bc4b888` | níveis de qualidade reais (Baixo/Médio/Alto) no Windows; ondas e intervalo no cliente cooperativo; timeout de desconexão de 10 s; documentação `Docs/FASE7` |
| `d8ba96c` | limpeza da build (−35 MB); picos de quadro (áudio pré-carregado, silhueta reaproveitada, fonte aquecida); HUD de equipe, queda, reanimação, espectador e fim de jogo cooperativo |
| `8bf7b50` | soldados, chefes, Gritador e Tanque em rede; itens, portas, baús, barricadas e energia pelo host; créditos e compras validados pelo host |
| `11da423` | fabricação validada pelo host; bug de entrega dupla (corrida no mesmo item) achado e corrigido; balanceamento cooperativo por onda |
| `b287d9c` | rodada A (seção 1.1) |
| `4627dd1` | rodada B (seção 1.2) |

### 2.1 Estado de cada Marco da Fase 7

| Marco | Estado | Resumo |
|---|---|---|
| 1 — Estabilidade e otimização | 🟡 quase | medido em build, gargalos corrigidos (luzes, qualidade, áudio, silhueta, fonte, carga de impactos), limpeza feita. **Falta**: primeiro tiro na release, primeiro golpe, CPU com muitos inimigos |
| 2 — Multiplayer cooperativo completo (Infinito) | 🟡 quase | tudo implementado e validado **no mesmo PC** (até 3 jogadores). **Falta**: teste em dois PCs (seu); munição continua local |
| 3 — Reforma das animações | ⛔ | depende dos **clipes Mixamo** |
| 4 — Narrativa, dublagem e áudio | 🟡 | AudioMixer e ducking ✅. **Vozes** ⛔ (decisão sua); prólogo e áudio de gameplay não refeitos |
| 5 — Expansão do mundo | ⛔ | depende de um **pacote de props** |
| 6 — Inimigos e game design | ⛔ | os 4 inimigos novos dependem de **clipes próprios** |
| 7 — Campanha cooperativa | ⛔ | não iniciado (o cooperativo só tem o Infinito) |
| 8 — UI, acessibilidade, experiência | 🟡 | HUD de equipe e qualidade ✅. **Falta**: conferir teclas físicas e gamepad (seu), tela de opções, remapeamento |
| 9 — Testes automatizados | ⛔ | Unity Test Framework não instalado; há testes automáticos próprios de rede e desempenho (seção 16) |
| 10 — Distribuição | 🟡 | nome do produto trocado, README, GDD e zip de entrega prontos (10/11); versão na tela e licenças não tratados |

---

## 3. Visão técnica

| Item | Valor |
|---|---|
| Gênero | tiro em terceira pessoa + sobrevivência/terror, numa base militar infestada à noite |
| Motor | Unity **6000.6.7f1** (Unity 6.6), URP 17.6 (Forward+, HDR) |
| Plataforma | Windows (builds de teste). Mac e Linux não testados |
| Cena | `Assets/Scenes/MilitaryBase_Combat.unity`: mapa, sistemas e menus numa cena só. A `GetStarted_Scene` do template está **desligada** da build |
| Modos | Campanha (10 capítulos), Modo Infinito e Multijogador cooperativo (Infinito, 2–4 vagas) |
| Dificuldades | Fácil, Normal, Difícil, Pesadelo |
| Código | **127 scripts próprios** (namespace `Combat`), **~32.700 linhas**. Com Starter Assets e o template: 133 scripts, ~34.400 linhas |
| Idioma | português (sem pacote de localização) |
| Salvamento | `PlayerPrefs`: progresso, recordes, traje, opções, névoa do mapa, dados do multiplayer |
| Versão | `4.0.1` (não aparece na tela) |
| Nome do produto | **Base Militar - Sobreviva à Noite**, empresa "Cesar Otavio" (trocados em 10/11; antes eram os do template) |

### 3.1 Builds

| Build | Menu | Pasta | Tamanho |
|---|---|---|---|
| Release | *Phase 7 ▸ 2* | `Builds/Win64` | **416 MB** (era 451) |
| Desenvolvimento (com Profiler) | *Phase 7 ▸ 1* | `Builds/Win64Dev` | 493 MB |

As duas foram refeitas hoje com todas as mudanças e ficam fora do Git.

### 3.2 Branches

| Branch / tag | Conteúdo |
|---|---|
| `main` | checkpoint **antes da Fase 3** (muito desatualizada) |
| `fase3-imersao`, `fase5-mundo`, `fase6-multiplayer` | fases anteriores |
| tags `fase5-checkpoint`, `fase6-checkpoint`, `fase7-checkpoint` | pontos de volta |
| **`fase7-definitive`** | **tudo**: Fases 3, 5, 6 e 7. É o estado atual |

Não há "Fase 4" no histórico: os pedidos foram da Fase 3 direto para a 5.

### 3.3 Código por pasta (`Assets/SourceFiles/Scripts/Combat/`)

| Pasta | Scripts | O que tem |
|---|---|---|
| (raiz) | 23 | jogador, armas, vida, inimigos, efeitos, HUD, fluxo (`GameFlow`), roteamento de áudio |
| `Campaign/` | 29 | campanha, missões, chefes, loja, inventário, progressão, Infinito, UI |
| `Horror/` | 20 | percepção, ruído, energia, portas, eventos, clima, passos, opções |
| `Network/` | 13 | sessão, lobby, jogadores, bonecos, réplicas, mundo (`NetWorld`), HUD de equipe, balanceamento |
| `Editor/` | 13 | ferramentas *Tools ▸ Combat*, limpeza da build |
| `Exploration/` | 6 | minimapa, mapa tático, névoa, marcadores |
| `Inventory/`, `Crafting/`, `Items/` | 13 | mochila, itens no mundo, receitas, bancada, catálogo |
| `Narrative/` | 4 | falas, diretor, legendas, rádio |
| `Animation/`, `Lighting/`, `Effects/`, `Diagnostics/` | 6 | inclinação, reações, lanterna, orçamento de sombras, impactos, sonda |

---

## 4. Bibliotecas e assets

### 4.1 Pacotes Unity

| Pacote | Versão | Uso |
|---|---|---|
| Universal Render Pipeline | 17.6.0 | renderização, pós-processamento, 3 perfis de qualidade |
| Input System | 1.20.1 | todos os controles |
| Cinemachine | 6.6.0 | câmera em terceira pessoa, espectador |
| uGUI + TextMesh Pro | 2.6.0 | toda a UI, construída por código |
| Netcode for GameObjects | 2.13.3 | multiplayer |
| Unity Transport | 6.6.0 | UDP direto por IP:porta |
| AI Assistant | 2.21.0-pre.1 | ferramenta de editor (a ponte que uso para operar a Unity); não vai para o jogo |
| AI Inference (Sentis) | 2.6.1 | **sem uso no jogo**. Continua instalado (o AI Assistant pode depender dele), mas os shaders dele **não entram mais na build** |
| IET Framework (Learn) | 6.0.7 | tutoriais do template, sem uso |
| Rider / Visual Studio | 3.1.0 / 2.0.27 | IDE |

**Não instalados** (úteis no futuro): Unity Test Framework, Localization, Animation Rigging, Timeline, Memory Profiler. **IL2CPP** também não está instalado (só Mono).

### 4.2 Pacotes de terceiros

| Pacote | Uso |
|---|---|
| Tiny Teacup Studio — Military Base Pack | o mapa |
| Survivalist | jogador, trajes, bonecos do multiplayer |
| Low Poly Weapons VOL.1 | as 6 armas (**10 modelos sem uso**) |
| Kevin Iglesias — Zombie Animations | animações dos zumbis |
| Starter Assets (Third Person) | base do movimento |
| Template "Get Started" | sobras. As pegadas do jogador usam 11 sons dele, por isso não foi apagado |

**Licenças para redistribuição: ainda não conferidas** (Marco 10).

### 4.3 Conteúdo produzido no projeto

- 37 ícones de itens e 6 de armas.
- 30 itens, 6 receitas e 54 falas.
- 225 arquivos de áudio (armas, terror e campanha sintetizados, música e ambiente).
- Mapa da base gerado dos colisores.
- O mixer de áudio.

---

## 5. Como se joga

### 5.1 Controles (teclado e mouse)

| Tecla | Ação |
|---|---|
| WASD / Shift | andar / correr (gasta estamina) |
| Espaço | pular (pulo duplo) |
| C | agachar. Segurar = deitar (desligado até haver clipes) |
| Ctrl | rolar |
| Botão esquerdo / direito | atirar / mirar |
| Q / E mirando | inclinar |
| Q (sem mirar) | trocar de arma |
| V / B | coronhada ou faca / empurrar |
| T | trocar o ombro da câmera |
| R | recarregar |
| E | usar (armas, baús, portas, geradores, painéis, documentos, bancada, materiais). **No cooperativo, segurar E reanima** |
| G | largar arma |
| F | lanterna |
| 1–6, Z / X | escolher / usar item rápido |
| Tab | mochila e fabricação |
| M | mapa tático |
| L / N / K | loja / próxima onda (só o host no cooperativo) / abandonar missão opcional |
| Esc | pausa |

O gamepad tem mapeamento completo. **Tab, M, E e o gamepad nunca foram confirmados num teclado e controle físicos**: as teclas simuladas não chegam ao jogo nesta sessão.

### 5.2 Menus

- **Menu principal**: Campanha, Modo Infinito, Dificuldade, **Multijogador**, Personagem (traje), Progresso, Controles, Opções, Sair.
- **Opções em 3 páginas**:
  - imagem e conforto;
  - acessibilidade (legendas, setas, marcador, tamanho de legenda);
  - volumes (6 barras, agora passando pelo mixer);
  - minimapa;
  - **gráficos**: qualidade **Baixo / Médio / Alto** reais, resolução, janela, FOV, sombras 30–90 m, efeitos, limite de FPS, VSync, pós-processamento.
- A tela de opções com os nomes novos de qualidade **não foi conferida visualmente**.

### 5.3 Dificuldades

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

No cooperativo, o balanceamento por jogador (seção 13.4) se soma a isso.

---

## 6. Campanha — "Protocolo Aurora" (sem mudanças na Fase 7)

- Prólogo de ~45 s (Espaço pula); o jogador começa desarmado.
- Cada capítulo: missões → uma missão dispara a onda → intervalo com loja.

| # | Capítulo | Destaque | Onda |
|---|---|---|---|
| 1 | Primeiro contato | pegar arma, investigar, sobreviver | 5 zumbis |
| 2 | Sem energia | fusível, gerador | 7 Z |
| 3 | Mensagem desconhecida | sinal perdido + lateral | 8 Z + 1 soldado |
| 4 | Arsenal | cartão, **escolha** entre duas missões | 9 Z + 2 S |
| 5 | Experimento 07 | **chefe E-07** | 6 Z + chefe |
| 6 | Comunicação comprometida | torre + lateral | 10 Z + 2 S |
| 7 | Laboratório | caixa-preta + lateral | 10 Z + 3 S |
| 8 | Falha de contenção | válvulas em sequência | 12 Z + 2 S |
| 9 | Protocolo de evacuação | códigos, rádio, heliponto | 13 Z + 3 S |
| 10 | Sobreviva à noite | **chefe Aurora**, defender até o pouso | 14 Z + 2 S + chefe |

- **Missões**: 14 tipos de objetivo, bônus opcionais, marcadores, "pular depois de X s" para nunca travar.
- **Narrativa**:
  - 54 falas com prioridade e validade, legendas com nome, efeito de rádio;
  - 7 personagens (Duarte, Piloto, Valença, Nogueira, Moraes, Arantes, Siqueira);
  - 9 documentos colecionáveis e cinemáticas com letterbox.
- **Sem vozes**: as falas só têm legenda.
- **Não testado depois da Fase 7**: um capítulo completo de campanha. As mudanças da Fase 7 foram quase todas no cooperativo e em desempenho; o single-player foi conferido pela sonda (onda do Infinito).

---

## 7. Modo Infinito

- Ondas sem fim, dificuldade crescente, loja no intervalo, recorde salvo.
- Missões laterais (só no host, no cooperativo).
- É o modo do multiplayer.

---

## 8. Jogador

| Sistema | O que faz |
|---|---|
| Movimento | andar, correr com estamina, pulo duplo, rolagem, agachar com IK |
| Escalada | bordas, pendurar, subir com IK das mãos |
| Armas | pose procedural com IK, mira com zoom, recuo por molas, dispersão, recarga em etapas, ferrolho/bomba visível, cápsulas |
| Armas no corpo | 2 armas em sockets, sacar e trocar visíveis, pegar do chão abaixando |
| Inclinação, corpo a corpo | Q/E mirando; coronhada/faca e empurrão |
| Lanterna tática | no peito, com balanço e auto-íris |
| Vida | regeneração, colete, vidas; **no cooperativo: queda e reanimação em vez de vidas** |
| Ruído e passos | correr, atirar e metal atraem inimigos; som por superfície |
| Traje | 7 peças; aparece no multiplayer |
| Deitar (prone) | infraestrutura pronta, **faltam os clipes** |

---

## 9. Armas

| Arma | Classe | Dano | Cadência | Pente/Reserva | Loja |
|---|---|---|---|---|---|
| M1911 | pistola | 28 | semi | 12 / 48 | inicial |
| Uzi | SMG | 14 | ~920/min | 32 / 128 | 400 (nv 1) |
| Benelli M4 | escopeta | 13 × 9 | bomba | 7 / 28 | 600 (nv 1) |
| AK-74 | fuzil | 26 | 600/min | 30 / 90 | 650 (nv 1) |
| M4 | fuzil | 22 | 750/min | 30 / 90 | 650 (nv 2) |
| M107 | precisão .50 | 110 | ferrolho | 5 / 15 | 900 (nv 3) |

- **7 melhorias**: cano, carregador, empunhadura, compensador, silenciador, mira holográfica, recarga rápida.
- **Impactos por material** (concreto, metal, madeira, terra, grama, água). Os sons de madeira, terra e água são **provisórios**.
- **Novo na Fase 7**: a biblioteca de impactos carrega com a cena (seção 1.2).

---

## 10. Itens, mochila, fabricação e loja

- **Barra rápida**: 6 tipos (kit médico, granada, molotov, sinalizador, barricada, distração).
- **Carga**: 2 armas, colete até 60, **mochila de 18 espaços**, itens de missão sem limite.
- **Materiais**: 8 tipos, em baús e 12 pontos fixos.
- **Receitas**: 6 (bandagem, kit improvisado, distração, sinalizador, kit de barricada, reforço de colete). A bancada fica no Depósito, com **visual provisório**.
- **Loja**: 25 entradas em 5 abas. Créditos vêm de abates, ondas, missões, baús e chefes.
- **No cooperativo**:
  - créditos numa conta no host;
  - compras aprovadas pelo host;
  - **fabricação aprovada pelo host**, que mantém um livro dos materiais que cada um pegou;
  - **munição continua local**.
- **"Pacote de suprimentos"**: o item existe, mas sem receita nem fonte no mapa.

---

## 11. Inimigos

| Inimigo | Detalhes |
|---|---|
| Zumbi comum | NavMesh, perambula, persegue, ataca |
| Corredor | 60% de vida, ×3,6 de velocidade |
| Tanque | ×4,5 de vida, ×2,6 de dano, quebra portas e barricadas |
| Gritador | grita e chama outros |
| Rastejante | embosca em cantos escuros |
| Soldados (4 visuais) | patrulham, atiram em rajadas, lutam contra você e contra os infectados |
| Experimento 07 (chefe) | 1500 de vida, investida telegrafada |
| Aurora (chefe final) | 2600 de vida, 2 fases, onda de choque, fraca à luz |

- **Sistemas**: percepção por visão e audição, reações direcionais, ragdoll, dano na cabeça.
- **Novo na Fase 7**:
  - todos replicados no cooperativo;
  - os chefes perseguem o sobrevivente mais próximo, não só o host;
  - a onda de choque acerta todos no raio.
- **Os 4 inimigos novos do plano da Fase 5 não começaram** (dependem de clipes).

---

## 12. Mundo, terror e exploração

- **Base**: portões, hangares, torres, arsenal, laboratório, heliponto, depósito com bancada.
- **Clima**: chuva, raios, vento, neblina.
- **Energia por setores**: apagão, emergência, geradores.
- **Portas e barricadas**: com cartão; inimigos arrombam; o jogador constrói e reforça.
- **Terror**: 12 eventos e diretor de tensão.
- **Minimapa** com névoa salva e **mapa tático** com marcas. **Aliados aparecem no cooperativo.**
- **Iluminação**:
  - os 53 postes tinham duas luzes cada, e a duplicada (com sombra) foi desligada;
  - um orçamento limita as sombras às 1–4 lâmpadas mais próximas;
  - o apagão com essa mudança **não foi conferido em jogo**.

---

## 13. Multiplayer cooperativo (estado atual)

### 13.1 Como funciona

- **Conexão**: Netcode for GameObjects + Unity Transport, **direto por IP:porta** (UDP 7777). Sem servidor pago.
- **Pela internet**: Radmin VPN ou Hamachi (guia em `Docs/FASE6_RADMIN_HAMACHI.md`).
- **Host-Client**:
  - o **host** roda a IA, as ondas, o mundo e a economia;
  - cada jogador move o próprio personagem;
  - os outros veem um boneco com o modelo, o traje, a arma e a lanterna.
- **Lobby**: criar sala (nome, dificuldade, 2–4 vagas, porta, senha) ou entrar. Mostra pronto, traje, ping e o botão remover. Recusa com motivo (versão, sala cheia, partida iniciada, senha).
- **Segurança**:
  - identidade pelo ID da camada de rede;
  - todo pedido do cliente é validado pelo host (distância, estado, taxa, valores);
  - nenhuma mensagem executa código;
  - a senha trafega sem criptografia (salas entre amigos).

### 13.2 O que está sincronizado

| Sistema | Estado |
|---|---|
| Personagens, zumbis, dano validado pelo host | ✅ mesmo PC |
| Ondas, restantes, intervalo e recompensa no cliente | ✅ mesmo PC |
| Timeout de desconexão (~10 s) | ✅ mesmo PC (medido 7,6–12,7 s) |
| HUD de equipe: vida, distância, ping, estado; aliados no minimapa | ✅ mesmo PC, com 2 jogadores |
| Queda (2 por onda; 30 s e 20 s), reanimação de 5 s segurando E, espectador, volta na onda seguinte, fim de jogo da equipe | ✅ mesmo PC. A tecla E física e a tela de fim no cliente não foram vistas |
| Soldados (movimento, arma, tiros), chefes (barra, avisos), Gritador | ✅ mesmo PC |
| Tanque quebrando barricada com o cliente olhando | 🧪 |
| Itens no chão com ID, reserva no host; largar arma ou material | ✅ mesmo PC |
| Dois jogadores pegando o mesmo item ao mesmo tempo | ✅ (bug de entrega dupla **achado e corrigido**) |
| Portas, baús, barricadas, gerador, apagão | ✅ mesmo PC |
| Créditos e compras pelo host | ✅ mesmo PC |
| Fabricação pelo host (legítima aceita, trapaça recusada) | ✅ mesmo PC |
| Balanceamento por número de jogadores | 🧪 cálculo conferido no log; o equilíbrio real depende de jogar |
| 3 jogadores (host + 2) | ✅ conectaram e jogaram (mesmo PC) |
| 4 jogadores | ⛔ não testado |
| **Dois PCs (LAN / Radmin / Hamachi)** | ⛔ **bloqueado: precisa de um segundo computador** |

### 13.3 O que ainda não é sincronizado ou tem limitação

- **Munição** gasta continua local.
- **Missões laterais** do Infinito só existem no host; o cliente não as vê.
- Só a loja **do host** segura o fim do intervalo.
- Arma largada por cliente perde as melhorias.
- Os baús reabastecem a cada onda nas duas máquinas.
- Soldados réplica não mostram recarga.
- Os golpes especiais do chefe aparecem pelo movimento e pelos avisos, sem animação extra.
- Uma vez, na build anterior, o host caído foi registrado como morto ~1 s depois da queda. Não se repetiu; o motivo de cada morte agora vai para o log.
- Material inventado localmente fica na mochila do cliente, mas não serve para fabricar nem para largar.
- **Campanha cooperativa** não existe.

### 13.4 Balanceamento cooperativo (por jogador extra)

| Ajuste | Por jogador extra | 4 jogadores |
|---|---|---|
| Infectados na onda | +55% | ×2,65 |
| Vivos ao mesmo tempo | +35% | ×2,05 |
| Chance de especial | +4 pontos | +12% |
| Vida do chefe | +60% | ×2,80 |
| Intervalo | +5 s | +15 s |

Os valores são ajustáveis no `NetworkManager`. São um **ponto de partida** a calibrar jogando.

---

## 14. Áudio

- **225 arquivos**: armas, terror, campanha, música, ambiente.
- **AudioMixer** com 7 grupos e ducking (seção 1.1).
- **Carregamento**: 209 efeitos pré-carregados com a cena; músicas e ambientes longos em streaming.
- **Rádio**: passa-altas, passa-baixas e distorção na voz, com chiado e clique de squelch.
- **Sem vozes**: o manifesto `Docs/FASE5_DIALOGOS_MANIFESTO.csv` lista as 54 falas. Quando os `.wav` chegarem, a religação é automática.
- **Não refeitos ainda**: passos, impactos de madeira, terra e água, sons dos chefes e ambientes internos (Marco 4, item 10).

---

## 15. Desempenho (medido em build, neste PC)

**Hardware**: Ryzen 7 5700U com GPU integrada Radeon, tela 1080p. Ferramenta: a sonda `PerfProbe` dentro da build (seção 16).

### 15.1 Release atual (onda de 30 infectados)

| Qualidade | Média | Observação |
|---|---|---|
| **Médio, 1080p** | **10,5–10,7 ms** (~95 FPS) | p95 ~12,5–12,9 ms |
| **Baixo, 720p** | **~6,9–7,1 ms** (~140 FPS) | |
| Alto, 1080p, sombras 90 m | ~17,3 ms (dev; ~58 FPS) | no limite de 60 FPS |

- **Metas**:
  - 60 FPS no Médio em 1080p: ✅;
  - iGPU no Baixo em 720p: ✅;
  - GPU dedicada: **não testada**;
  - host com IA e rede: **não medido** pela sonda.

### 15.2 O que foi corrigido na Fase 7 (todos com medição antes e depois)

| Correção | Ganho medido |
|---|---|
| 53 luzes duplicadas dos postes desligadas + orçamento de sombras | −1,2 a −4,4 ms |
| Níveis de qualidade reais (antes só um nível no Windows) | 1080p com 30 infectados: 16,0 ms (dev, nível único) → 11,8 ms (release, Médio). Dev contra dev em `Docs/FASE7/01_PERFORMANCE.md` §4.3 |
| Áudio pré-carregado (antes descomprimia no primeiro som) | picos de 15–20 ms sumiram |
| Silhueta de terror reaproveitada (antes clonava um zumbi) | pico de 21,7 ms sumiu |
| Caracteres da fonte no carregamento | pico de 28 ms sumiu |
| Shaders do Sentis fora da build | −35 MB |
| Biblioteca de impactos pré-carregada | primeiro tiro 42–64 → 30–36 ms (dev) |
| Sonda sem picos falsos | medições mais confiáveis |

### 15.3 Ainda aberto

- **Primeiro tiro na release: 55 + 65 ms** (P1).
- **Primeiro golpe de cada infectado**: 14 ms no `Animator`.
- Raro: `ThirdPersonController.Update` com 13–16 ms (5 vezes em 100 execuções).
- Picos de apresentação na GPU no início das execuções.
- **CPU com muitos inimigos** (~10 ms na thread principal com 30): física dos ragdolls, percepção. Precisa melhorar antes de aumentar o número de inimigos ou o mapa.

---

## 16. Ferramentas

### 16.1 Menus *Tools ▸ Combat*

- **Fases anteriores**: mapa, menu, noite, terror, campanha; *Phase 3* (1–7), *Phase 5* (1–4), *Phase 6* (1–2).
- ***Phase 7***:
  1. build de desenvolvimento;
  2. build release;
  3. otimização das luzes dos postes;
  4. importação de áudio;
  5. mundo em rede;
  6. **AudioMixer**.

### 16.2 Testes automáticos próprios (na build)

- **Desempenho**:
  ```
  BaseMilitar.exe -perf-run nome -perf-scene yard|wave -perf-enemies 30 -perf-profile low|medium|high -perf-size 1920x1080 -perf-out pasta [-perf-fire 1.5] [-perf-discover]
  ```
  Grava média, p95, CPU e GPU, contadores, 120+ marcadores e o detalhe de cada quadro acima de 25 ms.
- **Multiplayer**:
  ```
  BaseMilitar.exe -net-join IP:porta -net-name Nome -net-log arquivo.txt
  ```
  Com um destes modos:
  - `-net-hit`: atira;
  - `-net-revive`: reanima;
  - `-net-world`: baús, portas, itens;
  - `-net-world2`: barricada, gerador, fabricação e trapaça;
  - `-net-race HH:mm:ss`: dois clientes pegam o mesmo item no mesmo instante.
- **Fora da Unity**: geradores de ícones e de áudio (`Tools/`).
- **Não há** testes unitários (Unity Test Framework).

---

## 17. Documentação

- **Fases anteriores**: `Docs/FASE3_*`, `FASE5_*`, `FASE6_*` (auditorias, entregas, testes, guias).
- **Fase 7**: `Docs/FASE7/`:
  - `00` estado inicial;
  - `01` desempenho, com todas as séries;
  - `02` multiplayer;
  - `03`–`07` animações, narrativa, mundo, inimigos e UI;
  - `08` testes;
  - `09` release;
  - `10` pendências;
  - `11` entrega por execução;
  - `medicoes/` com CSVs e capturas.
- **Desatualizado**: a tabela de revisão do `02_MULTIPLAYER.md` §1 é a do início do Marco 1. O estado atual está nas seções 5–7 daquele arquivo e neste relatório.

---

## 18. Pendências — lista completa

### 18.1 Bugs e riscos conhecidos

| Prioridade | Item | Próximo passo |
|---|---|---|
| **P1** | **Primeiro tiro trava** (release 55 ms + 65 ms no quadro seguinte) | aquecer os efeitos na carga (traçante, clarão, cápsula, marca, partículas) e medir com `-perf-fire` |
| P2 | Primeiro golpe de infectado: 14 ms no `Animator` | investigar o `SetTrigger`/`speed` na primeira vez |
| P2 | `ThirdPersonController.Update` 13–16 ms raro | já está em partes; esperar um pico cair nelas |
| P2 | Tanque quebrando barricada com o cliente olhando | testar |
| P2 | Valores do balanceamento cooperativo | calibrar jogando em equipe |
| P2 | Morte do host caído ~1 s depois da queda (vista 1 vez) | o log agora mostra o motivo, se voltar |
| P2 | Apagão depois da mudança das luzes dos postes | conferir em jogo |
| P2 | Tela de opções com Baixo/Médio/Alto | conferir visualmente |
| P3 | Aviso "Light.shadowResolution… Built-In" da lanterna | cosmético |

Nenhum P0 conhecido.

### 18.2 Bloqueado esperando algo **seu**

| # | O que falta | Por que depende de você | Destrava |
|---|---|---|---|
| A | **Teste em 2 PCs** (LAN e depois Radmin/Hamachi) | só há um computador nesta sessão | dar o multiplayer como completo; ping real; firewall |
| B | **Teste com 4 jogadores** | mais máquinas ou pessoas | sala cheia, desempenho do host |
| C | **Teclado e controle físicos**: Tab, M, E (inclusive segurar E para reanimar), Q/E mirando, Esc, gamepad | as teclas simuladas não chegam ao jogo | fechar Marco 8 |
| D | **Ouvir o rádio no meio de uma onda** | precisa de ouvido humano | validar o ducking |
| E | **Opções ▸ Gráficos**: a imagem está aceitável em Baixo, Médio e Alto? | julgamento visual | validar os níveis |
| F | **Clipes Mixamo** (lista em `Docs/FASE3_ASSETS.md` §3.1) | sua conta Adobe; eu não crio contas nem baixo sem você | Marcos 3 e 6 (animações, prone, inimigos novos) |
| G | **Vozes**: (a) TTS que você autoriza, com a chave configurada por você; (b) gravação; (c) só legendas | não envio dados a serviços externos sem autorização nem clono vozes reais | Marco 4 (o lote 1 seria a missão 1, com 9 falas) |
| H | **Pacote de props** com licença verificada | escolha e licença | Marco 5 (bancada real, enfermaria, oficina, alojamentos) |
| I | **Merge na `main`** | decisão sua | a `main` passa a ter tudo |

### 18.3 Trabalho que eu posso fazer **agora**, sem nada seu

**Qualidade e desempenho**
1. Aquecer os efeitos do primeiro tiro e medir dev e release (P1).
2. Investigar o primeiro golpe do infectado no `Animator`.
3. CPU com muitos inimigos: física dos ragdolls, percepção, IA.
4. Aquecer variantes de shader (picos de apresentação no início).
5. Instalar o Unity Test Framework e criar os primeiros testes: mochila, fabricação, dano, aprovação de conexão, reanimação, roteamento de áudio.

**Multiplayer**

6. Munição validada pelo host.
7. Missões laterais do Infinito visíveis no cliente.
8. Loja de qualquer jogador segurando o intervalo (ou um "pronto" de todos).
9. Arma largada pelo cliente mantendo as melhorias.
10. Ping no mapa ou chat rápido entre jogadores.
11. Campanha cooperativa (Marco 7): missões e objetivos sincronizados. É o maior bloco.

**Conteúdo e acabamento**

12. Pacote de suprimentos com receita e fonte.
13. Novas armas com os 10 modelos sem uso.
14. Diário, salvamento no meio do capítulo, remapeamento de teclas.
15. Localização em inglês (as chaves `dlg.*` já existem).
16. Áudio de gameplay: substituir os sons provisórios por sintetizados melhores, sem serviço externo.
17. Marco 10: versão na tela e lista de licenças (nome, README, GDD e zip já feitos).

### 18.4 Itens provisórios que precisam de troca

- Bancada de trabalho (primitivas) → modelo real (depende de H).
- Visual dos materiais no chão (caixa com ícone).
- Sons de impacto em madeira, terra e água.
- Sons das telas de inventário (reaproveitados).

### 18.5 Melhorias que recomendo (não pedidas, mas valem a pena)

| Melhoria | Por quê | Custo |
|---|---|---|
| **Aquecimento de efeitos e shaders na carga** | elimina as travas de "primeira vez" que ainda restam | baixo |
| **Testes automáticos** (Unity Test Framework) | o projeto tem 127 scripts e nenhum teste unitário; cada mudança hoje depende de testes manuais e da sonda | médio |
| **Merge na `main` com tag** | a `main` está 5 fases atrás; um acidente na branch atual seria caro | baixo (decisão sua) |
| **IL2CPP** (instalação pelo Unity Hub) | código nativo, sem JIT, geralmente mais rápido em CPU. **Não há prova** de que resolva os picos atuais (o JIT foi descartado) | médio; instalação sua |
| **Otimizar a CPU com muitos inimigos** antes de expandir o mapa ou somar inimigos | a thread principal é o limite com 30 infectados | médio |
| **Assar a iluminação** dos postes (lightmaps) | tiraria o custo de luzes em tempo real, mas muda o visual e o apagão; precisa de teste | alto |
| **Tela de "o que há de novo" e versão no menu** | ajuda testes com amigos | baixo |

---

## 19. Decisões que você precisa tomar

| # | Decisão | Opções | Minha recomendação |
|---|---|---|---|
| 1 | **Próximo foco** | (a) fechar o Marco 1: primeiro tiro, golpe, CPU; (b) campanha cooperativa (Marco 7); (c) testes automáticos (Marco 9); (d) acabamento e distribuição (Marco 10) | **(a)** e depois **(c)**: são rápidos, deixam a base sólida e não dependem de você |
| 2 | **Vozes** | TTS autorizado / gravação / só legendas | se quiser dublagem, **gravação ou TTS com a sua chave**, começando pelas 9 falas da missão 1; senão, só legendas e seguimos |
| 3 | **Animações** | baixar os clipes Mixamo / adiar | baixar: é o que destrava os Marcos 3 e 6, o maior salto visual |
| 4 | **Expansão do mapa** | escolher um pacote de props / adiar | adiar até a CPU com muitos inimigos estar resolvida |
| 5 | **Multiplayer** | fazer o teste em dois PCs agora / depois | **agora**: é o único jeito de dar o cooperativo como pronto, e erros de rede real aparecem só ali |
| 6 | **Merge na `main`** | sim (merge sem fast-forward, com tag) / esperar o teste em 2 PCs | esperar o teste em 2 PCs e então fazer |
| 7 | **IL2CPP** | instalar e testar / não | só se a CPU continuar no limite depois das otimizações |

---

## 20. O que me mandar — checklist

- [ ] **Decisão 1** (próximo foco).
- [ ] **Teste em 2 PCs**: copie `Builds/Win64` para os dois. Num, MULTIJOGADOR → Criar sala; no outro, Entrar com o IP mostrado no lobby (LAN primeiro, depois Radmin). Me mande o que aconteceu e o `Player.log` de cada um (`%USERPROFILE%\AppData\LocalLow\Cesar Otavio\Base Militar - Sobreviva à Noite\Player.log`).
- [ ] **Teclado e controle**: Tab, M, E (inclusive segurar para reanimar), Q/E mirando, Esc e gamepad funcionam?
- [ ] **Áudio**: durante uma fala de rádio no meio de uma onda, dá para ouvir os infectados chegando?
- [ ] **Gráficos**: Baixo, Médio e Alto estão aceitáveis?
- [ ] **Vozes**: (a), (b) ou (c).
- [ ] **Clipes Mixamo**: sim ou não (lista em `Docs/FASE3_ASSETS.md` §3.1; destino `Assets/SourceFiles/Animation/Mixamo/`).
- [ ] **Props**: escolher um pacote, ou pedir que eu sugira opções com licença verificada.
- [ ] **Merge na `main`**: sim, não ou depois do teste em 2 PCs.

---

## 21. Ordem sugerida

| Ordem | Bloco | Quem | Por quê |
|---|---|---|---|
| 1 | Teste em 2 PCs + teclas físicas + ouvir o áudio | você | valida o que já está feito, antes de construir mais em cima |
| 2 | Primeiro tiro, primeiro golpe, aquecimento de shaders | eu | fecha o Marco 1 |
| 3 | Unity Test Framework e os primeiros testes | eu | protege tudo o que vem depois |
| 4 | Munição pelo host, missões e loja no cliente | eu | fecha o Marco 2 |
| 5 | Clipes Mixamo → animações e inimigos novos | você → eu | Marcos 3 e 6 |
| 6 | Vozes da missão 1 → resto da campanha | você → eu | Marco 4 |
| 7 | CPU com muitos inimigos → props → expansão do mapa | eu (+ props seus) | Marco 5 |
| 8 | Campanha cooperativa | eu | Marco 7 |
| 9 | Acabamento, nome, versão, licenças, pacote | eu (+ decisões suas) | Marco 10 |
