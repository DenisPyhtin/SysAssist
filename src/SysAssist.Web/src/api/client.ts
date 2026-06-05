const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5089'

export type CurrentUserDto = {
  id: string
  login: string
  displayName: string
  email: string
  roles: string[]
}

export type LoginResponse = {
  accessToken: string
  expiresAt: string
  user: CurrentUserDto
}

export type OperationResultDto = {
  success: boolean
  message: string
  correlationId?: string
}

export type DashboardDto = {
  activeEventsCount: number
  pendingApprovalsCount: number
  enabledModulesCount: number
  healthSummary: HealthSummaryItemDto[]
  recentEvents: EventDto[]
  recentNotifications: NotificationDto[]
  recentAudit: AuditEntryDto[]
}

export type HealthSummaryItemDto = {
  status: string
  count: number
}

export type EventDto = {
  id: string
  externalEventId?: string
  createdAt: string
  source: string
  eventType: string
  severity: string
  target: string
  status: string
  correlationId?: string
  summary: string
  moduleId?: string
  payloadJson?: string
  recommendation?: EventRecommendationDto
}

export type EventRecommendationDto = {
  id: string
  classification: string
  explanation: string
  confidence: number
  probableCause?: string
  nextStep?: string
  suggestedActionId?: string
}

export type ApprovalDto = {
  id: string
  eventId: string
  actionId: string
  status: string
  requestedAt: string
  requestedByUserId?: string
  decidedAt?: string
  decidedByUserId?: string
  decisionComment?: string
}

export type ModuleDto = {
  id: string
  key: string
  name: string
  type: string
  description?: string
  isEnabled: boolean
  healthStatus: string
  lastHealthCheckAt?: string
  lastFetchAt?: string
  supportsPolling: boolean
  supportsWebhooks: boolean
  supportsActions: boolean
  useFallbackMode: boolean
  safeMode: boolean
}

export type ModuleSettingDto = {
  id: string
  key: string
  value?: string
  isSecret: boolean
  isRequired: boolean
  valueType: string
  description?: string
  hasValue: boolean
}

export type ModuleActionDto = {
  id: string
  moduleId: string
  actionKey: string
  name: string
  description?: string
  riskLevel: string
  requiresApproval: boolean
  isEnabled: boolean
}

export type CustomModuleManifestDto = {
  apiVersion: string
  key: string
  name: string
  version: string
  author?: string
  category?: string
  description?: string
  entrypoint?: string
  enabled?: boolean
  permissions?: string[]
  tags?: string[]
  settings?: Array<{ key: string; type: string; required?: boolean; secret?: boolean; description?: string; defaultValue?: string }>
  actions?: Array<{ key: string; name: string; risk?: string; requiresApproval?: boolean; description?: string }>
}

export type CustomModuleRuntimeEventDto = {
  eventType: string
  severity: string
  target: string
  summary: string
  externalEventId?: string
}

export type CustomModuleEventsResultDto = {
  success: boolean
  message: string
  events: CustomModuleRuntimeEventDto[]
}

export type AuditEntryDto = {
  id: string
  createdAt: string
  actor: string
  action: string
  resource: string
  result: string
  correlationId?: string
}

export type SystemLogDto = {
  id: string
  createdAt: string
  level: string
  component: string
  message: string
  correlationId?: string
  detailsJson?: string
}

export type NotificationDto = {
  id: string
  createdAt: string
  channel: string
  recipient: string
  subject?: string
  status: string
  relatedEventId?: string
}

export type DiagnosticsDto = {
  status: string
  checkedAt: string
  components: string[]
}

export type ProductionReadinessGateDto = {
  key: string
  passed: boolean
  message: string
  required: boolean
}

export type ProductionReadinessDto = {
  status: string
  checkedAt: string
  gates: ProductionReadinessGateDto[]
}

export type LicenseStatusDto = {
  status: string
  edition: string
  tenantId?: string
  subject?: string
  expiresAt?: string
  isValid: boolean
  features: string[]
  moduleLimit?: number
  message: string
  fingerprint: string
}

export type SabotageScenarioDto = {
  key: string
  name: string
  description: string
  moduleKey: string
  severity: string
  eventType: string
  target: string
  summary: string
  requiresEnabledModule: boolean
  likelyCreatesApproval: boolean
}

