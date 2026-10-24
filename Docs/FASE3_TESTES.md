# Fase 3 — Testes

Todos os testes abaixo foram **executados em Play Mode no Unity Editor 6000.6.7f1** via MCP, com scripts que simulam as ações do jogador (teclas pelo Input System, chamadas de API) e capturas de tela.

Hardware: AMD Ryzen 7 5700U com GPU integrada, Game View em 2560×1440.

**Não foram criados testes automatizados** (EditMode/PlayMode do Unity Test Framework): o pacote não está instalado no projeto.

## 1. Executados

| # | Caso | Resultado | Evidência |
|---|---|---|---|
| 1 | Lanterna ligada × desligada no pátio | OK: com a lanterna, os contêineres a ~20 m ficam visíveis; sem ela, ficam pretos | capturas `fl_r40`/`fl_off` |
| 2 | Lanterna dentro do laboratório com **apagão** | OK: as luzes de emergência vermelhas funcionam; a íris automática foi criada depois que o feixe estourou a parede a 4 m | `fl_lab` |
| 3 | Brilho da arma pela lanterna | corrigido: a luz não afeta mais a camada do jogador; não há mais ponto estourado nas mãos | `perf` |
| 4 | Ícones de itens (22) e de armas (6) | OK, revisados visualmente | folhas de contato |
| 5 | Barra rápida com ícones | OK | `pro_3` |
| 6 | Prólogo | OK: 6 falas em sequência, ~43 s; a música abaixa (0,35) durante as falas; volta ao jogo no fim; `radio_wake` não repetiu | log do diretor, `pro_0`, `pro_3` |
| 7 | Pegar arma do chão | OK: Drawing → Ready; o corpo abaixa e a pistola vai à mão; feed "M1911"; fala `fc_armed` com legenda | `grab_0`, `grab_2` |
| 8 | Troca AK ↔ M1911 | OK: Stowing (AK às costas) → Drawing (M1911 do coldre) → Ready; **2 modelos de arma no personagem, sem duplicata** | log, `sw_0`, `sw_4` |
| 9 | Inclinação esquerda/direita mirando | OK: Amount −1 e +1; visão e arma rolam juntas; **Q não trocou de arma** enquanto inclinava | `lean_*` |
| 10 | Fila de diálogo | OK: `Play` enfileira e toca; uma fala com intervalo mínimo ativo é descartada | log |
| 11 | Reação a impacto do zumbi | executado (40 de dano de frente); efeito visível só numa captura estática: **avaliação visual limitada** | `hit_0` |
| 12 | HUD redesenhado | OK: cartões de vida, arma (ícones da arma e da reserva) e pontuação; estado "FERIDO" | `hit_0`, `perf` |
| 13 | Ferramenta do Animator 2D sem os clipes | OK: lista os clipes faltantes e não altera nada | log |
| 14 | Desempenho com 30 inimigos | **~27 ms/quadro (~37 FPS) no editor**; lanterna ligada 27,1 ms × desligada 26,5 ms (pior quadro 47–55 ms) | log |
| 15 | Console | sem erros nas sessões normais. Os NullReference vistos vieram de uma sessão que **recompilou no meio do Play** (artefato do editor) | — |

## 2. Problemas encontrados e corrigidos durante os testes

1. Feixe invisível: a intensidade era baixa demais para a queda física. Calibrado para 320, alcance 40 e cookie com centro forte.
2. Feixe estourando paredes próximas: criada a íris automática pela distância do impacto do feixe.
3. Luz da lanterna estourando a própria arma: a luz passou a ignorar a camada do jogador.
4. Legenda escondida pela faixa da cinemática: canvas acima dela e posição sobre a faixa.
5. Botões e plural da tela final (fase anterior): já corrigidos.

## 3. Não testado / limitações

- **Opções novas** (volumes, legendas): compilam e estão ligadas, mas **não as abri em Play**.
- **Prone, agachar animado, locomoção 2D**: sem clipes não há o que testar além do comportamento inerte (item 13).
- **Reações com cambaleio, limite de ragdolls (10) e congelamento de corpos**: não medidos isoladamente.
- **Gamepad**: as ações novas têm binding, mas não houve teste com controle físico.
- **Falas reativas** de apagão, munição baixa, missão falha e chefe derrotado: dependem de eventos; só a fila e a regra de intervalo mínimo foram verificadas.
- **Meta de 60 FPS**: não atingida nesta máquina no editor (37 FPS com 30 inimigos em 1440p, GPU integrada). Um build e o Profiler numa máquina representativa são necessários antes de concluir sobre desempenho.
