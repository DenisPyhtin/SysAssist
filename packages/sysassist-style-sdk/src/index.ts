export type SysAssistChartPreset = {
  compact?: boolean
  id: string
  metric?: string
  metrics?: string[]
  smoothing?: string
  stages?: string[]
  title: string
  type: string
}

export type SysAssistDashboardPreset = {
  charts: SysAssistChartPreset[]
  id: string
  name: string
  palette: string[]
}

export type SysAssistStyleManifest = {
  apiVersion: 'sysassist.style/v1'
  author: string
  dashboardPreset: SysAssistDashboardPreset
  description: string
  key: string
  name: string
  tags: string[]
  themeAttribute: string
  themeCss: string
  version: string
}

export function defineSysAssistStyleLibrary(manifest: SysAssistStyleManifest) {
  if (manifest.apiVersion !== 'sysassist.style/v1') {
    throw new Error('apiVersion must be sysassist.style/v1')
  }
  if (!/^[a-z0-9][a-z0-9-_.]+$/.test(manifest.key)) {
    throw new Error('Style key must use lowercase letters, numbers, dash, underscore, or dot.')
  }
  return manifest
}

export function applySysAssistStyleLibrary(manifest: SysAssistStyleManifest, root: HTMLElement = document.documentElement) {
  root.dataset.sysassistTheme = manifest.themeAttribute
}
