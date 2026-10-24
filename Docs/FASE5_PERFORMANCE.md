# Fase 5 — Desempenho

**Todas as medições são do editor**: Game View em 2560×1440, Ryzen 7 5700U com GPU integrada. **Não há medição em build.** O editor custa caro, então estes números servem para comparar mudanças entre si, não para prometer FPS.

## 1. Medições

| Situação | Média | Mediana | p95 | Observação |
|---|---|---|---|---|
| Fase 3: onda de 30 inimigos | ~27 ms (~37 FPS) | — | — | `FASE3_TESTES.md` |
| Fase 3 (impactos novos): onda de 34, até 17 vivos | 27,8 ms (~36 FPS) | 27,1 | 32,1 | pior quadro 330 ms, um pico isolado |
| Fase 5: pátio inicial sem inimigos, minimapa **ligado** | 28,71 ms | 28,69 | 32,05 | 388 quadros |
| Fase 5: mesmo lugar, minimapa **desligado** | 28,74 ms | 28,54 | 31,05 | 389 quadros |

**Leituras:**

1. **O minimapa não tem custo mensurável.** Ele não usa câmera extra: é uma textura pré-gerada mais algumas imagens de UI.
2. **O tempo de quadro mal muda entre 0 e 17 inimigos** (cerca de 28 ms). O gargalo não é a IA nem o combate; provavelmente é a renderização da cena com uma GPU integrada a 1440p.
   - O console mostra a URP reduzindo a resolução das sombras para caber 41 a 46 sombras de luzes pontuais no atlas.
   - O próximo passo é medir num build com o Profiler (CPU × GPU) antes de mexer.

## 2. Escolhas de desempenho desta fase

- **Mapa**: gerado uma vez no editor (512×512), sem renderização em tempo real.
- **Névoa**: grade 64×64, atualizada 4 vezes por segundo e só quando muda. Grava no máximo a cada 5 s.
- **Telas** (mochila, mapa): só redesenham quando algo muda ou a cada 15 quadros.
- **Materiais no mundo**: uma luz pontual pequena por item, sem sombra, raio 1,6 m. São 12 na cena; os dos baús somam mais alguns por partida.
- **Bancada**: uma luz spot sem sombra.
- **Qualidade dos efeitos** (nova opção): reduz as partículas de impacto (40% / 70% / 100%) e o limite de furos de bala (32 / 56 / 80).
- **Distância das sombras** (nova opção): 30, 50, 70 ou 90 m, na pipeline URP.
  - No editor, o valor original volta quando o Play para, para não alterar o asset.

## 3. Recomendações (não executadas)

1. **Build Windows + Profiler** numa onda pesada, separando CPU e GPU.
2. **Sombras de luzes pontuais**: hoje há mais de 40 sombras disputando o atlas.
   - Desligar a sombra dos postes distantes.
   - Ou usar luz baked/mixed nos postes fixos.
3. **Antes de expandir o mapa** (enfermaria, alojamentos, oficina): essa medição em build é pré-requisito. O pedido também diz "não criar um mapa muito maior antes de medir".
