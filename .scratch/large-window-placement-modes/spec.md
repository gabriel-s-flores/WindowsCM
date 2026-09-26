# Barra grande: monitor fixo e modo livre

Status: resolved
Type: feature
Blocked by: auto-paste-toggle (branch empilhada)

## Pedido

Além de encostar a barra grande nas 4 bordas da tela (como hoje), ter um
modo para múltiplos monitores e um modo livre, em que o usuário arrasta a
barra para onde quiser.

## Decisões do usuário

- Múltiplos monitores = **escolher um monitor fixo** onde a barra sempre abre.
- Modo livre = **arrastar e redimensionar**, lembrando posição e tamanho.

## Solução

Detalhes em `docs/adr/0006-large-window-placement-modes.md`.
Configurações → Layout & Posicionamento → Área principal → **Posicionamento**:
"Na borda — monitor do mouse" (padrão, igual antes), "Na borda — monitor
fixo" (lista de monitores + **Identificar**) e "Livre — arrastar e
redimensionar" (+ **Restaurar posição**).

## Smoke manual (Windows)

- [ ] Monitor fixo com 2 telas: com o mouse na tela 1, o atalho abre a barra
      na tela escolhida, encostada na borda configurada.
- [ ] Identificar mostra "1"/"2" no centro de cada tela e some em 2 s sem
      roubar o foco.
- [ ] Desconectar o monitor escolhido: a barra abre no principal; reconectar:
      volta para o escolhido.
- [ ] Livre: arrastar pela alça do topo e pelas áreas vazias do cabeçalho;
      redimensionar pelas 4 bordas e cantos; fechar e reabrir no mesmo lugar e
      tamanho; horizontal e vertical guardam retângulos separados.
- [ ] Livre: cliques em cards, botões e busca continuam funcionando normalmente.
- [ ] Restaurar posição recentraliza a barra no próximo uso.
