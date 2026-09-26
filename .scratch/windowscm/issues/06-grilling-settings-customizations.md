# Grilling configs + customizações

Type: grilling
Status: resolved
Blocked by: 01

## Question

Com o inventário `01` em mãos, decidir com o humano (HITL) a paridade de cada opção das 4 páginas prefs: o que entra na spec 1.0, defaults Windows, o que cai (ex.: `Sync Primary`, `Yaru`, `nautilus`), e o vocabulário do `CONTEXT.md`.

Inclui General (History/Feedback/Behavior/Exclusions/Locations), Customization (Dialog/Item/Header/Items-por-tipo/Theme), Shortcuts (7 grupos), Actions (CRUD + Default Actions).

Só começar quando `01` estiver `resolved`. Chamar Skill `grilling` + `domain-modeling`, atualizar `CONTEXT.md` inline, propor ADRs só se hard-to-reverse + surpreendente + trade-off real.

## Answer

Decisões HITL (2 rodadas, tudo aceito como recomendado) + `CONTEXT.md` criado na raiz (7 termos: item, item type, pin, tag, action, incognito mode, history). Sem ADRs (nada hard-to-reverse + surpreendente + trade-off real nesta leva).

- Q8 cortes GNOME-only: Yaru fora; WM_CLASS → processo/exe + seletor de apps; `indicator-display` fora (tray cobre); Dependencies → página "Sobre/Diagnóstico" (versões das libs + abrir pastas no Explorer); Locations mantém.
- Q9 profiles: Default + Compact mantidos (literais do `profiles.ts:164`).
- Q10 History UI: combo só SQLite (Memory debug-only), file-chooser `.db`, length 10–500 d.50, time 0–1440 d.0, 3 opções clipboard-history (keep-pinned-and-tagged default); Behavior igual ao original.
- Q11 idioma + vocabulário: **UI inglês v1** (PT-BR/i18n fora da spec); **item** = domínio/UI, **entry** = só código/DB (em `CONTEXT.md`).
- Q12 sons: as 9 opções + slider dB (conversão ogg→wav é spike de implementação).
- Q13 per-tipo: as 6 telas inteiras com defaults mapeados (thumbnail de arquivo via Shell providers).
- Q14 primeira execução: perfil Default, `show-at-pointer` off, regra show-at-cursor mantida.
- Q15: middle-click pin, open-behavior toggle, swap-copy, swap-scroll — mantidos.
