# Colar automaticamente no campo selecionado

Status: resolved
Type: feature
Blocked by: performance-stability-audit (branch empilhada)

## Pedido

Quando o usuário aciona o WindowsCM depois de clicar numa área onde é
possível colar, o item escolhido deve ser colado nessa área. Ativado por
padrão, com opção de desativar pelo menu.

## Situação anterior

- Escolher um item (Enter/clique) já copiava e injetava Ctrl+V na janela que
  estava em foco quando o popup foi pedido, mas não havia como desligar isso
  (só o Shift, item a item).
- Aberto **pela bandeja**, a janela em foco nesse instante é a barra de
  tarefas (ou o próprio menu da bandeja): a colagem mirava a barra de tarefas
  e o item nunca chegava ao campo que o usuário tinha clicado.
- Se o último clique foi na área de trabalho, o Ctrl+V era injetado lá mesmo
  (no Explorer isso pode colar arquivos).

## Solução

- `BehaviorSettings.AutoPaste` (padrão `true`; arquivos antigos carregam
  `true`) → `PasteOptions.AutoPaste`.
- Menu da bandeja: item marcável **Colar automaticamente**, ao lado do modo
  anônimo; aplica na hora e salva. Configurações → Geral: card com o mesmo
  toggle; os dois ficam sincronizados.
- `PasteTargetPolicy` (Core, pura): classifica a janela em foco (app, o
  próprio WindowsCM, barra de tarefas/bandeja/Iniciar, área de trabalho) e
  decide o alvo. `Win32ForegroundTracker` guarda a última janela onde o
  usuário estava (hook `EVENT_SYSTEM_FOREGROUND`), para que abrir pela
  bandeja cole no app de antes.
- `PasteOrchestrator`: com `AutoPaste` desligado, ou sem alvo colável, copia,
  fecha o popup e não injeta tecla nenhuma (toast "Adicionado à área de
  transferência"). Shift+Enter continua só copiando sem fechar.

## Limite conhecido

A detecção é por janela, não por controle: "onde é possível colar" = uma
janela de aplicativo (não a área de trabalho nem a barra de tarefas). Saber
se o controle focado dentro do app é editável exigiria UI Automation, com
risco de falso negativo em apps que não expõem isso — deixaria de colar em
campos válidos.
