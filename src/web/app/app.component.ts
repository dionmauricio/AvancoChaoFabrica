import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

interface Project {
  id: string;
  name: string;
  code: string | null;
  isActive: boolean;
}

interface Supplier {
  id: string;
  name: string;
  taxId: string | null;
  isActive: boolean;
}

interface ProductionOrder {
  id: string;
  number: string;
  kind: number;
  projectId: string;
  supplierId: string | null;
  revision: string | null;
  dataDate: string | null;
  isActive: boolean;
}

interface Dashboard {
  itemCount: number;
  totalWeightKg: number | null;
  shippedWeightKg: number;
  manufacturingBalanceKg: number | null;
  weightedProgressPercent: number | null;
  dataDate: string;
  overdueItems: number;
  ordersMissingStageConfiguration: number;
  itemsMissingWeight: number;
  stages: StageSummary[];
}

interface StageSummary {
  stageCode: string;
  stageName: string;
  sequence: number;
  weightPercent: number | null;
  completionPercent: number | null;
  reportedItems: number;
  itemsWithoutReport: number;
}

interface ImportRow {
  rowNumber: number;
  status: string;
  message: string | null;
  values: Record<string, string | null>;
}

interface ImportSheet {
  sheetName: string;
  headerRow: number;
  dataStartRow: number;
  tableType: string;
  sourceDataDate: string | null;
  sourceRevision: string | null;
  headers: string[];
  stageWeights: Record<string, string | null>;
  totalDataRows: number;
  isTruncated: boolean;
  rows: ImportRow[];
}

interface SkippedSheet {
  sheetName: string;
  reason: string;
}

interface ImportPreview {
  id: string;
  fileName: string;
  importedAt: string;
  sheets: ImportSheet[];
  skippedSheets: SkippedSheet[];
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="layout">
      <aside class="sidebar">
        <a class="brand" href="/" aria-label="Avanço, início">
          <span class="brand-mark">A</span>
          <span>AVANÇO<small>CHÃO DE FÁBRICA</small></span>
        </a>
        <div class="nav-label">ACOMPANHAMENTO</div>
        <a class="nav-item active" href="#dashboard"><span aria-hidden="true">▦</span> Visão geral</a>
        <a class="nav-item" href="#stages"><span aria-hidden="true">◷</span> Etapas de fabricação</a>
        <a class="nav-item" href="#imports"><span aria-hidden="true">⇧</span> Importar planilhas</a>
        <div class="sidebar-footer">Dados rastreáveis<br><span>por ordem, fornecedor e item</span></div>
      </aside>

      <main id="dashboard" class="main-content">
        <header class="topbar">
          <div>
            <p class="eyebrow">CONTROLE DE PRODUÇÃO</p>
            <h1>Acompanhamento de avanço</h1>
            <p class="subtitle">Visão consolidada de fabricação e expedição</p>
          </div>
          <div class="status-chip" [class.offline]="!apiAvailable">
            <span class="status-dot"></span> {{ apiAvailable ? 'API conectada' : 'API indisponível' }}
          </div>
        </header>

        <section class="filters card" aria-label="Filtros do painel">
          <label>Projeto
            <select [(ngModel)]="projectId" (ngModelChange)="loadDashboard()">
              <option value="">Todos os projetos</option>
              <option *ngFor="let project of projects" [value]="project.id">{{ project.name }}</option>
            </select>
          </label>
          <label>Fornecedor
            <select [(ngModel)]="supplierId" (ngModelChange)="loadDashboard()">
              <option value="">Todos os fornecedores</option>
              <option *ngFor="let supplier of suppliers" [value]="supplier.id">{{ supplier.name }}</option>
            </select>
          </label>
          <label>Ordem
            <select [(ngModel)]="orderId" (ngModelChange)="loadDashboard()">
              <option value="">Todas as ordens</option>
              <option *ngFor="let order of filteredOrders" [value]="order.id">
                {{ order.number }} · {{ order.kind === 0 ? 'OS' : 'OF' }}
              </option>
            </select>
          </label>
          <button class="button button-quiet" type="button" (click)="clearFilters()">Limpar filtros</button>
        </section>

