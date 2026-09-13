# Issue 35: Modo Anônimo Efêmero, Limpeza Total e Feedback Visual Inconfundível

Status: resolved
Type: fix
Blocked by: 34

## Contexto

A funcionalidade de modo anônimo (incógnito) apresentava comportamentos disfuncionais:
1. **Captura Descartada**: `CaptureService` retornava `null` imediatamente caso `IsIncognito` estivesse ativo. Os clipes copiados em modo anônimo não eram salvos nem podiam ser visualizados/colados pelo usuário.
2. **Dessincronização de Estado**: Atalhos globais e opções de bandeja forçavam valores conflitantes no popup e no monitor.
3. **Ausência de Repositório Efêmero**: Não havia isolamento entre armazenamento temporário e banco SQLite persistente em disco.
4. **Feedback Visual Imperceptível**: O único indicador era a cor de um pequeno botão, sem avisos, banner ou destaque.

## Requisitos

1. **Sessão Efêmera em Memória**:
   - As cópias realizadas durante o modo anônimo são armazenadas em um banco em memória (`:memory:`) e diretório temporário isolado.
   - Os clipes da sessão anônima ficam disponíveis no popup para navegação, pesquisa e colagem.
2. **Limpeza Irreversível ao Desativar**:
   - Assim que o usuário desativa o modo anônimo, a sessão temporária é destruída, o banco em memória é descartado e todos os arquivos efêmeros de imagem são deletados do disco.
   - Nenhum vestígio é gravado no banco SQLite persistente.
3. **Feedback Visual Claro e Inconfundível**:
   - Banner superior proeminente `MODO ANÔNIMO ATIVO` com botão de saída rápida ("Sair do anônimo").
   - Acento roxo Fluent Windows 11 no popup (borda e topo).
   - Botão destacado na barra de ferramentas e indicação no menu da bandeja (`✓ Modo anônimo (ativo)`).
   - Estado vazio contextual caso não haja clipes copiados ainda na sessão.
4. **Ciclo TDD**:
   - Testes unitários cobrindo isolamento, captura efêmera, limpeza total e sincronização.
