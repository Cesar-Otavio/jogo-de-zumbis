# Fase 7 — Pendências priorizadas

## 1. Bugs e riscos conhecidos

| Prioridade | Item | Situação |
|---|---|---|
| P2 | Tanque quebrando barricada vista pelo cliente | implementado, sem teste |
| P2 | Valores do balanceamento cooperativo | ponto de partida; ajustar depois de jogar em equipe |
| P1 | **Primeiro tiro da partida trava**: 30–36 ms em dev depois da correção (era 42–64), **55 ms na release** + 65 ms no quadro seguinte | aquecer os *pools* de efeitos na carga (`01_PERFORMANCE.md` §9.5) |
| P2 | Primeiro golpe de cada infectado: 14 ms no `Animator` (`SetTrigger`/`speed`) | investigar |
| P2 | `ThirdPersonController.Update` 13–16 ms raro (5 em 100 execuções) | partes já marcadas, falta um pico cair nelas |
| P2 | Uma morte de host caído ~1 s depois da queda, vista uma vez na build anterior | não se repetiu; motivos agora no log |
| P2 | Tela de opções com os nomes novos de qualidade não conferida | conferir |
| P3 | Aviso "Light.shadowResolution is compatible only with Built-In" da lanterna | cosmético |

Nenhum P0 conhecido.

## 2. O que depende de você

| # | Pedido | Para destravar |
|---|---|---|
| 1 | **Teste em dois PCs**: copie `Builds/Win64` para os dois. Num, MULTIJOGADOR → Criar sala; no outro, Entrar com o IP mostrado no lobby (LAN primeiro, depois Radmin VPN). Mande o resultado e o `Player.log` de cada um (`%USERPROFILE%\AppData\LocalLow\Cesar Otavio\Base Militar - Sobreviva à Noite\Player.log`) | validar o multiplayer de verdade (Marco 2) |
| 2 | **Teclas e controle**: Tab, M, E, Q/E mirando, Esc e um gamepad. Confirmar se funcionam | Marco 8 |
| 3 | **Opções ▸ Gráficos**: trocar a QUALIDADE entre Baixo/Médio/Alto e dizer se a imagem está aceitável em cada uma | validar os níveis novos |
| 4 | **Clipes Mixamo** (lista em `Docs/FASE3_ASSETS.md` §3.1) | Marco 3 |
| 5 | **Vozes**: escolher TTS autorizado, gravação ou só legendas | Marco 4 |
| 6 | **Pacote de props** com licença verificada | Marco 5 |
| 7 | **Decisão sobre a `main`** (abaixo) | integração |

## 3. Proposta de integração (sem merge automático)

- A `main` está no checkpoint antes da Fase 3.
- A proposta é juntar `fase7-definitive` na `main` com **merge sem fast-forward**, preservando o histórico das fases. Fica a seu critério quando fazer, por exemplo depois do teste em dois PCs.
- As tags de checkpoint (`fase5-checkpoint`, `fase6-checkpoint`, `fase7-checkpoint`) continuam para voltar atrás.

## 4. Próximos passos que posso fazer sem nada seu (ordem)

**Já feito**: limpeza segura, picos de quadro, HUD de equipe, queda/reanimação/espectador/fim de jogo cooperativo (ver `11_ENTREGA_FINAL.md`).

1. **Marco 2**: feito no mesmo PC (barricada, gerador, apagão, corrida, fabricação pelo host, balanceamento). Falta o **teste em dois PCs (seu)** para considerar o multiplayer completo.
2. **Marco 1 (resto)**:
   - aquecer os efeitos do primeiro tiro e medir na release (§9.5 do desempenho);
   - CPU com muitos inimigos (física dos ragdolls, percepção).
3. **Marco 4**: AudioMixer feito. Falta só ouvir o resultado em jogo (seu). O resto do Marco 4 (vozes) depende da sua escolha de TTS, gravação ou só legendas.
4. **Marco 9**: Unity Test Framework e os primeiros testes (mochila, fabricação, dano, aprovação de conexão, reanimação).
