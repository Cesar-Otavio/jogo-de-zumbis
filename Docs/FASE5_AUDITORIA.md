# Fase 5 — Auditoria inicial

Data: 24/10/2026.

- **Branch**: `fase5-mundo`, criado a partir de `fase3-imersao`.
- **Checkpoint**: tag `fase5-checkpoint` (commit `1cf548f`).
- **Árvore**: estava limpa antes de começar; não havia mudanças não commitadas para preservar.
- **Para voltar**: `git checkout fase5-checkpoint`.

## 1. O que mudou desde o relatório anterior

O `Docs/RELATORIO_JOGO.md` descreve o estado de `9b3ed2c`. Depois dele entraram:

- impactos por material (`SurfaceImpactLibrary`);
- menu principal com planos de câmera;
- painel de controles atualizado.

Os números do relatório continuam valendo:

- 89 scripts (agora 90);
- 10 capítulos;
- 19 itens;
- 25 + 6 ícones.

## 2. Verificações pedidas

| Item | Resultado |
|---|---|
| Multiplayer LAN/VPN | **Não existe.** Nenhum pacote de rede no `manifest.json`, nenhum script com Netcode, Mirror, FishNet ou NetworkBehaviour. Os sistemas novos separam estado local (mochila, marcas, interface) de estado do mundo (itens no chão, bancadas, descobertas), mas **não há sincronização implementada**. |
| Pacotes | URP 17.6, Input System 1.20.1, Cinemachine 6.6, uGUI 2.6, TextMesh Pro, AI Assistant (editor). Nada novo instalado nesta fase. |
| MCP | `Unity_RunCommand` (C# no editor), `Unity_GetConsoleLogs`, capturas de câmera e de cena. Geração de assets sem assinatura. |
| Inventário | Só a barra rápida de 6 itens (`InventoryManager`), com capacidade por item. Itens de missão ficam em `RunState`, não na barra. **Não havia mochila.** |
| Crafting | **Não existia.** |
| Minimapa / mapa | **Não existiam.** As `MissionAnchor` (13 locais com nome) e o `MissionManager.TryGetMarker` já davam posições e o objetivo atual. |
| Diálogos | `DialogueLibrary` (50 falas) e `DialogueDirector` com fila, prioridades, `Once`, cooldown, validade e legendas. O import de vozes por ID (`SoundFX/Dialogue/pt-BR/<id>.wav`) já existia (*Phase 3 ▸ 4*). **Nenhum arquivo de voz.** |
| Iluminação | Já revisada na Fase 3: lanterna calibrada, `NightProfile` mais leve, ambiente mais claro, atlas de sombras 4096. Cerca de 150 luzes na cena. |
| Opções gráficas | Só brilho, granulação, tremor e flashes. **Não havia** qualidade, resolução, janela, FOV, sombras, efeitos, FPS, VSync nem intensidade de pós. |
| Teclas | M, Tab e J livres no teclado. **Nenhum botão livre no gamepad** (o Select já troca de item). |
| Assets de cenário | Military Base Pack: hangares, garagem, torre, blocos, cerca, barris, contêineres, postes, banheiro. **Não há móveis** (camas, mesas, armários, bancadas, monitores). |

## 3. Lacunas e riscos

1. **Interiores e móveis**: o pacote de cenário não tem mobília. Enfermaria, alojamentos, sala de controle e oficina (seção 8 do pedido) dependem de assets que não existem no projeto.
   - A bancada desta entrega foi montada com primitivas e materiais do projeto, e está marcada como **provisória**.
2. **Dublagem**: não há serviço de TTS autorizado nesta sessão, então nenhuma voz foi gerada (ver `FASE5_NARRATIVA_AUDIO.md`).
3. **Animações**: os inimigos novos da seção 11 precisariam de clipes próprios; os existentes são poucos (Fase 3).
4. **Desempenho**: medido só no editor (cerca de 36 FPS com 17 inimigos em 1440p, GPU integrada). Expandir o mapa antes de um build com Profiler é arriscado.
5. **Gamepad**: sem botão livre, mapa e mochila abrem pelo menu de pausa.

## 4. Área escolhida para a exploração (objetivo F)

**Depósito de suprimentos** (`depot`, oeste da base, em torno de x −103, z 45). Motivos:

- já é um local com nome usado pela campanha, mas hoje não tem função;
- fica longe do pátio central, então ir até ele é uma decisão de risco;
- o nome justifica materiais e uma bancada.

Nesta entrega o depósito recebeu:

- a **bancada de trabalho**;
- materiais por perto;
- a fala de Duarte ao ser descoberto.

Ver `FASE5_MAPA_EXPLORACAO.md` para o que falta lá (props, armário, segredo).
