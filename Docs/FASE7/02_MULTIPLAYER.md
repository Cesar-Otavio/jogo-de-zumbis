# Fase 7 — Multiplayer (revisão do Modo Infinito cooperativo)

A arquitetura do MVP continua a mesma; ver `Docs/FASE6_ARQUITETURA_REDE.md`. Este documento registra a revisão feita no Marco 1 e o que mudou.

## 1. Revisão: o que o Infinito cooperativo precisa e onde está

Legenda dos estados: ✅ concluído e validado · 🧪 implementado, aguardando teste · 🟡 parcial · ⛔ não iniciado.

| Sistema | Estado | Observação |
|---|---|---|
| Conexão, aprovação, lobby, spawn de personagens reais | ✅ (mesmo PC) | Fase 6 |
| Zumbis comuns e dano validado pelo host | ✅ (mesmo PC) | Fase 6 |
| **Timeout de desconexão** | ✅ (mesmo PC) | **novo**: 10 s em vez de ~30 s |
| **Onda, inimigos restantes e intervalo no HUD do cliente** | ✅ (mesmo PC) | **novo**: espelho do spawner do host |
| **Recompensa de fim de onda no cliente** | ✅ (mesmo PC) | **novo**: pontos, créditos e cura vêm do evento espelhado; cada jogador recebe a sua |
| Pular o intervalo (N) | ✅ | só o host (é ele quem roda as ondas) |
| Missões laterais do Infinito | 🟡 | só existem no host. O cliente não as vê; ficou bloqueado de propósito, para não criar missões locais divergentes |
| Loja aberta segurando o intervalo | 🟡 | só a loja do **host** segura o fim do intervalo |
| Soldados, Gritador, Tanque, chefes | ⛔ | só `ZombieAI` comum é replicado. As variantes aparecem com a aparência certa, mas habilidades como o grito e a quebra de barricada não estão sincronizadas |
| Incapacitação, reanimação, espectador, game over cooperativo | ✅ (mesmo PC) | ver §5 |
| Mundo (portas, energia, baús, itens, materiais, bancadas) | ⛔ | local em cada máquina: risco de loot duplicado |
| Economia validada pelo host (créditos, compras, munição, fabricação) | ⛔ | hoje cada jogador tem a sua economia local |
| HUD de equipe (vida dos aliados, distância, ping, aliados no minimapa) | ✅ (mesmo PC) | ver §5.2; layout conferido só nas capturas do host |
| Teste em dois PCs (LAN/Radmin/Hamachi) | ⛔ **bloqueado** | precisa de um segundo computador |

## 2. Mudanças deste marco

### 2.1 Timeout (`NetSession.cs`)
- `DisconnectTimeoutMS = 10000` e `HeartbeatTimeoutMS = 500`.
- Um jogo que parou de responder sai da sala em ~10 s.
- O heartbeat a cada 0,5 s mantém vivo um link saudável, mesmo quieto (VPN incluída).
- **Medido**: cliente encerrado à força às 17:12:24.7. O host ainda tinha 2 jogadores às 17:12:32.3 e tinha 1 às 17:12:37.4, ou seja, a remoção aconteceu **entre 7,6 e 12,7 s** depois.
- **Não testado**: VPN com perda de pacotes. Não houve ferramenta de simulação de rede nesta sessão.

### 2.2 Ondas no cliente (`NetGame.cs`, `EnemySpawner.cs`)
- **No host**: `NetGame.Waves` (`WaveState`: onda, flags, restantes, segundos de intervalo) é atualizada todo quadro, mas só vai pela rede quando muda; o intervalo, por exemplo, muda uma vez por segundo.
- **No cliente**: `EnemySpawner.Mirror(...)` recebe esse estado. O spawner continua **desligado**, e nada nasce no cliente.
  - O HUD (`CombatHUD`, `CampaignHUD`) lê o spawner como sempre.
  - Os eventos `WaveStarted` e `WaveCleared` disparam no cliente: banner de onda, som, recompensa e cura de intervalo.
- `EndlessDirector` não cria missões no cliente.

## 3. Testes executados (editor como host + build de desenvolvimento como cliente, 127.0.0.1)

| Teste | Resultado |
|---|---|
| Contagem antes da onda 1 no cliente | ✅ "intervalo 5…0 s" |
| Onda 1 ativa no cliente com restantes | ✅ "onda 1 ativa, restantes 6 → 5 → … → 1" |
| Fim da onda no cliente | ✅ intervalo de 45 s; pontos 375 → 675 (+300 da onda), créditos 50 → 110 (+60) |
| Abates do cliente validados pelo host | ✅ 6/6 |
| Desconexão à força | ✅ removido em 7,6–12,7 s |
| Console do host | ✅ sem erros. Só o aviso esperado de "moveu rápido demais" no reposicionamento do início |

## 4. Próximos passos do Marco 2 (ordem)

1. HUD de equipe: lista com vida dos aliados, ping, distância e aliados no minimapa.
2. Incapacitação, reanimação (5 s, validada pelo host), espectador e game over cooperativo.
3. Replicação do Gritador (grito uma vez por evento, só no host), do Tanque, dos soldados e dos chefes.
4. Mundo com IDs estáveis: portas, energia, baús e itens.
5. Economia e fabricação validadas pelo host.
6. **Você**: teste em dois PCs (ver `10_PENDENCIAS.md`).

