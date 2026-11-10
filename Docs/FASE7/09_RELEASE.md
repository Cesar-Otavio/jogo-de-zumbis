# Fase 7 — Release (Marco 10)

**Estado: NÃO INICIADO como entrega.** Há builds de teste:

| Build | Menu | Pasta | Tamanho |
|---|---|---|---|
| Release | *Phase 7 ▸ 2* | `Builds/Win64` | 416 MB (era 451 antes da limpeza) |
| Desenvolvimento | *Phase 7 ▸ 1* | `Builds/Win64Dev` | 493 MB |

- **Versão**: `Application.version` = 4.0.1. Ainda não aparece na tela nem tem esquema beta.
- **Nome do produto e empresa**: "Base Militar - Sobreviva à Noite" e "Cesar Otavio" (trocados em 10/11; antes eram os do template). O save e o `Player.log` ficam em `%USERPROFILE%\AppData\LocalLow\Cesar Otavio\Base Militar - Sobreviva à Noite\`.
- **Limpeza já feita** (`01_PERFORMANCE.md` §7.1):
  - `GetStarted_Scene` desligada da lista de cenas;
  - shaders do AI Inference (Sentis) descartados da build. O pacote continua instalado, e os avisos dele ainda aparecem no log da build.
- **Licenças de terceiros**: ainda não conferidas para redistribuição.
