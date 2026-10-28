# Fase 6 — Jogar com amigos: LAN, Radmin VPN e Hamachi

O jogo usa conexão direta por IP e porta UDP. O computador que cria a partida é o **Host**: joga normalmente e roda a simulação principal (inimigos, ondas, dano). Não há servidor externo, matchmaking nem Relay.

O Radmin VPN e o Hamachi **só criam uma rede virtual** entre os computadores, como se estivessem na mesma casa. Toda a lógica de rede é do jogo.

## 1. Antes de tudo

- **Todos com a mesma versão do jogo** (o mesmo build). Uma versão diferente é recusada com a mensagem "Versão incompatível".
- **Porta padrão**: **UDP 7777**. Pode ser trocada em Criar partida e em Entrar em partida, mas tem que ser igual nos dois lados.
- **Até 4 jogadores** (o Host escolhe 2, 3 ou 4).

## 2. Mesma casa (LAN)

1. O Host abre o jogo → **MULTIJOGADOR** → **CRIAR PARTIDA** → **CRIAR**.
2. No lobby, o Host vê a lista **"PASSE ESTE IP AOS AMIGOS"**. Use o endereço marcado como "rede local", por exemplo `192.168.0.10`.
3. Os amigos: **MULTIJOGADOR** → **ENTRAR EM PARTIDA** → digitam o IP e a porta → **CONECTAR**.

## 3. Pela internet com Radmin VPN

1. Todos instalam o Radmin VPN (gratuito, site oficial da Famatech).
2. O Host clica em **Rede → Criar rede**, com nome e senha da rede.
3. Os amigos clicam em **Rede → Entrar em rede existente**, com o mesmo nome e senha.
4. O Host cria a partida no jogo. No lobby aparece o IP marcado como **Radmin VPN** (começa com `26.`).
5. Os amigos entram com esse IP `26.x.x.x` e a porta 7777.

## 4. Pela internet com Hamachi

Mesma ideia: todos entram na mesma rede do Hamachi, e os amigos usam o **IPv4 do Hamachi** do Host (começa com `25.`), que aparece no lobby marcado como Hamachi.

## 5. Firewall (no computador do Host)

Se os amigos ficam em "Conectando…" e depois recebem "o Host não respondeu", quase sempre é o firewall do Windows do Host bloqueando a entrada UDP.

- **Na primeira vez** que o jogo abre uma sala, o Windows pode perguntar se o programa pode se comunicar. Marque **Redes privadas** e permita.
- **Regra manual e restrita** (recomendado, em vez de desligar o firewall):
  1. Painel de Controle → Windows Defender Firewall → **Configurações avançadas** → **Regras de Entrada** → **Nova Regra**.
  2. Tipo: **Porta** → **UDP** → porta específica **7777**.
  3. **Permitir a conexão** → marque **apenas Particular** (não marque Público).
  4. Nome: "Base Militar co-op".
- **Radmin e Hamachi**: o adaptador virtual costuma ficar como rede "Pública". Mude-o para **Particular**:
  - Configurações → Rede e Internet → Ethernet → o adaptador do Radmin/Hamachi → **Rede particular**.
  - Ou crie a regra acima marcando também Público, **só para a porta 7777 UDP**.

**Nunca desligue o firewall inteiro.**

## 6. Mensagens de erro

| Mensagem | O que fazer |
|---|---|
| "Endereço IPv4 inválido" | Digite só o IP, sem porta nem espaços (ex.: `26.12.34.56`). |
| "o Host não respondeu" (depois de 10 s) | IP errado, VPN desconectada, Host ainda sem sala aberta ou firewall do Host bloqueando (seção 5). |
| "Sala cheia" | O Host escolheu menos jogadores; ele pode criar de novo com 3 ou 4. |
| "Senha incorreta" | Peça a senha da sala ao Host. |
| "Versão incompatível" | Use o mesmo build do Host. |
| "A partida já começou" | Nesta versão só se entra pelo lobby, antes de o Host iniciar. |
| "Não foi possível abrir a porta UDP 7777" (Host) | Outro programa (ou outra cópia do jogo) usa a porta. Feche-o ou escolha outra porta, por exemplo 7778, e passe aos amigos. |
| "Conexão perdida com o Host" | O Host saiu ou a VPN caiu. O jogo volta ao menu com a mensagem. |
| "Você foi removido da sala pelo Host" | O Host usou REMOVER no lobby. |

## 7. Testar no mesmo computador

Abra duas cópias do jogo (o build). Em uma, crie a partida; na outra, entre com o IP `127.0.0.1`. É assim que esta fase foi testada (ver `FASE6_TESTES.md`).