## 5. Marco 2 (segunda execução): HUD de equipe, queda, reanimação, espectador e fim de jogo cooperativo

### 5.1 Regras implementadas

| Regra | Valor |
|---|---|
| Golpe que mataria | vira **queda** (no chão, ignorado pelos infectados, invulnerável, sem regeneração) |
| Quedas por onda | 2; o golpe depois da 2ª queda é morte |
| Tempo até sangrar | 30 s na 1ª queda, 20 s na 2ª |
| Reanimar | segurar **E** a até 2,5 m por **5 s**; volta com 30% da vida |
| Quem decide | **o host**: confere o ID de quem pediu (dado pela rede), os estados dos dois, a distância no começo e no fim e o tempo mínimo; cancela se quem reanima se afasta ou cai; um aliado por vez |
| Morte | espectador: a câmera segue um aliado de pé (clique ou gatilho direito troca); não atira nem interage |
| Volta | **no início da próxima onda**, decidido pelo host; as quedas zeram |
| Fim de jogo | quando **ninguém está de pé**, o host manda todos para a tela de fim de jogo |
| Vidas | no cooperativo não há vidas individuais (o painel VIDAS some) |

### 5.2 HUD de equipe (`Network/TeamHUD.cs`)

- **Painel por aliado** (até 3): nome, barra de vida e, conforme o estado:
  - de pé: distância e ping;
  - caído: "CAÍDO 23 s";
  - morto: "MORTO".
- **Conexão**: canto superior esquerdo, "HOST · N jogadores" no host e "Conexão 45 ms" no cliente (verde/âmbar/vermelho).
- **Mensagens no centro, acima da etiqueta de nome**:
  - "VOCÊ CAIU";
  - "X está te reanimando", com barra;
  - "Reanimando X…", com barra;
  - "ESPECTADOR · seguindo X".
- **Aliados no minimapa** (azul, presos à borda quando longe) e **no mapa tático**. Eles não contam no "% explorado".
- **Etiqueta de nome** do aliado: "CAÍDO 23 s" ou "MORTO", e desce até o corpo quando ele cai.

### 5.3 Arquivos

- **Novo**: `Network/TeamHUD.cs`.
- **Alterados**:
  - `Network/NetPlayer.cs`: `CoopCondition`, variáveis de vida, RPCs com validação, ping medido pelo host, respawn na onda;
  - `Network/NetPuppet.cs`: interação de reanimar, alvo ignorado quando caído, etiqueta, marcador no mapa, raiz da câmera do espectador;
  - `Network/NetGame.cs`: respawn na onda nova, fim de jogo da equipe;
  - `Network/NetMatch.cs`;
  - `PlayerCombat.cs`: `EnterDowned`, `ExitDowned`, `KillNow`, `RespawnNow`;
  - `Health.cs`: `PreventDeath`, `Untargetable`, `Revive`, sem regeneração caído;
  - `Horror/EnemyPerception.cs`: ignora alvos `Untargetable`;
  - `RagdollController.cs`: `BodyPosition`;
  - `GameFlow.cs`: sem vidas no cooperativo, `CoopGameOver`;
  - `CombatHUD.cs`;
  - `Exploration/MapMarker.cs`, `MinimapView.cs`, `TacticalMapView.cs`: tipo `Ally`; marcadores que saíram somem do mapa.

### 5.4 Testes (editor como host + build de desenvolvimento como cliente, mesmo PC)

O cliente de teste usa `-net-revive`: vai até o aliado caído e faz as mesmas chamadas que o HUD faz com E pressionado. **Segurar a tecla E de verdade não foi testado** (as teclas simuladas não chegam ao jogo nesta sessão).

| Teste | Resultado |
|---|---|
| Host cai (golpe letal) | ✅ Downed, 1 HP, relógio de 30 s no cliente |
| Cliente reanima o host | ✅ (2ª build) progresso 13% → 93% em 5 s, host de pé. Na 1ª build **falhou**: o teste pedia a reanimação no mesmo quadro do teletransporte, e o host via a posição antiga e recusava. O teste passou a esperar 0,5 s |
| Host reanima o cliente | ✅ host aceitou (distância 1,2 m), cliente de pé |
| Sangrar até o fim | ✅ "Host morreu" depois de 30 s; 2ª queda depois de 20 s |
| Espectador | ✅ câmera do host seguindo `Puppet_Amigo/SpectateRoot`; captura `coop_espectador_host.png` |
| Volta na próxima onda | ✅ onda 2: host de pé, 100 HP, câmera de volta, quedas zeradas |
| Equipe inteira caída → fim de jogo | ✅ `GameFlow` = GameOver no host; captura `coop_fim_de_jogo_host.png` |
| Regeneração enquanto caído | ❌ → corrigido: subia de 1 para 60 HP no chão; agora não regenera |
| Console do host | ✅ sem erros de jogo (os NullReference do NGO só aparecem quando o código recompila com o Play aberto) |

**Observação não resolvida**: na 1ª build, uma vez, o host caído foi registrado como morto cerca de 1 s depois da queda, com zumbis da onda 2 por perto. **Não se repetiu** na 2ª build. Agora cada morte registra o motivo no log (`[Net] X morto: …` e `[Net] morte local: …`), para diagnosticar se voltar.

**Não testado**:
- 3 ou 4 jogadores;
- dois PCs;
- a tela de fim de jogo no **cliente** (o comando é o mesmo RPC para todos, mas só vi a do host);
- gamepad no espectador.
