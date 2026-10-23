# Prompt de desenvolvimento — Sistema de acompanhamento do avanço de fabricação

## Objetivo

Desenvolva um sistema web profissional para acompanhar o avanço de fabricação de estruturas metálicas e seus itens, ordens, fornecedores e expedições. O sistema será usado para consultar e atualizar os dados operacionais e para gerar visões equivalentes às planilhas de acompanhamento fornecidas.

As planilhas anexadas representam **visões relacionadas do mesmo trabalho**, não projetos ou bases independentes por padrão. Uma planilha pode mostrar o avanço por fornecedor, outra por ordem e outra a relação de materiais/expedição. O sistema deve relacionar essas visões aos mesmos registros subjacentes, sem duplicar peso, itens ou avanço.

Antes de implementar, examine todas as planilhas e demais arquivos de referência disponíveis no projeto. Identifique abas, cabeçalhos, fórmulas, unidades, datas-base, revisões, valores de domínio e relações entre os registros. Não presuma que o leiaute será idêntico em todos os arquivos ou que uma coluna com nome parecido possui sempre o mesmo significado.

## Tecnologias e princípios

- API e backend: .NET.
- Banco de dados principal: PostgreSQL.
- Frontend: Angular.
- Utilize Clean Code, separação de responsabilidades, princípios SOLID e padrões adequados ao problema, sem criar abstrações desnecessárias.
- MongoDB, RabbitMQ ou outras tecnologias são opcionais; só os adote se houver uma necessidade concreta e justificada.
- Projete uma API documentada, validada, segura, testável e versionável.
- Use português do Brasil na interface, com datas, números, percentuais, pesos e unidades formatados de forma consistente.

## Como interpretar as planilhas

As abas são relatórios e fontes de dados com propósitos diferentes. Considere especialmente:

- **Análise geral por fornecedor**: fornecedor/subfornecedor, descrição, peso, avanço por etapa, saldo em fabricação, datas contratual e reprogramada, tendência de entrega na obra e percentual de avanço.
- **Análise geral por ordem**: visão consolidada da ordem e seus itens, pesos e etapas de fabricação.
- **Banco de dados**: registros detalhados de fabricação, com campos que podem incluir projeto, fornecedor/subfornecedor, OF, desenho, TAG, tipagem, marca, descrição, solicitante, quantidade, peso e avanço por etapa.
- **Análise detalhada, análise por prioridade e programação**: detalhamento dos itens, datas previstas, situação e sequenciamento, conforme os campos existentes em cada arquivo.
- **LX e LX-R2 / listas de expedição**: itens e materiais para expedição, que podem incluir pacote de contratação, RT, CWA, disciplina, CWP, TAG de referência, código do material (SKU), descrição, quantidade, unidade, peso unitário e total, documento e revisão Vale, fornecedor, romaneio e data de embarque.
- **Resumos e demais abas**: utilize-os para identificar indicadores e regras de negócio, mas trate valores agregados como visões calculadas, não como registros adicionais.

Os nomes e a presença das colunas podem variar. Mapeie os cabeçalhos reais para um modelo coerente, preservando o nome e o valor de origem para rastreabilidade. Mantenha explícitas as unidades, principalmente kg, quantidade e percentual.

Não confunda **OS (ordem de serviço)** com **OF (ordem de fabricação)**. Preserve a nomenclatura encontrada em cada fonte e modele a relação entre elas somente quando confirmada pelos dados ou pelo responsável pelo processo.

## Modelo funcional

Implemente, no mínimo, operações de consulta, criação, edição e inativação, conforme aplicável, para:

- Clientes e projetos.
- Fornecedores e subfornecedores.
- Ordens de serviço e/ou fabricação, sem misturar identificadores distintos.
- Desenhos, pacotes, TAGs e itens vinculados às ordens.
- Etapas e apontamentos de avanço de fabricação por item e fornecedor.
- Datas planejadas, contratuais e reprogramadas.
- Expedições, romaneios, documentos de liberação e embarques.
- Mão de obra alocada por ordem, com equipe/profissional, função, período e horas, quando esses dados forem fornecidos pelo processo. Não invente dados de mão de obra ausentes nas planilhas.

Use inativação em vez de exclusão definitiva quando houver histórico ou registros dependentes. Registre quem criou ou alterou dados e quando.

## Avanço e indicadores

Represente as etapas encontradas nas fontes sem presumir que todas as ordens usam a mesma sequência. As planilhas incluem, conforme a visão, etapas como recebimento de matéria-prima, preparação, montagem, solda, acabamento, pré-montagem, pintura, embalagem e expedição.

