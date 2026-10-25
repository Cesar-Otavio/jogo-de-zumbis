# Fase 7 — Pendências priorizadas

## 1. Bugs e riscos conhecidos

| Prioridade | Item | Situação |
|---|---|---|
| P1 | Cooperativo: mundo e economia locais em cada máquina (risco de loot e recompensa em duplicidade) | conhecido desde a Fase 6; Marco 2 |
| P1 | Cooperativo: Gritador, Tanque, soldados e chefes sem replicação de habilidades | Marco 2 |
| P2 | Picos isolados de 25–38 ms (e um de 3 s) nas medições | investigar aquecimento de shaders e GC |
| P2 | Tela de opções com os nomes novos de qualidade não conferida | conferir |
| P3 | Aviso "Light.shadowResolution is compatible only with Built-In" da lanterna | cosmético |

Nenhum P0 conhecido.

## 2. O que depende de você

| # | Pedido | Para destravar |
|---|---|---|
| 1 | **Teste em dois PCs**: copie `Builds/Win64` para os dois. Num, MULTIJOGADOR → Criar sala; no outro, Entrar com o IP mostrado no lobby (LAN primeiro, depois Radmin VPN). Mande o resultado e o `Player.log` de cada um (`%USERPROFILE%\AppData\LocalLow\DefaultCompany\Tutorial no Editor do Guia de Configuração\Player.log`; o nome da empresa e do produto ainda são os do template, um ajuste do Marco 10) | validar o multiplayer de verdade (Marco 2) |
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

1. **Marco 1 (resto)**:
   - limpeza segura: tirar a `GetStarted_Scene` da build, avaliar a remoção do AI Inference e das sobras do template, com verificação de referências;
   - investigar os picos de quadro;
   - medir CPU por script (física dos ragdolls, percepção).
2. **Marco 2**:
   - HUD de equipe;
   - incapacitação, reanimação, espectador e game over cooperativo;
   - inimigos especiais, soldados e chefes em rede;
   - mundo com IDs estáveis;
   - economia validada.
3. **Marco 4**: AudioMixer com os grupos pedidos, sem perder os volumes atuais.
4. **Marco 9**: Unity Test Framework e os primeiros testes (mochila, fabricação, dano, aprovação de conexão).
