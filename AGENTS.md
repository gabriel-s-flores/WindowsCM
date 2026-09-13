# WindowsCM — Copyous para Windows

Clone do Copyous (GNOME) para Windows. Ver vault Obsidian `WindowsCM` em `C:\Users\gabri\Documents\obsidian\WindowsCM` (espelho legível) e `CONTEXT.md` / `docs/adr/` (canônicos no repo quando criados).

## Agent skills

### Issue tracker

Local markdown em `.scratch/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Vocabulário padrão (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context (`CONTEXT.md` + `docs/adr/` na raiz). See `docs/agents/domain.md`.

## Regras de Entrega e Compilação

### Paridade Estrita de Localização (Português e Inglês)
- **Zero Strings Hardcoded**: É expressamente proibido inserir textos visíveis ao usuário (títulos, botões, dicas/tooltips, descrições, itens de menu, alertas, badges de layout, formatos de tempo, etc.) hardcoded em código C# ou em arquivos XAML.
- **Paridade Obrigatória**: Toda e qualquer feature nova ou alteração de interface DEVE implementar paridade absoluta (100%) entre Português e Inglês em `IAppStrings`, `PortugueseAppStrings` e `EnglishAppStrings`.
- **Recursos Dinâmicos XAML**: No XAML, textos devem utilizar `{DynamicResource Loc_<Propriedade>}`. Ao alternar o idioma, o dicionário de recursos deve ser substituído em tempo de execução para invalidar dinamicamente todos os elementos de janelas abertas.
- **Testes de Regressão Automatizados**: Novos textos adicionados a `IAppStrings` devem ser cobertos pelos testes em `LocalizationTests.cs`, garantindo que nenhuma propriedade retorne nulo ou vazio em nenhum dos dois idiomas.

### Compilação Obrigatória (Portátil e Instalador) Após Cada Tarefa
Após concluir qualquer tarefa ou alteração no projeto, é obrigatório compilar e disponibilizar tanto a versão portátil quanto o instalador executável **sempre na mesma pasta** (`dist/`):
- **Portátil**: `dist/WindowsCM-portable/` (executável single-file `WindowsCM.exe` + `LICENSE`) e arquivo zip `dist/WindowsCM-portable.zip`.
- **Instalador**: `dist/WindowsCM-Setup-1.0.0.exe` (gerado via Inno Setup).

Para compilar ambos os alvos automaticamente em uma única etapa para a pasta `dist/`, execute:
```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-dist.ps1
```
