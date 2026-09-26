# Verificação de estabilidade e desempenho (app real no Windows)

Status: resolved
Type: task
Blocked by: large-window-placement-modes (branch empilhada)

## Pergunta

"Você fez os testes de performance e estabilidade com as novas funções? Esse
aplicativo precisa ser sólido como uma rocha."

Resposta honesta na época: não. Só havia testes funcionais (unitários + CI).
Esta entrega cria a verificação e corrige o que ela encontrou.

## O que passou a existir

- **Smoke do app real no CI Windows** (`scripts/smoke/app-smoke.ps1`, roda em
  todo PR): 300 cópias (texto, código, links, texto de 2 MB, imagens 1600×900,
  listas de arquivos, emoji) com rajadas; referência sem o WindowsCM; latência
  do popup (carga e ocioso) e do atalho; memória e heap gerenciado após GC
  forçado (`dotnet-gcdump`); modos de posicionamento; colar automaticamente de
  ponta a ponta no Notepad pelo atalho real; log de erros.
- **Testes de carga/escala/fuzz no Core** (coleção isolada): composição de
  produção com 5 threads; 2.100 itens com 20 × 1 MB; 1.500 `settings.json`
  corrompidos; 20.000 layouts de monitor aleatórios.

## Bugs encontrados e corrigidos

| # | Gravidade | Origem | Bug |
|---|---|---|---|
| 1 | Crítico | main | WindowsCM bloqueava o copiar dos outros apps: 131 de 300 cópias falhavam (0 de 60 sem ele). Lia o clipboard no próprio aviso de mudança, disputando com a origem. Agora espera 100 ms de silêncio (rajadas lidas uma vez, teto 500 ms). |
| 2 | Alto | main | Print codificado em PNG com o clipboard aberto (~0,5 s num 4K): ninguém copiava/colava nesse intervalo. Agora só copia bytes e codifica depois de fechar. |
| 3 | Alto | main | Imagens de Print Screen / `SetImage` nunca eram capturadas (DIB `BI_BITFIELDS` rejeitado). |
| 4 | Alto | main | `settings.json` com lista de categorias nula impedia o app de abrir. |
| 5 | Alto | PR #6 | Colar após abrir pela bandeja falhava: o Windows devolvia o foco à barra de tarefas. Agora restaura o destino antes de esconder o popup. |
| 6 | Médio | main | Colar numa janela travada podia travar o WindowsCM (`AttachThreadInput`). |
| 7 | Médio | main | `settings.json` corrompido era sobrescrito sem cópia (agora `.corrupt`). |
| 8 | Médio | main | Leitor pedia todos os formatos (força o Excel a renderizar HTML para uma cópia que vira imagem). |
| 9 | Médio | — | Memória: GC retinha ~100 MB a mais; `GCConserveMemory=7`. |

Falsos alarmes investigados (bugs do teste, não do app): links "sumindo"
(`"$i?ref"` no PowerShell), modo livre "fora do lugar" (tela de 1024 px),
colagem "falhando" (espera fixa em runner compartilhado).

## Números (runner Windows, 2 vCPU, sem GPU)

- Clipboard dos outros apps: 0 falhas em 300 cópias.
- Popup: ~150 ms ocioso (p50), 66–190 ms sob carga (p50).
- Atalho → popup: 246–339 ms aquecido; 630–780 ms na 1ª abertura (JIT).
- Heap gerenciado após GC: estável ~60 MB (sem vazamento, 600 cópias).
- Memória privada: estabiliza ~190–210 MB; working set ~300 MB.

## Não coberto (limites honestos)

- Hardware multimonitor real e DPI alto (runner: 1 monitor, 96 DPI).
- Fontes reais com renderização atrasada (Office/Excel/navegadores).
- Sessões de dias; app de destino travado (coberto só por código/revisão).

## Próximo passo sugerido

- `PublishReadyToRun` no `build-dist.ps1` para reduzir a 1ª abertura (medir
  com o smoke contra o executável publicado).
