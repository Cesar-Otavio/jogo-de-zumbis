# Fase 6 — Auditoria para o multiplayer

Data: 25/10/2026.

- **Branch**: `fase6-multiplayer`, criado a partir de `fase5-mundo`.
- **Checkpoint**: tag `fase6-checkpoint` (commit `5b3801c`).
- **Árvore**: estava limpa antes de começar.
- **Para voltar**: `git checkout fase6-checkpoint`.

## 1. Estado inicial

| Item | Encontrado |
|---|---|
| Unity | 6000.6.7f1 |
| Rede | **nenhum pacote** de rede no `manifest.json` e nenhum script de rede. O multiplayer não existia. |
| Cena | `MilitaryBase_Combat`: menu e jogo na mesma cena; voltar ao menu recarrega a cena |
| Jogador | `PlayerRobot.prefab` **colocado na cena** (não é instanciado). Personagem `Robot` com 22 componentes (controlador, input, armas, inventário, lanterna, inclinação, traje…), câmera própria e rig do Cinemachine |
| Expansões da Fase 5 | mochila, crafting, minimapa e mapa tático **existem** (estado local por jogador) |

## 2. Dependências de "um jogador só"

| Padrão | Ocorrências | Arquivos (principais) |
|---|---|---|
| `FindAnyObjectByType<PlayerCombat>()` e estáticos (`InventoryManager.Player`, `LeanController.Player`) | 36 | GameFlow, CombatHUD, CampaignHUD, CampaignManager, MissionManager, ShopManager, BossAI, EnemyShooterAI, EnemySpawner, ZombieAI, PowerGridManager, TensionDirector, EnvironmentalEventDirector, DialogueDirector, CinematicPlayer, ConsumablePickup |
| `Camera.main` | 14 arquivos | HUD, efeitos, mapa, lanterna, interações |
| Spawn local de inimigos | `EnemySpawner` (ondas), `MissionManager` (pressão), `BossAI` | |
| Pontuação, créditos e vidas de um jogador | `GameFlow`, `RunState` (estático) | |
| Objetos do mundo sem ID estável | baús, itens no chão, portas, geradores, barricadas | |

**Bom sinal**: a percepção dos inimigos (`EnemyPerception`) escolhe alvos percorrendo `Health.Living` por facção, e não pelo jogador. Qualquer personagem com `Health` do time dos jogadores vira alvo válido, sem mexer na IA.

## 3. Estratégia (para não quebrar o single-player)

- **Nada muda sem sessão**: `Net.Listening` / `Net.Active` são falsos e todos os sistemas seguem como antes.
- **O jogador continua sendo o da cena** em cada máquina: input, câmera, HUD, mochila, loja, mapa e lanterna ficam locais, com os 36 acessos intactos.
  - Cada jogador **publica** o estado do próprio personagem.
  - Nas outras máquinas, ele aparece como **boneco** (`NetPuppet`), com o modelo real.
- **O Host manda** em inimigos, ondas e dano.
  - Os clientes não criam inimigos (o `EnemySpawner` é desligado neles).
  - Os clientes veem **réplicas** dos zumbis do Host (`EnemyReplica`): o mesmo prefab e variante, com a IA retirada.
- **Interceptação de dano** (`Health.Intercept`):
  - um tiro do cliente numa réplica vira **pedido ao Host**, que valida distância, taxa e valor;
  - um golpe de zumbi no boneco de um jogador (no Host) vira **dano no dono**.
- **Migração em marcos**: só o necessário para o MVP agora (conexão, lobby, jogadores, uma arma, zumbis, dano e morte). Missões, mundo, loja e campanha ficam para os marcos seguintes.

## 4. Riscos

1. **Personagem do dono**: o movimento é decidido por cada jogador (resposta imediata). O Host só confere a velocidade e registra o que for implausível; não há correção.
2. **Mundo não sincronizado no MVP**: portas, geradores, energia, baús e itens no chão são locais em cada máquina. Há risco de estados diferentes e de loot duplicado entre jogadores.
3. **Soldados e chefes**: ainda não são replicados. No Modo Infinito os soldados aparecem a partir de certa onda, então os clientes não os veriam.
4. **Ondas no HUD do cliente**: o banner de onda e o intervalo vêm do `EnemySpawner` local, que está desligado.
5. **Game Over**: cada jogador tem as próprias vidas. Se o Host perde todas, as ondas param para todos.
6. **Testes reais em LAN e VPN** exigem dois computadores; nesta sessão só há um.