- Configure as etapas, sua ordem e os pesos percentuais utilizados no cálculo do avanço ponderado. Use os pesos definidos na fonte/processo; não os invente nem aplique um único conjunto global se houver diferenças.
- Diferencie quantidade/peso total, peso executado em cada etapa, percentual por etapa, avanço ponderado total, peso expedido e saldo em fabricação.
- Apresente a regra de cálculo e os dados que a originaram. Evite somar percentuais simples quando o cálculo depender de peso ou ponderação.
- Trate avanço e expedição como conceitos distintos; não considere automaticamente um item concluído só por estar expedido ou uma etapa concluída por falta de apontamento.
- Calcule atrasos comparando as datas pertinentes e mostre claramente a data-base usada. Não classifique uma entrega como atrasada se faltar data necessária.
- Permita análise geral e detalhamento por cliente/projeto, fornecedor, OS/OF, desenho, pacote, TAG, item, etapa e período.

## Importação, histórico e qualidade dos dados

Disponibilize uma forma controlada de importar arquivos Excel e revisar os dados antes de confirmar a carga. Preserve, por importação, o arquivo de origem, aba, linha, data da importação, data-base e revisão informadas, além do usuário responsável.

- Detecte registros repetidos e ofereça uma prévia de inclusões, alterações, possíveis duplicidades e inconsistências antes de gravar.
- Relacione as planilhas por identificadores e contexto confirmados, como projeto, fornecedor, OF/OS, desenho, pacote e TAG. **Não use somente a TAG** como critério de identidade quando isso puder associar itens diferentes.
- Ao encontrar o mesmo item em diferentes visões ou arquivos, atualize/relacione o registro apropriado em vez de duplicá-lo. Preserve cada versão importada e a sua origem.
- Quando fontes divergirem, não sobrescreva silenciosamente nem escolha automaticamente um arquivo como autoridade apenas pelo nome. Mostre os valores e suas respectivas revisões/datas-base e permita resolver o conflito com uma regra auditável.
- Valide campos obrigatórios, formatos, unidades, percentuais, datas, pesos, quantidades e relações. Valores inválidos ou ausentes devem ser sinalizados, nunca convertidos silenciosamente em zero.
- Algumas planilhas podem conter fórmulas quebradas ou valores `#REF!`. Não trate esses erros como valores válidos nem como zero: sinalize a inconsistência e, quando possível, recalcule o indicador a partir dos registros detalhados e das regras confirmadas.
- Mantenha histórico de alterações e importações para que relatórios anteriores possam ser interpretados com sua data-base e revisão correspondentes.

## Telas e relatórios esperados

Implemente, no mínimo:

1. **Painel geral** com filtros por cliente, projeto, fornecedor, OS/OF e período. Apresente peso total, avanço ponderado, avanço por etapa, saldo em fabricação, expedição e alertas de prazo.
2. **Visão por fornecedor** com os campos da análise geral da planilha, incluindo peso, etapas, saldo, datas e tendência de entrega.
3. **Visão por OS/OF** com consolidação e detalhamento dos itens, desenhos, pacotes e TAGs, mantendo claro qual tipo de ordem está sendo exibido.
4. **Detalhe do item** com identificação, peso/quantidade, fornecedor, etapa e histórico de apontamentos, datas e expedições relacionados.
5. **Programação e prioridades** com datas e critérios de priorização explícitos, filtros, pendências e atrasos; não invente prioridade quando ela não estiver nos dados.
6. **Expedições** com itens, quantidade, pesos, documentos/revisões, fornecedor, romaneio e data de embarque disponíveis na fonte.
7. **Importações e divergências** com prévia, validações, erros e resolução auditável dos conflitos entre arquivos.

As visões devem permitir navegar do total consolidado até os registros detalhados que o compõem. Totais e percentuais apresentados devem ser conciliáveis com os dados filtrados e indicar sua data-base.

## Interface visual

Utilize a paleta:

- Primária: `#FCA311`.
- Secundária: `#0E0E0E`.
- Apoio: branco gelo e cinza claro para fundos, divisões e contraste.

Crie uma interface responsiva, acessível e consistente. Use tabelas com ordenação, filtros, paginação e estados vazios/carregando/erro claros. Destaque etapas, atrasos e divergências sem depender somente de cores; inclua rótulos ou ícones com significado acessível.

## Critérios de aceite

- Um mesmo registro importado de visões diferentes não gera dupla contagem.
- É possível consultar o avanço consolidado e navegar até o fornecedor, a ordem e os itens que formam o resultado.
- Avanço por etapa, avanço total, saldo e expedição são exibidos separadamente e calculados conforme pesos/regras confirmados.
- Data-base, revisão e origem dos dados ficam visíveis e rastreáveis.
- Divergências, dados inválidos e erros de fórmula são apresentados para revisão, sem conversão silenciosa em valores válidos.
- As telas e a API cobrem os fluxos de consulta e manutenção descritos acima, com validações e testes automatizados dos cálculos e das importações.

Implemente a solução por etapas, começando pela análise dos arquivos e pelo mapeamento do domínio. Antes de codificar regras ambíguas — especialmente identidade de registros, significado de OS/OF, precedência entre fontes e cálculo do avanço — documente a dúvida e solicite confirmação. Não preencha lacunas das planilhas com suposições apresentadas como fatos.
