# Fase 7 — Desempenho (Marco 1)

## 1. Como foi medido

- **Hardware**: AMD Ryzen 7 5700U (8 núcleos/16 threads), **GPU integrada AMD Radeon** (Direct3D 11, 5 GB compartilhados), 10 GB de RAM. Tela 1920×1080.
  - **Nada aqui vale para outras GPUs.** Não houve teste em GPU dedicada.
- **Sonda `PerfProbe`** (`Scripts/Combat/Diagnostics/PerfProbe.cs`): roda **dentro da build** quando o jogo é aberto com `-perf-run`.
  - Coloca o jogo numa situação fixa:
    - **pátio**: Modo Infinito sem inimigos;
    - **onda**: 30 infectados gerados pelo `EnemySpawner.SpawnPressure`, perseguindo o jogador invulnerável.
  - Espera 10 s e grava todos os quadros por 20 s. Depois escreve `summary.csv`, um `.txt` com o detalhe e uma captura de tela.
- **O que ela mede**:
  - **tempos**: `FrameTimingManager` (CPU da thread principal, thread de render e GPU);
  - **contadores** (`ProfilerRecorder`): draw calls, SetPass, triângulos, shadow casters, GC por quadro, memória;
  - **marcadores da thread principal**: scripts, física, animação, NavMesh, UI, culling;
  - **tempo de GPU por passe**: sombras, SSAO.
  - Contadores e marcadores só existem na **build de desenvolvimento**; a build release mede só os tempos.
- **Builds**: *Tools ▸ Combat ▸ Phase 7 ▸ 1* (desenvolvimento, `Builds/Win64Dev`) e *Phase 7 ▸ 2* (release, `Builds/Win64`).
- **Limitações**:
  - Cada cenário rodou **uma vez** (20 s). A variação entre execuções iguais ficou em torno de ±10%.
  - 1440p não foi testado: a tela é 1080p, e uma janela maior que a tela não renderiza na resolução pedida.
  - O tiroteio não entra na carga: o jogador não atira na onda.
  - O editor ficou aberto ao fundo durante as medições.
  - Os dados brutos estão em `Docs/FASE7/medicoes/`.

## 2. Linha de base (antes das correções, build de desenvolvimento)

| Cenário | Resolução | Média (ms) | p95 | CPU principal | GPU | Draw calls | SetPass | Luzes com sombra visíveis |
|---|---|---|---|---|---|---|---|---|
| Pátio, sombras 30 m | 1280×720 | 10,44 | 12,36 | 10,00 | 7,17 | 945 | 194 | 29,6 |
| Onda, sombras 30 m | 1280×720 | 13,46 | 15,34 | 13,39 | 9,00 | 994 | 207 | 34,0 |
| Pátio, sombras 50 m | 1920×1080 | 16,90 | 19,63 | 10,20 | **16,36** | 1012 | 196 | 30,9 |
| Onda, sombras 50 m | 1920×1080 | 16,02 | 17,35 | 14,19 | 11,41 | 1314 | 243 | 34,3 |
| Pátio, sombras 90 m | 1920×1080 | 15,26 | 15,81 | 11,06 | 14,99 | 2227 | 346 | 35,0 |
| Onda, sombras 90 m | 1920×1080 | 17,60 | 19,41 | 15,47 | 14,36 | 2292 | 350 | 34,4 |
| Onda, opções atuais | 1280×720 | 19,23 | 24,76 | 15,27 | 10,77 | 1315 | 240 | 34,6 |

Nessa linha de base **só existia um nível de qualidade (PC)**. Os perfis "baixo/médio/alto" mudavam só a distância das sombras e a qualidade dos efeitos.

### 2.1 Onde ia o tempo (onda de 30, 720p, build de desenvolvimento)

| Parte da thread principal | ms por quadro |
|---|---|
| Preparação da renderização (`FinishFrameRendering`) | 5,37 |
| — culling de sombras de luzes locais | 1,87 |
| — desenho das sombras | 1,68 |
| Física (`PhysicsFixedUpdate`) | 2,76 |
| Scripts (`Update`) | 2,12 |
| Corrotinas + chamadas atrasadas | ~2,2 |
| Animação (Director/Animators) | ~1,0 |
| UI (canvases) | 0,84 |
| NavMesh + IA | ~1,4 |

