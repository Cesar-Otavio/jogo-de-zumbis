# Fase 6 — Entrega da primeira execução (MVP de rede)

**Branch:** `fase6-multiplayer`. **Checkpoint antes da fase:** tag `fase6-checkpoint` (commit 5b3801c).

## Situação geral

O **MVP cooperativo de 2 jogadores funciona** entre duas instâncias independentes na mesma máquina:
- lobby;
- spawn de dois personagens reais;
- movimento, animação e arma sincronizados;
- zumbis do host replicados;
- dano compartilhado validado pelo host;
- abates creditados.

O multiplayer **não está completo**. Dois PCs, Radmin/Hamachi, 3 ou 4 jogadores e a maior parte dos sistemas do jogo (economia, mundo, revive, soldados, chefes) ainda não foram feitos ou testados. Os detalhes estão em [FASE6_TESTES.md](FASE6_TESTES.md).

## Os 12 passos da primeira execução

| # | Passo | Situação |
|---|---|---|
| 1 | Checkpoint | ✅ tag `fase6-checkpoint` + branch `fase6-multiplayer` |
| 2 | Instalar NGO e Transport | ✅ NGO 2.13.3, Transport 6.6.0 (gratuitos, pacotes oficiais da Unity) |
| 3 | NetworkManager funcional | ✅ UnityTransport, aprovação de conexão, tick 30 |
| 4 | Criar/Entrar | ✅ menu MULTIJOGADOR → Criar sala / Entrar por IP:porta |
| 5 | Lobby de 2 jogadores | ✅ testado (pronto, traje, ping, remover, IPs do host) |
| 6 | Spawn de dois personagens reais | ✅ modelo Survivalist com traje, sem cápsulas |
| 7 | Movimento, câmera local, animação básica | ✅ (câmera e HUD são sempre locais) |
| 8 | Sincronizar uma arma | ✅ arma na mão, clarão, traçante e som do tiro |
| 9 | Sincronizar um zumbi | ✅ todos os `ZombieAI` do host, a 10 Hz |
| 10 | Dano e morte compartilhados | ✅ cliente → zumbi (validado) e zumbi → cliente |
| 11 | Teste em duas instâncias | ✅ editor (host) + build Windows (cliente), loopback |
| 12 | Evidências e limitações | ✅ este documento + `FASE6_TESTES.md` |

## Arquivos

### Scripts novos — `Assets/SourceFiles/Scripts/Combat/Network/`

| Arquivo | Função |
|---|---|
| `Net.cs` | atalhos `Listening`, `Active`, `HasAuthority`, `IsClientOnly` |
| `NetSession.cs` | Host/Join/Leave, aprovação (protocolo, versão, sala cheia, partida iniciada, senha), timeout, status |
| `NetGame.cs` | estado da sala, início da partida, snapshots de inimigos, dano validado em inimigos, confirmação de abate |
| `NetPlayer.cs` | objeto de cada jogador: nome, pronto, traje, `PlayerState` a 20 Hz, tiros, dano no dono |
| `NetPuppet.cs` | boneco remoto com o modelo real: interpolação, animação, arma, lanterna, ragdoll, nome |
| `EnemyReplica.cs` | zumbi visual no cliente (sem IA) e envio dos acertos ao host |
| `NetMatch.cs` | início da partida em cada máquina, posição de spawn, escala de inimigos, avisos, crédito de abate |
| `NetSessionPrefabs.cs` | referências: prefab `NetGame`, personagem, lista de armas |
| `NetMenuView.cs` | telas Início, Criar, Entrar e Lobby |
| `NetAutoTest.cs` | automação de testes por linha de comando (`-net-join` etc.); só age com esses argumentos |

### Editor
- `Scripts/Combat/Editor/CombatSetup.Phase6.cs`:
  - *Phase 6/1 - Network Setup*: cria os prefabs e o NetworkManager e configura a UI.
  - *Phase 6/2 - Windows Build*.

