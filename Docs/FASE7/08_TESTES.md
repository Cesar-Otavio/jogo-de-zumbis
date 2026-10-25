# Fase 7 — Testes (Marco 1)

Tudo abaixo foi **executado**. O que não foi está nas seções finais.

## 1. Compilação e builds

| Item | Resultado |
|---|---|
| Compilação do editor depois de cada mudança | ✅ sem erros. Um erro de sintaxe que eu mesmo introduzi ao editar a `PerfProbe` foi corrigido antes da build |
| Build de desenvolvimento (4 vezes) | ✅ sem erros. Só os avisos de shader do AI Inference/Sentis e o de pre-baked collision |
| Build release | ✅ sem erros, 451 MB |

## 2. Desempenho (build, sonda `PerfProbe`)

- **31 execuções de 20 s** (mais 2 de descoberta), em 4 rodadas: base, luzes, níveis de qualidade e release.
- Resultados em `01_PERFORMANCE.md`; dados brutos em `medicoes/`.
- **Todas as execuções terminaram** com código 0 e escreveram o resultado.
- A partida em si (Modo Infinito, 30 infectados perseguindo o jogador) rodou em todas sem exceção.

## 3. Iluminação

| Teste | Resultado |
|---|---|
| A/B visual (antes/depois do orçamento de sombras), mesmo ponto, mesma sessão | ✅ imagens quase iguais; levemente menos luz perto de alguns postes |
| Luzes do pacote desligadas na cena | ✅ 53 desligadas, 0 restantes |
| Luzes com sombra visíveis por quadro | ✅ de ~34 para 3–6 (conforme a opção) |
| Apagão com as luzes do pacote desligadas | ⛔ **não testado em jogo**. Por código, a LampLight segue `PowerGridManager`, e a do pacote não existe mais |

## 4. Níveis de qualidade

| Teste | Resultado |
|---|---|
| Lista na build | ✅ Baixo, Médio, Alto (a sonda leu `QualitySettings` na build) |
| Cada nível aplica o pipeline certo | ✅ Baixo = `Mobile_RPAsset`, Médio = `Medium_RPAsset`, Alto = `PC_RPAsset` |
| Custo por nível | ✅ ver `01_PERFORMANCE.md` §4.3 |
| Tela de opções mostrando os nomes novos | ⛔ não conferida visualmente |
| Save antigo abrindo no padrão (Alto) | ⛔ não testado com um save antigo real (só por código) |

## 5. Cooperativo (editor como host + build como cliente, mesmo PC)

| Teste | Resultado |
|---|---|
| Conexão, lobby, pronto, início | ✅ |
| Ondas no HUD do cliente (contagem, onda ativa, restantes, intervalo) | ✅ pelo log do cliente |
| Recompensa de fim de onda no cliente | ✅ +300 pontos, +60 créditos |
| Abates do cliente | ✅ 6 abates validados pelo host |
| Timeout de desconexão | ✅ 7,6–12,7 s (antes ~30 s) |

## 6. Não executados / bloqueados

- **Dois computadores** (LAN, Radmin, Hamachi): bloqueado, há um PC só.
- **3–4 jogadores**: não executado.
- **Rede com latência ou perda simulada**: não há ferramenta nesta sessão.
- **Teclado físico e gamepad**: bloqueado (teclas simuladas não chegam ao jogo).
- **Sessão longa** (memória e vazamentos): não executado. A memória total ficou estável em cada execução de 20 s (580–680 MB).
- **Campanha completa** depois das mudanças: não executado.
- **Testes automatizados**: o Unity Test Framework continua não instalado (Marco 9).

## 7. Segunda execução (limpeza, picos, cooperativo)

| Teste | Resultado |
|---|---|
| Builds dev (5) e release (1) depois das mudanças | ✅ sem erros de compilação |
| Tamanho da build release | ✅ 451 → 416 MB |
| Picos: dev, onda de 30, 40 s × 2 | ✅ 1 e 0 quadros > 25 ms (antes 5–8, até 61 ms) |
| Release, 3 cenários de 30 s | ✅ pior quadro 14–16,5 ms |
| Cooperativo: queda, reanimação nos dois sentidos, sangramento, espectador, volta na onda, fim de jogo da equipe | ✅ (detalhes em `02_MULTIPLAYER.md` §5.4) |
| Fonte: "•" existe na fonte principal; "◆" não existe em nenhuma (era o quadrado no feed) | ✅ verificado com `TMP_FontAsset.HasCharacter` |
| Importação de áudio | ✅ 209 efeitos com preload, 5 longos em streaming |

**Não executados**:
- E segurado de verdade;
- fim de jogo visto no cliente;
- 3–4 jogadores;
- dois PCs;
- uma partida single-player completa depois das mudanças. As rotas single-player só mudam quando `Net.Active`, exceto três pontos que valem também no single-player:
  - a regeneração parada em `Untargetable`, que só é ligada no cooperativo;
  - o pré-carregamento de áudio;
  - a silhueta reaproveitada.

## 8. Terceira execução (inimigos especiais, mundo, economia)

Ver `02_MULTIPLAYER.md` §6.4 (12 testes executados e o que ficou de fora).

**Regressão single-player**: a sonda (onda de 30, Médio, 1080p, 30 s) rodou sem exceção, com 12,5 ms de média. Ela registrou dois picos de 26 e 44 ms em `Update`: um no `ZombieAI` (o residual já conhecido) e outro num script ainda sem marcador.
