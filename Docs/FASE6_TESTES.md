# Fase 6 — Testes de multiplayer (primeira execução)

Este documento registra **apenas testes que foram executados de fato**, com o resultado observado.
O que não foi testado está listado separadamente como **pendente** ou **bloqueado**.

## Ambiente

| Item | Valor |
|---|---|
| Engine | Unity 6000.6.7f1 (URP) |
| Rede | Netcode for GameObjects 2.13.3 + Unity Transport 6.6.0 |
| Host | Editor Unity em Play Mode (`MilitaryBase_Combat`) |
| Cliente | Build Windows `Builds/Win64/BaseMilitar.exe` (1280×720 em janela) |
| Endereço | `127.0.0.1:7777` (UDP), na mesma máquina |
| Automação do cliente | `NetAutoTest` (`-net-join`, `-net-name`, `-net-log`, `-net-hit`) |

Os dois processos são independentes: cada um tem sua própria cena, seu próprio jogador, câmera e HUD. A comunicação passa só pela rede.

> Limitação do teste: as duas instâncias rodam no mesmo PC, pelo loopback. Isso valida o protocolo e a lógica, mas **não** valida rede real, NAT, firewall, Radmin VPN ou Hamachi.

## Resultados

### T1 — Conexão e lobby — **PASSOU**
- O cliente conectou, foi aprovado (protocolo 1, versão 4.0.1) e marcou PRONTO sozinho.
- O lobby do host mostrou:
  - Host (HOST, você);
  - Amigo (Pronto, traje 1-1-4, 119 ms);
  - o botão REMOVER;
  - os IPs `192.168.0.193` (Wi-Fi) e `172.22.192.1` (WSL).
- `CanStart` ficou verdadeiro só depois que o cliente ficou pronto.
- O ping de 119 ms foi medido com o editor rodando a poucos quadros por segundo. Não é uma latência real de rede.

### T2 — Início da partida e spawn — **PASSOU**
- As duas instâncias iniciaram juntas, com SpawnIndex 0 e 1, a cerca de 3 m uma da outra.
- Cada máquina controla o próprio personagem real, o Survivalist, e não uma cápsula.
- No host, uma captura de câmera mostrou o boneco do cliente com o modelo real, um traje diferente e a etiqueta de nome.
- O cliente viu o boneco do host empunhando a arma de índice 3 (M1911).

### T3 — Zumbis replicados — **PASSOU**
- No cliente, o `EnemySpawner` ficou desligado e não havia nenhum zumbi local com IA (0).
- Os zumbis do host apareceram no cliente como réplicas, com posição, animação e vida.

### T4 — Dano do cliente validado pelo host — **PASSOU**
- O cliente matou os 6 zumbis da onda 1 com 18 acertos.
- Cada acerto foi enviado como pedido de dano (`DamageEnemyRpc`) e aplicado pelo host depois da validação.
- O host creditou os abates ao cliente: 6 abates, 450 pontos. Os popups de +75 e +10 CR apareceram no cliente.

### T5 — Zumbi do host atacando o cliente — **PASSOU**
- Um zumbi de 600 HP foi criado ao lado do boneco do cliente. A IA do host escolheu `Puppet_Amigo` como alvo.
- O golpe do zumbi baixou a vida real do cliente de 100 para 82, e o boneco no host espelhou esse valor.
- O host também atirou nele (600 → 572). Os acertos do cliente o mataram em cerca de 8 s, e o abate foi para o cliente (7 abates, 525 pontos).
- O corpo sumiu depois de 16 s e a réplica foi removida no cliente.

### T6 — Queda do cliente — **PASSOU (com observação)**
- O processo do cliente foi encerrado à força.
- Depois de cerca de 30 s, que é o timeout padrão do transporte, o host removeu o jogador e o boneco, e a partida continuou.
- Observação: 30 s é muito tempo. Ainda não ajustei o `DisconnectTimeoutMS`.

### T7 — Voltar ao menu e hospedar de novo — **FALHOU → CORRIGIDO → PASSOU**
- Primeira tentativa: depois de `GameFlow.ReturnToMenu()`, que recarrega a cena, o novo `NetworkManager` não era o singleton, e `NetGame.SpawnFor` disparou uma NullReferenceException.
- Correções:
  - `NetSession.EnsureSingleton()` antes de `Host` e de `Join`;
  - `GameFlow.Reload()` destrói o NetworkManager antigo com `DestroyImmediate`;
  - o spawn passou a usar `SpawnManager.InstantiateAndSpawn`.
