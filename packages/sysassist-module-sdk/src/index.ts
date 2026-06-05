export type SysAssistSettingType = 'string' | 'url' | 'int' | 'decimal' | 'bool' | 'password' | 'json'
export type SysAssistRiskLevel = 'Low' | 'Medium' | 'High' | 'Critical'
export type SysAssistHealthStatus = 'Healthy' | 'Warning' | 'Error' | 'NotConfigured' | 'Disabled'
export type SysAssistEventSeverity = 'Info' | 'Low' | 'Warning' | 'Error' | 'Critical'

export type SysAssistModuleSetting = {
  key: string
  type: SysAssistSettingType
  required?: boolean
  secret?: boolean
  defaultValue?: string
  description?: string
}

export type SysAssistModuleAction = {
  key: string
  name: string
  risk: SysAssistRiskLevel
  requiresApproval: boolean
  description?: string
}

export type SysAssistModuleManifest = {
  apiVersion: 'sysassist.module/v1'
  key: string
  name: string
  version: string
  author: string
  category: string
  description: string
  entrypoint: string
  permissions: string[]
  tags: string[]
  settings: SysAssistModuleSetting[]
  actions: SysAssistModuleAction[]
}

export type SysAssistHealthResult = {
  status: SysAssistHealthStatus
  message: string
  latencyMs?: number
  details?: Record<string, unknown>
}

export type SysAssistIncomingEvent = {
  externalEventId?: string
  eventType: string
  severity: SysAssistEventSeverity
  target: string
  summary: string
  payload?: Record<string, unknown>
}

export type SysAssistActionResult = {
  success: boolean
  message: string
  requiresApproval?: boolean
  result?: Record<string, unknown>
}

export type SysAssistModuleAdapter<TSettings extends Record<string, unknown> = Record<string, unknown>> = {
  manifest: SysAssistModuleManifest
  checkHealth: (settings: TSettings, signal?: AbortSignal) => Promise<SysAssistHealthResult>
  fetchEvents?: (settings: TSettings, signal?: AbortSignal) => Promise<SysAssistIncomingEvent[]>
  executeAction?: (actionKey: string, settings: TSettings, parameters?: Record<string, unknown>, signal?: AbortSignal) => Promise<SysAssistActionResult>
}

const keyPattern = /^[a-z0-9][a-z0-9-_.]+$/

export function defineSysAssistModule<TSettings extends Record<string, unknown>>(adapter: SysAssistModuleAdapter<TSettings>) {
  validateModuleManifest(adapter.manifest)
  return adapter
}

export function validateModuleManifest(manifest: SysAssistModuleManifest) {
  const missing = ['apiVersion', 'key', 'name', 'version', 'author', 'category', 'description', 'entrypoint']
    .filter((key) => !manifest[key as keyof SysAssistModuleManifest])
  if (missing.length > 0) {
    throw new Error(`SysAssist manifest is missing: ${missing.join(', ')}`)
  }
  if (manifest.apiVersion !== 'sysassist.module/v1') {
    throw new Error('apiVersion must be sysassist.module/v1')
  }
  if (!keyPattern.test(manifest.key)) {
    throw new Error('Module key must use lowercase letters, numbers, dash, underscore, or dot.')
  }
  for (const setting of manifest.settings) {
    if (!keyPattern.test(setting.key)) {
      throw new Error(`Invalid setting key: ${setting.key}`)
    }
  }
  for (const action of manifest.actions) {
    if (!keyPattern.test(action.key)) {
      throw new Error(`Invalid action key: ${action.key}`)
    }
  }
  return manifest
}

export function healthy(message: string, details?: Record<string, unknown>, latencyMs?: number): SysAssistHealthResult {
  return { status: 'Healthy', message, details, latencyMs }
}

export function warning(message: string, details?: Record<string, unknown>, latencyMs?: number): SysAssistHealthResult {
  return { status: 'Warning', message, details, latencyMs }
}

export function failed(message: string, details?: Record<string, unknown>, latencyMs?: number): SysAssistHealthResult {
  return { status: 'Error', message, details, latencyMs }
}

export function notConfigured(message: string): SysAssistHealthResult {
  return { status: 'NotConfigured', message }
}

export function event(input: SysAssistIncomingEvent): SysAssistIncomingEvent {
  return input
}

export function actionSuccess(message: string, result?: Record<string, unknown>): SysAssistActionResult {
  return { success: true, message, result }
}

export function actionFailure(message: string, result?: Record<string, unknown>): SysAssistActionResult {
  return { success: false, message, result }
}
