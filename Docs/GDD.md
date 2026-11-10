# GDD — Base Militar: Sobreviva à Noite

**Documento de Design do Jogo**

| | |
|---|---|
| Autor | Cesar Otavio da Silva Boiani |
| Disciplina | Desenvolvimento de Jogos Digitais — Prof. Luis Filipe Grael Tinós |
| Versão do jogo | 4.0.1 |
| Engine | Unity 6000.6.7f1 (URP) |
| Plataforma | Windows (PC), teclado e mouse ou controle |
| Repositório | https://github.com/Cesar-Otavio/jogo-de-zumbis |

---

## 1. Visão geral

### 1.1 Conceito (pitch)

Uma noite de tempestade numa base militar isolada. Um experimento chamado **Protocolo Aurora** fugiu do controle, e os soldados viraram infectados. O jogador, um sobrevivente sem armas, precisa explorar a base, restaurar a energia, recuperar informações e resistir a ondas de inimigos até o helicóptero de resgate chegar.

### 1.2 Ficha

| Item | Definição |
|---|---|
| Gênero | tiro em terceira pessoa + sobrevivência com elementos de terror |
| Câmera | terceira pessoa, sobre o ombro (o ombro pode ser trocado) |
| Público | jogadores a partir de 14 anos que gostam de ação e terror (violência com sangue estilizado, visual low poly) |
| Sessão | capítulos de 5–15 min; Modo Infinito até o jogador cair |
| Modos | Campanha (10 capítulos), Modo Infinito e cooperativo online de 2–4 jogadores |
| Referências | *Resident Evil 4/2 Remake* (câmera e tensão), *Call of Duty Zombies* (ondas e loja), *Left 4 Dead* (cooperativo) |

### 1.3 Pilares de design

1. **Tensão no escuro**: noite, chuva, apagões, lanterna e sons que denunciam o inimigo antes de ele aparecer.
2. **Armas com peso**: recuo, recarga em etapas e mira que faz diferença; cada arma tem um papel.
3. **Recursos limitados**: munição, créditos e materiais obrigam a escolher entre comprar, fabricar ou economizar.
4. **Pressão crescente**: cada onda e cada capítulo trazem mais inimigos e inimigos mais perigosos.

---

## 2. Loop de jogo

```
Explorar a base → cumprir a missão → disparar a onda → sobreviver
        ↑                                                   ↓
   comprar/fabricar  ←  intervalo com loja (50–80 s)  ←  recompensa
```

- **Loop curto** (segundos): mirar, atirar, recarregar, se reposicionar, usar itens.
- **Loop médio** (minutos): missão → onda → intervalo → loja.
- **Loop longo** (sessões): concluir capítulos, subir de nível (até 30), liberar armas e trajes, bater recordes.

---

## 3. Mecânicas

### 3.1 Movimento

| Ação | Detalhe |
|---|---|
| Andar / correr | correr gasta estamina, que recupera parada |
| Pulo | pulo duplo no ar |
| Agachar | mais lento e mais silencioso |
| Rolar | esquiva rápida, invulnerável no começo |
| Escalar | o personagem se pendura em bordas e sobe |
| Inclinar | Q/E enquanto mira, para atirar pela quina sem se expor |

### 3.2 Combate

- **Tiro**: raycast do cano até a mira, com dispersão que aumenta ao correr ou atirar seguido e diminui ao mirar. Tiro na **cabeça** causa dano extra.
- **Recuo** simulado por molas, **recarga em etapas** (pente, ferrolho, bomba da escopeta) e **cápsulas** físicas.
- **Corpo a corpo**: coronhada ou faca, e empurrão que faz o inimigo cambalear e recuar (os pesados resistem).
- **Arremessáveis**: granada (explosão), molotov (área de fogo), sinalizador (luz que atrai e enfraquece a chefe final), distração (barulho que atrai inimigos).

### 3.3 Armas

