# Fase 6 — Arquitetura de rede

## 1. Base

| Item | Escolha |
|---|---|
| Framework | **Netcode for GameObjects 2.13.3** (`com.unity.netcode.gameobjects`), a versão compatível com o Unity 6000.6 |
| Transporte | **Unity Transport 6.6.0** (`com.unity.transport`), UDP direto por IP e porta. Sem Relay, matchmaking nem servidor dedicado |
| Topologia | **Host-Client** com autoridade do Host |
| Escuta | o Host escuta em `0.0.0.0` (todas as interfaces: LAN, Radmin, Hamachi) na porta escolhida (padrão 7777) |
| Cena | `EnableSceneManagement = false`: todos já têm a mesma cena carregada, e o estado vem dos objetos de rede do próprio jogo |
| Taxa | tick de rede 30/s; jogadores publicam 20 estados/s; zumbis 10 snapshots/s |

## 2. Quem manda em quê

| Assunto | Autoridade | Como |
|---|---|---|
| Entrar na sala | Host | aprovação (`ConnectionApprovalCallback`): protocolo e versão do jogo, sala cheia, partida já iniciada, senha. O motivo da recusa volta ao cliente (`Reason`) |
| Início da partida | Host | `NetGame.StartMatch`: confere se todos estão prontos, define o ponto de spawn de cada um, `StartMatchRpc` para todos. Nenhum cliente inicia a própria onda |
| Personagem de cada jogador | **o dono** (resposta imediata) | o dono publica o `PlayerState`; o Host confere a velocidade (mais de 25 m/s fica registrado); os outros veem o boneco interpolado |
| Inimigos, IA, ondas | Host | o `EnemySpawner` só roda no Host. Nos clientes ele é desligado e qualquer inimigo local é removido |
| Dano em inimigos | Host | tiro do cliente → `DamageEnemyRpc` → o Host valida e aplica |
| Dano em jogadores | dono | golpe do zumbi no boneco (no Host) → `DamageRpc` para o dono → `Health.TakeDamage` local |
| Vida dos outros | espelhada | o boneco mostra a vida publicada pelo dono (`Health.Mirror`) |
| Munição, armas, mochila, créditos, loja | dono, **ainda sem validação do Host** | próximo marco |
| Portas, energia, barricadas, baús, itens no chão | **locais**, ainda não sincronizados | próximo marco |

## 3. Objetos de rede

| Prefab | Componente | Quem cria |
|---|---|---|
| `Network/NetPlayer.prefab` | `NetworkObject` + `NetPlayer` | o NGO, um por jogador conectado (Player Prefab) |
| `Network/NetGame.prefab` | `NetworkObject` + `NetGame` | o Host, ao abrir a sala |

Os dois estão na lista `Network/NetworkPrefabs.asset`, ligada ao `NetworkManager` da cena.

## 4. Estado sincronizado

**`NetPlayer`** (`NetworkVariable`):

| Variável | Escreve | Conteúdo |
|---|---|---|
| `PlayerName` | dono | nome (até 20 caracteres) |
| `Ready` | dono | pronto no lobby |
| `Outfit` | dono | 7 escolhas de traje, 4 bits cada (camisa, calça, botas, boné, colete, mochila, luvas) |
| `State` | dono | posição, rotação, velocidade de animação, 8 flags (no chão, pulo, queda, pendurado, agachado, mirando, lanterna, morto), contadores de rolagem e escalada, arma (índice), vida e vida máxima |
| `SpawnIndex` | Host | ponto de spawn 0–3 |

**`NetGame`** (`NetworkVariable`, escreve o Host): sala, modo, dificuldade, máximo de jogadores, partida iniciada.

**Zumbis** (`EnemyState`, cerca de 26 bytes cada, em pacotes de até 24): id, prefab, variante, posição, rotação, velocidade de animação, morto, contador de ataques, vida (0–255).

## 5. RPCs

| RPC | Para | Entrega | Uso |
|---|---|---|---|
| `NetGame.StartMatchRpc` | todos | confiável | iniciar a partida com modo e dificuldade |
| `NetGame.AnnounceRpc` | todos | confiável | avisos (entrou, saiu) |
| `NetGame.EnemiesRpc` | clientes | **não confiável** | snapshots dos zumbis (o próximo substitui o perdido) |
| `NetGame.DamageEnemyRpc` | Host | confiável | pedido de dano; o remetente vem do NGO (`SenderClientId`), nunca de um campo enviado |
| `NetGame.KillConfirmedRpc` | um cliente | confiável | abate confirmado: pontos, créditos e XP para quem matou |
| `NetPlayer.ShotRpc` | os outros | não confiável | desenhar o tiro do jogador (clarão, traçante, som) |
| `NetPlayer.DamageRpc` | dono | confiável | dano recebido no boneco do Host |

## 6. Validação do dano (Host)

Um pedido de dano só é aplicado se:

1. o inimigo existe e está vivo;
2. o jogador que pediu está conectado, tem boneco e está vivo;
3. a distância entre o boneco e o alvo é de até 320 m (o alcance da arma mais longa, mais folga);
4. o jogador fez no máximo **40 acertos por segundo** (cobre os 9 bagos da escopeta);
5. o valor é limitado a 200 (ou 400 em tiro na cabeça). Isso cobre o maior dano real (M107 com melhoria) sem aceitar valores absurdos.

O cliente calcula o dano com as mesmas regras do single-player (arma, parte do corpo, melhorias), e o Host só aceita dentro desses limites. **Ainda não** é feita uma checagem de cadência e munição contra a arma do jogador; fica para o marco de economia, quando a munição passar pelo Host.

## 7. Single-player intocado

- Sem sessão, `Net.Listening` é falso: `Health.Intercept` fica nulo, o `EnemySpawner` roda e a pausa congela o tempo, como antes.
- **Durante o co-op**:
  - a pausa e as telas (mochila, mapa) **não** congelam nem desaceleram o tempo, porque o mundo é de todos;
  - Reiniciar ou Voltar ao menu sai da sessão.
