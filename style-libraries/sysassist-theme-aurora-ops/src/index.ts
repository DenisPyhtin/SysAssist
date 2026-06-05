import { dashboardPreset } from '../dashboard-preset'
import '../theme.css'

export { dashboardPreset }

export const auroraOpsStyleLibrary = {
  apiVersion: 'sysassist.style/v1',
  key: 'theme-aurora-ops',
  name: 'Aurora Ops Theme',
  version: '1.0.0',
  author: 'SysAssist Design',
  description: 'Cool operational palette with service-temperature, uptime, and capacity dashboard presets.',
  themeAttribute: 'aurora-ops',
  themeCss: 'theme.css',
  tags: ['theme', 'dashboard', 'calm', 'charts'],
  dashboardPreset,
} as const

export default auroraOpsStyleLibrary