export type UserDto = {
  id: string
  login: string
  displayName: string
  email: string
  isActive: boolean
  roles: string[]
}

export type RoleDto = {
  id: string
  name: string
  description?: string
}

export type CreateUserRequest = {
  login: string
  displayName: string
  email: string
  password: string
  isActive: boolean
}

export type UpdateUserRequest = {
  displayName: string
  email: string
  isActive: boolean
  password?: string
}

export type SupportBundlePreview = {
  generatedAt: string
  product: string
  version: string
  databaseProvider: string
  license?: LicenseStatusDto
  modules: ModuleDto[]
  healthHistory: unknown[]
  incidents: EventDto[]
  recommendations: unknown[]
  approvals: ApprovalDto[]
  actions: ModuleActionDto[]
  audit: AuditEntryDto[]
  logs: SystemLogDto[]
  notifications: NotificationDto[]
  users: UserDto[]
  roles: RoleDto[]
  diagnostics: DiagnosticsDto
}

function tokenFromStorage(): string | undefined {
  try {
    const raw = localStorage.getItem('sysassist.session')
    return raw ? (JSON.parse(raw) as { token?: string }).token : undefined
  } catch {
    return undefined
  }
}

async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const token = tokenFromStorage()
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers,
    },
  })

  if (!response.ok) {
    if (response.status === 401 && path !== '/api/auth/login') {
      localStorage.removeItem('sysassist.session')
      window.location.reload()
    }
    const message = await response.text().catch(() => '')
    throw new Error(message || `API responded with ${response.status}`)
  }

  return (await response.json()) as T
}

export async function login(loginName: string, password: string): Promise<LoginResponse> {
  return apiFetch<LoginResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ login: loginName, password }),
  })
}

export function getDashboard(): Promise<DashboardDto> {
  return apiFetch('/api/dashboard')
}

export function getDashboardSummary(): Promise<DashboardDto> {
  return getDashboard()
}

export function listEvents(): Promise<EventDto[]> {
  return apiFetch('/api/events')
}

export function listApprovals(): Promise<ApprovalDto[]> {
  return apiFetch('/api/approvals')
}

export function decideApproval(id: string, approve: boolean, comment: string): Promise<OperationResultDto> {
  return apiFetch(`/api/approvals/${id}/${approve ? 'approve' : 'reject'}`, {
    method: 'POST',
    body: JSON.stringify({ comment }),
  })
}

export function listModules(): Promise<ModuleDto[]> {
  return apiFetch('/api/modules')
}

export function listModuleSettings(moduleId: string): Promise<ModuleSettingDto[]> {
  return apiFetch(`/api/modules/${moduleId}/settings`)
}

export function saveModuleSettings(moduleId: string, settings: ModuleSettingDto[]): Promise<OperationResultDto> {
  return apiFetch(`/api/modules/${moduleId}/settings`, {
    method: 'PUT',
    body: JSON.stringify({ settings: settings.map((item) => ({ key: item.key, value: item.key === 'UseFallbackMode' ? 'false' : item.value })) }),
  })
}

export function replaceModuleSecret(moduleId: string, key: string, value: string): Promise<OperationResultDto> {
  return apiFetch(`/api/modules/${moduleId}/settings/${key}/secret`, {
    method: 'PUT',
    body: JSON.stringify({ value }),
  })
}

export function moduleCommand(moduleId: string, command: 'enable' | 'disable' | 'health' | 'fetch-events' | 'test-connection' | 'reset-demo-mode'): Promise<OperationResultDto> {
  return apiFetch(`/api/modules/${moduleId}/${command}`, { method: 'POST' })
}

export function listActions(): Promise<ModuleActionDto[]> {
  return apiFetch('/api/actions')
}

export function listModuleActions(moduleId: string): Promise<ModuleActionDto[]> {
  return apiFetch(`/api/modules/${moduleId}/actions`)
}

export function listCustomModules(): Promise<CustomModuleManifestDto[]> {
  return apiFetch('/api/custom-modules')
}

export function uploadCustomModule(manifest: CustomModuleManifestDto): Promise<OperationResultDto> {
  return apiFetch('/api/custom-modules', {
    method: 'POST',
    body: JSON.stringify(manifest),
  })
}

