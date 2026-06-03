import {
  actionSuccess,
  defineSysAssistModule,
  event,
  failed,
  healthy,
  warning,
  type SysAssistModuleManifest
} from '../src/index'

type Settings = {
  endpointUrl: string
  expectedStatus?: number
}

const manifest: SysAssistModuleManifest = {
  apiVersion: 'sysassist.module/v1',
  key: 'acme-http-monitor',
  name: 'ACME HTTP Monitor',
  version: '0.1.0',
  author: 'ACME Operations',
  category: 'LocalCheck',
  description: 'Checks an HTTP endpoint and produces SysAssist-ready events.',
  entrypoint: './dist/index.js',
  permissions: ['network:http'],
  tags: ['http', 'probe', 'custom'],
  settings: [
    { key: 'endpoint-url', type: 'url', required: true },
    { key: 'expected-status', type: 'int', defaultValue: '200' }
  ],
  actions: [
    { key: 'collect-diagnostics', name: 'Collect diagnostics', risk: 'Low', requiresApproval: false }
  ]
}

export default defineSysAssistModule<Settings>({
  manifest,
  async checkHealth(settings, signal) {
    if (!settings.endpointUrl) {
      return failed('endpointUrl is required')
    }
    const started = performance.now()
    const response = await fetch(settings.endpointUrl, { signal })
    const latencyMs = Math.round(performance.now() - started)
    const expected = settings.expectedStatus ?? 200
    return response.status === expected
      ? healthy(`HTTP ${response.status} returned as expected.`, { url: settings.endpointUrl }, latencyMs)
      : warning(`HTTP ${response.status}; expected ${expected}.`, { url: settings.endpointUrl }, latencyMs)
  },
  async fetchEvents(settings, signal) {
    const response = await fetch(settings.endpointUrl, { signal })
    if (response.ok) {
      return []
    }
    return [
      event({
        externalEventId: `acme-http-${Date.now()}`,
        eventType: 'http.status_mismatch',
        severity: 'Warning',
        target: settings.endpointUrl,
        summary: `Endpoint returned HTTP ${response.status}`,
        payload: { status: response.status }
      })
    ]
  },
  async executeAction(actionKey) {
    return actionSuccess(`${actionKey} diagnostics collected.`)
  }
})
