# Fase 7 — Estado inicial (auditoria)

Data: 25/10/2026. Levantado direto do projeto, antes de qualquer alteração desta fase.

## 1. Controle de versão

| Item | Encontrado |
|---|---|
| Branch ativa | `fase6-multiplayer`, árvore **limpa** (nenhuma alteração pendente) |
| Último commit | `debc258` (relatório completo). O relatório citava `de51e09`, mas `debc258` é o commit dele logo depois |
| Checkpoint criado | tag **`fase7-checkpoint`** = `debc258` |
| Branch de trabalho | **`fase7-definitive`**, criada a partir do checkpoint |
| `main` | desatualizada (antes da Fase 3). **Não houve merge**: a proposta de integração está em `10_PENDENCIAS.md` |
| Para voltar | `git checkout fase7-checkpoint` |

## 2. Projeto

| Item | Valor |
|---|---|
| Unity | 6000.6.7f1, URP 17.6 (Forward+) |
| Pacotes | os mesmos do relatório (NGO 2.13.3, Transport 6.6.0, Input System 1.20.1, Cinemachine 6.6.0, uGUI 2.6.0, AI Inference 2.6.1 **sem uso**, IET Framework **sem uso**) |
| Cenas na build | `MilitaryBase_Combat` e **`GetStarted_Scene`** (sobra do template, incluída na build) |
| Código próprio | 117 scripts, ~28.500 linhas (antes desta fase) |
| Build existente | `Builds/Win64` (release, Fase 6) |

## 3. Iluminação da cena (medida no editor)

| Grupo | Quantidade | Detalhe |
|---|---|---|
| Luzes no total | 155 (143 ativas) | |
| **"Spot Light" do pacote de mapa**, uma por poste | **53** | marcadas como **Baked, com sombra Hard**, mas a cena **não tem lightmaps** (`LightmapSettings.lightmaps = 0`, `bakingOutput.isBaked = false`). Na prática renderizam **em tempo real com sombra** |
| "LampLight" (criada na Fase 2, rede elétrica e oscilação) | 53 | realtime, sem sombra, range 26 m, intensidade 70 |
| Luzes de status de dispositivos, galpões, laboratório | ~35 | realtime, sem sombra |
| Lua (direcional) | 1 | Mixed, sombra Soft |
| Reflection probes / Occlusion culling | 0 / não assado | |

**Consequências observadas:**
- Cada poste tinha **duas** luzes. A do pacote (fraca, intensidade 2) projetava sombra e **não era desligada nos apagões**, porque não pertence à `LightFlicker` nem à `PowerGridManager`.
- Isso explica o aviso da URP visto na Fase 5: ela reduzia a resolução das sombras para caber 41–46 sombras no atlas.

## 4. Qualidade gráfica

| Item | Encontrado |
|---|---|
| Níveis de qualidade | `Mobile` (Mobile_RPAsset) e `PC` (PC_RPAsset) |
| No Windows | **`Mobile` estava excluído do Standalone**, então a build tinha **um único nível**. A opção QUALIDADE do menu não tinha o que trocar, e o perfil "baixo" não existia de verdade |
| PC_RPAsset | escala 1,0, HDR, sombra principal 2048 com 4 cascatas, sombras adicionais 4096, sombras suaves de alta qualidade, **SSAO** no renderer |
| Frame Timing Stats | desligado (sem tempos de CPU/GPU em build) |

## 5. Multiplayer (ponto de partida)

Igual ao relatório de 25/10:
- MVP de 2 jogadores testado no mesmo PC;
- só zumbis comuns são replicados;
- o mundo, a economia, os soldados, os chefes e o revive não são sincronizados;
- o timeout de desconexão é o padrão do transporte (~30 s);
- o cliente não mostra o número da onda nem o intervalo.

## 6. Ferramentas de medição disponíveis nesta sessão

| Ferramenta | Situação |
|---|---|
| Build de desenvolvimento + `ProfilerRecorder` / `FrameTimingManager` dentro do jogo | **usada** (`PerfProbe`, ver `01_PERFORMANCE.md`) |
| Unity Profiler, Frame Debugger e Rendering Debugger (janelas) | precisam de alguém operando a interface; **não usados**. A sonda grava os mesmos contadores e marcadores |
| `Recorder` no editor | não registrou amostras nesta sessão (sempre 0), por isso as medições foram feitas na build |
| Memory Profiler | não instalado; a memória vem dos contadores `Total Used Memory` e `GC Used Memory` |