export function deleteCustomModule(key: string): Promise<OperationResultDto> {
  return apiFetch(`/api/custom-modules/${encodeURIComponent(key)}`, { method: 'DELETE' })
}

export function customModuleCommand(key: string, command: 'enable' | 'disable'): Promise<OperationResultDto> {
  return apiFetch(`/api/custom-modules/${encodeURIComponent(key)}/${command}`, { method: 'POST' })
}

export function testCustomModule(key: string): Promise<OperationResultDto> {
  return apiFetch(`/api/custom-modules/${encodeURIComponent(key)}/test-connection`, { method: 'POST' })
}

export function fetchCustomModuleEvents(key: string): Promise<CustomModuleEventsResultDto> {
  return apiFetch(`/api/custom-modules/${encodeURIComponent(key)}/fetch-events`, { method: 'POST' })
}

export function executeCustomModuleAction(key: string, actionKey: string, parametersJson = '{}'): Promise<OperationResultDto> {
  return apiFetch(`/api/custom-modules/${encodeURIComponent(key)}/actions/${encodeURIComponent(actionKey)}/execute`, {
    method: 'POST',
    body: JSON.stringify({ target: '', parametersJson }),
  })
}

export async function executeAction(id: string, timeoutMs = 45_000): Promise<OperationResultDto> {
  const controller = new AbortController()
  const timeoutId = window.setTimeout(() => controller.abort(), timeoutMs)

  try {
    return await apiFetch(`/api/actions/${id}/execute`, {
      method: 'POST',
      signal: controller.signal,
      body: JSON.stringify({ target: '', parametersJson: '{}' }),
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new Error(`Action timed out after ${Math.round(timeoutMs / 1000)} seconds. The UI was unlocked, so you can retry after checking SafeMode and module status.`, { cause: error })
    }

    throw error
  } finally {
    window.clearTimeout(timeoutId)
  }
}

export function listAudit(): Promise<AuditEntryDto[]> {
  return apiFetch('/api/audit')
}

export function listLogs(): Promise<SystemLogDto[]> {
  return apiFetch('/api/logs')
}

export function listNotifications(): Promise<NotificationDto[]> {
  return apiFetch('/api/notifications')
}

export function markNotificationRead(id: string): Promise<OperationResultDto> {
  return apiFetch(`/api/notifications/${id}/mark-read`, { method: 'POST' })
}

export function listUsers(): Promise<UserDto[]> {
  return apiFetch('/api/users')
}

export async function createUser(request: CreateUserRequest): Promise<UserDto> {
  return apiFetch('/api/users', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export async function updateUserRoles(id: string, roles: string[]): Promise<OperationResultDto> {
  return apiFetch(`/api/users/${id}/roles`, {
    method: 'PUT',
    body: JSON.stringify({ roles }),
  })
}

export function listRoles(): Promise<RoleDto[]> {
  return apiFetch('/api/roles')
}

export function getDiagnostics(): Promise<DiagnosticsDto> {
  return apiFetch('/api/diagnostics')
}

export function runDiagnostics(): Promise<OperationResultDto> {
  return apiFetch('/api/diagnostics/run', { method: 'POST' })
}

export function listSabotageScenarios(): Promise<SabotageScenarioDto[]> {
  return apiFetch('/api/sabotage/scenarios')
}

export function triggerSabotage(key: string): Promise<OperationResultDto> {
  return apiFetch(`/api/sabotage/${encodeURIComponent(key)}/trigger`, { method: 'POST' })
}

export function getProductionReadiness(): Promise<ProductionReadinessDto> {
  return apiFetch('/api/production-readiness')
}

export function getLicenseStatus(): Promise<LicenseStatusDto> {
  return apiFetch('/api/license')
}

export async function downloadSupportBundle(): Promise<SupportBundlePreview> {
  const token = tokenFromStorage()
  const response = await fetch(`${apiBaseUrl}/api/support-bundle`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })
  if (!response.ok) {
    const message = await response.text().catch(() => '')
    throw new Error(message || `API responded with ${response.status}`)
  }
  return (await response.json()) as SupportBundlePreview
}