| Arma | Papel | Dano | Cadência | Pente |
|---|---|---|---|---|
| M1911 | pistola inicial, precisa | 28 | semiautomática | 12 |
| Uzi | curta distância, muito rápida | 14 | ~920/min | 32 |
| Benelli M4 | escopeta, controle de multidão | 13 × 9 bagos | bomba | 7 |
| AK-74 | fuzil de dano alto | 26 | 600/min | 30 |
| M4 | fuzil equilibrado | 22 | 750/min | 30 |
| M107 | precisão, com luneta | 110 | ferrolho | 5 |

- **Melhorias na loja**: cano reforçado (+15% de dano), carregador estendido (+40%), empunhadura (−30% de recuo), compensador, silenciador, mira holográfica, recarga rápida.
- O jogador carrega **2 armas**, visíveis no corpo (quadril, costas ou peito).

### 3.4 Vida, defesa e furtividade

- **Vida** com regeneração lenta depois de alguns segundos sem levar dano.
- **Colete**: absorve metade do dano, até 60 pontos.
- **Ruído**: correr, atirar e pisar em metal fazem barulho que os inimigos escutam. Agachado e com silenciador, o jogador passa despercebido.

### 3.5 Itens, mochila e fabricação

- **Barra rápida** (teclas 1–6): kit médico, granada, molotov, sinalizador, barricada, distração.
- **Mochila** (Tab): 18 espaços para materiais e excedentes.
- **8 materiais** (sucata, tecido, fiação, material médico, eletrônicos, peças mecânicas, químicos, peças táticas), achados em baús e pela base.
- **6 receitas**: bandagem (na mochila); kit médico improvisado, distração, sinalizador, kit de barricada e reforço de colete (na bancada do depósito).

### 3.6 Economia

- **Créditos** por abate, por onda, por missão e por baú.
- **Loja** com 25 itens em 5 abas (armas, munição, proteção, consumíveis, melhorias), aberta no intervalo entre ondas.

### 3.7 Mundo interativo

- **Energia por setores**: normal, emergência ou apagão. Os geradores precisam de fusível.
- **Portas**: trancadas por cartão; os inimigos arrombam.
- **Barricadas**: o jogador monta e reforça; o Tanque quebra.
- **Baús** com armas, munição e materiais.
- **Documentos** colecionáveis contam a história do Protocolo Aurora.

---

## 4. Condições de vitória, derrota e pontuação

| Elemento | Regra |
|---|---|
| Vidas | Fácil 5, Normal 3, Difícil 2, Pesadelo 1. Morrer gasta uma vida e o jogador volta |
| Derrota | sem vidas → fim de jogo. No cooperativo: todos caídos ao mesmo tempo |
| Vitória (campanha) | concluir o capítulo 10, defendendo o heliponto até o resgate pousar |
| Vitória (Infinito) | não há fim; vale o recorde de ondas e pontos |
| Pontos | por abate (mais por cabeça e por especiais), por onda e por missão, multiplicados pela dificuldade (×0,75 a ×2) |
| Progressão | nível até 30 libera armas na loja e peças de traje; 8 conquistas |

---

## 5. Inimigos e IA

### 5.1 Tipos

| Inimigo | Comportamento | Diferencial |
|---|---|---|
| Zumbi comum | anda, persegue, golpeia | a base das ondas |
| Corredor | muito rápido | 60% da vida, ×3,6 de velocidade |
| Tanque | lento e resistente | ×4,5 de vida, ×2,6 de dano, quebra portas e barricadas |
| Gritador | para e grita | chama outros infectados; empurrá-lo interrompe o grito |
| Rastejante | se esconde no escuro | ataque de emboscada |
| Soldado | patrulha, procura cobertura, atira em rajadas | atira no jogador e nos infectados |
| Experimento 07 (chefe) | investida telegrafada | fica atordoado se bater numa parede |
| Aurora (chefe final) | 2 fases, onda de choque, invoca corredores | fica vulnerável sob luz forte |