        <div *ngIf="dashboardError" class="alert alert-error" role="alert">{{ dashboardError }}</div>
        <div *ngIf="loading" class="loading" role="status">Carregando indicadores…</div>

        <section *ngIf="dashboard as data" class="metrics" aria-label="Indicadores de produção">
          <article class="metric-card">
            <span class="metric-label">AVANÇO PONDERADO</span>
            <strong>{{ data.weightedProgressPercent === null ? '—' : (data.weightedProgressPercent | number:'1.1-1') + '%' }}</strong>
            <span class="metric-note">calculado pelas etapas configuradas</span>
          </article>
          <article class="metric-card">
            <span class="metric-label">PESO TOTAL</span>
            <strong>{{ data.totalWeightKg === null ? '—' : (data.totalWeightKg | number:'1.0-0') }} <small *ngIf="data.totalWeightKg !== null">kg</small></strong>
            <span class="metric-note">{{ data.itemCount | number }} itens no filtro</span>
          </article>
          <article class="metric-card">
            <span class="metric-label">EM FABRICAÇÃO</span>
            <strong>{{ data.manufacturingBalanceKg === null ? '—' : (data.manufacturingBalanceKg | number:'1.0-0') }} <small *ngIf="data.manufacturingBalanceKg !== null">kg</small></strong>
            <span class="metric-note">peso total menos peso expedido registrado</span>
          </article>
          <article class="metric-card metric-alert">
            <span class="metric-label">PRAZOS VENCIDOS</span>
            <strong>{{ data.overdueItems | number }}</strong>
            <span class="metric-note">pela data-base {{ data.dataDate | date:'dd/MM/yyyy' }}</span>
          </article>
        </section>

        <section id="stages" class="card stage-card">
          <div class="section-heading">
            <div><p class="eyebrow">PRODUÇÃO</p><h2>Avanço por etapa</h2></div>
            <span class="date-label" *ngIf="dashboard">Data-base: {{ dashboard.dataDate | date:'dd/MM/yyyy' }}</span>
          </div>
          <p *ngIf="dashboard?.stages?.length === 0" class="empty-state">
            Configure os pesos das etapas nas ordens para visualizar o avanço.
          </p>
          <div class="stage-list" *ngIf="dashboard?.stages?.length">
            <div class="stage-row" *ngFor="let stage of dashboard?.stages">
              <div class="stage-name">{{ stage.stageName }}<small>
                Peso: {{ stage.weightPercent === null ? 'incompleto' : (stage.weightPercent | number:'1.0-2') + '%' }} ·
                {{ stage.reportedItems }} medido(s), {{ stage.itemsWithoutReport }} sem apontamento
              </small></div>
              <div class="progress-track" role="progressbar"
                   [attr.aria-label]="'Avanço ' + stage.stageName"
                   [attr.aria-valuenow]="stage.completionPercent ?? 0"
                   aria-valuemin="0" aria-valuemax="100">
                <span [style.width.%]="stage.completionPercent ?? 0"></span>
              </div>
              <strong class="stage-percent">
                {{ stage.completionPercent === null ? '—' : (stage.completionPercent | number:'1.0-1') + '%' }}
              </strong>
            </div>
          </div>
          <div class="shipment-summary" *ngIf="dashboard as data">
            <span>Expedido: <strong>{{ data.shippedWeightKg | number:'1.0-0' }} kg</strong></span>
            <span class="shipment-note">Expedição e avanço de fabricação são indicadores separados.</span>
          </div>
          <p class="data-quality" *ngIf="dashboard && (dashboard.itemsMissingWeight || dashboard.ordersMissingStageConfiguration)">
            Indicadores incompletos: {{ dashboard.itemsMissingWeight }} item(ns) sem peso e
            {{ dashboard.ordersMissingStageConfiguration }} ordem(ns) sem pesos de etapa configurados.
            O avanço ponderado fica indisponível quando faltam dados necessários.
          </p>
        </section>

