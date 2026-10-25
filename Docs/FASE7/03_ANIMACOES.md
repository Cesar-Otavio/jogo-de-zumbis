# Fase 7 — Animações (Marco 3)

**Estado: NÃO INICIADO nesta fase.** Pela ordem do plano, vem depois do multiplayer.

## O que já existe (verificado no relatório de 25/10)

- **Locomoção 2D**: os parâmetros `MoveX`/`MoveY`/`Crouched` são enviados pelo `ThirdPersonController`, e a ferramenta *Phase 3 ▸ 6* monta o Animator. **Faltam os clipes.**
- **Infraestrutura de prone** pronta, desligada sem clipes.
- **IK procedural** das armas, sockets de armas no corpo, inclinação, rolagem e escalada.

## Dependência externa

- Os clipes Mixamo da lista em `Docs/FASE3_ASSETS.md` §3.1, baixados pela **sua conta Adobe**:
  - formato *FBX for Unity*, *Without Skin*, 30 fps, *In Place* na locomoção;
  - destino `Assets/SourceFiles/Animation/Mixamo/`.
- Eu não acesso contas externas nem baixo assets sem autorização.

## Próximo passo quando os clipes chegarem

1. Rodar *Phase 3 ▸ 6*.
2. Testar as transições.
3. Medir o custo de animação com a `PerfProbe`. Hoje fica em ~1 ms com 30 infectados.
