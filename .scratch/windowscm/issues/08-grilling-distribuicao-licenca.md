# Distribuição + licença + readiness to-spec

Type: grilling
Status: resolved
Blocked by: 02, 03

## Question

Fechar com o humano os últimos itens para `to-spec` poder rodar:instalador Inno Setup (portable single-file, upgrade, uninstall limpa?), MSIX adiado documentado em Out of scope, autostart `Run` + `--hidden`, `Mutex` single-instance, alvo Win10 20H2+/Win11, GPL-3.0-or-later (headers, NOTICE, atribuição ao Copyous/Pano), e checklist de readiness (todos 01–07 resolved? fog zerado?).

Só começar quando `02` e `03` estiverem `resolved`. Chamar Skill `grilling` + `domain-modeling`.

## Answer

Decisões HITL (aceitas como recomendado) + readiness confirmado. Sem ADRs.

- Q16 installer: Inno **per-user** (sem admin), `%LocalAppData%\Programs\WindowsCM`,
  atalho Start Menu (sem Desktop default), autostart **opt-in** (`Run` +
  `--hidden`, checkbox installer + toggle settings), portable = single-file
  zipado, upgrade preserva dados, **uninstall mantém DB/settings** (checkbox
  de remoção, desmarcado).
- Q17 GPL-3.0: `LICENSE` raiz + cópia no installer, headers SPDX nos fontes
  (fase `implement`), Sobre com créditos (Copyous/Pano + 4 libs MIT), repo
  público dia 1.
- Q18 readiness: 01–07 resolved, fog zero, `CONTEXT.md`, sem ADRs pendentes —
  **mapa done, handoff para `to-spec` autorizado**.
