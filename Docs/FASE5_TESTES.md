# Fase 5 — Testes

- **Onde**: em Play Mode, no Unity Editor 6000.6.7f1, via MCP, com scripts que chamam o código do jogo e conferem o resultado. Modo Infinito, sem inimigos.
- **Hardware**: Ryzen 7 5700U com GPU integrada, janela Game em 2560×1440.
- **Testes automatizados**: não foram criados (o Unity Test Framework não está instalado).

## 1. Executados

| # | Caso | Resultado |
|---|---|---|
| 1 | Mochila vazia após `StartGame` | OK (0/18) |
| 2 | Empilhamento: 23 sucatas → pilhas 10, 10, 3 | OK |
| 3 | Remover é tudo ou nada: pedir 24 de 23 falha sem mexer em nada | OK |
| 4 | Barra rápida cheia: kit médico excedente vai para a mochila | OK (barra 3/3, +2 na mochila) |
| 5 | Usar um kit da barra puxa a reserva da mochila | OK |
| 6 | Bandagem sem tecido → "Materiais insuficientes" | OK |
| 7 | Kit médico fora da bancada → "Requer bancada de trabalho" | OK |
| 8 | **Duplo clique no mesmo quadro** fabrica uma vez só | OK (c1 = sim, c2 = não; tecido 5 → 3, 1 bandagem) |
| 9 | Kit médico na bancada com a barra cheia | OK: materiais consumidos, kit foi para a mochila |
| 10 | Usar bandagem | OK (50 → 70 de vida). Com a vida cheia: recusada, com "Vida cheia" |
| 11 | Descartar 1 sucata | OK: mochila 10 → 9 e um `WorldItem` aparece no chão |
| 12 | Item de missão (fusível) não ocupa a mochila | OK |
| 13 | Mochila cheia recusa um item novo | OK (18/18, tecido não entrou) |
| 14 | Mochila cheia permite uma receita que libera os próprios espaços | OK (reforço de colete) |
| 15 | Pegar material no mundo (interação) | OK: tecido 0 → 1, o objeto some |
| 16 | Abrir e fechar a mochila (flag `inventory` = Tab) | OK: abre, tempo a 0,25, cursor livre, **tiro bloqueado**; fecha, tempo 1, cursor preso |
| 17 | Abrir o mapa (flag `map` = M) | OK, tempo a 0,25 |
| 18 | Esc com o mapa aberto fecha o mapa (não pausa); segundo Esc pausa | OK |
| 19 | Minimapa: posição, névoa, seta, "N", locais e borda | OK (captura `p5_hud2`) |
| 20 | Descoberta do depósito: feed + fala de Duarte com legenda | OK (captura `p5_hud`) |
| 21 | Tela da mochila, tela da bancada e mapa tático | OK (`p5_backpack2`, `p5_crafting`, `p5_map2`) |
| 22 | Página GRÁFICOS das opções | OK (`p5_options`) |
| 23 | Custo do minimapa (12 s ligado × 12 s desligado, sem inimigos) | 28,71 × 28,74 ms em média: **sem diferença mensurável** |
| 24 | Bancada no lugar certo | corrigido (ver §2) |
| 25 | Console | sem erros de código novo. Os `NullReferenceException` do `PickupFeedView` vieram de uma **recompilação no meio do Play** (artefato do editor, já visto na Fase 3) |

## 2. Problemas encontrados e corrigidos

1. **Bancada dentro de um contêiner**: a posição fixa caía sobre o contêiner azul. Agora o setup procura o ponto livre mais próximo com `CheckBox`, e os materiais também.
2. **Placa "OFICINA" espelhada** para quem fica de frente para a bancada: girada 180°.
3. **Fundo dos painéis transparente** (mochila e mapa a 96–97% deixavam o mundo aparecer): agora 100% opacos.
4. **Mapa tático sobre o subtítulo**: o mapa ficou menor (820 px) e mais baixo.
5. **Nome da bancada sobre o nome do depósito** no mapa: a bancada aparece só pelo ícone.
6. **Feed cortado** ("Local descoberto: Ba…"): o texto passou a "◆ nome do local".
7. **Borda serrilhada do minimapa** (a máscara não suaviza): um aro suave por cima.
8. **"SEM BORDAS" quebrado em 3 linhas** nas opções: virou "S/ BORDA".
9. **Variáveis possivelmente não atribuídas** após `TryGetMarker` (erro de compilação): corrigido.
10. **Mapa cortando o oeste da base**: a margem passou de 40 para 62 m.

## 3. Não testado / limitações

- **Teclas físicas M, Tab e E**: as teclas simuladas pelo Input System não chegaram ao jogo nesta sessão (editor sem foco), nem as que já funcionavam antes, como o Esc.
  - Testei a partir das flags que as ações preenchem (`OnMap`/`OnInventory` seguem a convenção das outras 22 ações), e as ações aparecem no `PlayerInput`.
  - **Peço que você confirme M e Tab no teclado.**
- **Gamepad**: os botões MOCHILA e MAPA do menu de pausa e a navegação por gamepad nas telas não foram testados com um controle físico.
- **Resolução e modo de janela**: só funcionam num build; o editor ignora.
- **Distância de sombras, qualidade, FPS, VSync, FOV e pós-processamento**: os valores mudam e são aplicados, mas não comparei imagens antes e depois de cada um.
- **Baús soltando materiais**: o código está ligado, mas nenhum baú foi aberto nos testes.
- **Persistência da névoa entre sessões**: o código grava em `PlayerPrefs`, mas não fechei e reabri o jogo para conferir.
- **Clique para marcar no mapa tático**: não testado (precisa de clique de mouse real).
- **Modo Campanha** com as telas novas: o objetivo no minimapa e no mapa usa o mesmo `TryGetMarker` dos marcadores existentes, mas não joguei um capítulo.
