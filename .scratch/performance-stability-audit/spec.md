# Auditoria de performance e estabilidade

Status: resolved
Type: fix

## Relato

O app crashou muitas vezes, principalmente com muitos itens no histórico, e
apresentou lentidão.

## Achados (ordem de gravidade)

### Crashes

1. **Exceção no thread do listener de clipboard mata o processo.**
   `MessageOnlyClipboardListener.WndProc` é um callback nativo (reverse
   P/Invoke) que dispara `ClipboardChanged` → `ClipboardMonitor` →
   `CaptureService` → SQLite/IO de imagem, e depois o feedback de cópia do
   `App`. Qualquer exceção ali (SQLite ocupado, disco cheio, PNG corrompido,
   `Dispatcher.Invoke` falhando) atravessa a fronteira nativa e derruba o
   processo sem aviso.
2. **Store do modo anônimo sem sincronização.** O `SqliteHistoryStore`
   `:memory:` criado pelo `IncognitoSessionCoordinator` não passa pelo
   `LockedHistoryStore`: é usado ao mesmo tempo pelo listener (captura), pela
   UI (popup), pelo pool (preview de links) e pelo pipe. `SqliteConnection`
   não é thread-safe, e `SetIncognito(false)` descarta o store enquanto outra
   thread ainda o usa → corrupção nativa / `ObjectDisposedException`.
3. **Sem rede de proteção global.** Nenhum handler para
   `DispatcherUnhandledException`, `AppDomain.UnhandledException` ou
   `TaskScheduler.UnobservedTaskException`, e nenhum log: qualquer erro num
   handler de UI fecha o app e não deixa rastro para diagnóstico.
4. **Estado compartilhado do `CaptureService` sem lock.** `_lastSeen` é lido
   e escrito pelo listener (captura) e pela UI (`CopiedFromHistory`).
5. **`Clipboard.SetText` sem proteção** na ação "Copiar" (clipboard ocupado
   por outro app → `COMException`).
6. **Dispose do listener no thread errado.** `PostQuitMessage` era chamado
   no thread da UI, então o loop do listener nunca saía e o `Join` esperava
   2 s em todo encerramento.

### Lentidão

7. **Limite do histórico só aplicado na inicialização.** `Evict` rodava só
   no startup e ao fechar as configurações; durante a sessão o histórico
   crescia sem limite (e as imagens órfãs ficavam no disco até reiniciar).
8. **Varredura completa do histórico em caminhos quentes.** `List()` (todas
   as linhas + parse de JSON por linha) a cada cópia (feedback), a cada
   colagem (`PasteOrchestrator`, `CopiedFromHistory`, `ActivateAsync`) e a
   cada abertura do popup (`EnsureLinkPreviewsForRecentItems`).
9. **Preview de texto proporcional ao conteúdo inteiro.** `GetPreviewText`,
   `GetTitle` e `CodeSyntaxTokenizer.Tokenize` faziam `Split` do conteúdo
   todo para usar 1–8 linhas; `DetectLanguage` rodava uma regex com `.*` e
   sem timeout sobre o texto inteiro (backtracking em textos minificados
   grandes). Tudo na thread de UI, a cada card realizado. Linhas gigantes
   (JSON/JS minificado) iam inteiras para o `TextBlock`.
10. **Miniaturas decodificadas a cada realização de card.** O
    `ImageThumbConverter` decodificava o PNG do disco na thread de UI sem
    cache; os caches de miniatura do Shell e de metadados de mídia cresciam
    sem limite.
11. **Tempestade de refresh.** Cada favicon baixado fazia `Items.Refresh()`
    e cada preview de link concluído recarregava o modelo e as duas janelas.
12. **Tamanho em disco de cada arquivo de uma cópia múltipla.**
    `FileDisplayHelper.GetFileDetails` fazia `File.Exists` + `FileInfo` para
    todo caminho de um item "Arquivos" (4 conversores por card, thread de
    UI), embora o tamanho só apareça para um arquivo único. Copiar 2000
    arquivos no Explorer = ~16 mil syscalls por card; em caminhos de rede,
    segundos de travamento.

