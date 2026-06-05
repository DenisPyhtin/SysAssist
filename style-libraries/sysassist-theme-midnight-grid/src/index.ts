import { dashboardPreset } from '../dashboard-preset'
import '../theme.css'

export { dashboardPreset }

export const midnightGridStyleLibrary = {
  apiVersion: 'sysassist.style/v1',
  key: 'theme-midnight-grid',
  name: 'Midnight Grid Theme',
  version: '1.0.0',
  author: 'SysAssist Design',
  description: 'High-focus midnight palette with dependency graph, incident waterfall, and correlation density presets.',
  themeAttribute: 'midnight-grid',
  themeCss: 'theme.css',
  tags: ['theme', 'dashboard', 'focus', 'charts'],
  dashboardPreset,
} as const

export default midnightGridStyleLibrary
