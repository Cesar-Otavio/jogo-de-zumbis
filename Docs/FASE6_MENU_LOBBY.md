# Fase 6 — Menu multijogador e lobby

Menu principal → **MULTIJOGADOR** (`NetMenuView`, tela própria por cima do menu, no estilo do `UiKit`).

## 1. Telas

### Início
- **Seu nome** (até 20 caracteres, guardado para a próxima vez).
- **CRIAR PARTIDA**, **ENTRAR EM PARTIDA**, **VOLTAR**.
- Um resumo de como jogar com amigos (LAN, Radmin, Hamachi, firewall).

### Criar partida
| Campo | Valor |
|---|---|
| Nome da sala | texto (padrão "Sobreviventes Aurora") |
| Modo | **Modo Infinito**. A campanha cooperativa é um marco seguinte, e a tela diz isso |
| Dificuldade | Fácil, Normal, Difícil, Pesadelo |
| Jogadores | 2, 3 ou 4 |
| Porta UDP | padrão 7777 (aceita de 1024 a 65535) |
| Senha | opcional |

**CRIAR**:
1. valida a porta;
2. abre o Host, escutando em todas as interfaces;
3. cria o objeto da partida;
4. vai para o lobby.

Nenhuma onda começa.

### Entrar em partida
- **IP do Host** (IPv4), **porta** e **senha**.
- **CONECTAR** e **CANCELAR**.
- A linha de estado mostra "Conectando a …", "Conectado. Aguardando o Host." ou o erro em vermelho.
- **Tempo limite**: 10 s sem resposta → "o Host não respondeu…". O jogo nunca fica preso em "Conectando".

### Lobby
- **Título e regras**: nome da sala, modo, dificuldade e jogadores (ex.: 2/4).
- **4 espaços**, cada um com:
  - nome, com marca **HOST** e **(você)**;
  - **Pronto** ou **Aguardando**;
  - traje (resumo);
  - **ping** (RTT do transporte).
- **Host**:
  - muda a dificuldade (‹ ›);
  - vê **"PASSE ESTE IP AOS AMIGOS"** com os IPv4 da máquina, marcados como rede local, Radmin VPN ou Hamachi, e a porta;
  - **REMOVER** em cada cliente;
  - **INICIAR PARTIDA**, desabilitado com o motivo enquanto alguém não estiver pronto. O Host pode começar sozinho.
- **Cliente**: **PRONTO** (alterna) e a mensagem "Aguardando o Host iniciar a partida".
- **Todos**: **SAIR DA SALA**, e um registro das últimas mensagens (entradas, saídas, erros).

## 2. Estados da conexão (`NetSession.Status`)

| Estado | Quando |
|---|---|
| Offline | sem sessão |
| Hosting | sala aberta |
| Connecting | cliente tentando (até 10 s) |
| Connected | cliente aceito pelo Host |
| Failed | recusado, tempo esgotado ou conexão perdida, com o motivo em `Message` |

## 3. Recusas do Host (aprovação de conexão)

Em ordem de verificação:

1. pedido ilegível → "Pedido de conexão inválido";
2. protocolo ou versão do jogo diferentes → "Versão incompatível: o Host usa X (protocolo N)";
3. sala no limite → "Sala cheia";
4. partida já iniciada → "A partida já começou. Entre pelo lobby da próxima";
5. senha errada → "Senha incorreta".

O motivo chega ao cliente e aparece na tela de entrar.

## 4. Início da partida

1. O Host aperta INICIAR, e `NetGame.StartMatch` confere se todos estão prontos.
2. Cada jogador recebe um ponto de spawn (0–3) na ordem de entrada.
3. `MatchStarted = true` (novos pedidos de entrada são recusados).
4. `StartMatchRpc(modo, dificuldade)` para todos. Cada máquina:
   - aplica a dificuldade da sala;
   - fecha a tela de multijogador;
   - inicia o jogo (`GameFlow.StartGame`);
   - **cliente**: desliga o `EnemySpawner` e remove inimigos locais;
   - coloca o personagem no seu ponto de spawn, ao lado do ponto inicial, num lugar livre;
   - mostra o aviso "Partida cooperativa iniciada" ou "Você entrou na partida do Host".

## 5. Desconexão

| Situação | Comportamento |
|---|---|
| Cliente sai durante o lobby ou a partida | o Host remove o objeto e o boneco dele e avisa "Um jogador saiu da sessão". A partida continua |
| Host sai, ou a conexão cai, durante a partida | o cliente volta ao menu e a tela de multijogador abre com "Conexão perdida com o Host" |
| Pausa → Reiniciar ou Menu principal no co-op | sai da sessão (não dá para reiniciar a partida dos outros) |
| Reconexão durante a partida | **não implementada** nesta versão (o pedido aceita isso no MVP); só se entra pelo lobby |
| Troca de Host | não implementada |