- **GPU**: 8,7 ms no total. SSAO 1,1 ms; sombra da lua 0,7 ms; sombras adicionais 0,5–1,0 ms.
- **Leitura**:
  - **Em 720p o gargalo é a CPU da thread principal**, e não a GPU. A hipótese inicial ("sombras dos postes na GPU") só se confirmou em parte.
  - As sombras dos postes custavam mais em **CPU** (culling + desenho, ~3,5 ms) do que em GPU.
  - **Em 1080p** CPU e GPU ficam parecidas, as duas entre 10 e 16 ms.

## 3. Gargalo 1 corrigido: luzes dos postes

### 3.1 Causa
- Cada um dos 53 postes tinha uma "Spot Light" do pacote, marcada como Baked, com sombra Hard.
- A cena nunca foi assada, então elas rodavam **em tempo real com sombra**:
  - ~34 sombras visíveis por quadro;
  - o dobro de luzes;
  - **fora da rede elétrica**: continuavam acesas no apagão.
- A luz que ilumina de fato é a "LampLight" (intensidade 70 contra 2), que segue a energia e a oscilação.

### 3.2 Correção
- *Phase 7 ▸ 3* desliga as 53 luzes do pacote. O componente fica na cena, só desligado: é reversível.
- **`LampShadowBudget`**: dá sombra suave só às lâmpadas **acesas** mais próximas da câmera:
  - quantidade pela opção de distância de sombras (30/50/70/90 m → 1/2/3/4 lâmpadas);
  - nenhuma se o nível de qualidade não suporta sombras adicionais (Baixo);
  - histerese de 20%, para a sombra não piscar entre dois postes;
  - lâmpada apagada (apagão, queimada) perde a sombra.
- **Comparação visual** (A/B no mesmo ponto, mesma sessão): a imagem quase não muda; um pouco menos de luz perto de alguns postes. As sombras dos postes próximos passam a vir da luz forte.
- **Ganho colateral**: no apagão, os postes agora apagam por inteiro.

### 3.3 Resultado (mesmos cenários, build de desenvolvimento)

| Cenário | Antes (ms) | Depois (ms) | GPU antes → depois | Luzes com sombra |
|---|---|---|---|---|
| Pátio 720p, sombras 30 m | 10,44 | **9,21** | 7,17 → 5,12 | 29,6 → 3 |
| Onda 720p, sombras 30 m | 13,46 | **12,29** | 9,00 → 8,35 | 34,0 → 3 |
| Pátio 1080p, sombras 50 m | 16,90 | **12,50** | 16,36 → 12,28 | 30,9 → 4 |
| Onda 1080p, sombras 50 m | 16,02 | **14,67** | 11,41 → 11,51 | 34,3 → 4 |
| Pátio 1080p, sombras 90 m | 15,26 | **13,73** | 14,99 → 13,23 | 35,0 → 6 |
| Onda 1080p, sombras 90 m | 17,60 | **15,57** | 14,36 → 12,38 | 34,4 → 6 |
| Onda 720p, opções atuais | 19,23* | **12,27** | 10,77 → 8,52 | 34,6 → 4 |

\* Essa execução teve um travamento isolado de 3 s. A mediana passou de 14,15 para 12,61 ms.

SetPass caiu de 194–350 para 157–203, e os shadow casters caíram pela metade.

## 4. Correção 2: níveis de qualidade reais

### 4.1 Causa
O nível `Mobile` estava excluído do Windows. A build tinha **um único nível de qualidade**, então a opção QUALIDADE não fazia nada.

### 4.2 Correção (`ProjectSettings/QualitySettings.asset`)

| Nível | Pipeline | Principais diferenças |
|---|---|---|
| **Baixo** | `Mobile_RPAsset` | escala de render 0,8 com FSR, sombra da lua 1024 com 1 cascata, **sem sombras de luzes adicionais**, sem sombras suaves, **sem SSAO** |
| **Médio** (novo) | `Medium_RPAsset` | escala 1,0, sombra da lua 2048 com 2 cascatas, sombras adicionais 2048, sombras suaves de baixa qualidade, **sem SSAO** |
| **Alto** (padrão no Windows) | `PC_RPAsset` | o de antes: 4 cascatas, sombras adicionais 4096, sombras suaves altas, SSAO |

