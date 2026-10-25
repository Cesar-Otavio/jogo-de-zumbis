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
| Incapacitação, reanimação, espectador, game over cooperativo | ⛔ | |
| Mundo (portas, energia, baús, itens, materiais, bancadas) | ⛔ | local em cada máquina: risco de loot duplicado |
| Economia validada pelo host (créditos, compras, munição, fabricação) | ⛔ | hoje cada jogador tem a sua economia local |
| HUD de equipe (vida dos aliados, distância, ping, aliados no minimapa) | ⛔ | só existe a etiqueta de nome sobre o boneco |
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
