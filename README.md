# Avanço | Chão de Fábrica

Aplicação inicial para consolidar o acompanhamento de fabricação e expedição. A API em .NET e o frontend Angular organizam projetos, fornecedores, OS/OF, itens, etapas, mão de obra e expedições.

## Dependencias instaladas e verificadas

- .NET SDK 10.0.402 e ferramenta local `dotnet-ef` 10.0.13.
- Node.js 24.22.1, npm 12.2.1 e dependencias Angular/OIDC restauradas em `node_modules`.
- PostgreSQL 18.6 instalado como servico Windows `postgresql-x64-18`, em execucao e aceitando conexoes na porta 5432.
- `angular-auth-oidc-client` 20.0.3 e Angular Router 20.3.34, alinhados ao Angular 20.
- `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3; Entity Framework Core 10.0.13.

O banco `avanco` ja recebeu a migracao de usuarios locais. A API foi iniciada com sucesso, criou a conta administrativa inicial, aceitou autenticacao e recusou consulta anonima (HTTP 401). O frontend respondeu HTTP 200 e encaminhou chamadas pela rede de desenvolvimento.

## Configuracao local

O arquivo `.env` da raiz contem a conexao PostgreSQL, uma chave JWT aleatoria e credenciais bootstrap da conta `admin`. Ele esta excluido do Git. Consulte `BootstrapAdmin__Password` nesse arquivo para o primeiro acesso e mantenha-o privado. A conta foi criada no banco; trocar/remover as credenciais bootstrap depois exige o fluxo de troca de senha, que ainda nao existe.

A API aplica migracoes automaticamente no ambiente `Development`. Para outro ambiente, aplique `dotnet-ef database update` antes da inicializacao ou defina `Database__ApplyMigrationsOnStartup=true` durante uma janela de manutencao. O PostgreSQL local precisa continuar ativo.

Inicie a API em um terminal:

```powershell
 dotnet run --project .\src\AvancoChaoFabrica.Api --launch-profile AvancoChaoFabrica.Api
```

Inicie o frontend em outro terminal:

```powershell
 npm start
```

A API fica em `http://localhost:5080`. O Angular escuta na porta 4200 e encaminha `/api` e `/health` ao backend pelo proxy de desenvolvimento. Em outra maquina da rede local, use `http://IP-DO-SERVIDOR:4200`; descubra o IP com `ipconfig`. No Firewall do Windows, permita a porta 4200 somente no perfil de rede privada e na sub-rede confiavel. O backend permanece vinculado a `localhost` no perfil de desenvolvimento.

O frontend de desenvolvimento Angular e HTTP; use este acesso apenas em rede privada confiavel. Nao exponha a porta 4200 nem a API diretamente a Internet. Para uso permanente na LAN, configure HTTPS com certificado confiavel nos computadores clientes.

## Frontend e backend separados / implantacao futura

O Angular e o backend .NET sao projetos independentes. `npm run build` gera `dist/avanco-chao-fabrica/browser`. O frontend carrega `src/web/assets/app-config.json` em tempo de execucao:

- `apiBaseUrl`: vazio para o proxy same-origin; para hosts separados, informe a origem completa da API, sem `/api` no final.
- `authMode`: `local` para contas locais do backend ou `oidc` para provedor OpenID Connect.
- `identity.authority` e `identity.clientId`: obrigatorios em modo OIDC; cadastre a origem atual como redirect URI no provedor.

No backend, `Authentication__Mode=Local` usa contas no PostgreSQL e assina tokens com `Authentication__SigningKey` (minimo 32 bytes). O login usa senha com hash, token com validade de 30 minutos e limite de tentativas. `Admin` e `Operator` podem alterar dados; `Reader` tem acesso de consulta. O administrador inicial vem de `BootstrapAdmin__UserName` e `BootstrapAdmin__Password`. O endpoint autenticado `POST /api/auth/users` permite criar novas contas.

Para hospedar frontend e API em origens diferentes, configure `Cors__AllowedOrigins__0` no servidor da API com a origem HTTPS exata do frontend e `apiBaseUrl` no `app-config.json`. Nunca use `*` para CORS. Em uma instalacao Windows permanente, IIS pode servir os arquivos estaticos Angular, terminar HTTPS e encaminhar `/api` para Kestrel em loopback. Para hospedar na web futuramente, troque `Authentication__Mode` para `OpenIdConnect`, configure `Authentication__Authority`/`Authentication__Audience` e use `authMode: oidc` no frontend; nao reutilize a chave local.

A base ainda requer controles operacionais antes de uma producao formal: troca e recuperacao de senha, desativacao/gestao completa de usuarios, auditoria do usuario que altera cada registro, backup/restauracao testados, HTTPS configurado para os clientes LAN e testes de integracao PostgreSQL/UI. O modo de desenvolvimento nao e servidor de producao.

## Testes e auditoria de dependencias

```powershell
 dotnet test .\tests\AvancoChaoFabrica.Api.Tests
 npm run build
 npm audit
 dotnet list .\src\AvancoChaoFabrica.Api\AvancoChaoFabrica.Api.csproj package --vulnerable --include-transitive
```

Validacao local atual: 7 testes aprovados, builds .NET/Angular aprovados e `npm audit`/NuGet sem vulnerabilidades conhecidas. O projeto ainda nao tem testes de integracao para login, autorizacao ou PostgreSQL.
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
- A base ainda requer controles operacionais antes de uma producao formal: troca/recuperacao de senha, gestao completa de usuarios, auditoria, backup e testes de integracao. Consulte a secao de implantacao.

Validações anteriores neste ambiente: builds da API e do Angular aprovados, testes unitários aprovados e auditorias npm/NuGet sem vulnerabilidades. A migração do banco precisa ser aplicada localmente após preencher `.env`.