- **Saves**: a qualidade passou a ser salva na chave `QualityLevel`. A antiga (`Quality`) guardava o índice de uma lista de um item só, que agora apontaria para "Baixo".
- **Na primeira abertura**: quem tinha save antigo começa no padrão (Alto). As outras opções não mudam.

### 4.3 Resultado por nível

| Cenário | Build | Média (ms) | FPS | p95 | CPU principal | GPU |
|---|---|---|---|---|---|---|
| **Baixo**, 720p, pátio | dev | 7,44 | 134 | 8,63 | 7,35 | 4,76 |
| **Baixo**, 720p, onda de 30 | dev | 10,94 | 91 | 12,53 | 10,90 | 6,53 |
| **Médio**, 1080p, pátio | dev | 9,38 | 107 | 10,59 | 8,35 | 4,31 |
| **Médio**, 1080p, onda de 30 | dev | 12,92 | 77 | 14,19 | 12,58 | 7,99 |
| **Alto**, 1080p (sombras 90 m), pátio | dev | 12,80 | 78 | 13,41 | 9,08 | 12,55 |
| **Alto**, 1080p (sombras 90 m), onda de 30 | dev | 17,28 | 58 | 20,76 | 14,99 | 13,65 |
| **Baixo**, 720p, onda de 30 | **release** | **9,14** | **109** | 10,55 | 8,99 | 5,63 |
| **Médio**, 1080p, pátio | **release** | **8,74** | **114** | 9,65 | 6,73 | 7,57 |
| **Médio**, 1080p, onda de 30 | **release** | **11,80** | **85** | 13,50 | 10,16 | 6,57 |

**Comparação com a medição antiga do editor** (Fase 5: ~27–28 ms, ~36 FPS, 1440p na Game View): o editor custa muito mais que a build. Os números não são comparáveis um a um.

## 5. Estado das metas (neste hardware)

| Meta | Situação |
|---|---|
| iGPU: perfil de baixo custo estável a 720p/900p | **Atingida neste PC**: Baixo 720p com 30 infectados = 9,1 ms em release (p95 10,6). Não testado em 900p |
| 60 FPS estáveis em 1080p | **Atingida no Médio** (release, 11,8 ms, p95 13,5). No Alto com sombras de 90 m, a média fica em 17,3 ms (dev), no limite |
| GPU dedicada | **não testada** |
| Host com IA + rede | **não medido** com a sonda. No teste cooperativo o host foi o editor |

## 6. Próximos gargalos medidos (não corrigidos ainda)

1. **CPU da thread principal com muitos inimigos** (10–15 ms com 30 infectados):
   - física 1,1–2,8 ms (sincronização de transforms dos ossos e ragdolls);
   - scripts ~1,9 ms;
   - animação ~1,2 ms;
   - NavMesh/IA ~1,4 ms;
   - UI 0,6–0,8 ms.
   - O próximo passo é o detalhe por script com o Profiler (janela) ou marcadores próprios em `ZombieAI`, `EnemyPerception` e `CombatHUD`.
2. **SSAO** (~1,1 ms de GPU em 720p) no Alto: avaliar deixar opcional.
3. **Picos**: máximos de 25–38 ms em quase todas as execuções e um de 3 s. Investigar compilação de shaders na primeira vez (aquecer variantes) e GC.
4. **GetStarted_Scene e AI Inference (Sentis)** ainda entram na build (451 MB release). A limpeza vai em seguida, com verificação de referências.

## 7. Marco 1, segunda parte: limpeza e picos de quadro

### 7.1 Limpeza segura

