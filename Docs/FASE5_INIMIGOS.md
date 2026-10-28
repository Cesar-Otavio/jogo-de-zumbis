# Fase 5 — Inimigos

**Estado: não iniciado nesta entrega.** O pedido manda começar pelos objetivos A–F; os inimigos novos são o Marco 5.

## 1. O que já existe e foi preservado

Zumbi comum, Corredor, Tanque, Gritador, Rastejante (`ZombieVariant`), soldados (`EnemyShooterAI`), Experimento 07 e Aurora (`BossDefinition`).

## 2. Plano para o Marco 5 (com o que o código já oferece)

| Inimigo | Base técnica | O que precisa de código novo | Asset necessário |
|---|---|---|---|
| Infectado blindado | `ZombieVariant` + `Hitbox` (já há multiplicador de cabeça) | proteção por parte do corpo: o colete reduz o dano no tronco e se quebra depois de X dano; cabeça e pernas expostas | material de colete/capacete; o ideal é um modelo de zumbi com equipamento |
| Cuspidor contaminado | variante + projétil (o `ThrownItem` já tem física e área de efeito, como o molotov) | ataque à distância com preparação visível (som + pose), poça ácida temporária (`FireZone` adaptado), recarga | clipe de "cuspir" ou de arremesso; partícula verde |
| Perseguidor sombrio | `EnemyPerception` (já tem emboscada, `Ambusher`) | perder e retomar o rastro, recuar depois de atacar, som próximo característico, nunca atacar sem aviso | clipe de corrida agachada; sons |
| Infectado de contenção | variante + `FireZone` adaptado | nuvem de contaminação temporária ao morrer (dano leve por segundo, visível) | roupa de contenção (material) |
| Variantes ambientais (soldado, manutenção, pesquisador, segurança, civil) | `ZombieVariant.Tint` + materiais | trocar o material do corpo por variante | texturas de roupa; o pacote Survivalist tem uniformes militares |

**Regras para todos** (pedido, seção 11.7): usar a percepção, o ruído, o NavMesh, o dano e a dificuldade existentes, participar das ondas, e não criar um framework de IA novo.

**Risco**: sem clipes próprios, os inimigos novos repetiriam as animações atuais. Isso iria contra "não considerar mudança de cor como inimigo novo". Os clipes devem vir antes.
