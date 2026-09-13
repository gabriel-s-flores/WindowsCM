# 32: Refatoramento Visual de Configurações, Limite de 100 Itens, Limpeza de Cache e Cores Customizadas por Tipo

Type: task

Status: resolved

Blocked by: 31

## User Report / Requirements:
1. Refatoramento visual da área de configurações no padrão moderno Windows 11 Fluent Design.
2. Configuração de limite máximo da área de clipboard: 100 como máximo e recomendado.
3. Configuração para limpar cache diretamente no aplicativo com feedback em tempo real.
4. Configuração para personalizar as cores dos tipos de arquivos, links, códigos e demais itens da clipboard.
5. Execução pelo fluxo de Matt Pocock (seams acordadas, deep modules, TDD em fatias verticais red-green).

## Answer
Implementado e validado em 2026-09-12 seguindo com rigor o fluxo de Matt Pocock (`codebase-design`, `domain-modeling` e `tdd`):

1. **Limite Máximo e Recomendado da Área de Clipboard (100 itens)**:
   - Atualizado `SettingLimits.cs` com `HistoryLengthMax = 100` e `HistoryLengthDefault = 100` (faixa 10..100).
   - `HistorySettings.cs` e `AppSettings.cs` realizam clamp automático para a nova faixa limite.
   - Adicionado slider Fluent na janela de configurações com display ao vivo (`100 itens`) e botão "Restaurar recomendado (100)".
   - Atualização em tempo real aciona `_store.Evict` para truncar itens excedentes imediatamente ao reduzir o limite.

2. **Limpeza de Cache Diretamente no Aplicativo**:
   - Criado módulo profundo `CacheCleaner.cs` em `WindowsCM.Core.Settings`: método estático puro `Clear(string cacheDir) -> CacheCleanResult`.
   - Exclui com segurança todos os arquivos e subdiretórios de cache (favicons, miniaturas de links, arquivos temporários), preservando a integridade do histórico e diretórios de dados/configurações.
   - Retorna contagem de arquivos excluídos e bytes liberados com tolerância a arquivos bloqueados por I/O.
   - Interface com botão de ação rápida "Limpar Cache Agora" e feedback visual imediato em tempo real na janela de configurações.

3. **Personalização de Cores dos Tipos de Itens**:
   - Criado modelo `ItemColorSettings.cs` em `WindowsCM.Core.Settings`: propriedades para cada `ItemKind` (`Link`, `Code`, `File`, `Image`, `Character`, `Color`, `Text`), métodos de normalização de Hex (`#RGB`, `#RRGGBB`), validação e redefinição.
   - Integrado ao `AppSettings.cs` com clamping e persistência em `settings.json`.
   - Atualizado `ItemTypeTheme.cs` para suportar `ItemColorSettings?` com cálculo automático de fundo translúcido (alpha 12% no tema Claro, 15% no tema Escuro) a partir da cor personalizada.
   - Atualizado `PopupThemeBrushes.cs` para gerar brushes dinâmicos a partir de `ItemColorSettings`.
   - Na janela de configurações, lista completa com pré-visualização ao vivo de amostra de badge para cada tipo, caixas de entrada Hex, botões "Escolher cor" via seletor nativo, "Padrão" individual e "Restaurar todas as cores padrão".
   - Sincronização imediata: ao alterar uma cor, o tema da aplicação e do popup são atualizados instantaneamente em tempo real.

4. **Refatoramento Visual Completo da Janela de Configurações (Windows 11 Fluent Design)**:
   - Redesenhada a interface `SettingsWindow.xaml` e `SettingsWindow.xaml.cs` com layout moderno de duas colunas (Sidebar com ícones Fluent e navegação por abas).
   - Suporte dinâmico aos temas Claro e Escuro do Windows 11 com brushes do `PopupThemeBrushes`.
   - Cards arredondados com hierarquia tipográfica (`Segoe UI Variable Text`), bordas sutis e sombras.
   - 5 seções bem estruturadas: Histórico & Geral, Cores dos Tipos, Cache & Pastas, Atalhos Globais e Sobre & Sistema.

5. **Testes Unitários & Qualidade**:
   - Test-Driven Development (TDD) estrito em fatias verticais com ciclos Red → Green.
   - Criados `CacheCleanerTests.cs` e `ItemColorSettingsTests.cs`.
   - Atualizados `HistorySettingsTests.cs`, `AppSettingsTests.cs` e `ItemTypeThemeTests.cs`.
   - Total de 839 testes executados com 100% de sucesso e 0 avisos/erros.
