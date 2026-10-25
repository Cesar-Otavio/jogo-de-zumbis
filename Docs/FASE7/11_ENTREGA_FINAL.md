# Fase 7 — Relatório consolidado

## Marco 1 — Estabilidade e otimização (primeira execução)

### Identificação

| Item | Valor |
|---|---|
| Branch | `fase7-definitive` (a partir da tag `fase7-checkpoint` = `debc258`) |
| Commits | `e4a0db6` (sonda + luzes dos postes) e o commit desta entrega (níveis de qualidade, ondas no cliente, timeout, documentação) |
| Unity | 6000.6.7f1, URP 17.6 |
| Hardware de medição | Ryzen 7 5700U + Radeon integrada, tela 1080p |

### Funcionalidades

| Item | Estado |
|---|---|
| Sonda de desempenho em build (`PerfProbe`) + menus de build dev/release | **CONCLUÍDO E VALIDADO** (31 execuções) |
| Luzes duplicadas dos postes removidas + orçamento de sombras (`LampShadowBudget`) | **CONCLUÍDO E VALIDADO** em medição e A/B visual. Apagão não conferido em jogo |
| Níveis de qualidade reais (Baixo/Médio/Alto) no Windows | **IMPLEMENTADO, AGUARDANDO TESTE** da tela de opções. O efeito na build está medido |
| Ondas, restantes e intervalo no cliente cooperativo + recompensa de onda | **CONCLUÍDO E VALIDADO** no mesmo PC. Falta testar em dois PCs |
| Timeout de desconexão de 10 s | **CONCLUÍDO E VALIDADO** no mesmo PC |
| Limpeza de dependências (Sentis, template, `GetStarted_Scene`) | **NÃO INICIADO** |
| Perfis de iluminação por setor | **NÃO INICIADO** |
| Teste em dois PCs | **BLOQUEADO** (falta um segundo computador) |

### Arquivos

- **Scripts novos**:
  - `Scripts/Combat/Diagnostics/PerfProbe.cs`;
  - `Scripts/Combat/Lighting/LampShadowBudget.cs`;
  - `Scripts/Combat/Editor/CombatSetup.Phase7.cs`.
- **Scripts alterados**:
  - `LightFlicker.cs`: expõe a `Light`;
  - `EnemySpawner.cs`: `Mirror`/`StopMirror`, `RemainingInWave` espelhado;
  - `Network/NetGame.cs`: `WaveState` e `Waves`;
  - `Network/NetSession.cs`: timeouts;
  - `Network/NetAutoTest.cs`: registra ondas e créditos;
  - `Campaign/EndlessDirector.cs`: missões só no host;
  - `Campaign/CampaignHUD.cs`: N só no host;
  - `Horror/GameSettings.cs`: chave `QualityLevel`.
- **Cena** `MilitaryBase_Combat`: 53 luzes do pacote desligadas e o objeto `LampShadowBudget`.
- **Assets**: `Settings/Medium_RPAsset.asset` (novo); `ProjectSettings/QualitySettings.asset` (3 níveis, padrão Alto no Windows); `ProjectSettings/ProjectSettings.asset` (Frame Timing Stats ligado).
- **Pacotes**: nenhum novo.

### Desempenho (release, este PC)

| | Antes (dev) | Depois (release) |
|---|---|---|
| 1080p, 30 infectados | 16,0 ms (um só nível) | **11,8 ms** no Médio (85 FPS) |
| 720p, 30 infectados | 13,5 ms | **9,1 ms** no Baixo (109 FPS) |

A comparação justa, dev contra dev, está em `01_PERFORMANCE.md`: −1,2 a −4,4 ms com as luzes, e mais com os níveis novos.

### Validação e pendências

Ver `08_TESTES.md` e `10_PENDENCIAS.md`.

### Próximo marco recomendado

1. Terminar o **Marco 1**: limpeza segura e picos de quadro.
2. Seguir para o **Marco 2**: HUD de equipe, depois reanimação e game over cooperativo.
3. Em paralelo, **você** faz o teste em dois PCs.

## Marco 1 (fim) e Marco 2 (início) — segunda execução

| Item | Estado |
|---|---|
| Limpeza: cena do template fora da build; shaders do AI Inference fora da build (pacote mantido) | **CONCLUÍDO E VALIDADO** (−35 MB na release) |
| Picos de quadro: áudio pré-carregado, silhueta reaproveitada, caracteres da fonte no carregamento | **CONCLUÍDO E VALIDADO** em medição (release sem quadros > 17 ms); 1 pico residual em dev documentado |
| HUD de equipe (aliados, ping, conexão, minimapa) | **IMPLEMENTADO, AGUARDANDO TESTE** com 3–4 jogadores e em dois PCs; validado no mesmo PC com 2 |
| Queda, reanimação validada pelo host, espectador, volta na onda, fim de jogo da equipe | **CONCLUÍDO E VALIDADO no mesmo PC** (exceto a tecla E física e a tela do cliente) |

### Novos arquivos
- `Network/TeamHUD.cs`
- `Campaign/TextWarmup.cs`
- `Editor/BuildStripping.cs`

### Alterados
- **Cooperativo**: `NetPlayer`, `NetPuppet`, `NetGame`, `NetMatch`, `NetAutoTest`, `PlayerCombat`, `Health`, `EnemyPerception`, `RagdollController`, `GameFlow`, `CombatHUD`, `MapMarker`, `MinimapView`, `TacticalMapView`.
- **Picos e limpeza**: `EnvironmentalEventDirector`, `Editor/CombatSetup.Phase7` (menu 4: áudio), `Diagnostics/PerfProbe`.
- **Marcadores de profiler** no `Update` de 15 scripts.

### Assets
- 214 `.meta` de áudio (preload/streaming).
- `ProjectSettings/EditorBuildSettings.asset` (cena do template desligada).