- Reteste: hospedar → voltar ao menu → hospedar de novo. Resultado: um único NetworkManager, que é o singleton, escutando, com `NetGame` criado, 1 jogador e nenhum erro no console.

### T8 — Recusa por senha — **PASSOU**
- O host abriu a sala "Sala Fechada" (máx. 2, com senha). O cliente tentou entrar com a senha vazia.
- O log do cliente registrou `Failed: Senha incorreta.` cerca de 3 s depois de iniciar a conexão. O cliente não entrou na sessão.

### T9 — Single-player depois da integração — **PARCIAL**
- Em Play Mode no editor, sem sessão de rede, a cena abre no menu normal sem erros novos no console. O botão MULTIJOGADOR aparece sem deslocar os outros.
- Por código: todo desvio de rede depende de `Net.Listening` ou `Net.Active`. Sem sessão, spawner, IA, pausa e slow-motion seguem pelo caminho antigo.
- **Não executado:** uma partida single-player completa depois das mudanças. Isso fica como regressão pendente.

### T10 — Reteste com a build nova — **PASSOU**
- Build Windows gerada de novo com todas as correções (`Assembly-CSharp.dll` às 05:13). Host no editor, sala "Reteste", sem senha.
- Lobby do host:
  - o log mostrou "Amigo entrou na sessão", agora com o nome e não "Jogador 2";
  - a dificuldade "FÁCIL" aparece entre as setas;
  - o Amigo aparecia Pronto, com traje 1-1-4 e 369 ms (editor a poucos quadros por segundo).
- Partida:
  - o cliente matou os 6 zumbis da onda 1 com 18 acertos (6 abates e 450 pontos no estado da partida);
  - o HUD do cliente mostrou **ABATES 1** e +10 CR logo no primeiro abate (antes ficava em 0);
  - o boneco do host aparecia com a M1911.
- No `player.log` do cliente: **0** avisos "Can't remove … depends on it" e nenhuma exceção.

## Falhas encontradas e corrigidas

| Falha | Causa | Correção | Situação |
|---|---|---|---|
| NRE ao hospedar depois de voltar ao menu | o singleton apontava para o NetworkManager destruído | `EnsureSingleton` + `DestroyImmediate` + `InstantiateAndSpawn` | Retestado, ok |
| Avisos "Can't remove X because Y depends on it" no boneco | ordem de remoção dos componentes | remoção em passes, respeitando `RequireComponent` | Retestado na build nova (T10): 0 avisos |
| Contador ABATES do cliente ficava em 0 | o HUD lê `PlayerCombat.Kills`, que só conta abates locais | `PlayerCombat.CountNetworkKill()` | Retestado (T10): HUD marca ABATES 1 no 1º abate |
| Aviso de entrada dizia "Jogador 2" | o nome chega depois do objeto do jogador | o anúncio espera o `PlayerName` | Retestado (T10): "Amigo entrou na sessão" |
| Nome da dificuldade longe das setas no lobby | layout | rótulo entre as setas | Retestado (T10): "FÁCIL" entre `<` e `>` |
| Prefabs de rede registrados em duplicidade | lista própria + `DefaultNetworkPrefabs` | uso só da lista padrão | Ok |

> As correções acima foram retestadas com uma build Windows nova, gerada depois delas (T10).

## Pendentes (não executados)
- Teste em **dois PCs físicos** pela LAN.
- Teste por **Radmin VPN** ou **Hamachi** (exige outra máquina e a instalação dessas ferramentas pelo usuário).
- Partida com **3 e 4 jogadores** e a recusa "Sala cheia" com um cliente a mais.
- Recusa "A partida já começou" e recusa por versão diferente.
- Botão REMOVER (expulsar) e saída voluntária do cliente pelo botão SAIR.
- **Controle (gamepad)** e **teclado físico**: as entradas simuladas não chegam ao jogo nesta sessão, então os testes usaram as flags de `StarterAssetsInputs` e chamadas diretas.
- Latência e perda reais (só testado no loopback, sem medir com o Profiler).
- Regressão completa do single-player (campanha inteira).

## Bloqueados nesta etapa (dependem de sistemas que ainda não existem)
- Soldados inimigos, chefes e Screamer replicados: só zumbis comuns (`ZombieAI`) são replicados.
- Portas, energia, loot e objetos do mundo sincronizados.
- Munição, economia e loja validadas pelo host.
- Game over cooperativo, queda e revive.
- HUD de ondas no cliente.