### Prefabs e assets
- `Assets/SourceFiles/Network/NetPlayer.prefab`
- `Assets/SourceFiles/Network/NetGame.prefab`
- `Assets/DefaultNetworkPrefabs.asset`, mantido pelo NGO.
- Na cena `MilitaryBase_Combat`: os objetos `NetworkManager` e `NetworkUI`.

### Scripts existentes alterados (sem mudar o single-player)
- `Health.cs`: o gancho `Intercept` desvia o dano para a rede, e `Mirror` espelha a vida.
- `ZombieAI.cs`: `AttackCount` e `AnimationSpeed`, para a replicação.
- `WeaponHolder.cs`: `LastShotFrom` / `LastShotTo`.
- `PlayerOutfit.cs`: `Choices` / `ShowChoices`.
- `PlayerCombat.cs`: `CountNetworkKill`.
- `GameFlow.cs`:
  - botão MULTIJOGADOR;
  - escala de inimigos por jogador;
  - o cliente não roda o spawner;
  - a pausa não congela o tempo em rede;
  - `ReturnToMenu`, `CreditNetworkKill`, `Reload` encerra a sessão.
- `Inventory/GameplayOverlay.cs`: sem câmera lenta em rede.

### Pacotes
- `com.unity.netcode.gameobjects` 2.13.3
- `com.unity.transport` 6.6.0

Ambos são oficiais e gratuitos. Nenhum serviço pago (Relay, Lobby, Vivox) foi configurado.

### Documentos
`FASE6_AUDITORIA_MULTIPLAYER.md`, `FASE6_ARQUITETURA_REDE.md`, `FASE6_MENU_LOBBY.md`, `FASE6_RADMIN_HAMACHI.md`, `FASE6_TESTES.md` e este arquivo.

## Segurança aplicada
- A identidade de cada jogador é o `ClientId` dado pelo NGO (`rpc.Receive.SenderClientId`). Nenhum ID enviado pelo cliente é aceito como prova.
- O dano pedido pelo cliente é validado pelo host:
  - o inimigo existe e está vivo;
  - o atirador existe e está vivo;
  - distância de até 320 m;
  - no máximo 40 acertos/s;
  - dano limitado (200, ou 400 na cabeça) e força limitada.
- Nenhuma mensagem de rede executa código ou comandos. As RPCs só carregam números e vetores.
- A senha da sala é comparada no host. Ela trafega sem criptografia no payload de conexão (aceitável para salas entre amigos; não use uma senha importante).
- A documentação do Radmin/Hamachi orienta liberar só o jogo na porta UDP escolhida, **sem desativar o firewall**.

## Limitações conhecidas
- O desconectado só sai da sala depois de cerca de 30 s (timeout padrão do transporte).
- Só zumbis comuns são replicados. Soldados, chefes e Screamer ainda não.
- Portas, energia, loot, baús, mochila, munição, créditos e loja continuam locais e não são validados.
- Não há game over cooperativo, queda nem revive. Cada jogador morre e reaparece pelo próprio fluxo.
- O cliente não tem o HUD de ondas. O modo em rede é só o Infinito (a campanha não está sincronizada).
- A build do cliente usada nos testes é anterior às últimas correções (remoção de componentes do boneco, contador de abates, aviso de entrada, rótulo de dificuldade).
- A build inclui shaders não usados do AI Inference/Sentis (a build tem 421 MB). Isso ainda não foi otimizado.

## Próximos passos sugeridos
1. Gerar uma nova build e testar em **dois PCs**, primeiro na LAN e depois pelo Radmin VPN, seguindo `FASE6_RADMIN_HAMACHI.md`.
2. Testar 3 e 4 jogadores e as recusas "Sala cheia" e "A partida já começou".
3. Reduzir o timeout de desconexão e mostrar um aviso de "reconectando".
4. Replicar soldados, chefes e o Screamer.
5. Sincronizar o mundo: portas, energia, loot.
6. Validar no host munição, créditos e loja, e criar o game over cooperativo com queda e revive.
7. Adicionar o HUD de ondas no cliente e sincronizar o minimapa (marcadores dos aliados).