| Item | O que foi feito | Por quê |
|---|---|---|
| `GetStarted_Scene` | **desligada da lista de cenas da build** (o arquivo continua no projeto) | sobra do template, não é usada pelo jogo |
| AI Inference (Sentis) | **continua instalado**; os 107 shaders e compute shaders dele são **descartados só da build** (`Editor/BuildStripping.cs`) | o AI Assistant, a ponte usada para operar a Unity nesta sessão, pode precisar do pacote; nenhum pacote declara dependência dele, mas desinstalar seria um risco sem ganho para o jogo |
| Pasta do robô Timmy e template | **não removidas** | as pegadas do jogador usam 11 sons de `TimmyRobot/Sfx` (verificado com `AssetDatabase.GetDependencies` da cena) |

- **Efeito medido**:
  - `resources.assets` da build caiu de 9,3 MB para 148 KB;
  - build de desenvolvimento: de 525 para 492 MB;
  - **build release: de 451 para 416 MB**.
- **Aviso**: os avisos de shader do Sentis ainda aparecem no log da build quando o cache é refeito. Eles vêm da importação; as variantes não entram no jogo.

### 7.2 Picos de quadro: causas encontradas

A sonda passou a registrar, em cada quadro acima de 25 ms, os marcadores que pesaram. Além disso, recebeu marcadores próprios no `Update` de 15 scripts do jogo (`ProfilerMarker`, custo desprezível fora da build de desenvolvimento).

| Causa | Evidência | Correção |
|---|---|---|
| **Sons carregados na primeira vez que tocam**: os 219 efeitos estavam com *Preload Audio Data* desligado e *Decompress On Load*, então o primeiro som de cada tipo era lido e descomprimido no meio do quadro | picos de 15–20 ms dentro de `ZombieAI.Update` quando os infectados chegavam ao jogador | *Phase 7 ▸ 4*: 209 efeitos pré-carregados com a cena; 5 músicas e ambientes longos (> 700 KB) em streaming. Memória: +~9 MB |
| **Silhueta de terror montada na hora**: cada evento clonava um zumbi inteiro e tirava os componentes com `DestroyImmediate` | `EnvironmentalEventDirector.Update` com 21,7 ms num quadro | a figura é montada uma vez no início e reaproveitada (só liga e desliga) |
| **Atlas dinâmico da fonte**: caracteres novos (acentos, setas) rasterizados durante o jogo | `PlayerUpdateCanvases` / `UGUI.Rendering.UpdateBatches` com 28 ms num quadro | `Campaign/TextWarmup.cs` adiciona os caracteres usados ao fallback no carregamento (só na build). O "◆" do feed e do objetivo, que não existe na fonte e aparecia como quadrado, virou "•" |

### 7.3 Resultado (onda de 30 infectados, 1920×1080, Médio)

| Execução | Quadros > 25 ms | Pior quadro | p99 |
|---|---|---|---|
| Antes (dev, 40 s) | 5 a 8 | 29,9 a **61,5 ms** | 14,7–15,3 ms |
| Depois, 1ª (dev, 40 s) | **1** (25,2 ms, ZombieAI no primeiro contato) | 25,2 ms | 14,7 ms |
| Depois, 2ª (dev, 40 s) | **0** | **15,9 ms** | 14,7 ms |

### 7.4 Build release final (mesmo PC)

| Cenário | Média | p95 | Pior quadro | CPU principal | GPU | Memória total |
|---|---|---|---|---|---|---|
| **Médio**, 1080p, onda de 30 | 11,06 ms (90 FPS) | 12,81 | **16,46** | 9,01 | 6,59 | 538 MB |
| **Baixo**, 720p, onda de 30 | 6,73 ms (149 FPS) | 7,77 | **14,02** | 6,71 | 3,90 | 528 MB |
| **Médio**, 1080p, pátio | 8,66 ms | 9,49 | **15,87** | 6,41 | 8,24 | 510 MB |

Na release, nenhum quadro passou de 17 ms nas três execuções de 30 s. Antes, a release tinha máximos de 23–27 ms.

### 7.5 Ainda não identificado

- O pico residual de ~25 ms no primeiro contato dos infectados aconteceu em 1 de 2 execuções de desenvolvimento.
- Ele fica dentro de `ZombieAI.Update`, mas ainda não sei em qual chamada. O próximo passo é pôr marcadores mais finos no ataque e no caminho.
