# Fase 3 — Animações

## 1. Arquitetura

O jogador usa **um Animator** (Humanoid) para o corpo e **poses procedurais com IK** para tudo que envolve armas. Isso já era assim e foi mantido: o projeto não tem clipes de arma, e a pose procedural é o que mantém mãos, mira e cano coerentes.

| Responsável | Controla | Arquivo |
|---|---|---|
| `ThirdPersonController` | locomoção (Speed, MoveX/MoveY), pulo, queda, agachar, prone, rolagem, estamina | `Scripts/ThirdPersonController.cs` |
| `ClimbingSystem` | pendurar e subir, IK das mãos na borda | `Scripts/ClimbingSystem.cs` |
| `WeaponHolder` | pose da arma, IK das mãos, recarga, recuo, **guardar/sacar/pegar** (`WeaponHolder.Gear.cs`) | `Scripts/Combat/WeaponHolder*.cs` |
| `PlayerCombat` | decide mirar, atirar, trocar e interagir; prioridades entre ações | `Scripts/Combat/PlayerCombat.cs` |
| `LeanController` | inclinação lateral (Q/E mirando) | `Scripts/Combat/Animation/LeanController.cs` |
| `HitReactor` | reações direcionais dos inimigos | `Scripts/Combat/Animation/HitReactor.cs` |
| `TacticalFlashlight` | feixe e balanço da lanterna | `Scripts/Combat/Lighting/TacticalFlashlight.cs` |

Cada parâmetro do Animator tem **um único dono**: só o `ThirdPersonController` escreve `Speed`, `MoveX`, `MoveY`, `Crouched` e `Prone`.

## 2. Estados e transições (Animator do jogador)

```
Base Layer
 ├─ Idle Walk Run Blend   1D (Speed): Idle 0 · Walk_N 2 · Run_N 6        ← hoje
 │                        2D (MoveX, MoveY) quando houver clipes Mixamo   ← Phase 3 / 6
 ├─ Crouch (2D)           só existe com os clipes de agachar; Crouched=true
 ├─ Prone (1D Speed)      só existe com os clipes de deitar; Prone=true
 ├─ JumpStart → InAir → JumpLand (Jump, FreeFall, Grounded)
 ├─ Hang ↔ ClimbUp        (Hanging, ClimbUp)
 └─ Roll                  (trigger Roll)
```

O Animator 2D é montado por **Tools ▸ Combat ▸ Phase 3 ▸ 6 - Build Locomotion Animator**:
- A ferramenta copia o controller do Starter Assets para `Animation/Player_Locomotion.controller` e o atribui **só ao jogador**; os soldados continuam no original.
- Se faltar `Walk_Back`, `Strafe_Left` ou `Strafe_Right`, ela lista o que falta e não altera nada.

## 3. O que foi implementado nesta fase

| Recurso | Como | Estado |
|---|---|---|
| Lanterna tática | `TacticalFlashlight`: presa no peito, aponta para onde a câmera mira, atraso e balanço por velocidade (andar 0,8°, correr 2,4°, mirar 0,15°), acompanha a rolagem, íris automática perto de paredes, cookie com centro forte, luz de preenchimento sem sombra, não ilumina o próprio jogador | **Concluído e testado** |
| Guardar / sacar / trocar | `WeaponHolder.Gear`: a arma viaja da mão ao coldre (pistola: quadril direito; fuzis/escopeta/sniper: costas na diagonal; submetralhadora: peito, configurável) e vice-versa. A mão direita segue o cabo; a de apoio solta cedo e volta no fim. O mesmo modelo muda de lugar: nunca há duplicata nem sumiço. Disparo bloqueado até "Ready". Interrupção (rolar, escalar, morrer, largar) leva ao estado final na hora | **Concluído e testado** (troca AK ↔ M1911: 2 modelos, sem duplicata) |
| Pegar arma do chão | mesma sequência, partindo da pose do objeto no mundo; o quadril desce um pouco (IK nos pés) | **Concluído e testado** |
| Sprint com arma abaixada | já existia (`low ready`, `_sprintBlend`); a arma **não** vai para o coldre num sprint curto | Mantido |
| Inclinação (Q/E) | `LeanController`: só mirando com arma. O pivô da câmera desliza 0,42 m; a mira em 1ª pessoa usa esse pivô, então visão, arma e linha de tiro andam juntas; rolagem de 7°; tronco inclinado por `bodyRotation` + pés presos por IK; um *sphere cast* limita a inclinação pela parede | **Concluído e testado** |
| Rolagem | já era direcional; agora o corpo gira suavemente até a direção nos primeiros 0,12 s (antes era instantâneo); i-frames continuam no estado de jogo (`IsRolling`) | Concluído |
| Locomoção 2D (strafe, ré, diagonais) | parâmetros `MoveX`/`MoveY` em m/s enviados pelo controlador; ferramenta que monta o Blend Tree 2D | **Infraestrutura pronta, asset pendente** |
| Agachar animado | estado `Crouch` + `Crouched`; com ele o falso agachamento por IK desliga | **Infraestrutura pronta, asset pendente** (hoje: IK no quadril, que já existia) |
| Prone (deitar) | segurar C (ou ↑ no direcional) por 0,6 s; cápsula de 0,55 m, 0,7 m/s, sem sprint/rolagem/pulo, só levanta se houver espaço | **Infraestrutura pronta, asset pendente.** Fica **desligado** enquanto o Animator não tiver o parâmetro `Prone` (regra do pedido: só ativar com animações adequadas) |
| Reações dos inimigos | `HitReactor`: o tronco é empurrado na direção do tiro e volta por mola; dano ≥ 45 em 0,5 s ou headshot = cambaleio. Só na fase de IK: IA, NavMesh e tempo de ataque intactos | Concluído (verificado visualmente em 1 caso) |
| Ragdoll | no máximo 10 corpos simulando ao mesmo tempo (os mais antigos congelam); corpo parado por 3,5 s congela; `maxDepenetrationVelocity` 2,5 evita "explosões" | Concluído |
| Dessincronia dos zumbis | já existia (`CycleOffset` aleatório, `WalkSpeedScale` pela velocidade real, golpe sincronizado com `AttackHitTime`) | Mantido |