## Plano / solução

- `IHistoryStore.GetById` / `GetLatest` (consultas indexadas, com
  implementação padrão para fakes) substituem `List()` nos caminhos quentes.
- `IncognitoSessionCoordinator` serializa toda chamada encaminhada sob o
  mesmo lock que troca/descarta o store efêmero.
- `CaptureService` serializa captura/`CopiedFromHistory`/toggle e aplica os
  limites do histórico (`CaptureOptions.HistoryMaxItems`, etc.) a cada item
  gravado. Os arquivos de imagem de itens removidos continuam sendo limpos
  pela varredura de órfãos do startup (limpar em tempo de execução disputaria
  com a troca de sessão anônima e poderia apagar imagens da sessão errada).
  Mudanças no slider de limite chegam à captura sem precisar fechar as
  configurações.
- `ClipboardMonitor` nunca deixa uma falha de leitura/captura escapar
  (evento `CaptureFailed`); o `WndProc` do listener também captura tudo.
- `ErrorLog` (Core) grava em `%LOCALAPPDATA%\WindowsCM\logs\windowscm.log`
  com rotação por tamanho, sem conteúdo do clipboard. `UnhandledErrorPolicy`
  mantém o app vivo em erros de UI isolados, mas deixa encerrar numa rajada
  (evita loop infinito de erro por frame).
- Previews limitados: `TextPreview` percorre só o começo do texto e corta
  linhas longas; `DetectLanguage` usa uma fatia de 4000 caracteres com regex
  compilada com timeout.
- `LruCache` (Core) limita os caches de miniatura e metadados. Miniaturas de
  imagem passam a ser cacheadas e pré-aquecidas em background (capturas novas
  e itens de imagem do histórico no startup).
- Refresh coalescido: previews de links e favicons agendam um único refresh
  por janela de ~150–250 ms.
- `GetFileDetails` só consulta o disco quando o item tem um único arquivo.
- `Clipboard.SetText` da ação "Copiar" protegido, com balão localizado
  (`TrayCopyFailedBalloon`, PT/EN) em vez de falhar em silêncio.
- O listener encerra a si mesmo via `WM_CLOSE` postado para a própria janela.

## Fora do escopo (achados para tickets futuros)

- **IDs ambíguos ao ver o histórico normal com o anônimo ativo.** Fixar,
  excluir, editar e colar a partir dessa visão resolvem o id no store
  *ativo* (o anônimo), então podem agir sobre outro item com o mesmo id.
  Não é crash nem lentidão, mas é perda de dados — merece ticket próprio.
- **Recopiar um item existente apaga título/metadados.** O "bump" do
  `AddOrUpdate` sobrescreve `title`/`metadata` com os da nova captura (nulos).
- **Miniatura do Shell (vídeos, PDFs) ainda é extraída na thread de UI** na
  primeira exibição; um pré-aquecimento exigiria um worker STA dedicado.
- **`VirtualizationMode=Recycling`** nas listas reduziria a criação de
  containers ao rolar; não aplicado sem poder validar visualmente no Windows.
- **`EmojiService`** cria uma factory Direct2D por emoji e não libera os
  objetos COM (limitado pelo cache por emoji único).

## Verificação

- Testes do Core (novos + existentes) no `dotnet test`: 1136 passam; as falhas
  restantes são exatamente as do baseline (antes das mudanças) e só ocorrem
  fora do Windows (caminhos `C:\`, pipes nomeados) — o CI Windows as cobre.
- Build da solução inteira, incluindo o app WPF
  (`dotnet build -p:EnableWindowsTargeting=true`), sem warnings.
- CI Windows (build + testes) no PR. `scripts/build-dist.ps1` (portátil +
  instalador) exige Windows + Inno Setup: roda no job de release do CI a cada
  push verde na `main`.
