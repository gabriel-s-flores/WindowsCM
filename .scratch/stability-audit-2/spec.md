# Segunda auditoria de estabilidade e desempenho

Status: resolved
Type: fix
Base: main (após o merge do PR #10)

## Pedido

"Faça mais uma verificação e auditoria com foco em performance e estabilidade: teste, valide e encontre bugs e pontas soltas. O objetivo é o app ser o mais estável e performático possível, sem crashes nem lentidão."

## Como foi feito

- Quatro auditorias de código em paralelo: ciclo de vida/threads, popup/conversores, captura/colagem/histórico, transferência/previews/ações/configurações. Cada achado foi conferido no código antes de entrar na lista. Parte deles também foi reproduzida com teste ou medida.
- Microbenchmarks descartáveis (fora do repo) nos caminhos quentes do Core com entradas patológicas: textos de 2 MB, JSON minificado, milhões de linhas, emoji ZWJ, cabeçalhos de DIB forjados, históricos com CF_HTML grande e imagens 4K/8K.
- Cada correção no Core ganhou um teste que falhava antes. A camada WPF não roda no Linux: ela foi revisada no código e é validada pelo CI Windows (build, testes, smoke do app real e smoke de scroll).

## Achados e correções

### Crashes e travamentos

| # | Onde | Problema | Correção |
|---|---|---|---|
| 1 | Popup | Clicar no texto de um card de código lançava exceção: o clique cai num `Run` e `VisualTreeHelper.GetParent` não aceita `Run`. O 6º clique em 10 s encerrava o app. | `VisualTreeWalk` sobe por elementos de conteúdo via árvore lógica. |
| 2 | Popup | Alt+F4 fechava o popup de vez; o próximo atalho lançava exceção na janela fechada. | Os popups se escondem no `WM_CLOSE` e só fecham ao sair. |
| 3 | Popup/SQLite | Colar uma linha acima de 50 KB na busca fazia o `LIKE` falhar ("pattern too complex") a cada refresh; com "lembrar busca", o popup não abria mais. | Busca limitada a 1.000 caracteres; o store usa `instr` para consultas desse tamanho. |
| 4 | Popup | Uma colagem que falhava antes de esconder o popup deixava `_isActivating` ligado: o compacto ficava no topo, ignorando cliques e a perda de foco. | O flag é limpo quando a ativação termina; as falhas são registradas e explicadas num balão. |
| 5 | Inicialização | Banco corrompido, local personalizado indisponível (drive não montado) ou arquivo travado: o app morria em toda inicialização, sem ícone nem mensagem. | `HistoryStoreOpener`: guarda o arquivo danificado e recria; usa o local padrão; em último caso roda em memória. Cada caso tem balão localizado. |
| 6 | Bandeja | Exceções nos handlers do ícone (WinForms) mostravam o diálogo de "exceção não tratada" do WinForms, cujo Sair encerrava sem limpeza nem log. | `Application.ThreadException` registrado e sujeito à mesma política de rajada. |
| 7 | Configurações | Falha ao salvar no fechamento deixava a janela fechada referenciada; "Configurações" lançava exceção até reiniciar. | A referência é limpa primeiro; salvar e reaplicar são protegidos. |
| 8 | Instância | Com uma instância elevada rodando, a segunda abertura travava na ACL do mutex. | Passa a contar como "outra instância é a primária". |
| 9 | IPC | O cliente esperava a resposta para sempre se a UI da primária travasse; um cliente mudo prendia o pipe de instância única. | Prazo na leitura dos dois lados. |
| 10 | Clipboard | `CF_UNICODETEXT` sem terminador era lido além do bloco (lixo ou access violation). | Decodificação limitada a `GlobalSize`. |
| 11 | Clipboard | `DragQueryFile(i)` é O(n) por arquivo, chamado duas vezes por arquivo com o clipboard aberto: dezenas de segundos para 50 mil arquivos, com todos os apps sem copiar/colar. | `DROPFILES` lido numa passada; um caminho estranho não descarta mais a cópia inteira. |
| 12 | Clipboard | Um DIB de 52 bytes declarando 20000×20000 alocava 1,5 GB antes de falhar; alguns cabeçalhos estouravam `int`. | Tamanhos validados em 64 bits antes de alocar; limite de 8192×8192 (também para PNG). |
| 13 | Colar imagem | Decodificação na thread de UI: 4K = 346 ms/255 MB, 8K = 1,15 s/1 GB. | Fora da UI, com buffers de tamanho exato: 4K = 130 ms/64 MB, 8K = 0,5 s/256 MB. |
| 14 | Ações | O timeout só começava depois de escrever o item no stdin: um comando que não lê a entrada travava para sempre. Se o comando saía sem ler, o pipe quebrado perdia o resultado. | Entrada alimentada em paralelo à espera com prazo; saída limitada. |
| 15 | Transferência | Uploads inteiros na memória (vídeo de 1,5 GB ≈ 3,5 GB no processo), sem prazo e sem limite de conexões. | Multipart gravado direto em disco; ociosidade de 30 s; 16 conexões; texto até 16 MB. |
| 16 | Previews | O link de um ISO ou de uma transmissão ao vivo era baixado inteiro para a memória, a cada abertura do popup. | Lê só HTML/imagem/JSON, até 1 MB/5 MB, com prazo próprio para o corpo; falhas voltam só após 30 min. |
| 17 | Histórico | As datas eram gravadas com a cultura do Windows. Com separador de hora `.` (fi-FI, da-DK ou ajuste personalizado), toda leitura falhava e o popup nunca abria. Em outro calendário (th-TH, fa-IR), os anos ficavam séculos fora. | Gravação invariante; linhas antigas relidas com a cultura que as escreveu e regravadas ao abrir. |

### Perda de dados e comportamento errado

| # | Problema | Correção |
|---|---|---|
| 17b | Arrastar o slider "limite do histórico" de 100 para 10 e de volta apagava 90 itens (evict a cada passo). | Evict só ao fechar as configurações; atualizações ao vivo agrupadas (200 ms). |
| 18 | As caixas de cor hex e de extensões eram recriadas a cada tecla, impedindo a digitação. | O painel só é reconstruído quando o esquema de cores muda. |
| 19 | Um `settings.json` travado por um instante no logon carregava os padrões e o próximo salvamento sobrescrevia tudo. | Leitura com novas tentativas. |
| 20 | Com ordens diferentes nos dois popups, um refresh global reordenava o modelo compartilhado: clicar no card i do popup grande colava outro item. | Só o popup aberto é atualizado, na ordem dele. |
| 21 | Com o modo anônimo ativo e a visão do histórico normal aberta, colar/fixar/editar/excluir agiam na sessão anônima pelo id, e o registro da colagem (data, supressão do eco) caía em outro item. | App, view model e registro da colagem agem sobre o histórico exibido. |
| 22 | Colar uma imagem capturada no modo anônimo dizia "arquivo ausente". | Lê a pasta da sessão anônima. |
| 23 | Recopiar um item apagava o título e os metadados (preview); a nova busca do preview também sobrescrevia um título dado pelo usuário. | Título preservado; metadados mesclados (enriquecimentos ficam, CF_HTML antigo sai se a nova cópia não trouxer); link que já tem preview não é buscado de novo. |
| 24 | Uma linha com data ilegível fazia toda leitura falhar; o popup não abria. | A linha é ignorada. |
| 25 | Uma captura cuja gravação falhava era marcada como vista; a recópia era descartada. | Marcada só após gravar. |
| 26 | "Limpar cache" quebrava os previews de link até reiniciar. | A pasta é recriada ao gravar. |
| 27 | Nomes de arquivo recebidos com `:` viravam fluxo NTFS oculto; `? *` falhavam o envio. | Nomes saneados; `CreateNew` evita sobrescrever. |
| 28 | Duplo clique no ícone da bandeja abria e fechava o popup. | Só o clique alterna. |
| 29 | Atalhos com Alt (Alt+P etc.) nunca funcionavam (`Key.System`); no compacto, digitar com AltGr (ś, ć) abria Configurações ou pedia para limpar. | `SystemKey`; atalhos só com Alt sozinho; menu mostra o atalho real de fixar (Ctrl+S). |
| 30b | Um preview terminando depois de alternar o modo anônimo gravava no item de mesmo id da outra sessão. | A gravação é descartada se a sessão mudou. |

### Segurança (pontas soltas)

| # | Problema | Correção |
|---|---|---|
| 30 | `/api/upload` aceitava texto/arquivos de qualquer aparelho da rede e os colocava direto no clipboard. | Chave de 128 bits por execução, só no QR. Ver revisão do ADR 0003. |
| 31 | Servidor ouvindo a rede desde a inicialização (alerta de firewall para todos). | Inicia no primeiro uso. |
| 32 | Compartilhamentos com token de 32 bits e sem expiração. | 128 bits, 24 h. |

### Desempenho

- Validação JSON de cada linha a cada leitura (abrir o popup com 30 cópias de navegador/VS Code): 79 → 23 ms via `json_valid`. O CF_HTML passa a ser gravado sem escapes de 6 caracteres (cerca de metade do tamanho).
- Card de link: 21 parses do JSON por realização → 1. Conversores de link/caractere retornam na hora para outros tipos.
- Miniaturas de imagem e de preview de link decodificadas fora da UI (antes: 50–200 ms por card 4K ao rolar).
- Card de "Arquivos" com milhares de caminhos: contados, não divididos; só os 10 primeiros detalhados.
- Busca com debounce de 120 ms, aplicada antes de qualquer tecla que age sobre a lista.
- Tema reconstruído só quando o esquema muda (antes, a cada notificação de preferência do Windows).
- Watchdog sem vazar um handle de kernel por segundo; feedback de cópia sem bloquear a thread de captura.
- Emoji: objetos Direct2D/DirectWrite liberados na hora, cache limitado, até 24 emojis por tile. Favicons: cache limitado e novas tentativas só após 15 min.
- Imagens de itens removidos apagadas durante a execução (no máximo a cada 15 min), gravação atômica, pastas anônimas órfãs removidas na inicialização. O cache de imagens de link é varrido na inicialização. As varreduras só rodam contra o histórico real aberto normalmente (a pasta de imagens é compartilhada: com o local padrão substituindo um drive ausente, a varredura apagaria as imagens do histórico real) e leem as linhas de imagem direto do banco.

## Fora do escopo (registrado para depois)

- **Injeção de comando em ações personalizadas**: grupos da regex (texto do clipboard) entram crus na linha do `cmd.exe`. Só afeta ações de comando criadas pelo usuário com grupos de captura; as embutidas não são afetadas. A correção pede um escape seguro para `cmd.exe`, validado no Windows.
- **Desligamento cancelado**: o WPF encerra o app no `WM_QUERYENDSESSION`, e a limpeza de fim de sessão também roda aí. Um desligamento cancelado (ou o Restart Manager do instalador) apaga o histórico não fixado.
- **Corrida anônimo × captura pendente**: sair do modo anônimo até 0,5 s depois de uma cópia pode gravá-la no histórico normal.
- **Segunda abertura durante a inicialização** pode mostrar "não está rodando" se a primeira levar mais de 2 s para abrir o pipe (limite documentado em pesquisa).
- **`VirtualizationMode=Recycling`** continua não aplicado: muda o ciclo de vida dos cards e precisa ser medido no Windows.
- Todo link copiado é buscado automaticamente, inclusive de intranet (links de uso único podem ser "gastos"); as exclusões de link são o controle atual.

## Verificação

- Core: 1345 testes passam no Linux; as falhas são as mesmas da base (caminhos `C:\`, o contrato do instalador e um teste de pipe que só se comporta assim fora do Windows), que passam no CI Windows. Mais de 100 testes novos cobrem as correções.
- Revisão independente do diff inteiro (Core e WPF) antes do PR; os problemas que ela encontrou foram corrigidos. Os mais sérios: a varredura de imagens contra um histórico substituto, as datas dependentes de cultura e o registro da colagem durante o modo anônimo.
- Build da solução inteira (inclui o app WPF, `-p:EnableWindowsTargeting=true`) sem warnings.
- CI Windows no PR: build, testes, smoke do app real e smoke de scroll.
- `dist/`: a versão portátil (`WindowsCM-portable/` + `.zip`) foi gerada por publish cruzado no Linux. O instalador exige Windows + Inno Setup e é produzido pelo job de release do CI a cada push verde na `main`.
