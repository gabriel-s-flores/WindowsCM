# Popup congela e fecha ao rolar uma lista cheia

Status: resolved
Type: fix

## Relato

"Quando eu dou scroll em uma lista cheia o aplicativo simplesmente para de
responder e crasha" — persistiu depois da auditoria de performance e
estabilidade. Print: popup grande (horizontal, 1880 px), ~75% da lista
rolada, janela esbranquiçada de "Não está respondendo".

## Reprodução (`scripts/smoke/scroll-smoke.ps1`)

Nenhum teste rolava o popup. O smoke de scroll (app real, CI Windows,
1920×1080) enche o histórico (100 itens: prompts longos, código, prints,
links reais, arquivos, emoji, cores), abre o popup e gira a roda sobre os
cards (roda "jogada", touchpad, rajadas), medindo a volta de uma mensagem
pela thread de UI (`WM_NULL`). Ao travar, salva as pilhas gerenciadas
(`dotnet-stack`); ao cair, resume o dump do runtime (`dotnet-dump`).

- Histórico só com arquivos locais: não trava (3 rodadas).
- Um item de arquivo num compartilhamento que não responde
  (`\\10.255.255.1\share\report.pdf`), fundo no histórico: **o popup parou
  de responder após 4,7 s de scroll, por ~20 s**, no instante em que o card
  apareceu. Pilha da thread de UI:
  `File.Exists` ← `ImagePreviewVisibilityConverter.Convert` ←
  `BindingExpression.Activate` ← `FrameworkTemplate.LoadContent` ←
  `VirtualizingStackPanel.MeasureChild`.

## Causa

Os conversores dos cards consultavam o disco e o Shell **na thread de UI**,
a cada realização do card: `File.Exists`/`FileInfo` (miniatura, visibilidade
de imagem/arquivo, tamanho, detalhes), extração de miniatura do Shell,
`SHGetFileInfo` no caminho real (ícone), tags de áudio/vídeo pelo property
store — de 9 a ~20 chamadas por card de arquivo. O `FileIconConverter`
chamava `Directory.Exists` até para cards de texto (primeira linha do
texto). Num caminho que responde devagar — compartilhamento offline, WSL
(`\\wsl.localhost\…`) com a distro parada, disco dormindo, pendrive removido
— cada chamada bloqueia por segundos. Com a virtualização padrão, o card é
recriado toda vez que volta à tela, então rolar para frente e para trás
repete o bloqueio: "Não está respondendo" e, ao fechar, o "crash".

Não confirmado na máquina do usuário (é preciso o log/Event Viewer): é o
mecanismo reproduzido que casa com o relato (trava ao rolar até itens
antigos).

## Solução

- `BackgroundProbeCache` (Core, testado): a consulta nunca roda a sondagem
  na thread de quem pergunta. Falta ou entrada velha (30 s) agenda uma
  sondagem por chave; o valor velho continua servido; `Updated` só quando a
  resposta muda.
- `CardFileFacts` (App): existência, pasta, tamanho, miniatura (imagem
  decodificada ou Shell), ícone do arquivo, capa/tags de áudio e selo de
  vídeo, sondados num worker STA dedicado (os handlers do Shell querem STA;
  de uma thread MTA o COM voltaria para a STA principal — a da UI).
- Os conversores só leem `CardFileFacts`. Enquanto sonda, o card mostra o
  ícone genérico da extensão (`GetExtensionIcon`, só registro) e o nome.
  Os dois popups fazem um único `Items.Refresh()` por rajada de respostas.
  Troca de idioma limpa o cache (tamanhos/durações formatados).

Custo por frame de scroll (perfil de CPU da thread de UI, 20 s de scroll):

- `DropShadowEffect` saiu da borda raiz para um irmão vazio atrás dela. Com
  o efeito num ancestral, cada `TranslatePoint` da sincronização do mouse
  (uma por frame) calculava `VisualDescendantBounds` do popup inteiro:
  7,2 s de 20 s.
- Realce de sintaxe só para cards de código (`CodeContentConverter`); antes
  criava centenas de `Run` por card de texto, num `TextBlock` recolhido.

Resultado: CPU gerenciado da thread de UI 9,5 s → 5,2 s; sincronização do
mouse 7,2 s → 2,3 s; realce 0,93 s → 0,05 s. Volta da UI durante o scroll:
p95 55 → 26 ms (roda), 13 → 1 ms (rajada).

## Logs para o próximo congelamento

- `UiHangWatchdog` (Core, testado): uma thread em segundo plano posta um
  no-op na UI a cada 1 s; se não roda em 5 s, grava em
  `%LOCALAPPDATA%\WindowsCM\logs\windowscm.log`
  `[ui-hang] the UI thread has not answered for 5 s (private … MB, …
  handles, … threads, GDI …, USER …)` e, quando volta, a duração total.
- `ErrorLog.Note` para eventos que não são exceção.

## Fora do escopo

- `VirtualizationMode=Recycling`: cortaria a criação de templates (2,4 s de
  20 s no perfil), mas muda o ciclo de vida dos cards; medir antes.
- CPU total do processo no runner (~3,5 núcleos) é renderização por
  software (sem GPU); não medido em hardware real.
- `LinkPreviewImageConverter` ainda decodifica a imagem de preview (cache
  local) na UI a cada realização.

## Verificação

- Core: 1232 testes passam no Linux; as 8 falhas são as mesmas da `main`
  (caminhos `C:\`, pipes) e passam no CI Windows.
- Build do app WPF (`-p:EnableWindowsTargeting=true`) sem warnings.
- Smoke de scroll no CI: vermelho na `main` com o compartilhamento
  inacessível (travou 20 s), verde com a correção.