## 4. Regras de interrupção (prioridade da maior para a menor)

| Ação em andamento | Morte | Rolar | Escalar | Trocar arma | Recarregar | Mirar | Atirar | Inclinar |
|---|---|---|---|---|---|---|---|---|
| **Morte** | — | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **Rolagem** | cancela | — | ✗ | conclui na hora | interrompe sem pôr munição | cancela | ✗ | cancela |
| **Escalada** | cancela | ✗ | — | conclui na hora (arma vai às costas) | interrompe | ✗ | ✗ | ✗ |
| **Guardar/sacar** | conclui na hora | conclui na hora | conclui na hora | 2º pedido fica na fila por 0,35 s | cancela (sem munição) | permitido, mas sem disparo | ✗ até Ready | ✗ |
| **Recarga** | cancela | cancela | cancela | cancela; **munição só entra no `FinishReload`** | — | sai da mira | escopeta pode cancelar para atirar | permitido |
| **Inclinar** | cancela | cancela | cancela | Q/E não trocam/interagem enquanto inclina | permitido | exige mira | permitido | — |
| **Prone** | cancela | ✗ | ✗ | permitido | permitido | permitido | permitido | ✗ |

Nenhum estado depende de um evento de animação para terminar: todos têm tempo próprio ou são concluídos à força. Por isso o personagem não fica preso numa ação inválida.

## 5. IK

- **Mãos na arma**: `WeaponHolder.OnAnimatorIK`. Mão direita no `GripPoint`; esquerda no `SupportPoint` (ou no carregador/bolsa durante a recarga), com cotovelos guiados por *hints*. Durante guardar/sacar, o peso da mão de apoio vai a 0 e volta.
- **Pés**: no agachar, no inclinar e no pegar do chão, as posições animadas dos pés são presas por IK enquanto o quadril se move.
- **Cabeça e tronco**: `SetLookAt` mirando no ponto de mira, com peso menor no sprint.
- **Pesos graduais**: nada liga em 0 → 1 de uma vez; tudo passa por `Smooth`/`MoveTowards`. Na rolagem e na escalada o IK da arma vai a 0 (`Holstered`).
- **Animation Rigging** não foi instalado: o IK nativo do Humanoid cobre o necessário sem trazer um segundo sistema.

## 6. Configuração no Inspector

| Componente | Campo | Padrão | Observação |
|---|---|---|---|
| `TacticalFlashlight` | Intensity / Range / OuterAngle / InnerAngle | 320 / 40 / 54 / 26 | o alcance útil é ~25 m; o URP atenua até o *range* |
| | Spill Intensity / Range / Ahead | 4,5 / 8 / 1,8 | |
| | FullPowerDistance | 11 | íris automática |
| | Chest Offset | (0,16; 0,08; 0,26) | lado direito do peito |
| `WeaponHolder` | StowTime / DrawTime / GrabTime | 0,32 / 0,4 / 0,6 s | |
| | HolsterOffset / BackOffset / ChestOffset | ver script | sockets no quadril e no peito (ossos Hips e UpperChest) |
| | SubmachineGunOnChest | ligado | |
| `LeanController` | Distance / SpineAngle / ViewRoll | 0,42 m / 16° / 7° | |
| `ThirdPersonController` | ProneSpeed / ProneHeight / ProneHoldTime | 0,7 / 0,55 / 0,6 | só com clipes de prone |
| `HitReactor` | StaggerDamage / MaxBend / StaggerBend | 45 / 22° / 32° | |
| `RagdollController` | SettleTime / MaxSimulated | 3,5 s / 10 | |

## 7. Pendências de animação

A lista completa de clipes está em `FASE3_ASSETS.md`, seção 3.1. Sem esses clipes, os itens a seguir **não** devem ser considerados acabados visualmente:
- strafe e ré;
- partida e parada;
- agachar andando;
- prone;
- interações físicas com clipe próprio (abrir porta, usar painel, aplicar kit médico);
- identidade de movimento dos zumbis especiais;
- soldados com clipes de arma.