### 5.2 Máquina de estados da percepção

Todo inimigo tem visão (cone e distância, bloqueada por paredes) e audição (o ruído do jogador). O estado muda assim:

```
Patrulha → (ouviu algo) → Investigar → (não achou) → Procurar → Voltar → Patrulha
    └──────── (viu o jogador) ────────→ Perseguir → (perto) → Atacar
```

A navegação usa o NavMesh da Unity. Os inimigos contornam obstáculos e quebram portas quando o caminho está bloqueado.

---

## 6. Modos e progressão

### 6.1 Campanha "Protocolo Aurora"

| # | Capítulo | Objetivo principal | Onda final |
|---|---|---|---|
| 1 | Primeiro contato | pegar uma arma, investigar, sobreviver | 5 zumbis |
| 2 | Sem energia | achar o fusível, ligar o gerador | 7 zumbis |
| 3 | Mensagem desconhecida | rastrear o sinal (+ missão lateral) | 8 zumbis + 1 soldado |
| 4 | Arsenal | cartão e porta; **escolha** entre defender o arsenal ou restaurar a energia | 9 Z + 2 S |
| 5 | Experimento 07 | derrotar o **primeiro chefe** | 6 Z + chefe |
| 6 | Comunicação comprometida | torre de vigilância (+ lateral) | 10 Z + 2 S |
| 7 | Laboratório | recuperar a caixa-preta (+ lateral) | 10 Z + 3 S |
| 8 | Falha de contenção | válvulas em sequência | 12 Z + 2 S |
| 9 | Protocolo de evacuação | códigos, rádio, heliponto | 13 Z + 3 S |
| 10 | Sobreviva à noite | farol, **chefe Aurora**, defender até o pouso | 14 Z + 2 S + chefe |

O jogo salva ao fim de cada capítulo.

### 6.2 Modo Infinito

Ondas sem fim. A cada onda aumentam a quantidade de inimigos, a chance de especiais e a vida deles. Há missões laterais opcionais e loja no intervalo.

### 6.3 Cooperativo (2–4 jogadores)

- **Conexão**: o Modo Infinito em rede, com um jogador como **host**. Conexão direta por IP (rede local ou VPN como Radmin/Hamachi).
- **Regras**:
  - o jogador que perde toda a vida **cai** e pode ser **reanimado** por um aliado (segurando E por 5 s);
  - quem morre assiste como **espectador** e volta na onda seguinte.
- **Escala com a equipe**: mais inimigos (+55% por jogador extra), chefes com mais vida e intervalos maiores.

### 6.4 Dificuldades

| | Fácil | Normal | Difícil | Pesadelo |
|---|---|---|---|---|
| Dano inimigo | ×0,6 | ×1 | ×1,4 | ×1,8 |
| Vida inimiga | ×0,8 | ×1 | ×1,25 | ×1,5 |
| Quantidade de inimigos | ×0,75 | ×1 | ×1,3 | ×1,6 |
| Suprimentos | ×1,3 | ×1 | ×0,85 | ×0,7 |
| Pontuação | ×0,75 | ×1 | ×1,5 | ×2 |

---

## 7. Mundo e ambientação

- **Mapa**: uma base militar com portões, muros, hangares, torres de vigia, arsenal, laboratório, depósito com bancada e heliponto.
- **Clima**: noite com chuva, raios, vento e neblina.
- **Terror**: 12 tipos de eventos (portas batendo, passos, silhuetas, lâmpadas piscando). Um **diretor de tensão** alterna sustos e calmaria.
- **Luz como recurso**: a lanterna tática do jogador, os setores que apagam e os sinalizadores.

---

## 8. Interface e feedback

