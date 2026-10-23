# Avanço | Chão de Fábrica

Aplicação inicial para consolidar o acompanhamento de fabricação e expedição. A API em .NET e o frontend Angular organizam projetos, fornecedores, OS/OF, itens, etapas, mão de obra e expedições.

## Requisitos

- .NET 10 SDK.
- Node.js 20.19+ ou 22.12+ e npm.
- PostgreSQL local.

## Configure a senha do banco

Abra o arquivo `.env` na raiz do projeto e substitua `COLOQUE_SUA_SENHA_AQUI` pela senha do usuário PostgreSQL:

```dotenv
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=avanco;Username=postgres;Password=COLOQUE_SUA_SENHA_AQUI
```

O arquivo `.env` é lido pela API e pelas ferramentas de migração e está excluído do Git. Não o compartilhe nem o adicione ao repositório. O `.env.example` é apenas um modelo.

Se o banco `avanco` ainda não existir, crie-o no pgAdmin em **Databases > Create > Database**.

## Inicie o sistema

Na raiz do projeto, restaure a ferramenta EF Core, aplique as tabelas ao banco e inicie a API:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project .\src\AvancoChaoFabrica.Api
dotnet run --project .\src\AvancoChaoFabrica.Api -- --urls http://localhost:5080
```

Em outro terminal, inicie a interface Angular:

```powershell
npm install
npm start
```

Abra `http://localhost:4200`. A API fica em `http://localhost:5080`; seu estado pode ser conferido em `/health` e o documento OpenAPI em `/openapi/v1.json`.

## Testes

```powershell
dotnet test .\tests\AvancoChaoFabrica.Api.Tests
npm run build
npm audit
```

## API inicial

- `GET/POST/PUT /api/customers`, `/api/projects` e `/api/suppliers`
- `GET/POST/PUT/DELETE /api/orders`
- `POST /api/orders/{id}/items`, `GET /api/items`, `PUT/DELETE /api/items/{itemId}`
- `PUT /api/orders/{id}/stage-weights` — configura as etapas da ordem; os pesos devem somar 100%.
- `POST /api/orders/{id}/items/{itemId}/progress` — registra um apontamento histórico.
- `GET/POST /api/orders/{id}/labor`
- `GET/POST /api/items/{itemId}/shipments`
- `GET /api/dashboard` — filtros opcionais `projectId`, `supplierId` e `productionOrderId`.
- `POST /api/imports/preview` — prévia de planilha `.xlsx`; não grava itens operacionais.
- `GET /api/imports` e `GET /api/imports/{id}` — histórico das prévias.

## Regras e limitações

- OS e OF são tipos distintos.
- O avanço ponderado fica indisponível quando faltam pesos ou apontamentos necessários. O painel não converte informações ausentes em zero.
- Peso total e saldo ficam indisponíveis se houver item sem peso. Expedições sem peso não reduzem o saldo.
- A prévia reconhece detalhamento de fabricação, listas de expedição/material, resumos por fornecedor/ordem e visões de programação. Mostra a linha do cabeçalho, início dos dados, pesos de etapas, data-base, revisão e abas não reconhecidas.
- Cada aba apresenta a contagem total e uma amostra de até 200 linhas. A indicação de amostra limitada deixa explícito quando nem todas as linhas foram persistidas na prévia.
- Erros de fórmula e possíveis repetições dentro da mesma aba são sinalizados. Linhas correspondentes em outras abas ou em prévias anteriores não são classificadas automaticamente como duplicadas, pois as planilhas são visões relacionadas do mesmo trabalho.
- A prévia ainda não concilia nem importa linhas operacionais. A identidade dos itens, a precedência de revisões e a regra oficial de avanço precisam ser confirmadas antes dessa etapa.
- A base ainda não está pronta para produção: faltam autenticação, telas completas de gestão/reconciliação e testes de integração e da interface.

Validações anteriores neste ambiente: builds da API e do Angular aprovados, testes unitários aprovados e auditorias npm/NuGet sem vulnerabilidades. A migração do banco precisa ser aplicada localmente após preencher `.env`.