        <section id="imports" class="card import-card">
          <div class="section-heading">
            <div><p class="eyebrow">CARGA CONTROLADA</p><h2>Importar planilha</h2></div>
            <span class="file-limit">.xlsx · até 25 MB</span>
          </div>
          <p class="import-help">
            A prévia detecta cabeçalhos, erros de fórmula e possíveis repetições. Nenhum item de fabricação
            é criado ou atualizado automaticamente nesta etapa.
          </p>
          <div class="upload-row">
            <label class="file-picker">
              <input type="file" accept=".xlsx" (change)="onFileSelected($event)">
              <span aria-hidden="true">⇧</span> Selecionar arquivo Excel
            </label>
            <span class="selected-file" *ngIf="selectedFile">{{ selectedFile.name }}</span>
            <button class="button button-primary" type="button"
                    [disabled]="!selectedFile || importing" (click)="previewImport()">
              {{ importing ? 'Analisando…' : 'Analisar arquivo' }}
            </button>
          </div>
          <div *ngIf="importError" class="alert alert-error" role="alert">{{ importError }}</div>
          <div *ngIf="preview as result" class="preview">
            <div class="preview-heading">
              <strong>{{ result.fileName }}</strong>
              <span>Prévia salva · {{ result.importedAt | date:'dd/MM/yyyy HH:mm' }}</span>
            </div>
            <div *ngFor="let sheet of result.sheets" class="sheet-preview">
              <h3>{{ sheet.sheetName }} <small>· {{ tableTypeLabel(sheet.tableType) }}
                · cabeçalho na linha {{ sheet.headerRow }}
                · dados a partir da linha {{ sheet.dataStartRow }}
                · data-base {{ sheet.sourceDataDate || 'não informada' }}
                · revisão {{ sheet.sourceRevision || 'não informada' }}</small></h3>
              <p class="table-caption">
                {{ sheet.totalDataRows | number }} linhas de dados na aba ·
                {{ sheet.rows.length | number }} incluídas na amostra
                <strong *ngIf="sheet.isTruncated">· amostra limitada; nem todas as linhas são exibidas</strong>
              </p>
              <p class="table-caption" *ngIf="(sheet.stageWeights | keyvalue).length">
                Pesos de etapa detectados:
                <span *ngFor="let weight of sheet.stageWeights | keyvalue; let last = last">
                  {{ weight.key }}: {{ weight.value }}<span *ngIf="!last"> · </span>
                </span>
              </p>
              <div class="table-scroll">
                <table>
                  <thead><tr><th>Linha</th><th>Situação</th><th *ngFor="let header of visibleHeaders(sheet)">{{ header }}</th></tr></thead>
                  <tbody>
                    <tr *ngFor="let row of sheet.rows.slice(0, 30)">
                      <td>{{ row.rowNumber }}</td>
                      <td><span class="row-status" [class.invalid]="row.status !== 'Review'">{{ statusLabel(row.status) }}</span>
                        <small class="row-message" *ngIf="row.message">{{ row.message }}</small>
                      </td>
                      <td *ngFor="let header of visibleHeaders(sheet)">{{ row.values[header] || '—' }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <p class="table-caption" *ngIf="sheet.rows.length > 30">Mostrando 30 das {{ sheet.rows.length }} linhas retornadas.</p>
            </div>
            <div *ngIf="result.skippedSheets.length" class="alert">
              <strong>Abas não incluídas na prévia:</strong>
              <ul>
                <li *ngFor="let sheet of result.skippedSheets">
                  {{ sheet.sheetName }} — {{ sheet.reason }}
                </li>
              </ul>
            </div>
          </div>
        </section>
        <footer>Avanço de fabricação · Dados consolidados por origem e data-base</footer>
      </main>
    </div>
  `,
})
export class AppComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly api = `${window.location.protocol}//${window.location.hostname}:5080/api`;

  projects: Project[] = [];
  suppliers: Supplier[] = [];
  orders: ProductionOrder[] = [];
  dashboard: Dashboard | null = null;
  preview: ImportPreview | null = null;
  selectedFile: File | null = null;
  projectId = '';
  supplierId = '';
  orderId = '';
  loading = false;
  apiAvailable = false;
  importing = false;
  dashboardError = '';
  importError = '';

  get filteredOrders(): ProductionOrder[] {
    return this.orders.filter(order =>
      (!this.projectId || order.projectId === this.projectId) &&
      (!this.supplierId || order.supplierId === this.supplierId));
  }

  async ngOnInit(): Promise<void> {
    try {
      const [projects, suppliers, orders] = await Promise.all([
        firstValueFrom(this.http.get<Project[]>(`${this.api}/projects`)),
        firstValueFrom(this.http.get<Supplier[]>(`${this.api}/suppliers`)),
        firstValueFrom(this.http.get<ProductionOrder[]>(`${this.api}/orders`)),
      ]);
      this.projects = projects;
      this.suppliers = suppliers;
      this.orders = orders;
      this.apiAvailable = true;
      await this.loadDashboard();
    } catch (error: unknown) {
      this.apiAvailable = false;
      this.dashboardError = this.errorMessage(error, 'Não foi possível conectar à API.');
    }
  }

  async loadDashboard(): Promise<void> {
    this.loading = true;
    this.dashboardError = '';
    const params = new URLSearchParams();
    if (this.projectId) params.set('projectId', this.projectId);
    if (this.supplierId) params.set('supplierId', this.supplierId);
    if (this.orderId) params.set('productionOrderId', this.orderId);
    try {
      this.dashboard = await firstValueFrom(this.http.get<Dashboard>(
        `${this.api}/dashboard${params.toString() ? `?${params}` : ''}`,
      ));
      this.apiAvailable = true;
    } catch (error: unknown) {
      this.apiAvailable = false;
      this.dashboardError = this.errorMessage(error, 'Falha ao consultar o painel.');
    } finally {
      this.loading = false;
    }
  }

  clearFilters(): void {
    this.projectId = '';
    this.supplierId = '';
    this.orderId = '';
    void this.loadDashboard();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    this.preview = null;
    this.importError = '';
  }

  async previewImport(): Promise<void> {
    if (!this.selectedFile) return;
    this.importing = true;
    this.importError = '';
    const form = new FormData();
    form.append('file', this.selectedFile);
    try {
      this.preview = await firstValueFrom(this.http.post<ImportPreview>(
        `${this.api}/imports/preview`, form,
      ));
    } catch (error: unknown) {
      this.importError = this.errorMessage(error, 'Não foi possível analisar a planilha.');
    } finally {
      this.importing = false;
    }
  }

  visibleHeaders(sheet: ImportSheet): string[] {
    return sheet.headers.filter(header =>
      !header.startsWith('COLUMN_') && sheet.rows.some(row => row.values[header] !== null),
    ).slice(0, 8);
  }

  statusLabel(status: string): string {
    if (status === 'Invalid') return 'Revisar';
    if (status === 'PossibleDuplicate') return 'Possível duplicidade';
    return 'Prévia';
  }

  tableTypeLabel(tableType: string): string {
    const labels: Record<string, string> = {
      ManufacturingDetail: 'Detalhamento de fabricação',
      ShipmentList: 'Lista de expedição/material',
      SupplierSummary: 'Resumo por fornecedor',
      OrderSummary: 'Resumo por ordem',
      PlanningView: 'Visão de programação',
    };
    return labels[tableType] ?? tableType;
  }

  private errorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const apiError = error.error as { error?: string; title?: string } | null;
      return apiError?.error ?? apiError?.title ?? `${fallback} HTTP ${error.status}.`;
    }
    return fallback;
  }
}