| Elemento | Função |
|---|---|
| HUD de combate | vida, colete, vidas, arma e munição, pontos, abates e créditos |
| Mira dinâmica | abre e fecha com a dispersão; marcador de acerto (cabeça e abate) |
| Setas de ameaça e legendas de sons | indicam inimigos fora da tela (acessibilidade) |
| Cartão da missão e marcadores | objetivo atual com distância |
| Minimapa e mapa tático (M) | névoa de exploração, locais, objetivos, aliados |
| Loja, mochila e fabricação | telas de gerenciamento |
| Legendas de diálogo | nome de quem fala e efeito de rádio |
| Menus | principal, pausa, opções em 3 páginas, fim de jogo, lobby multijogador |

- **Feedback sensorial**: partículas e sons de impacto por material (concreto, metal, madeira, terra, água), tremor de câmera, efeito de tela ao ser ferido.
- **Opções de acessibilidade**: tamanho das legendas, tremor e flashes reduzidos, setas de inimigos.

---

## 9. Controles

Tabela completa no [README](../README.md#2-controles). Resumo: **WASD** move, **mouse** mira, **botão esquerdo** atira, **direito** mira, **E** interage, **R** recarrega, **Tab** abre a mochila, **M** o mapa, **Esc** pausa. Há mapeamento para controle.

---

## 10. Arte e áudio

- **Visual**: low poly com iluminação noturna (URP). Pós-processamento com névoa, cor fria e lanternas quentes.
- **Modelos**:
  - mapa: Military Base Pack (Tiny Teacup Studio);
  - personagem: Survivalist;
  - armas: Low Poly Weapons;
  - animações dos zumbis: Kevin Iglesias.
- **Áudio**:
  - 225 arquivos: armas, ambiente, músicas de tensão e efeitos de terror sintetizados por código;
  - **AudioMixer** com grupos (música, efeitos, diálogo, rádio, ambiente, interface);
  - durante as falas, o ambiente baixa e os sons de ameaça continuam audíveis.
- **Narrativa**: 54 falas com legendas (sem voz gravada) e 9 documentos colecionáveis.

---

## 11. Tecnologia

| Item | Escolha |
|---|---|
| Engine | Unity 6000.6.7f1, Universal Render Pipeline (Forward+) |
| Entrada | Input System (teclado, mouse e controle) |
| Câmera | Cinemachine |
| Navegação | NavMesh |
| Rede | Netcode for GameObjects + Unity Transport (host-cliente) |
| Interface | uGUI + TextMesh Pro, montada por código |
| Código | ~127 scripts C# próprios (~32 mil linhas) |
| Desempenho | ~95 FPS em 1080p (qualidade Média, 30 inimigos) num notebook com GPU integrada; 3 níveis de qualidade |

**Principais scripts**:
- `GameFlow` (estados e regras da partida);
- `PlayerCombat` e `WeaponHolder` (jogador e armas);
- `ZombieAI`, `EnemyShooterAI` e `EnemyPerception` (IA);
- `EnemySpawner` (ondas);
- `CampaignManager` e `MissionManager` (campanha);
- `CombatHUD` e `CampaignHUD` (interface);
- `NetGame`, `NetPlayer` e `NetWorld` (cooperativo).

---

## 12. Escopo, limitações e próximos passos

**Entregue e funcional**:
- campanha completa;
- Modo Infinito;
- 6 armas, 8 tipos de inimigos e 2 chefes;
- mochila e fabricação;
- cooperativo testado na mesma máquina com até 3 jogadores.

**Limitações conhecidas**:
- falas só com legenda (sem dublagem);
- algumas animações de locomoção (andar de lado, deitar) ainda não existem;
- o cooperativo não foi testado entre dois computadores diferentes;
- o primeiro tiro de cada partida tem uma pequena travada (carregamento de efeitos).

**Próximos passos**:
- dublagem;
- novas animações e 4 novos tipos de infectados;
- expansão do mapa (enfermaria, oficina, alojamentos);
- campanha cooperativa;
- testes automatizados.
