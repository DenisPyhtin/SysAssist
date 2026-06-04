import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import gsap from 'gsap'
import {
  Activity,
  AlertTriangle,
  Archive,
  Bell,
  Blocks,
  Check,
  CheckCircle2,
  ChevronRight,
  Clock3,
  ClipboardCheck,
  DatabaseZap,
  Download,
  FileClock,
  FileJson,
  FileText,
  Gauge,
  HardDrive,
  History,
  KeyRound,
  Layers3,
  LifeBuoy,
  Loader2,
  Lock,
  LogOut,
  Logs,
  Mail,
  MessageCircle,
  Palette,
  Play,
  PlugZap,
  Radio,
  RefreshCcw,
  RotateCcw,
  Search,
  Server,
  ShieldCheck,
  SlidersHorizontal,
  Star,
  Store,
  Tag,
  TerminalSquare,
  Upload,
  UserCog,
  UsersRound,
  X,
} from 'lucide-react'
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import {
  decideApproval,
  deleteCustomModule,
  downloadSupportBundle,
  executeCustomModuleAction,
  executeAction,
  fetchCustomModuleEvents,
  getDashboard,
  getDiagnostics,
  getLicenseStatus,
  getProductionReadiness,
  listActions,
  listApprovals,
  listAudit,
  listCustomModules,
  listEvents,
  listLogs,
  listModules,
  listModuleSettings,
  listNotifications,
  listRoles,
  listUsers,
  login,
  markNotificationRead,
  moduleCommand,
  replaceModuleSecret,
  runDiagnostics,
  saveModuleSettings,
  testCustomModule,
  uploadCustomModule,
  type ApprovalDto,
  type AuditEntryDto,
  type CreateUserRequest,
  type CustomModuleManifestDto,
  type CurrentUserDto,
  type EventDto,
  type ModuleDto,
  type ModuleSettingDto,
  type NotificationDto,
  type OperationResultDto,
  createUser,
  customModuleCommand,
  updateUserRoles,
} from '../api/client'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { cn } from '../lib/utils'
import { useShellStore, type PageId, type ToastTone } from '../store/useShellStore'

const navItems: Array<{ id: PageId; label: string; icon: typeof Gauge; roles?: string[] }> = [
  { id: 'dashboard', label: 'Dashboard', icon: Gauge, roles: ['Operator', 'Engineer', 'Admin', 'SeniorAdmin', 'Auditor'] },
  { id: 'events', label: 'Events', icon: Activity, roles: ['Operator', 'Engineer', 'Admin', 'SeniorAdmin'] },
  { id: 'approvals', label: 'Approvals', icon: ClipboardCheck, roles: ['Admin', 'SeniorAdmin'] },
  { id: 'modules', label: 'Modules', icon: Blocks, roles: ['Engineer', 'Admin', 'SeniorAdmin'] },
  { id: 'actions', label: 'Actions', icon: Play, roles: ['Engineer', 'Admin', 'SeniorAdmin'] },
  { id: 'audit', label: 'Audit', icon: History, roles: ['Auditor', 'Admin', 'SeniorAdmin'] },
  { id: 'logs', label: 'Logs', icon: Logs, roles: ['Auditor', 'Admin', 'SeniorAdmin'] },
  { id: 'notifications', label: 'Notifications', icon: Bell, roles: ['Operator', 'Engineer', 'Admin', 'SeniorAdmin'] },
  { id: 'users', label: 'Users / Roles', icon: UsersRound, roles: ['Admin'] },
  { id: 'support', label: 'Support Bundle', icon: LifeBuoy, roles: ['Auditor', 'Admin', 'SeniorAdmin'] },
  { id: 'diagnostics', label: 'Diagnostics', icon: TerminalSquare, roles: ['Engineer', 'Admin', 'SeniorAdmin'] },
]

const severityColors: Record<string, string> = {
  Critical: '#c9867d',
  High: '#b8996f',
  Warning: '#d8c2a3',
  Medium: '#b8996f',
  Low: '#8fa98a',
  Info: '#eee2d2',
}

const severityOrder = ['Critical', 'High', 'Warning', 'Medium', 'Low', 'Info']

const healthColors: Record<string, string> = {
  Healthy: '#b8996f',
  Degraded: '#b8996f',
  Warning: '#b8996f',
  Unhealthy: '#ef4444',
  Error: '#ef4444',
  Disabled: '#6b5a4c',
  NotConfigured: '#d8c2a3',
  Unknown: '#d8c2a3',
}

const healthSummaryOrder = ['Healthy', 'Warning', 'Error', 'Unhealthy', 'Degraded', 'NotConfigured', 'Disabled', 'Unknown']

function animateIfPresent(selector: string, fromVars: gsap.TweenVars, toVars: gsap.TweenVars) {
  const targets = gsap.utils.toArray(selector)
  if (targets.length > 0) {
    gsap.fromTo(targets, fromVars, toVars)
  }
}

function ResponsiveChartFrame({ children, className }: { children: (size: { height: number; width: number }) => React.ReactNode; className?: string }) {
  const frameRef = useRef<HTMLDivElement>(null)
  const [size, setSize] = useState({ height: 0, width: 0 })

  useEffect(() => {
    const node = frameRef.current
    if (!node) {
      return
    }

    const updateSize = () => {
      const rect = node.getBoundingClientRect()
      const nextSize = {
        height: Math.max(0, Math.floor(rect.height)),
        width: Math.max(0, Math.floor(rect.width)),
      }

      setSize((current) => (current.height === nextSize.height && current.width === nextSize.width ? current : nextSize))
    }

    updateSize()
    const animationFrame = window.requestAnimationFrame(updateSize)
    const observer = typeof ResizeObserver === 'undefined' ? undefined : new ResizeObserver(updateSize)
    observer?.observe(node)

    return () => {
      window.cancelAnimationFrame(animationFrame)
      observer?.disconnect()
    }
  }, [])

  const isReady = size.height >= 24 && size.width >= 24

  return (
    <div ref={frameRef} className={cn('responsive-chart-frame', className)}>
      {isReady ? children(size) : <div aria-hidden="true" className="responsive-chart-placeholder" />}
    </div>
  )
}

function healthToneClasses(status: string) {
  if (status === 'Healthy') {
    return 'border-emerald-300/30 bg-emerald-300/10 text-emerald-200'
  }
  if (status === 'Warning' || status === 'Degraded') {
    return 'border-amber-300/30 bg-amber-300/10 text-amber-200'
  }
  if (status === 'Disabled') {
    return 'border-slate-500/30 bg-slate-700/20 text-slate-400'
  }
  if (status === 'Unhealthy' || status === 'Error') {
    return 'border-rose-300/30 bg-rose-300/10 text-rose-200'
  }
  return 'border-stone-300/30 bg-stone-300/10 text-stone-200'
}

function healthPulseClass(status: string) {
  if (status === 'Healthy') {
    return 'bg-emerald-400'
  }
  if (status === 'Warning' || status === 'Degraded') {
    return 'bg-amber-400'
  }
  if (status === 'Disabled') {
    return 'bg-slate-500'
  }
  if (status === 'Unhealthy' || status === 'Error') {
    return 'bg-rose-400'
  }
  return 'bg-stone-300'
}

const healthDescriptions: Record<string, { title: string; detail: string; action: string }> = {
  Healthy: {
    title: 'Healthy modules',
    detail: 'Enabled integrations are responding and the latest checks completed without operator action.',
    action: 'Keep monitoring. No immediate intervention is needed.',
  },
  Warning: {
    title: 'Warning modules',
    detail: 'The integration is reachable, but recent checks or fallback signals show a condition that should be reviewed.',
    action: 'Open Modules and run Health before enabling real actions.',
  },
  NotConfigured: {
    title: 'Not configured',
    detail: 'Required settings are missing or the module has not been moved from safe demo configuration.',
    action: 'Open Module Settings and fill required connection fields.',
  },
  Disabled: {
    title: 'Disabled modules',
    detail: 'These modules are intentionally excluded from intake, polling, and operational commands.',
    action: 'Enable only when credentials and SafeMode posture are ready.',
  },
  Unhealthy: {
    title: 'Unhealthy modules',
    detail: 'The last health probe failed or the adapter reported a hard error.',
    action: 'Run diagnostics, then inspect logs and module settings.',
  },
  Error: {
    title: 'Adapter errors',
    detail: 'A real connection check threw an error or the remote service rejected the probe.',
    action: 'Open Modules, inspect the latest health message, then correct endpoint or secret settings.',
  },
  Unknown: {
    title: 'Unknown health',
    detail: 'No recent health signal is available for this status bucket.',
    action: 'Run diagnostics to refresh health evidence.',
  },
  Degraded: {
    title: 'Degraded modules',
    detail: 'The module is still partially available, but one or more checks are outside normal range.',
    action: 'Review recent events and run a focused module health check.',
  },
}

type HealthSummaryChartItem = {
  count: number
  hasActivePeer: boolean
  isActive: boolean
  status: string
}

type IntakeMetric = 'events' | 'severity' | 'approvals' | 'modules'
type IntakeRange = '60' | '360' | '1440' | 'custom'

const intakeMetricOptions: Array<{ id: IntakeMetric; label: string; description: string }> = [
  { id: 'events', label: 'Events', description: 'Incident intake volume' },
  { id: 'severity', label: 'Severity', description: 'Weighted criticality' },
  { id: 'approvals', label: 'Approvals', description: 'Risk gate pressure' },
  { id: 'modules', label: 'Modules', description: 'Unhealthy module load' },
]

const intakeRangeOptions: Array<{ id: IntakeRange; label: string; minutes: number; points: number }> = [
  { id: '60', label: 'Last hour', minutes: 60, points: 8 },
  { id: '360', label: 'Last 6 hours', minutes: 360, points: 10 },
  { id: '1440', label: 'Last 24 hours', minutes: 1440, points: 12 },
  { id: 'custom', label: 'Custom', minutes: 360, points: 10 },
]

function hasAnyRole(user: CurrentUserDto | undefined, roles: string[] | undefined) {
  if (!roles || roles.length === 0) {
    return true
  }
  return Boolean(user?.roles.some((role) => roles.includes(role)))
}

function pageRoles(page: PageId) {
  if (page === 'modules-store') {
    return ['Engineer', 'Admin', 'SeniorAdmin']
  }
  return navItems.find((item) => item.id === page)?.roles
}

function toneForSeverity(severity: string): ToastTone {
  if (severity === 'Critical' || severity === 'High') {
    return 'danger'
  }
  if (severity === 'Warning' || severity === 'Medium') {
    return 'warning'
  }
  if (severity === 'Low') {
    return 'success'
  }
  return 'neutral'
}

function toneForStatus(status: string): ToastTone {
  if (['Healthy', 'Completed', 'Executed', 'Success', 'Read', 'Ready'].includes(status)) {
    return 'success'
  }
  if (['Pending', 'PendingApproval', 'Analyzing', 'Degraded', 'LocalOnly'].includes(status)) {
    return 'warning'
  }
  if (['Blocked', 'Critical', 'Failed', 'Rejected', 'Unhealthy', 'Error'].includes(status)) {
    return 'danger'
  }
  return 'neutral'
}

function timeLabel(value?: string) {
  return value ? new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit', month: 'short', day: '2-digit' }).format(new Date(value)) : 'Never'
}

function fullDateTimeLabel(value?: string) {
  return value ? new Intl.DateTimeFormat(undefined, { day: '2-digit', month: 'long', hour: '2-digit', minute: '2-digit' }).format(new Date(value)) : 'No events'
}

function intakeTickLabel(value: string, rangeMinutes: number) {
  const date = new Date(value)
  return new Intl.DateTimeFormat(undefined, rangeMinutes <= 1440
    ? { hour: '2-digit', minute: '2-digit' }
    : { day: '2-digit', month: 'short', hour: '2-digit' }).format(date)
}

function shortId(value?: string) {
  return value ? value.slice(0, 8) : 'none'
}

function riskTone(value?: string): ToastTone {
  if (value === 'Critical' || value === 'High') {
    return 'danger'
  }
  if (value === 'Medium') {
    return 'warning'
  }
  if (value === 'Low') {
    return 'success'
  }
  return 'neutral'
}

function moduleReadyForActions(module?: ModuleDto) {
  return Boolean(module?.isEnabled && module.healthStatus === 'Healthy')
}

function moduleIconFor(type?: string) {
  if (type === 'Database') {
    return DatabaseZap
  }
  if (type === 'Infrastructure') {
    return Server
  }
  if (type === 'Notification') {
    return Mail
  }
  if (type === 'LocalCheck') {
    return HardDrive
  }
  if (type === 'Advisor') {
    return SlidersHorizontal
  }
  return Radio
}

function settingInputType(setting: ModuleSettingDto) {
  if (setting.isSecret) {
    return 'password'
  }
  if (setting.valueType === 'url') {
    return 'url'
  }
  if (setting.valueType === 'int' || setting.valueType === 'decimal') {
    return 'number'
  }
  return 'text'
}

type ModuleConnectionProfile = {
  defaults: Record<string, string>
  helper: string
  keys: string[]
  label: string
}

const moduleConnectionProfiles: Record<string, ModuleConnectionProfile> = {
  grafana: {
    label: 'HTTP API / Web UI',
    helper: 'Grafana health checks use /api/health. API token is optional for basic availability testing.',
    keys: ['BaseUrl', 'ApiToken', 'WebhookSecret', 'OrganizationId', 'SafeMode'],
    defaults: { BaseUrl: 'http://34.107.8.65:3000', SafeMode: 'true', VerifySsl: 'false' },
  },
  zabbix: {
    label: 'Zabbix API / JSON-RPC',
    helper: 'Use token or username/password. If you only expose server port 10051, the test verifies reachability, not JSON-RPC auth.',
    keys: ['BaseUrl', 'ApiToken', 'Username', 'Password', 'UseApiToken', 'SafeMode'],
    defaults: { BaseUrl: 'http://34.107.8.65:10051', UseApiToken: 'true', SafeMode: 'true', VerifySsl: 'false' },
  },
  'prometheus-alertmanager': {
    label: 'Prometheus / Alertmanager REST API',
    helper: 'Alertmanager and Prometheus readiness are tested through real HTTP probes. Webhook secret is stored only through the secret endpoint.',
    keys: ['PrometheusBaseUrl', 'AlertmanagerBaseUrl', 'WebhookSecret', 'ReceiverName', 'SeverityFilter', 'SafeMode'],
    defaults: { PrometheusBaseUrl: 'http://34.107.8.65:9090', AlertmanagerBaseUrl: 'http://34.107.8.65:9093', ReceiverName: 'sysassist', SeverityFilter: 'warning,critical', SafeMode: 'true', VerifySsl: 'false' },
  },
  postgresql: {
    label: 'TCP / Connection string',
    helper: 'ConnectionString is encrypted at rest and masked in the API. Test opens PostgreSQL and runs SELECT 1.',
    keys: ['ConnectionString', 'Host', 'Port', 'DatabaseName', 'Username', 'Password', 'UseConnectionString', 'SafeMode'],
    defaults: { Host: '34.107.8.65', Port: '5432', DatabaseName: 'sysassistant_infra', Username: 'sysadmin', UseConnectionString: 'true', SafeMode: 'true' },
  },
  redis: {
    label: 'TCP / Redis protocol',
    helper: 'Test opens Redis TCP and sends PING when real mode is enabled.',
    keys: ['RedisUrl', 'Host', 'Port', 'Password', 'DatabaseIndex', 'UseTls', 'SafeMode'],
    defaults: { RedisUrl: 'redis://34.107.8.65:6379', Host: '34.107.8.65', Port: '6379', DatabaseIndex: '0', UseTls: 'false', SafeMode: 'true' },
  },
  docker: {
    label: 'Docker API / socket',
    helper: 'Use a Docker TCP endpoint or local socket bridge. Test checks the configured real Docker endpoint.',
    keys: ['DockerEndpoint', 'UseLocalDockerSocket', 'DockerSocketPath', 'ApiVersion', 'ContainerNameFilter', 'AllowContainerRestart', 'SafeMode'],
    defaults: { DockerEndpoint: '', UseLocalDockerSocket: 'true', DockerSocketPath: '/var/run/docker.sock', AllowContainerRestart: 'false', SafeMode: 'true' },
  },
  'http-endpoint': {
    label: 'Blackbox HTTP probe',
    helper: 'EndpointUrl is tested directly. Use the probe URL you want SysAssist to watch.',
    keys: ['EndpointUrl', 'Method', 'ExpectedStatusCode', 'TimeoutSeconds', 'SafeMode'],
    defaults: { EndpointUrl: 'http://34.107.8.65:9115/probe', Method: 'GET', ExpectedStatusCode: '200', TimeoutSeconds: '10', SafeMode: 'true' },
  },
  'linux-host': {
    label: 'Raw metrics endpoint',
    helper: 'Agent mode tests the node exporter /metrics endpoint over HTTP.',
    keys: ['AgentBaseUrl', 'AgentMode', 'Hostname', 'CpuThresholdPercent', 'MemoryThresholdPercent', 'DiskThresholdPercent', 'SafeMode'],
    defaults: { AgentBaseUrl: 'http://34.107.8.65:9100/metrics', AgentMode: 'true', Hostname: '34.107.8.65', CpuThresholdPercent: '85', MemoryThresholdPercent: '85', DiskThresholdPercent: '90', SafeMode: 'true' },
  },
  nginx: {
    label: 'HTTP reverse proxy',
    helper: 'BaseUrl is checked with a real HTTP request. StatusUrl can point to nginx_status when it is exposed.',
    keys: ['BaseUrl', 'StatusUrl', 'ConfigTestCommand', 'ReloadCommand', 'SafeMode'],
    defaults: { BaseUrl: 'http://34.107.8.65:80', StatusUrl: 'http://34.107.8.65:80/nginx_status', SafeMode: 'true', VerifySsl: 'false' },
  },
}

type CustomModuleManifest = CustomModuleManifestDto

type MarketplaceKind = 'built-in' | 'custom' | 'style'

type MarketplaceModule = {
  author: string
  category: string
  description: string
  featured: boolean
  healthStatus?: string
  installed: boolean
  kind: MarketplaceKind
  key: string
  moduleId?: string
  name: string
  packagePath?: string
  tags: string[]
  themeId?: string
  version: string
}

type DashboardPreset = {
  id: string
  name: string
  palette: string[]
  charts: Array<{ id: string; metric?: string; metrics?: string[]; title: string; type: string }>
}

const dashboardPresetsByTheme: Record<string, DashboardPreset> = {
  'aurora-ops': {
    id: 'aurora-ops',
    name: 'Aurora Ops',
    palette: ['#38d3b6', '#7fdc91', '#73a8ff', '#edf8f5'],
    charts: [
      { id: 'service-temperature', type: 'area', title: 'Service temperature', metric: 'severity' },
      { id: 'uptime-sparkline', type: 'line', title: 'Uptime signal', metric: 'events' },
      { id: 'capacity-ribbon', type: 'composed', title: 'Capacity ribbon', metrics: ['modules', 'approvals'] },
    ],
  },
  'copper-command': {
    id: 'copper-command',
    name: 'Copper Command',
    palette: ['#d19a4d', '#efc985', '#c9ad62', '#df7464'],
    charts: [
      { id: 'approval-pressure', type: 'bar', title: 'Approval pressure', metric: 'approvals' },
      { id: 'remediation-funnel', type: 'funnel', title: 'Remediation funnel', metric: 'events' },
      { id: 'risk-heat-strip', type: 'heatstrip', title: 'Risk heat strip', metric: 'severity' },
    ],
  },
  'midnight-grid': {
    id: 'midnight-grid',
    name: 'Midnight Grid',
    palette: ['#7aa2ff', '#5bd4bd', '#c58cff', '#edf3ff'],
    charts: [
      { id: 'module-dependency-graph', type: 'network', title: 'Module dependency graph', metric: 'modules' },
      { id: 'incident-waterfall', type: 'waterfall', title: 'Incident waterfall', metric: 'events' },
      { id: 'correlation-density', type: 'scatter', title: 'Correlation density', metrics: ['events', 'severity'] },
    ],
  },
}

const customModulesStorageKey = 'sysassist.customModules'
const moduleStoreStorageKey = 'sysassist.moduleStore'
const activeStyleThemeStorageKey = 'sysassist.activeStyleTheme'
const themeStoreSchemaVersionKey = 'sysassist.themeStoreSchemaVersion'
const themeStoreSchemaVersion = 'default-coffee-v3'

type ModuleStoreHistoryEntry = { key: string; at: string; action: string }

type ModuleStoreState = {
  activeStyle?: string
  favorites: string[]
  history: ModuleStoreHistoryEntry[]
  installed: string[]
}

const storeStyleLibraries: MarketplaceModule[] = [
  {
    key: 'theme-aurora-ops',
    name: 'Aurora Ops Theme',
    version: '1.0.0',
    author: 'SysAssist Design',
    category: 'Style Library',
    featured: true,
    installed: false,
    kind: 'style',
    themeId: 'aurora-ops',
    packagePath: 'style-libraries/sysassist-theme-aurora-ops',
    tags: ['theme', 'dashboard', 'calm', 'charts'],
    description: 'Cool operational palette with service-temperature, uptime, and capacity dashboard presets.',
  },
  {
    key: 'theme-copper-command',
    name: 'Copper Command Theme',
    version: '1.0.0',
    author: 'SysAssist Design',
    category: 'Style Library',
    featured: true,
    installed: false,
    kind: 'style',
    themeId: 'copper-command',
    packagePath: 'style-libraries/sysassist-theme-copper-command',
    tags: ['theme', 'dashboard', 'warm', 'charts'],
    description: 'Warm command palette with approval-pressure, remediation funnel, and risk heat strip presets.',
  },
  {
    key: 'theme-midnight-grid',
    name: 'Midnight Grid Theme',
    version: '1.0.0',
    author: 'SysAssist Design',
    category: 'Style Library',
    featured: true,
    installed: false,
    kind: 'style',
    themeId: 'midnight-grid',
    packagePath: 'style-libraries/sysassist-theme-midnight-grid',
    tags: ['theme', 'dashboard', 'focus', 'charts'],
    description: 'Dense midnight operations palette with dependency, waterfall, and correlation dashboard presets.',
  },
]

function storeModulesFromBuiltIns(modules: ModuleDto[]): MarketplaceModule[] {
  return modules.map((module) => ({
    key: module.key,
    name: module.name,
    version: 'core',
    author: 'SysAssist Core',
    category: module.type,
    featured: ['grafana', 'zabbix', 'prometheus-alertmanager', 'postgresql', 'redis', 'docker'].includes(module.key),
    installed: module.isEnabled,
    kind: 'built-in',
    moduleId: module.id,
    healthStatus: module.healthStatus,
    tags: [
      'built-in',
      module.type.toLowerCase(),
      module.supportsPolling ? 'polling' : 'passive',
      module.supportsWebhooks ? 'webhook' : 'direct',
      module.supportsActions ? 'actions' : 'read-only',
    ],
    description: module.description ?? `Native SysAssist adapter for ${module.name}.`,
  }))
}

function storeModulesFromCustom(customModules: CustomModuleManifest[]): MarketplaceModule[] {
  return customModules.map((module) => ({
    key: module.key,
    name: module.name,
    version: module.version,
    author: module.author ?? 'Local package',
    category: module.category ?? 'Custom Module',
    featured: false,
    installed: module.enabled !== false,
    kind: 'custom',
    tags: [...(module.tags ?? []), 'custom', 'runtime'].filter(Boolean),
    description: module.description ?? 'Imported custom module with SysAssist runtime manifest.',
  }))
}

function loadActiveStyleTheme() {
  try {
    const storedTheme = localStorage.getItem(activeStyleThemeStorageKey) ?? undefined
    const parsed = JSON.parse(localStorage.getItem(moduleStoreStorageKey) ?? '{}') as Partial<ModuleStoreState>
    const migrated = migrateLegacyThemeStoreState(parsed, storedTheme)
    return normalizeModuleStoreState(migrated.value, migrated.storedTheme).activeStyle
  } catch {
    return undefined
  }
}

function saveActiveStyleTheme(themeId?: string) {
  if (themeId) {
    localStorage.setItem(activeStyleThemeStorageKey, themeId)
  } else {
    localStorage.removeItem(activeStyleThemeStorageKey)
  }
}

function applyStyleTheme(themeId?: string) {
  if (themeId) {
    document.documentElement.dataset.sysassistTheme = themeId
  } else {
    delete document.documentElement.dataset.sysassistTheme
  }
  window.dispatchEvent(new CustomEvent('sysassist-theme-changed', { detail: themeId }))
}

function styleKeyForTheme(themeId?: string) {
  return storeStyleLibraries.find((item) => item.themeId === themeId)?.key
}

function isStyleModuleKey(key: string) {
  return storeStyleLibraries.some((item) => item.key === key)
}

function normalizeModuleStoreState(value: Partial<ModuleStoreState>, storedTheme?: string): ModuleStoreState {
  const installed = Array.isArray(value.installed) ? value.installed.filter((item): item is string => typeof item === 'string') : []
  const favorites = Array.isArray(value.favorites) ? value.favorites.filter((item): item is string => typeof item === 'string') : []
  const history = Array.isArray(value.history) ? value.history.filter((item): item is ModuleStoreHistoryEntry => Boolean(item?.key && item?.action && item?.at)) : []
  const candidateTheme = typeof value.activeStyle === 'string' && value.activeStyle ? value.activeStyle : storedTheme
  const activeStyleKey = styleKeyForTheme(candidateTheme)
  const hasInstalledList = Array.isArray(value.installed)
  const activeStyle = activeStyleKey && (!hasInstalledList || installed.includes(activeStyleKey)) ? candidateTheme : undefined
  const nonStyleInstalled = installed.filter((item) => !isStyleModuleKey(item))

  return {
    activeStyle,
    favorites,
    history,
    installed: activeStyle && activeStyleKey ? [...nonStyleInstalled, activeStyleKey] : nonStyleInstalled,
  }
}

function migrateLegacyThemeStoreState(value: Partial<ModuleStoreState>, storedTheme?: string) {
  try {
    const currentVersion = localStorage.getItem(themeStoreSchemaVersionKey)
    if (currentVersion === themeStoreSchemaVersion) {
      return { value, storedTheme }
    }

    const installed = Array.isArray(value.installed) ? value.installed.filter((item): item is string => typeof item === 'string' && !isStyleModuleKey(item)) : []
    const migrated = {
      ...value,
      activeStyle: undefined,
      installed,
    }

    localStorage.setItem(themeStoreSchemaVersionKey, themeStoreSchemaVersion)
    localStorage.removeItem(activeStyleThemeStorageKey)
    localStorage.setItem(moduleStoreStorageKey, JSON.stringify(normalizeModuleStoreState(migrated, undefined)))
    delete document.documentElement.dataset.sysassistTheme

    return { value: migrated, storedTheme: undefined }
  } catch {
    return { value, storedTheme }
  }
}

function loadCustomModules() {
  try {
    return JSON.parse(localStorage.getItem(customModulesStorageKey) ?? '[]') as CustomModuleManifest[]
  } catch {
    return []
  }
}

function saveCustomModules(items: CustomModuleManifest[]) {
  localStorage.setItem(customModulesStorageKey, JSON.stringify(items))
}

function loadModuleStoreState() {
  try {
    const storedTheme = localStorage.getItem(activeStyleThemeStorageKey) ?? undefined
    const parsed = JSON.parse(localStorage.getItem(moduleStoreStorageKey) ?? '{}') as Partial<ModuleStoreState>
    const migrated = migrateLegacyThemeStoreState(parsed, storedTheme)
    const state = normalizeModuleStoreState(migrated.value, migrated.storedTheme)
    localStorage.setItem(moduleStoreStorageKey, JSON.stringify(state))
    saveActiveStyleTheme(state.activeStyle)
    return state
  } catch {
    return normalizeModuleStoreState({}, undefined)
  }
}

function saveModuleStoreState(state: ModuleStoreState) {
  const normalized = normalizeModuleStoreState(state, state.activeStyle)
  localStorage.setItem(moduleStoreStorageKey, JSON.stringify(normalized))
  saveActiveStyleTheme(normalized.activeStyle)
  return normalized
}

function validateCustomModuleManifest(value: unknown): CustomModuleManifest {
  if (!value || typeof value !== 'object') {
    throw new Error('Manifest must be a JSON object.')
  }
  const manifest = value as Partial<CustomModuleManifest>
  const apiVersion = manifest.apiVersion
  const key = manifest.key
  const name = manifest.name
  const version = manifest.version
  if (typeof apiVersion !== 'string' || typeof key !== 'string' || typeof name !== 'string' || typeof version !== 'string') {
    throw new Error('Manifest must include string fields: apiVersion, key, name, version.')
  }
  if (!apiVersion.startsWith('sysassist.module/')) {
    throw new Error('apiVersion must start with sysassist.module/.')
  }
  if (!/^[a-z0-9][a-z0-9-_.]+$/.test(key)) {
    throw new Error('key must use lowercase letters, numbers, dash, underscore, or dot.')
  }
  return {
    apiVersion,
    key,
    name,
    version,
    author: manifest.author,
    category: manifest.category,
    description: manifest.description,
    entrypoint: manifest.entrypoint,
    enabled: manifest.enabled ?? true,
    permissions: manifest.permissions ?? [],
    tags: manifest.tags ?? [],
    settings: manifest.settings ?? [],
    actions: manifest.actions ?? [],
  }
}

function moduleConnectionProfile(module?: ModuleDto) {
  return module ? moduleConnectionProfiles[module.key] : undefined
}

function connectionSettings(settings: ModuleSettingDto[], profile?: ModuleConnectionProfile) {
  if (!profile) {
    return settings.filter((setting) => setting.key !== 'Enabled' && (setting.key === 'SafeMode' || setting.isRequired))
  }
  const priority = new Map(profile.keys.map((key, index) => [key.toLowerCase(), index]))
  return settings
    .filter((setting) => priority.has(setting.key.toLowerCase()))
    .sort((a, b) => (priority.get(a.key.toLowerCase()) ?? 999) - (priority.get(b.key.toLowerCase()) ?? 999))
}

function defaultSettingValue(setting: ModuleSettingDto, profile?: ModuleConnectionProfile) {
  if (setting.isSecret) {
    return setting.hasValue ? '********' : ''
  }

  const defaultEntry = Object.entries(profile?.defaults ?? {}).find(([key]) => key.toLowerCase() === setting.key.toLowerCase())
  if (defaultEntry) {
    return defaultEntry[1]
  }
  if (setting.key === 'Enabled') {
    return 'true'
  }
  return setting.value ?? ''
}

function payloadText(event?: EventDto) {
  if (!event?.payloadJson) {
    return 'No payload recorded for this event.'
  }

  try {
    return JSON.stringify(JSON.parse(event.payloadJson), null, 2)
  } catch {
    return event.payloadJson
  }
}

function recommendationRisk(event?: EventDto) {
  if (!event?.recommendation) {
    return 'None'
  }
  if (event.severity === 'Critical' || event.severity === 'Error') {
    return 'Critical'
  }
  if (event.severity === 'Warning' || event.severity === 'High') {
    return 'Medium'
  }
  return 'Low'
}

function approvalActionPreview(action?: { description?: string; name?: string; riskLevel?: string }, event?: EventDto) {
  const actionName = action?.name ?? 'Selected action'
  const target = event?.target ?? 'selected target'
  const description = action?.description ? `${action.description} ` : ''
  return `${description}After approval, SysAssist will execute "${actionName}" for ${target}, write the result to audit, and keep SafeMode evidence attached to the request.`
}

function confidencePercent(value?: number) {
  if (value === undefined) {
    return 0
  }
  return Math.round(value <= 1 ? value * 100 : value)
}

function diagnosticFindingIsWarning(component: string) {
  const [name, ...detailParts] = component.split('=')
  const detail = detailParts.join('=')
  const numeric = Number(detail)
  if (name === 'diagnosticWarning') {
    return true
  }
  if (['errorModules', 'warningModules', 'notConfiguredModules', 'unknownModules', 'modulesWithoutHealthCheck', 'staleHealthChecks', 'recentErrors'].includes(name)) {
    return Number.isFinite(numeric) && numeric > 0
  }
  if (name === 'diagnosticsFreshness') {
    return detail === 'stale' || detail === 'never'
  }
  if (name === 'secretProtection') {
    return detail === 'disabled'
  }
  if (name === 'licenseStatus') {
    return detail.startsWith('Invalid') || detail.startsWith('Blocked')
  }
  return false
}

function AnimatedNumber({ value, className }: { className?: string; value: number }) {
  const [displayValue, setDisplayValue] = useState(0)

  useEffect(() => {
    let frame = 0
    const startedAt = performance.now()
    const duration = 850

    function tick(now: number) {
      const progress = Math.min(1, (now - startedAt) / duration)
      const eased = 1 - Math.pow(1 - progress, 3)
      setDisplayValue(Math.round(value * eased))
      if (progress < 1) {
        frame = requestAnimationFrame(tick)
      }
    }

    frame = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(frame)
  }, [value])

  return <span className={className}>{displayValue}</span>
}

function addMinutes(value: string, minutes: number) {
  return new Date(new Date(value).getTime() + minutes * 60_000).toISOString()
}

function eventWeight(event: EventDto) {
  if (event.severity === 'Critical') {
    return 5
  }
  if (event.severity === 'High') {
    return 4
  }
  if (event.severity === 'Warning') {
    return 2
  }
  return 1
}

function buildIntakeTrend(events: EventDto[], modules: ModuleDto[], rangeMinutes: number) {
  const points = rangeMinutes <= 90 ? 8 : rangeMinutes <= 480 ? 10 : rangeMinutes <= 1440 ? 12 : 14
  const rangeMs = rangeMinutes * 60_000
  const end = Date.now()
  const start = end - rangeMs
  const step = rangeMs / Math.max(1, points - 1)
  const unhealthyModuleCount = modules.filter((module) => module.isEnabled && module.healthStatus !== 'Healthy').length

  return Array.from({ length: points }, (_, index) => {
    const bucketStart = index === 0 ? start : start + step * (index - 1)
    const bucketEnd = start + step * index
    const bucketEvents = events.filter((event) => {
      const createdAt = new Date(event.createdAt).getTime()
      return createdAt >= bucketStart && createdAt <= bucketEnd
    })
    const severity = bucketEvents.reduce((sum, event) => sum + eventWeight(event), 0)
    const approvals = bucketEvents.filter((event) => event.status === 'PendingApproval' || event.recommendation?.suggestedActionId).length

    return {
      approvals,
      events: bucketEvents.length,
      label: new Date(bucketEnd).toISOString(),
      modules: unhealthyModuleCount,
      severity,
    }
  })
}

function HealthDonut({
  items,
  hoveredIndex,
  onHover,
}: {
  hoveredIndex?: number
  items: HealthSummaryChartItem[]
  onHover: (index?: number) => void
}) {
  const total = items.reduce((sum, item) => sum + item.count, 0)

  if (total === 0) {
    return <EmptyState icon={Blocks} title="No module health data" />
  }

  const segments = items.reduce<{
    offset: number
    values: Array<{ item: HealthSummaryChartItem; index: number; length: number; offset: number }>
  }>((acc, item, index) => {
    const percent = (item.count / total) * 100
    const gap = items.length > 1 ? 1.6 : 0
    return {
      offset: acc.offset + percent,
      values: [...acc.values, { item, index, length: Math.max(0.4, percent - gap), offset: acc.offset }],
    }
  }, { offset: 0, values: [] }).values

  return (
    <svg aria-label="Module health distribution" className="health-donut" onMouseLeave={() => onHover(undefined)} role="img" viewBox="0 0 120 120">
      <circle className="health-donut-track" cx="60" cy="60" r="42" />
      {segments.map(({ item, index, length, offset }) => (
          <circle
            key={item.status}
            aria-label={`${item.status}: ${item.count} modules`}
            className={cn('health-donut-segment', hoveredIndex === index && 'active', hoveredIndex !== undefined && hoveredIndex !== index && 'muted')}
            cx="60"
            cy="60"
            onClick={() => onHover(index)}
            onFocus={() => onHover(index)}
            onMouseEnter={() => onHover(index)}
            onMouseMove={() => onHover(index)}
            onPointerEnter={() => onHover(index)}
            pathLength="100"
            r="42"
            role="button"
            stroke={healthColors[item.status] ?? '#b8996f'}
            strokeDasharray={`${length} ${100 - length}`}
            strokeDashoffset={-offset}
            tabIndex={0}
          />
      ))}
    </svg>
  )
}

function PageFrame({ title, icon: Icon, actions, children }: { title: string; icon: typeof Gauge; actions?: React.ReactNode; children: React.ReactNode }) {
  const pageRef = useRef<HTMLElement>(null)

  useEffect(() => {
    if (!pageRef.current) {
      return
    }

    const ctx = gsap.context(() => {
      animateIfPresent(
        '[data-gsap="page-title"]',
        { opacity: 0, y: -10, filter: 'blur(8px)' },
        { opacity: 1, y: 0, filter: 'blur(0px)', duration: 0.65, ease: 'power3.out' },
      )
      animateIfPresent(
        '[data-gsap="reveal"]',
        { opacity: 0, y: 18, scale: 0.985 },
        { opacity: 1, y: 0, scale: 1, duration: 0.72, stagger: 0.045, ease: 'power3.out' },
      )
      animateIfPresent(
        '.timeline-item, .operation-row, .severity-card, .severity-legend-item',
        { opacity: 0, y: 14, filter: 'blur(4px)' },
        { opacity: 1, y: 0, filter: 'blur(0px)', duration: 0.48, stagger: 0.035, delay: 0.14, ease: 'power2.out' },
      )
      animateIfPresent(
        '.severity-meter-fill',
        { scaleX: 0, transformOrigin: 'left center' },
        { scaleX: 1, duration: 0.72, stagger: 0.05, delay: 0.24, ease: 'power3.out' },
      )
    }, pageRef)

    return () => ctx.revert()
  }, [title])

  return (
    <motion.main
      ref={pageRef}
      className="min-w-0 flex-1 overflow-y-auto px-4 py-5 sm:px-6 lg:px-8"
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.22 }}
    >
      <div className="mx-auto flex w-full max-w-[1360px] flex-col gap-5">
        <section data-gsap="page-title" className="flex flex-col gap-3 border-b border-stone-700/50 pb-5 md:flex-row md:items-center md:justify-between">
          <div className="flex min-w-0 items-center gap-3">
            <div className="page-icon grid h-11 w-11 shrink-0 place-items-center rounded-lg">
              <Icon className="h-5 w-5" />
            </div>
            <div className="min-w-0">
              <h1 className="truncate font-display text-2xl font-semibold text-slate-50 sm:text-3xl">{title}</h1>
              <div className="coffee-rule mt-1 h-px w-24" />
            </div>
          </div>
          {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
        </section>
        {children}
      </div>
    </motion.main>
  )
}

function AppLogo({ className }: { className?: string }) {
  return <img alt="SysAssist" className={cn('app-logo', className)} src="/app-icon.png" />
}

function Panel({ children, className }: { children: React.ReactNode; className?: string }) {
  return <section data-gsap="reveal" className={cn('glass-panel rounded-lg p-5', className)}>{children}</section>
}

function EmptyState({ icon: Icon, title }: { icon: typeof Gauge; title: string }) {
  return (
    <div className="grid min-h-36 place-items-center rounded-lg border border-dashed border-slate-700/70 bg-slate-950/40 p-6 text-center">
      <div>
        <Icon className="mx-auto h-7 w-7 text-slate-500" />
        <div className="mt-3 text-sm font-medium text-slate-300">{title}</div>
      </div>
    </div>
  )
}

function SkeletonBlock({ className }: { className?: string }) {
  return <div className={cn('animate-pulse rounded-lg bg-slate-800/70', className)} />
}

function StatusBadge({ value }: { value: string }) {
  return <Badge tone={toneForStatus(value)}>{value}</Badge>
}

function mutationErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Unexpected operation error'
}

function SeverityBadge({ value }: { value: string }) {
  return <Badge tone={toneForSeverity(value)}>{value}</Badge>
}

function Toasts() {
  const toasts = useShellStore((state) => state.toasts)
  const dismissToast = useShellStore((state) => state.dismissToast)
  return (
    <div className="fixed bottom-4 right-4 z-50 flex w-[min(360px,calc(100vw-2rem))] flex-col gap-2">
      <AnimatePresence>
        {toasts.map((toast) => (
          <motion.button
            key={toast.id}
            className={cn(
              'rounded-lg border p-3 text-left shadow-2xl backdrop-blur',
              toast.tone === 'success' && 'border-emerald-400/40 bg-emerald-950/80 text-emerald-100',
              toast.tone === 'warning' && 'border-amber-400/40 bg-amber-950/80 text-amber-100',
              toast.tone === 'danger' && 'border-rose-400/40 bg-rose-950/80 text-rose-100',
              toast.tone === 'neutral' && 'border-slate-500/40 bg-slate-950/90 text-slate-100',
            )}
            initial={{ opacity: 0, x: 24 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: 24 }}
            onClick={() => dismissToast(toast.id)}
            type="button"
          >
            <div className="text-sm font-semibold">{toast.title}</div>
            {toast.message ? <div className="mt-1 text-xs opacity-80">{toast.message}</div> : null}
          </motion.button>
        ))}
      </AnimatePresence>
    </div>
  )
}

function LoginPage() {
  const loginRef = useRef<HTMLDivElement>(null)
  const setSession = useShellStore((state) => state.setSession)
  const pushToast = useShellStore((state) => state.pushToast)
  const [loginName, setLoginName] = useState('admin')
  const [password, setPassword] = useState('')
  const [remember, setRemember] = useState(true)
  const mutation = useMutation({
    mutationFn: () => login(loginName, password),
    onSuccess: (response) => {
      setSession(response.accessToken, response.user)
      pushToast({ tone: 'success', title: 'Signed in', message: response.user.displayName })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Login failed', message: mutationErrorMessage(error) }),
  })

  useEffect(() => {
    if (!loginRef.current) {
      return
    }

    const ctx = gsap.context(() => {
      gsap.fromTo(
        '[data-login="reveal"]',
        { opacity: 0, y: 22, scale: 0.98 },
        { opacity: 1, y: 0, scale: 1, duration: 0.62, stagger: 0.06, ease: 'power3.out' },
      )
      gsap.fromTo(
        '[data-login="field"]',
        { opacity: 0, y: 12 },
        { opacity: 1, y: 0, duration: 0.45, stagger: 0.055, delay: 0.18, ease: 'power2.out' },
      )
    }, loginRef)

    return () => ctx.revert()
  }, [])

  function submit(event: FormEvent) {
    event.preventDefault()
    mutation.mutate()
  }

  return (
    <div ref={loginRef} className="app-shell coffee-stage grid min-h-screen place-items-center px-4 py-10 text-slate-100">
      <section className="w-full max-w-md">
        <form data-login="reveal" className="auth-form glass-panel w-full rounded-lg p-6" onSubmit={submit}>
          <div className="mb-6">
            <div className="brand-mark mb-3 grid h-12 w-12 place-items-center rounded-lg">
              <AppLogo className="h-9 w-9" />
            </div>
            <h1 className="font-display text-2xl font-semibold">SysAssist</h1>
            <div className="mt-2 text-sm text-stone-400">Sign in to continue</div>
          </div>
          <label data-login="field" className="block text-sm text-slate-300">
            Login
            <input className="field mt-2" value={loginName} onChange={(event) => setLoginName(event.target.value)} />
          </label>
          <label data-login="field" className="mt-4 block text-sm text-slate-300">
            Password
            <input className="field mt-2" type="password" value={password} onChange={(event) => setPassword(event.target.value)} />
          </label>
          <label data-login="field" className="mt-4 flex items-center justify-between gap-3 rounded-lg border border-slate-700/60 bg-slate-950/45 px-3 py-2 text-sm text-slate-300">
            <span>Remember me</span>
            <button className={cn('switch-visual', remember && 'on')} onClick={() => setRemember((value) => !value)} type="button" aria-pressed={remember} />
          </label>
          <Button data-login="field" className="mt-6 w-full" disabled={mutation.isPending} type="submit" variant="primary">
            {mutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Lock className="h-4 w-4" />}
            Sign in
          </Button>
        </form>
      </section>
      <Toasts />
    </div>
  )
}

function Sidebar() {
  const activePage = useShellStore((state) => state.activePage)
  const setActivePage = useShellStore((state) => state.setActivePage)
  const user = useShellStore((state) => state.user)
  return (
    <aside className="sidebar-shell hidden w-64 shrink-0 border-r border-slate-800/80 bg-[#211812]/95 px-3 py-4 backdrop-blur md:block xl:w-72">
      <div className="mb-6 flex h-12 items-center gap-3 px-2">
        <div className="brand-mark grid h-10 w-10 place-items-center rounded-lg">
          <AppLogo className="h-8 w-8" />
        </div>
        <div>
          <div className="font-display text-base font-semibold text-slate-50">SysAssist</div>
          <div className="text-xs text-slate-500">Enterprise Ops</div>
        </div>
      </div>
      <nav className="space-y-1">
        {navItems.map((item) => {
          const allowed = hasAnyRole(user, item.roles)
          const active = activePage === item.id
          return (
            <button
              key={item.id}
              className={cn(
                'flex h-10 w-full items-center gap-3 rounded-lg px-3 text-left text-sm transition',
                active ? 'border border-teal-300/30 bg-teal-300/10 text-teal-100' : 'text-slate-400 hover:bg-slate-900 hover:text-slate-100',
                !allowed && 'opacity-50',
              )}
              onClick={() => setActivePage(item.id)}
              type="button"
            >
              <item.icon className="h-4 w-4 shrink-0" />
              <span className="min-w-0 flex-1 truncate">{item.label}</span>
              {!allowed ? <Lock className="h-3.5 w-3.5" /> : null}
            </button>
          )
        })}
      </nav>
    </aside>
  )
}

function Topbar() {
  const search = useShellStore((state) => state.search)
  const setSearch = useShellStore((state) => state.setSearch)
  const clearSession = useShellStore((state) => state.clearSession)
  const user = useShellStore((state) => state.user)
  const { data: diagnostics } = useQuery({ queryKey: ['diagnostics', 'topbar'], queryFn: getDiagnostics })
  const { data: modules = [] } = useQuery({ queryKey: ['modules', 'topbar'], queryFn: listModules })
  const { data: license } = useQuery({ queryKey: ['license', 'topbar'], queryFn: getLicenseStatus })
  const fallbackModules = modules.filter((module) => module.useFallbackMode).length
  return (
    <header className="topbar-panel flex min-h-16 shrink-0 flex-col gap-3 border-b border-slate-700/50 px-4 py-3 backdrop-blur sm:flex-row sm:items-center sm:justify-between lg:px-8">
      <label className="relative min-w-0 flex-1 sm:max-w-xl">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
        <input className="field search-field h-10 pl-9" placeholder="Search events, modules, audit..." value={search} onChange={(event) => setSearch(event.target.value)} />
      </label>
      <div className="flex flex-wrap items-center gap-2">
        <Badge tone={fallbackModules ? 'warning' : 'success'}>{fallbackModules ? `Fallback ${fallbackModules}/${modules.length}` : 'Real mode'}</Badge>
        <Badge tone={diagnostics?.status === 'Healthy' ? 'success' : diagnostics?.status === 'Blocked' ? 'danger' : 'warning'}>{diagnostics?.status ?? 'Syncing'}</Badge>
        <Badge tone={license?.isValid ? 'success' : 'danger'}>{license?.edition ?? 'License'}</Badge>
        <div className="flex items-center gap-2 rounded-lg border border-slate-700/70 bg-slate-950/50 px-3 py-2">
          <ShieldCheck className="h-4 w-4 text-cyan-300" />
          <span className="max-w-36 truncate text-sm text-slate-200">{user?.displayName}</span>
        </div>
        <button className="icon-button" onClick={clearSession} title="Log out" type="button">
          <LogOut className="h-4 w-4" />
        </button>
      </div>
    </header>
  )
}

function DashboardPage() {
  const queryClient = useQueryClient()
  const setActivePage = useShellStore((state) => state.setActivePage)
  const openEvent = useShellStore((state) => state.openEvent)
  const pushToast = useShellStore((state) => state.pushToast)
  const [hoveredHealthIndex, setHoveredHealthIndex] = useState<number>()
  const [intakeMetric, setIntakeMetric] = useState<IntakeMetric>('events')
  const [intakeRange, setIntakeRange] = useState<IntakeRange>('360')
  const [customIntakeMinutes, setCustomIntakeMinutes] = useState(360)
  const [intakeMenuOpen, setIntakeMenuOpen] = useState(false)
  const [activeTheme, setActiveTheme] = useState(() => loadActiveStyleTheme())
  const { data, isLoading: isDashboardLoading } = useQuery({ queryKey: ['dashboard'], queryFn: getDashboard })
  const { data: modules = [] } = useQuery({ queryKey: ['modules', 'dashboard'], queryFn: listModules })
  const events = useMemo(() => data?.recentEvents ?? [], [data?.recentEvents])
  const sortedEvents = useMemo(() => [...events].sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()), [events])
  const criticalEvent = useMemo(() => [...events].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()).find((event) => event.severity === 'Critical' || event.severity === 'High'), [events])
  const unhealthyModules = useMemo(() => modules.filter((module) => module.isEnabled && module.healthStatus !== 'Healthy'), [modules])
  const enabledModuleCount = data?.enabledModulesCount ?? modules.filter((module) => module.isEnabled).length
  const totalModuleCount = modules.length
  const activeDashboardPreset = activeTheme ? dashboardPresetsByTheme[activeTheme] : undefined
  useEffect(() => {
    function handleThemeChanged(event: Event) {
      setActiveTheme((event as CustomEvent<string | undefined>).detail)
    }

    window.addEventListener('sysassist-theme-changed', handleThemeChanged)
    return () => window.removeEventListener('sysassist-theme-changed', handleThemeChanged)
  }, [])
  const diagnosticsMutation = useMutation({
    mutationFn: runDiagnostics,
    onSuccess: (result: OperationResultDto) => {
      queryClient.invalidateQueries({ queryKey: ['diagnostics'] })
      pushToast({ tone: result.success ? 'success' : 'warning', title: result.message })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Diagnostics failed', message: mutationErrorMessage(error) }),
  })
  const moduleStatusSummary = useMemo(() => {
    const counts = modules.reduce<Record<string, number>>((acc, module) => {
      acc[module.healthStatus] = (acc[module.healthStatus] ?? 0) + 1
      return acc
    }, {})
    return ['Healthy', 'Warning', 'Error', 'NotConfigured', 'Disabled'].map((status) => ({ status, count: counts[status] ?? 0 }))
  }, [modules])
  const healthSummary = useMemo(() => {
    const dashboardSummary = data?.healthSummary ?? []
    const source = dashboardSummary.some((item) => item.count > 0)
      ? dashboardSummary
      : moduleStatusSummary.filter((item) => item.count > 0)
    return [...source]
      .sort((left, right) => {
        const leftIndex = healthSummaryOrder.indexOf(left.status)
        const rightIndex = healthSummaryOrder.indexOf(right.status)
        return (leftIndex === -1 ? 999 : leftIndex) - (rightIndex === -1 ? 999 : rightIndex)
      })
      .map((item, index) => ({
        ...item,
        hasActivePeer: hoveredHealthIndex !== undefined,
        isActive: hoveredHealthIndex === index,
      } satisfies HealthSummaryChartItem))
  }, [data?.healthSummary, hoveredHealthIndex, moduleStatusSummary])
  const activeHealth = hoveredHealthIndex !== undefined
    ? healthSummary[hoveredHealthIndex]
    : healthSummary.find((item) => ['Error', 'Unhealthy', 'Warning', 'NotConfigured'].includes(item.status) && item.count > 0)
      ?? healthSummary.find((item) => item.status === 'Healthy' && item.count > 0)
      ?? healthSummary.find((item) => item.count > 0)
      ?? healthSummary[0]
  const activeHealthDescription = healthDescriptions[activeHealth?.status ?? 'Unknown'] ?? healthDescriptions.Unknown
  const activeHealthModules = modules.filter((module) => module.healthStatus === activeHealth?.status)
  const selectedIntakeMetric = intakeMetricOptions.find((item) => item.id === intakeMetric) ?? intakeMetricOptions[0]
  const selectedIntakeRange = intakeRangeOptions.find((item) => item.id === intakeRange) ?? intakeRangeOptions[1]
  const intakeRangeMinutes = intakeRange === 'custom' ? Math.max(15, Math.min(10080, customIntakeMinutes)) : selectedIntakeRange.minutes
  const intakeRangeLabel = intakeRange === 'custom' ? `Last ${intakeRangeMinutes} minutes` : selectedIntakeRange.label
  const intakeData = useMemo(() => buildIntakeTrend(sortedEvents, modules, intakeRangeMinutes), [intakeRangeMinutes, modules, sortedEvents])
  const comparisonMetric: IntakeMetric = intakeMetric === 'events' ? 'severity' : 'events'
  const severityData = useMemo(() => {
    const counts = events.reduce<Record<string, number>>((acc, event) => {
      acc[event.severity] = (acc[event.severity] ?? 0) + 1
      return acc
    }, {})
    const total = Math.max(events.length, 1)
    return severityOrder
      .map((name) => ({ name, value: counts[name] ?? 0, percentage: Math.round(((counts[name] ?? 0) / total) * 100) }))
      .filter((item) => item.value > 0)
  }, [events])
  const severityTotal = events.length
  const dominantSeverity = [...severityData].sort((a, b) => b.value - a.value)[0]
  const affectedTargetsCount = useMemo(() => new Set(events.map((event) => event.target)).size, [events])
  const moduleHealthCompact = (
    <Panel>
      <div className="module-health-compact-head">
        <div>
          <h2 className="panel-title">Module health</h2>
          <div className="mt-1 text-xs text-slate-500">Compact status overview</div>
        </div>
        <button className="module-health-open" onClick={() => setActivePage('modules')} type="button">Open modules</button>
      </div>
      <div className="module-health-summary">
        {moduleStatusSummary.map((item) => (
          <div key={item.status} className="module-health-summary-item">
            <span className={cn('status-pulse', healthPulseClass(item.status))} />
            <span>{item.status}</span>
            <strong>{item.count}</strong>
          </div>
        ))}
      </div>
      <div className="module-health-compact-list">
        {modules
          .filter((module) => module.healthStatus !== 'Healthy')
          .concat(modules.filter((module) => module.healthStatus === 'Healthy'))
          .slice(0, 5)
          .map((module) => {
            const Icon = moduleIconFor(module.type)
            return (
              <button key={module.id} className="module-health-compact-row" onClick={() => setActivePage('modules')} type="button">
                <span className={cn('module-health-compact-icon', healthToneClasses(module.healthStatus))}><Icon className="h-4 w-4" /></span>
                <span className="min-w-0 flex-1 text-left">
                  <span className="block truncate text-sm font-semibold text-stone-100">{module.name}</span>
                  <span className="block truncate text-xs text-stone-500">{module.type}</span>
                </span>
                <span className="module-health-compact-state">
                  <span className={cn('status-pulse', healthPulseClass(module.healthStatus))} />
                  <span>{module.healthStatus}</span>
                </span>
                <span className="module-health-compact-time">{timeLabel(module.lastHealthCheckAt)}</span>
              </button>
            )
          })}
        {modules.length === 0 ? <EmptyState icon={Blocks} title="No modules" /> : null}
      </div>
    </Panel>
  )
  const severityPanel = (
    <Panel className="severity-panel">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="panel-title">Events by severity</h2>
          <div className="mt-1 text-xs text-slate-500">Distribution across {severityTotal} recent events and {affectedTargetsCount} targets</div>
        </div>
        <Badge tone={dominantSeverity?.name === 'Critical' || dominantSeverity?.name === 'High' ? 'danger' : 'neutral'}>
          Top: {dominantSeverity?.name ?? 'No data'}
        </Badge>
      </div>
      <div className="severity-layout">
        <div className="severity-chart-card">
          <div className="severity-card-grid">
            <div className="severity-card">
              <span>Total events</span>
              <strong>{severityTotal}</strong>
            </div>
            <div className="severity-card">
              <span>Critical/High</span>
              <strong>{events.filter((event) => event.severity === 'Critical' || event.severity === 'High').length}</strong>
            </div>
            <div className="severity-card">
              <span>Targets</span>
              <strong>{affectedTargetsCount}</strong>
            </div>
          </div>
          <ResponsiveChartFrame className="mt-4 h-64 overflow-hidden rounded-md">
            {({ height, width }) => (
              <BarChart data={severityData} height={height} margin={{ left: -12, right: 14, top: 24, bottom: 4 }} width={width}>
                <CartesianGrid stroke="rgba(216,194,163,0.12)" strokeDasharray="4 8" vertical={false} />
                <XAxis dataKey="name" stroke="#b7a28c" tickLine={false} axisLine={false} />
                <YAxis allowDecimals={false} stroke="#b7a28c" tickLine={false} axisLine={false} width={28} />
                <Tooltip contentStyle={{ background: '#211812', border: '1px solid rgba(216,194,163,0.22)', borderRadius: 8 }} cursor={{ fill: 'rgba(216,194,163,0.05)' }} />
                <Bar animationDuration={680} dataKey="value" maxBarSize={54} radius={[8, 8, 3, 3]}>
                  {severityData.map((item) => <Cell key={item.name} fill={severityColors[item.name] ?? '#b8996f'} />)}
                </Bar>
              </BarChart>
            )}
          </ResponsiveChartFrame>
        </div>
        <div className="severity-legend">
          {severityData.map((item) => (
            <div key={item.name} className="severity-legend-item">
              <div className="flex items-center justify-between gap-3">
                <div className="flex min-w-0 items-center gap-2">
                  <span className="severity-dot" style={{ backgroundColor: severityColors[item.name] ?? '#b8996f' }} />
                  <span className="truncate text-sm font-semibold text-slate-100">{item.name}</span>
                </div>
                <strong>{item.value}</strong>
              </div>
              <div className="severity-meter">
                <span className="severity-meter-fill" style={{ backgroundColor: severityColors[item.name] ?? '#b8996f', width: `${item.percentage}%` }} />
              </div>
              <div className="mt-1 text-xs text-stone-500">{item.percentage}% of current intake</div>
            </div>
          ))}
          {severityData.length === 0 ? <EmptyState icon={Activity} title="No severity data" /> : null}
        </div>
      </div>
    </Panel>
  )

  return (
    <PageFrame
      icon={Gauge}
      title="Dashboard"
      actions={
        <Button
          onClick={() => {
            queryClient.invalidateQueries({ queryKey: ['dashboard'] })
            queryClient.invalidateQueries({ queryKey: ['modules'] })
            queryClient.invalidateQueries({ queryKey: ['events'] })
            pushToast({ tone: 'neutral', title: 'Dashboard refreshed' })
          }}
        >
          <RefreshCcw className="h-4 w-4" />
          Refresh
        </Button>
      }
    >
      <section className="grid gap-5 xl:grid-cols-2">
        <Panel className="recent-events-panel">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <h2 className="panel-title">Incident intake</h2>
              <div className="mt-1 text-xs text-slate-500">{selectedIntakeMetric.description} / {intakeRangeLabel}</div>
            </div>
            <div className="chart-filter" onMouseEnter={() => setIntakeMenuOpen(true)} onMouseLeave={() => setIntakeMenuOpen(false)}>
              <button className="chart-filter-trigger" onClick={() => setIntakeMenuOpen((value) => !value)} type="button">
                <SlidersHorizontal className="h-3.5 w-3.5" />
                {events.length ? `Updated ${fullDateTimeLabel(sortedEvents.at(-1)?.createdAt)}` : 'No events'}
              </button>
              <AnimatePresence>
                {intakeMenuOpen ? (
                  <motion.div
                    animate={{ opacity: 1, y: 0, scale: 1 }}
                    className="chart-filter-menu"
                    exit={{ opacity: 0, y: -8, scale: 0.98 }}
                    initial={{ opacity: 0, y: -8, scale: 0.98 }}
                    transition={{ duration: 0.16, ease: 'easeOut' }}
                  >
                    <div className="chart-filter-section">
                      <div className="chart-filter-label">Metric</div>
                      <div className="chart-filter-grid">
                        {intakeMetricOptions.map((option) => (
                          <button key={option.id} className={cn('chart-filter-option', intakeMetric === option.id && 'active')} onClick={() => setIntakeMetric(option.id)} type="button">
                            <span>{option.label}</span>
                            <small>{option.description}</small>
                          </button>
                        ))}
                      </div>
                    </div>
                    <div className="chart-filter-section">
                      <div className="chart-filter-label">Window</div>
                      <div className="chart-filter-ranges">
                        {intakeRangeOptions.map((option) => (
                          <button key={option.id} className={cn('chart-range-option', intakeRange === option.id && 'active')} onClick={() => setIntakeRange(option.id)} type="button">
                            {option.label}
                          </button>
                        ))}
                      </div>
                      <label className="chart-custom-range">
                        <span>Custom window, minutes</span>
                        <input
                          min={15}
                          max={10080}
                          step={15}
                          type="number"
                          value={customIntakeMinutes}
                          onChange={(event) => {
                            setCustomIntakeMinutes(Number(event.target.value) || 15)
                            setIntakeRange('custom')
                          }}
                        />
                      </label>
                    </div>
                  </motion.div>
                ) : null}
              </AnimatePresence>
            </div>
          </div>
          <ResponsiveChartFrame className="h-72 overflow-hidden rounded-md">
            {({ height, width }) => (
              <AreaChart data={intakeData} height={height} margin={{ left: 4, right: 30, top: 28, bottom: 12 }} width={width}>
                <defs>
                  <linearGradient id="events-gradient" x1="0" x2="0" y1="0" y2="1">
                    <stop offset="5%" stopColor="#b8996f" stopOpacity={0.42} />
                    <stop offset="95%" stopColor="#b8996f" stopOpacity={0.04} />
                  </linearGradient>
                  <linearGradient id="events-context-gradient" x1="0" x2="0" y1="0" y2="1">
                    <stop offset="5%" stopColor="#b8996f" stopOpacity={0.24} />
                    <stop offset="95%" stopColor="#b8996f" stopOpacity={0.02} />
                  </linearGradient>
                </defs>
                <CartesianGrid stroke="rgba(216,194,163,0.12)" strokeDasharray="4 8" vertical={false} />
                <XAxis dataKey="label" interval="preserveStartEnd" stroke="#b7a28c" tickFormatter={(value) => intakeTickLabel(String(value), intakeRangeMinutes)} tickLine={false} axisLine={false} minTickGap={24} />
                <YAxis allowDataOverflow={false} domain={[0, (dataMax: number) => Math.max(3, Math.ceil(dataMax) + 1)]} stroke="#b7a28c" tickLine={false} axisLine={false} width={34} />
                <Tooltip contentStyle={{ background: '#211812', border: '1px solid rgba(216,194,163,0.22)', borderRadius: 8 }} labelFormatter={(value) => fullDateTimeLabel(String(value))} />
                <Area animationDuration={520} dataKey={comparisonMetric} dot={false} fill="url(#events-context-gradient)" name={comparisonMetric} stroke="#b8996f" strokeDasharray="5 7" strokeOpacity={0.55} strokeWidth={1.6} type="monotone" />
                <Area activeDot={{ r: 5, stroke: '#eee2d2', strokeWidth: 2 }} animationDuration={640} dataKey={intakeMetric} dot={{ r: 2.8, strokeWidth: 1 }} fill="url(#events-gradient)" name={selectedIntakeMetric.label} stroke="#d8c2a3" strokeWidth={2.6} type="monotone" />
              </AreaChart>
            )}
          </ResponsiveChartFrame>
        </Panel>
        <Panel>
          <div className="mb-4 flex items-center justify-between gap-3">
            <h2 className="panel-title">Module health</h2>
            <Badge tone={activeHealth?.status === 'Healthy' ? 'success' : 'warning'}>{activeHealth?.status ?? 'No data'}</Badge>
          </div>
          <div className="health-overview">
            <div className="health-chart-wrap">
              {isDashboardLoading ? (
                <SkeletonBlock className="h-52 w-52 rounded-full" />
              ) : (
                <>
                  <HealthDonut hoveredIndex={hoveredHealthIndex} items={healthSummary} onHover={setHoveredHealthIndex} />
                  {healthSummary.some((item) => item.count > 0) ? (
                    <div className="health-center">
                      <div className="font-display text-3xl font-semibold text-slate-50">{activeHealth?.count ?? 0}</div>
                      <div className="text-xs text-slate-500">modules</div>
                    </div>
                  ) : null}
                </>
              )}
            </div>
            <div className={cn('health-hover-card', hoveredHealthIndex !== undefined && 'active')}>
              <div className="health-card-head">
                <div>
                  <div className="font-display text-base font-semibold text-slate-50">{activeHealthDescription.title}</div>
                  <div className="mt-1 text-xs text-slate-500">{activeHealth?.count ?? 0} of {totalModuleCount} modules</div>
                </div>
                <span className="health-swatch" style={{ backgroundColor: healthColors[activeHealth?.status ?? 'Unknown'] ?? '#b8996f' }} />
              </div>
              <p className="health-card-text">{activeHealthDescription.detail}</p>
              <div className="health-card-action">{activeHealthDescription.action}</div>
              <div className="health-card-modules">
                {(activeHealthModules.length ? activeHealthModules : modules.slice(0, 2)).slice(0, 2).map((module) => (
                  <span key={module.id}>{module.name}</span>
                ))}
                {activeHealthModules.length > 2 ? <span>+{activeHealthModules.length - 2}</span> : null}
              </div>
            </div>
          </div>
        </Panel>
      </section>
      {severityPanel}
      {activeDashboardPreset ? (
        <StyleDashboardPresetPanel
          enabledModuleCount={enabledModuleCount}
          events={events}
          modules={modules}
          pendingApprovals={data?.pendingApprovalsCount ?? 0}
          preset={activeDashboardPreset}
        />
      ) : null}
      <section className="dashboard-secondary-grid">
        <Panel>
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 className="panel-title">Recent events</h2>
              <div className="mt-1 text-xs text-slate-500">Latest signals with severity, source, and affected target</div>
            </div>
            <Badge tone={criticalEvent ? 'danger' : 'success'}>{criticalEvent ? 'Actionable' : 'Clear'}</Badge>
          </div>
          <Timeline events={events} />
        </Panel>
        <Panel className="operator-panel compact">
          <div className="operator-compact-head">
            <div>
              <h2 className="panel-title">Operator queue</h2>
              <div className="mt-1 text-xs text-slate-500">{criticalEvent ? 'Critical incident requires attention' : 'No immediate operator action'}</div>
            </div>
            <Badge tone={data?.pendingApprovalsCount ? 'warning' : 'success'}>{data?.pendingApprovalsCount === 1 ? '1 approval' : `${data?.pendingApprovalsCount ?? 0} approvals`}</Badge>
          </div>
          <div className="operator-compact-list">
            <button className="operator-compact-row" onClick={() => setActivePage('approvals')} type="button">
              <ClipboardCheck className="h-4 w-4 text-amber-200" />
              <span>Approvals</span>
              <strong>{data?.pendingApprovalsCount ?? 0}</strong>
            </button>
            <button className="operator-compact-row" disabled={!criticalEvent} onClick={() => criticalEvent && openEvent(criticalEvent.id)} type="button">
              <AlertTriangle className="h-4 w-4 text-rose-200" />
              <span>{criticalEvent ? 'Critical incident' : 'Incidents clear'}</span>
              <strong>{criticalEvent ? 'Open' : 'OK'}</strong>
            </button>
            <button className="operator-compact-row" onClick={() => setActivePage('modules')} type="button">
              <Blocks className="h-4 w-4 text-blue-200" />
              <span>Needs attention</span>
              <strong>{unhealthyModules.length || 'OK'}</strong>
            </button>
            <button className="operator-compact-row" disabled={diagnosticsMutation.isPending} onClick={() => diagnosticsMutation.mutate()} type="button">
              {diagnosticsMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin text-emerald-200" /> : <TerminalSquare className="h-4 w-4 text-emerald-200" />}
              <span>Diagnostics</span>
              <strong>Run</strong>
            </button>
          </div>
        </Panel>
      </section>
      {moduleHealthCompact}
    </PageFrame>
  )
}

function StyleDashboardPresetPanel({
  enabledModuleCount,
  events,
  modules,
  pendingApprovals,
  preset,
}: {
  enabledModuleCount: number
  events: EventDto[]
  modules: ModuleDto[]
  pendingApprovals: number
  preset: DashboardPreset
}) {
  const unhealthyCount = modules.filter((module) => module.isEnabled && module.healthStatus !== 'Healthy').length
  const severityScore = events.reduce((sum, event) => sum + eventWeight(event), 0)
  const metrics: Record<string, number> = {
    approvals: pendingApprovals,
    events: events.length,
    modules: unhealthyCount,
    severity: severityScore,
  }
  const maxMetric = Math.max(1, ...Object.values(metrics))

  return (
    <Panel className="style-dashboard-panel">
      <div className="style-dashboard-head">
        <div>
          <h2 className="panel-title">{preset.name} dashboard</h2>
          <div className="mt-1 text-xs text-slate-500">{enabledModuleCount}/{modules.length} enabled modules / {events.length} recent events</div>
        </div>
        <div className="style-palette-row">
          {preset.palette.map((color) => <span key={color} style={{ backgroundColor: color }} />)}
        </div>
      </div>
      <div className="style-dashboard-grid">
        {preset.charts.map((chart, index) => {
          const value = chart.metric ? metrics[chart.metric] ?? 0 : (chart.metrics ?? []).reduce((sum, metric) => sum + (metrics[metric] ?? 0), 0)
          return (
            <article key={chart.id} className="style-dashboard-widget">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <span>{chart.type}</span>
                  <strong>{chart.title}</strong>
                </div>
                <b>{value}</b>
              </div>
              <div className="style-widget-bars">
                {Array.from({ length: 8 }).map((_, barIndex) => {
                  const height = Math.max(12, ((value + barIndex + index) % (maxMetric + 8)) / (maxMetric + 8) * 100)
                  return <i key={barIndex} style={{ backgroundColor: preset.palette[(barIndex + index) % preset.palette.length], height: `${height}%` }} />
                })}
              </div>
            </article>
          )
        })}
      </div>
    </Panel>
  )
}

function Timeline({ events }: { events: EventDto[] }) {
  if (events.length === 0) {
    return <EmptyState icon={Activity} title="No events" />
  }
  return (
    <div className="recent-timeline">
      {events.slice(0, 4).map((event, index) => (
        <motion.button
          key={event.id}
          className="timeline-item w-full text-left"
          initial={{ opacity: 0, x: -8 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ delay: index * 0.04 }}
          onClick={() => useShellStore.getState().openEvent(event.id)}
          type="button"
        >
          <span className="timeline-rail">
            <span className="timeline-dot" style={{ backgroundColor: severityColors[event.severity] ?? '#b8996f' }} />
          </span>
          <div className="min-w-0 text-left">
            <div className="flex flex-wrap items-center gap-2">
              <div className="truncate text-sm font-semibold text-slate-100">{event.summary}</div>
              <span className="event-state-chip">{event.status}</span>
            </div>
            <div className="mt-2 flex flex-wrap gap-2 text-xs text-stone-500">
              <span className="event-meta-chip">{event.source}</span>
              <span className="event-meta-chip">{event.target}</span>
              <span className="event-meta-chip">{timeLabel(event.createdAt)}</span>
            </div>
          </div>
          <SeverityBadge value={event.severity} />
        </motion.button>
      ))}
    </div>
  )
}

function EventsPage() {
  const search = useShellStore((state) => state.search)
  const openEvent = useShellStore((state) => state.openEvent)
  const pushToast = useShellStore((state) => state.pushToast)
  const [severity, setSeverity] = useState('all')
  const [status, setStatus] = useState('all')
  const [source, setSource] = useState('all')
  const [dateRange, setDateRange] = useState('all')
  const [drawerEvent, setDrawerEvent] = useState<EventDto>()
  const [filterNow] = useState(() => Date.now())
  const queryClient = useQueryClient()
  const { data = [], isLoading } = useQuery({ queryKey: ['events'], queryFn: listEvents })
  const { data: modules = [] } = useQuery({ queryKey: ['modules', 'events'], queryFn: listModules })
  const fetchMutation = useMutation({
    mutationFn: () => moduleCommand((modules.find((item) => item.supportsPolling && item.isEnabled) ?? modules[0]).id, 'fetch-events'),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['events'] })
      pushToast({ tone: result.success ? 'success' : 'warning', title: result.message })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Fetch failed', message: mutationErrorMessage(error) }),
  })
  const filtered = useMemo(
    () =>
      data.filter((event) => {
        const matchesSearch = `${event.summary} ${event.source} ${event.target}`.toLowerCase().includes(search.toLowerCase())
        const createdAt = new Date(event.createdAt).getTime()
        const rangeOk = dateRange === 'all' || createdAt > filterNow - Number(dateRange) * 60_000
        return matchesSearch && rangeOk && (source === 'all' || event.source === source) && (severity === 'all' || event.severity === severity) && (status === 'all' || event.status === status)
      }),
    [data, dateRange, filterNow, search, severity, source, status],
  )

  return (
    <PageFrame
      icon={Activity}
      title="Incident Events"
      actions={
        <>
          <select className="field h-10 w-44" value={source} onChange={(event) => setSource(event.target.value)}>
            <option value="all">All sources</option>
            {[...new Set(data.map((event) => event.source))].map((item) => <option key={item}>{item}</option>)}
          </select>
          <select className="field h-10 w-40" value={severity} onChange={(event) => setSeverity(event.target.value)}>
            <option value="all">All severity</option>
            {[...new Set(data.map((event) => event.severity))].map((item) => <option key={item}>{item}</option>)}
          </select>
          <select className="field h-10 w-40" value={status} onChange={(event) => setStatus(event.target.value)}>
            <option value="all">All status</option>
            {[...new Set(data.map((event) => event.status))].map((item) => <option key={item}>{item}</option>)}
          </select>
          <select className="field h-10 w-36" value={dateRange} onChange={(event) => setDateRange(event.target.value)}>
            <option value="all">All dates</option>
            <option value="60">Last hour</option>
            <option value="360">Last 6h</option>
            <option value="1440">Last 24h</option>
          </select>
          <Button disabled={fetchMutation.isPending || modules.length === 0} onClick={() => fetchMutation.mutate()} variant="primary">
            {fetchMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
            Fetch Events
          </Button>
        </>
      }
    >
      <Panel>
        {isLoading ? <SkeletonBlock className="h-72" /> : (
          <div className="events-list">
            {filtered.map((event) => (
              <article key={event.id} className={cn('event-list-row', event.severity === 'Critical' && 'critical')}>
                <div className="event-list-time mono">{timeLabel(event.createdAt)}</div>
                <div className="event-list-main">
                  <div className="event-list-title">{event.summary}</div>
                  <div className="event-list-meta">
                    <span>{event.source}</span>
                    <span>{event.target}</span>
                    <span>{shortId(event.correlationId ?? event.id)}</span>
                  </div>
                </div>
                <div className="event-list-badges">
                  <SeverityBadge value={event.severity} />
                  <StatusBadge value={event.status} />
                  <span className="mono-chip">{event.severity === 'Critical' || event.severity === 'Error' ? 'approval_required' : 'collect_diagnostics'}</span>
                </div>
                <div className="event-list-actions">
                  <button className="icon-button" onClick={() => setDrawerEvent(event)} title="Preview event" type="button"><FileClock className="h-4 w-4" /></button>
                  <button className="icon-button" onClick={() => openEvent(event.id)} title="Open detail" type="button"><ChevronRight className="h-4 w-4" /></button>
                </div>
              </article>
            ))}
            {filtered.length === 0 ? <EmptyState icon={Activity} title="No events found" /> : null}
          </div>
        )}
      </Panel>
      <AnimatePresence>
        {drawerEvent ? <EventDrawer event={drawerEvent} onClose={() => setDrawerEvent(undefined)} /> : null}
      </AnimatePresence>
    </PageFrame>
  )
}

function EventDrawer({ event, onClose }: { event: EventDto; onClose: () => void }) {
  const recommendation = event.recommendation
  const confidence = confidencePercent(recommendation?.confidence)
  const risk = recommendationRisk(event)
  return (
    <motion.aside
      className="fixed inset-y-0 right-0 z-40 w-[min(460px,100vw)] border-l border-stone-700/70 bg-[#211812] p-5 shadow-2xl"
      initial={{ x: '100%' }}
      animate={{ x: 0 }}
      exit={{ x: '100%' }}
    >
      <button className="icon-button ml-auto" onClick={onClose} title="Close" type="button"><X className="h-4 w-4" /></button>
      <h2 className="mt-4 font-display text-2xl font-semibold text-slate-50">Event Detail</h2>
      <div className="mt-5 space-y-3">
        <DetailRow label="Summary" value={event.summary} />
        <DetailRow label="Source" value={event.source} />
        <DetailRow label="Type" value={event.eventType} />
        <DetailRow label="Target" value={event.target} />
        <DetailRow label="Severity" value={event.severity} />
        <DetailRow label="Status" value={event.status} />
        <DetailRow label="Correlation" value={event.correlationId ?? 'None'} />
      </div>
      <div className="mt-5 rounded-lg border border-cyan-300/20 bg-slate-950/55 p-4">
        <div className="mb-2 flex items-center justify-between">
          <h3 className="panel-title">Recommendation</h3>
          <Badge tone={riskTone(risk)}>{risk}</Badge>
        </div>
        <div className="text-sm text-slate-300">{recommendation?.nextStep ?? 'No recommendation linked to this event.'}</div>
        <div className="mt-3 h-2 overflow-hidden rounded-full bg-slate-800">
          <div className="h-full rounded-full bg-gradient-to-r from-cyan-300 to-violet-400" style={{ width: `${confidence}%` }} />
        </div>
      </div>
      <pre className="mt-5 max-h-56 overflow-auto whitespace-pre-wrap rounded-lg border border-slate-700/60 bg-[#17110d] p-4 text-xs text-slate-300">{payloadText(event)}</pre>
    </motion.aside>
  )
}

function EventDetailPage() {
  const selectedEventId = useShellStore((state) => state.selectedEventId)
  const setActivePage = useShellStore((state) => state.setActivePage)
  const { data = [] } = useQuery({ queryKey: ['events'], queryFn: listEvents })
  const { data: actions = [] } = useQuery({ queryKey: ['actions', 'event-detail'], queryFn: listActions })
  const { data: modules = [] } = useQuery({ queryKey: ['modules', 'event-detail'], queryFn: listModules })
  const event = data.find((item) => item.id === selectedEventId) ?? data[0]
  const recommendation = event?.recommendation
  const suggestedAction = actions.find((action) => action.id === recommendation?.suggestedActionId)
  const eventModule = modules.find((module) => module.id === event?.moduleId)
  const confidence = confidencePercent(recommendation?.confidence)
  const risk = recommendationRisk(event)
  const lifecycleSteps = event ? [
    { label: 'Event received', detail: 'Signal entered SysAssist intake.', completedAt: event.createdAt, done: true },
    { label: 'Normalized', detail: 'Source payload was parsed and mapped to a common incident model.', completedAt: addMinutes(event.createdAt, 1), done: true },
    { label: 'Recommendation', detail: recommendation?.nextStep ?? 'Recommendation evidence was prepared for the operator.', completedAt: addMinutes(event.createdAt, 2), done: Boolean(recommendation) },
    { label: event.status === 'PendingApproval' ? 'Approval requested' : 'Action decision', detail: event.status === 'PendingApproval' ? 'High-risk action was routed to SafeMode approval.' : 'Action path was selected from the recommendation.', completedAt: addMinutes(event.createdAt, 3), done: event.status !== 'New' },
    { label: 'Decision', detail: event.status === 'Completed' || event.status === 'Rejected' || event.status === 'Failed' ? `Decision completed with status ${event.status}.` : 'Waiting for operator decision.', completedAt: addMinutes(event.createdAt, 4), done: ['Completed', 'Rejected', 'Failed'].includes(event.status) },
    { label: 'Audit record', detail: 'Final evidence is sealed in the audit trail after the decision.', completedAt: addMinutes(event.createdAt, 5), done: event.status === 'Completed' },
  ] : []
  const activeLifecycleIndex = Math.max(0, lifecycleSteps.reduce((last, step, index) => step.done ? index : last, -1))
  return (
    <PageFrame icon={FileClock} title="Event Detail" actions={<Button onClick={() => setActivePage('events')}><ChevronRight className="h-4 w-4 rotate-180" />Events</Button>}>
      {event ? (
        <div className="space-y-5">
          <section className="hero-panel rounded-lg p-5">
            <div className="relative flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
              <div className="min-w-0">
                <div className="mb-3 flex flex-wrap gap-2">
                  <Badge>{eventModule?.name ?? event.source}</Badge>
                  <SeverityBadge value={event.severity} />
                  <StatusBadge value={event.status} />
                </div>
                <h2 className="font-display text-2xl font-semibold text-slate-50">{event.summary}</h2>
                <div className="mt-3 flex flex-wrap gap-3 text-sm text-slate-400">
                  <span className="mono-chip">{event.target}</span>
                  <span className="mono-chip">{event.correlationId ?? 'No correlation'}</span>
                  <span>{timeLabel(event.createdAt)}</span>
                </div>
              </div>
            </div>
          </section>
          <section className="event-detail-grid">
            <Panel className="min-w-0">
              <h2 className="panel-title mb-4">Payload JSON</h2>
              <pre className="max-h-80 overflow-auto whitespace-pre-wrap rounded-lg border border-slate-700/60 bg-[#17110d] p-4 text-xs text-slate-300">{payloadText(event)}</pre>
            </Panel>
            <Panel className="min-w-0">
              <div className="mb-4 flex items-center justify-between">
                <h2 className="panel-title">Recommendation</h2>
                <Badge tone={riskTone(risk)}>{risk}</Badge>
              </div>
              <div className="recommendation-card">
                <div>
                  <span>Classification</span>
                  <strong>{recommendation?.classification ?? 'No recommendation linked'}</strong>
                </div>
                <p>{recommendation?.probableCause ?? 'No probable cause recorded'}</p>
                <p>{recommendation?.nextStep ?? 'No next step recorded'}</p>
              </div>
              <div className="mt-4">
                <div className="mb-2 flex justify-between text-xs text-slate-500">
                  <span>Confidence</span>
                  <span>{confidence}%</span>
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-slate-800">
                  <div className="h-full rounded-full bg-gradient-to-r from-cyan-300 to-violet-400" style={{ width: `${confidence}%` }} />
                </div>
              </div>
            </Panel>
            <Panel className="min-w-0">
              <h2 className="panel-title mb-4">Suggested Action</h2>
              <div className="space-y-3">
                <DetailRow label="Action" value={suggestedAction?.name ?? (recommendation?.suggestedActionId ? shortId(recommendation.suggestedActionId) : 'No linked action')} />
                <DetailRow label="Risk Level" value={risk} />
                <DetailRow label="Approval Status" value={event.status === 'PendingApproval' ? 'Required' : 'Not required'} />
                <DetailRow label="SafeMode" value={eventModule ? (eventModule.safeMode ? 'Enabled' : 'Disabled') : 'Module not found'} />
              </div>
            </Panel>
          </section>
          <Panel>
            <h2 className="panel-title mb-4">Lifecycle Timeline</h2>
            <div className="lifecycle-strip">
              {lifecycleSteps.map((step, index) => (
                <div
                  key={step.label}
                  className={cn('lifecycle-step', step.done && 'done', index === activeLifecycleIndex && 'active')}
                  data-tooltip={`${step.detail} Finished: ${step.done ? fullDateTimeLabel(step.completedAt) : 'not completed yet'}`}
                >
                  <span className="lifecycle-dot" />
                  <span className="lifecycle-index">0{index + 1}</span>
                  <strong>{step.label}</strong>
                  <small>{step.done ? fullDateTimeLabel(step.completedAt) : 'Pending'}</small>
                </div>
              ))}
            </div>
          </Panel>
        </div>
      ) : <EmptyState icon={Activity} title="No event selected" />}
    </PageFrame>
  )
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-slate-700/50 bg-slate-950/45 p-3">
      <div className="text-xs uppercase tracking-[0.16em] text-slate-500">{label}</div>
      <div className="mt-1 break-words text-sm text-slate-100">{value}</div>
    </div>
  )
}

function ApprovalsPage() {
  const queryClient = useQueryClient()
  const pushToast = useShellStore((state) => state.pushToast)
  const { data = [] } = useQuery({ queryKey: ['approvals'], queryFn: listApprovals })
  const { data: events = [] } = useQuery({ queryKey: ['events'], queryFn: listEvents })
  const { data: actions = [] } = useQuery({ queryKey: ['actions'], queryFn: listActions })
  const [decision, setDecision] = useState<{ approval: ApprovalDto; approve: boolean }>()
  const [selectedApprovalId, setSelectedApprovalId] = useState<string>()
  const selectedApproval = data.find((item) => item.id === selectedApprovalId) ?? data.find((item) => item.status === 'Pending') ?? data[0]
  const selectedEvent = events.find((item) => item.id === selectedApproval?.eventId)
  const selectedAction = actions.find((item) => item.id === selectedApproval?.actionId)
  const mutation = useMutation({
    mutationFn: ({ approval, approve, comment }: { approval: ApprovalDto; approve: boolean; comment: string }) => decideApproval(approval.id, approve, comment),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['approvals'] })
      pushToast({ tone: result.success ? 'success' : 'danger', title: result.success ? 'Decision recorded' : 'Decision failed', message: result.message })
      setDecision(undefined)
    },
  })

  return (
    <PageFrame icon={ClipboardCheck} title="Approval Center">
      <section className="grid gap-5 xl:grid-cols-2">
        <div className="space-y-4">
          <section className="grid gap-3 md:grid-cols-3">
            {['Pending', 'Executed', 'Rejected'].map((status) => (
              <Panel key={status}>
                <div className="text-sm text-slate-400">{status}</div>
                <div className="mt-2 font-display text-3xl font-semibold text-slate-50">{data.filter((item) => item.status === status).length}</div>
              </Panel>
            ))}
          </section>
          <Panel>
            <div className="mb-4 flex items-center justify-between">
              <h2 className="panel-title">Requests</h2>
              <Badge tone="warning">SafeMode control center</Badge>
            </div>
            <div className="space-y-3">
              {data.map((approval) => {
                const event = events.find((item) => item.id === approval.eventId)
                const action = actions.find((item) => item.id === approval.actionId)
                const isSelected = selectedApproval?.id === approval.id
                return (
                  <button key={approval.id} className={cn('approval-request-card', isSelected && 'active')} onClick={() => setSelectedApprovalId(approval.id)} type="button">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <h3 className="font-display text-base font-semibold text-slate-50">{action?.name ?? shortId(approval.actionId)}</h3>
                          <Badge tone={riskTone(action?.riskLevel)}>{action?.riskLevel ?? 'Risk'}</Badge>
                          <StatusBadge value={approval.status} />
                        </div>
                        <p className="mt-2 line-clamp-2 text-sm text-slate-400">{event?.summary ?? approval.eventId}</p>
                        <div className="mt-3 flex flex-wrap gap-2 text-xs text-slate-500">
                          <span className="mono-chip">{event?.target ?? 'target'}</span>
                          <span>{timeLabel(approval.requestedAt)}</span>
                        </div>
                        {isSelected ? (
                          <div className="approval-action-preview">
                            <span>What happens after approval</span>
                            <p>{approvalActionPreview(action, event)}</p>
                          </div>
                        ) : null}
                      </div>
                      {approval.status === 'Pending' ? (
                        <div className="flex gap-2">
                          <Button className="h-9" onClick={(click) => { click.stopPropagation(); setDecision({ approval, approve: true }) }} variant="primary"><Check className="h-4 w-4" />Approve</Button>
                          <Button className="h-9" onClick={(click) => { click.stopPropagation(); setDecision({ approval, approve: false }) }}><X className="h-4 w-4" />Reject</Button>
                        </div>
                      ) : null}
                    </div>
                  </button>
                )
              })}
              {data.length === 0 ? <EmptyState icon={ClipboardCheck} title="No approval requests" /> : null}
            </div>
          </Panel>
        </div>
        <Panel>
          <div className="mb-4 flex items-center justify-between">
            <h2 className="panel-title">Request Inspector</h2>
            <Badge tone="success">SafeMode</Badge>
          </div>
          {selectedApproval ? (
            <div className="space-y-3">
              <DetailRow label="Action" value={selectedAction?.name ?? selectedApproval.actionId} />
              <DetailRow label="Risk" value={selectedAction?.riskLevel ?? 'Unknown'} />
              <DetailRow label="Event" value={selectedEvent?.summary ?? selectedApproval.eventId} />
              <DetailRow label="After approval" value={approvalActionPreview(selectedAction, selectedEvent)} />
              <DetailRow label="Parameters" value={`{"target":"${selectedEvent?.target ?? 'unknown'}","safeMode":true}`} />
              <DetailRow label="Audit impact" value="Decision will be written to immutable application audit trail." />
            </div>
          ) : <EmptyState icon={ClipboardCheck} title="Select approval request" />}
        </Panel>
      </section>
      {decision ? <DecisionModal decision={decision} isPending={mutation.isPending} onClose={() => setDecision(undefined)} onSubmit={(comment) => mutation.mutate({ ...decision, comment })} /> : null}
    </PageFrame>
  )
}

function DecisionModal({ decision, isPending, onClose, onSubmit }: { decision: { approval: ApprovalDto; approve: boolean }; isPending: boolean; onClose: () => void; onSubmit: (comment: string) => void }) {
  const [comment, setComment] = useState('')
  const rejectBlocked = !decision.approve && comment.trim().length === 0
  return (
    <div className="fixed inset-0 z-40 grid place-items-center bg-black/60 p-4 backdrop-blur-sm">
      <form className="glass-panel w-full max-w-md rounded-lg p-5" onSubmit={(event) => { event.preventDefault(); if (!rejectBlocked) onSubmit(comment) }}>
        <h2 className="font-display text-xl font-semibold">{decision.approve ? 'Approve action' : 'Reject action'}</h2>
        <textarea className="field mt-4 min-h-28 resize-none" placeholder="Decision comment" value={comment} onChange={(event) => setComment(event.target.value)} />
        <div className="mt-4 flex justify-end gap-2">
          <Button onClick={onClose} type="button">Cancel</Button>
          <Button disabled={isPending || rejectBlocked} type="submit" variant="primary">
            {isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : decision.approve ? <Check className="h-4 w-4" /> : <X className="h-4 w-4" />}
            {decision.approve ? 'Approve' : 'Reject'}
          </Button>
        </div>
      </form>
    </div>
  )
}

function ModulesPage() {
  const [selectedModuleId, setSelectedModuleId] = useState<string>()
  const queryClient = useQueryClient()
  const setActivePage = useShellStore((state) => state.setActivePage)
  const pushToast = useShellStore((state) => state.pushToast)
  const { data: modules = [], isLoading, isFetching } = useQuery({ queryKey: ['modules'], queryFn: listModules })
  const { data: customModules = [] } = useQuery({ queryKey: ['custom-modules'], queryFn: listCustomModules, initialData: loadCustomModules })
  const enabledModules = modules.filter((module) => module.isEnabled)
  const enabledCustomModules = customModules.filter((module) => module.enabled !== false)
  const selectedModule = enabledModules.find((item) => item.id === selectedModuleId) ?? enabledModules[0]

  async function importCustomModule(file: File) {
    const text = await file.text()
    const manifest = validateCustomModuleManifest(JSON.parse(text))
    const result = await uploadCustomModule(manifest)
    if (!result.success) {
      throw new Error(result.message)
    }
    await queryClient.invalidateQueries({ queryKey: ['custom-modules'] })
    saveCustomModules([manifest, ...customModules.filter((module) => module.key !== manifest.key)])
    pushToast({ tone: 'success', title: `${manifest.name} imported`, message: 'Module manifest is available in User modules.' })
  }

  async function removeCustomModule(key: string) {
    const result = await deleteCustomModule(key)
    await queryClient.invalidateQueries({ queryKey: ['custom-modules'] })
    if (!result.success) {
      pushToast({ tone: 'warning', title: result.message })
      return
    }
    saveCustomModules(customModules.filter((module) => module.key !== key))
    pushToast({ tone: 'neutral', title: 'Custom module removed' })
  }

  return (
    <PageFrame
      icon={Blocks}
      title="Modules"
      actions={<Button onClick={() => setActivePage('modules-store')} type="button" variant="primary"><Store className="h-4 w-4" />Module Store</Button>}
    >
      <section className="modules-hub">
        <CustomModuleUpload onImport={importCustomModule} />
        <ModuleConnector isLoading={isLoading || (isFetching && modules.length === 0)} modules={enabledModules} selectedModule={selectedModule} onSelectModule={setSelectedModuleId} />
        {enabledCustomModules.length > 0 ? (
          <Panel className="user-modules-panel">
            <div className="module-section-head">
              <div>
                <h2 className="panel-title">User modules</h2>
                <div className="mt-1 text-xs text-slate-500">Enabled custom modules from the Store registry.</div>
              </div>
              <Badge>{enabledCustomModules.length} enabled</Badge>
            </div>
            <div className="user-module-grid">
              {enabledCustomModules.map((module) => <UserModuleCard key={module.key} manifest={module} onRemove={() => removeCustomModule(module.key)} />)}
            </div>
          </Panel>
        ) : null}
      </section>
    </PageFrame>
  )
}

function ModuleConnector({ isLoading, modules, selectedModule, onSelectModule }: { isLoading?: boolean; modules: ModuleDto[]; selectedModule?: ModuleDto; onSelectModule: (id: string) => void }) {
  const queryClient = useQueryClient()
  const pushToast = useShellStore((state) => state.pushToast)
  const setActivePage = useShellStore((state) => state.setActivePage)
  const [moduleSearch, setModuleSearch] = useState('')
  const filtered = modules.filter((module) => `${module.name} ${module.key} ${module.type}`.toLowerCase().includes(moduleSearch.toLowerCase()))
  const bulkMutation = useMutation({
    mutationFn: async () => {
      await Promise.all(modules.map((module) => moduleCommand(module.id, 'disable')))
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['modules'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      pushToast({ tone: 'neutral', title: 'Connected modules disabled' })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Bulk module command failed', message: mutationErrorMessage(error) }),
  })

  return (
    <section className="module-connect-layout">
      <Panel className="module-picker-panel">
        <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="panel-title">Connected modules</h2>
            <div className="mt-1 text-xs text-slate-500">Enabled in Module Store and available for configuration.</div>
          </div>
          <div className="flex gap-2">
            <button className="module-mini-action" onClick={() => setActivePage('modules-store')} type="button">Open store</button>
            <button className="module-mini-action" disabled={bulkMutation.isPending || modules.length === 0} onClick={() => bulkMutation.mutate()} type="button">Disable all</button>
          </div>
        </div>
        <label className="relative block">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
          <input className="field search-field h-10 pl-9" placeholder="Search connected modules..." value={moduleSearch} onChange={(event) => setModuleSearch(event.target.value)} />
        </label>
        <div className="module-picker-list">
          {isLoading ? (
            Array.from({ length: 7 }).map((_, index) => (
              <div key={index} className="module-picker-skeleton">
                <span />
                <div><i /><i /></div>
                <b />
              </div>
            ))
          ) : null}
          {filtered.map((module) => {
            const Icon = moduleIconFor(module.type)
            return (
              <button key={module.id} className={cn('module-picker-item', module.id === selectedModule?.id && 'active')} onClick={() => onSelectModule(module.id)} type="button">
                <span className={cn('module-picker-icon', healthToneClasses(module.healthStatus))}><Icon className="h-4 w-4" /></span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-semibold text-slate-100">{module.name}</span>
                  <span className="mt-1 block truncate text-xs text-slate-500">{moduleConnectionProfile(module)?.label ?? module.type}</span>
                </span>
                <span className={cn('module-state-pill', module.isEnabled && 'on')}>{module.isEnabled ? 'On' : 'Off'}</span>
              </button>
            )
          })}
          {!isLoading && filtered.length === 0 ? (
            <div className="module-picker-empty">
              <EmptyState icon={Blocks} title={modules.length === 0 ? 'No connected modules' : 'No modules found'} />
              {modules.length === 0 ? <button onClick={() => setActivePage('modules-store')} type="button">Open Module Store</button> : null}
            </div>
          ) : null}
        </div>
      </Panel>
      {isLoading ? <Panel><div className="module-detail-loading"><Loader2 className="h-5 w-5 animate-spin" /><span>Loading modules...</span></div></Panel> : <ModuleConnectionPanel selectedModule={selectedModule} />}
    </section>
  )
}

function CustomModuleUpload({ onImport }: { onImport: (file: File) => Promise<void> }) {
  const pushToast = useShellStore((state) => state.pushToast)
  const [isImporting, setIsImporting] = useState(false)

  async function handleFile(file?: File) {
    if (!file) {
      return
    }
    setIsImporting(true)
    try {
      await onImport(file)
    } catch (error) {
      pushToast({ tone: 'danger', title: 'Module import failed', message: mutationErrorMessage(error) })
    } finally {
      setIsImporting(false)
    }
  }

  return (
    <Panel className="module-upload-panel">
      <div className="module-upload-copy">
        <span className="module-upload-icon"><Upload className="h-5 w-5" /></span>
        <div>
          <h2 className="panel-title">Upload custom module</h2>
          <p>Import a signed-ready `sysassist.module/v1` manifest from your module package. The manifest is validated locally before it appears in User modules.</p>
        </div>
      </div>
      <label className="module-upload-control">
        <input accept=".json,application/json" type="file" onChange={(event) => handleFile(event.target.files?.[0])} />
        {isImporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <FileJson className="h-4 w-4" />}
        Select manifest
      </label>
    </Panel>
  )
}

function UserModuleCard({ manifest, onRemove }: { manifest: CustomModuleManifest; onRemove: () => void | Promise<void> }) {
  const pushToast = useShellStore((state) => state.pushToast)
  const queryClient = useQueryClient()
  const firstAction = manifest.actions?.[0]
  const isEnabled = manifest.enabled !== false
  const runtimeMutation = useMutation({
    mutationFn: async (operation: 'test' | 'events' | 'action') => {
      if (operation === 'test') {
        return testCustomModule(manifest.key)
      }
      if (operation === 'events') {
        const result = await fetchCustomModuleEvents(manifest.key)
        return { success: result.success, message: result.message }
      }
      if (!firstAction) {
        return { success: false, message: 'No custom action is declared in this manifest.' }
      }
      return executeCustomModuleAction(manifest.key, firstAction.key)
    },
    onSuccess: (result) => pushToast({ tone: result.success ? 'success' : 'warning', title: result.message }),
    onError: (error) => pushToast({ tone: 'danger', title: 'Custom module runtime failed', message: mutationErrorMessage(error) }),
  })
  const stateMutation = useMutation({
    mutationFn: () => customModuleCommand(manifest.key, isEnabled ? 'disable' : 'enable'),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['custom-modules'] })
      pushToast({ tone: result.success ? 'success' : 'warning', title: result.message })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Custom module state failed', message: mutationErrorMessage(error) }),
  })

  return (
    <article className="user-module-card">
      <div className="user-module-main">
        <span className="user-module-icon"><PuzzleIcon /></span>
        <div className="min-w-0">
          <div className="user-module-title">{manifest.name}</div>
          <div className="user-module-meta">{manifest.key} / v{manifest.version}</div>
        </div>
        <Badge tone={isEnabled ? 'success' : 'neutral'}>{isEnabled ? 'On' : 'Off'}</Badge>
      </div>
      <p>{manifest.description ?? 'No module description provided.'}</p>
      <div className="module-tag-row">
        {(manifest.tags ?? []).slice(0, 4).map((tag) => <span key={tag}>{tag}</span>)}
        {manifest.category ? <span>{manifest.category}</span> : null}
      </div>
      <div className="user-module-runtime">
        <Button className="h-8" disabled={stateMutation.isPending} onClick={() => stateMutation.mutate()} type="button">{isEnabled ? <X className="h-3.5 w-3.5" /> : <Check className="h-3.5 w-3.5" />}{isEnabled ? 'Disable' : 'Enable'}</Button>
        <Button className="h-8" disabled={runtimeMutation.isPending || !isEnabled} onClick={() => runtimeMutation.mutate('test')} type="button"><DatabaseZap className="h-3.5 w-3.5" />Test</Button>
        <Button className="h-8" disabled={runtimeMutation.isPending || !isEnabled} onClick={() => runtimeMutation.mutate('events')} type="button"><FileClock className="h-3.5 w-3.5" />Events</Button>
        <Button className="h-8" disabled={runtimeMutation.isPending || !firstAction || !isEnabled} onClick={() => runtimeMutation.mutate('action')} type="button"><Play className="h-3.5 w-3.5" />Run</Button>
      </div>
      <div className="user-module-footer">
        <span>{manifest.actions?.length ?? 0} actions</span>
        <span>{manifest.settings?.length ?? 0} settings</span>
        <button onClick={onRemove} type="button">Remove</button>
      </div>
    </article>
  )
}

function PuzzleIcon() {
  return <PlugZap className="h-4 w-4" />
}

function ModuleConnectionPanel({ selectedModule }: { selectedModule?: ModuleDto }) {
  const queryClient = useQueryClient()
  const pushToast = useShellStore((state) => state.pushToast)
  const moduleId = selectedModule?.id ?? ''
  const { data: remoteSettings = [] } = useQuery({ queryKey: ['module-settings', moduleId], queryFn: () => listModuleSettings(moduleId), enabled: Boolean(moduleId) })
  const [draft, setDraft] = useState<ModuleSettingDto[]>([])
  const profile = moduleConnectionProfile(selectedModule)
  const settings = draft.length > 0 ? draft : remoteSettings
  const visibleSettings = connectionSettings(settings, profile)
  const requiredVisible = visibleSettings.filter((setting) => setting.isRequired)
  const missingRequired = requiredVisible.filter((setting) => setting.isSecret ? !setting.hasValue && !setting.value : !setting.value)
  const saveMutation = useMutation({
    mutationFn: () => saveModuleSettings(moduleId, settings),
    onSuccess: (result) => {
      queryClient.setQueryData(['module-settings', moduleId], settings)
      queryClient.invalidateQueries({ queryKey: ['module-settings', moduleId] })
      queryClient.invalidateQueries({ queryKey: ['modules'] })
      setDraft([])
      pushToast({ tone: result.success ? 'success' : 'warning', title: result.message })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Settings save failed', message: mutationErrorMessage(error) }),
  })
  const secretMutation = useMutation({
    mutationFn: ({ key, value }: { key: string; value: string }) => replaceModuleSecret(moduleId, key, value),
    onSuccess: (result) => {
      queryClient.setQueryData(['module-settings', moduleId], settings)
      queryClient.invalidateQueries({ queryKey: ['module-settings', moduleId] })
      pushToast({ tone: result.success ? 'success' : 'warning', title: result.message })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Secret update failed', message: mutationErrorMessage(error) }),
  })
  const commandMutation = useMutation({
    mutationFn: (command: 'test-connection' | 'enable' | 'disable') => moduleCommand(moduleId, command),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['modules'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      pushToast({ tone: result.success ? 'success' : 'warning', title: result.message })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Module operation failed', message: mutationErrorMessage(error) }),
  })
  const connectMutation = useMutation({
    mutationFn: async () => {
      const saveResult = await saveModuleSettings(moduleId, settings)
      if (!saveResult.success) {
        return saveResult
      }
      const enableResult = await moduleCommand(moduleId, 'enable')
      if (!enableResult.success) {
        return enableResult
      }
      return moduleCommand(moduleId, 'test-connection')
    },
    onSuccess: (result) => {
      queryClient.setQueryData(['module-settings', moduleId], settings)
      queryClient.invalidateQueries({ queryKey: ['module-settings', moduleId] })
      queryClient.invalidateQueries({ queryKey: ['modules'] })
      setDraft([])
      pushToast({ tone: result.success ? 'success' : 'warning', title: result.success ? 'Connection tested' : 'Connection needs attention', message: result.message })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Connection failed', message: mutationErrorMessage(error) }),
  })

  function updateSetting(setting: ModuleSettingDto, value?: string) {
    const base = draft.length > 0 ? draft : remoteSettings
    setDraft(base.map((item) => item.id === setting.id ? { ...item, value, hasValue: Boolean(value) } : item))
  }

  function applyDefaults() {
    const base = draft.length > 0 ? draft : remoteSettings
    setDraft(base.map((setting) => {
      const value = defaultSettingValue(setting, profile)
      return { ...setting, value, hasValue: setting.isSecret && setting.hasValue ? true : Boolean(value) }
    }))
  }

  if (!selectedModule) {
    return <Panel><EmptyState icon={Blocks} title="Select a module" /></Panel>
  }

  return (
    <div className="module-connect-detail space-y-5">
      <Panel className="module-connect-panel">
        <div className="module-connect-head">
          <div>
            <div className="module-title-row">
              <h2 className="panel-title">{selectedModule.name}</h2>
              <StatusBadge value={selectedModule.healthStatus} />
            </div>
            <div className="module-subtitle">{profile?.label ?? selectedModule.type}</div>
          </div>
        </div>
        <p className="module-connect-note">{profile?.helper ?? selectedModule.description}</p>
        <div className="module-meta-strip">
          <span>{selectedModule.isEnabled ? 'Enabled' : 'Disabled'}</span>
          <span>Real mode</span>
          <span>Health: {selectedModule.healthStatus}</span>
          <span>Checked: {timeLabel(selectedModule.lastHealthCheckAt)}</span>
          <span>Fetch: {timeLabel(selectedModule.lastFetchAt)}</span>
        </div>
        <div className="module-connect-actions">
          <Button disabled={saveMutation.isPending} onClick={() => saveMutation.mutate()} type="button"><CheckCircle2 className="h-4 w-4" />Save</Button>
          <Button disabled={commandMutation.isPending} onClick={() => commandMutation.mutate('test-connection')} type="button"><DatabaseZap className="h-4 w-4" />Test</Button>
          <Button disabled={connectMutation.isPending || missingRequired.length > 0} onClick={() => connectMutation.mutate()} type="button" variant="primary">
            {connectMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
            Connect
          </Button>
          <Button disabled={commandMutation.isPending} onClick={() => commandMutation.mutate(selectedModule.isEnabled ? 'disable' : 'enable')} type="button">
            {selectedModule.isEnabled ? <X className="h-4 w-4" /> : <Check className="h-4 w-4" />}
            {selectedModule.isEnabled ? 'Disable' : 'Enable'}
          </Button>
          <button className="module-link-action" onClick={applyDefaults} type="button"><RotateCcw className="h-3.5 w-3.5" /> Defaults</button>
        </div>
        {missingRequired.length > 0 ? <div className="module-connect-warning">Required before connecting: {missingRequired.map((setting) => setting.key).join(', ')}</div> : null}
        <div className="module-connect-form">
          {visibleSettings.map((setting) => (
            <label key={setting.id} className="module-connect-field">
              <span className="module-connect-label">
                <span>{setting.key}{setting.isRequired ? <span className="text-amber-300"> *</span> : null}</span>
              </span>
              {setting.valueType === 'bool' || setting.valueType === 'boolean' ? (
                <button className="module-connect-toggle" onClick={() => updateSetting(setting, setting.value === 'true' ? 'false' : 'true')} type="button">
                  <span>{setting.value === 'true' ? 'On' : 'Off'}</span>
                  <span className={cn('switch-visual', setting.value === 'true' && 'on')} />
                </button>
              ) : (
                <input
                  autoComplete="off"
                  className="field"
                  placeholder={setting.isSecret && setting.hasValue ? '********' : setting.description}
                  type={settingInputType(setting)}
                  value={setting.isSecret && setting.value === '********' ? '' : setting.value ?? ''}
                  onFocus={() => {
                    if (setting.isSecret && setting.value === '********') {
                      updateSetting(setting, '')
                    }
                  }}
                  onChange={(event) => updateSetting(setting, event.target.value)}
                />
              )}
              {setting.isSecret ? <Button className="mt-2 h-8" disabled={!setting.value || setting.value === '********'} onClick={() => secretMutation.mutate({ key: setting.key, value: setting.value ?? '' })} type="button"><KeyRound className="h-3.5 w-3.5" />Save secret</Button> : null}
            </label>
          ))}
        </div>
      </Panel>
    </div>
  )
}

function ModuleStorePage() {
  const setActivePage = useShellStore((state) => state.setActivePage)
  const pushToast = useShellStore((state) => state.pushToast)
  const queryClient = useQueryClient()
  const { data: modules = [] } = useQuery({ queryKey: ['modules', 'store'], queryFn: listModules })
  const { data: customModules = [] } = useQuery({ queryKey: ['custom-modules', 'store'], queryFn: listCustomModules, initialData: loadCustomModules })
  const [storeState, setStoreState] = useState(() => loadModuleStoreState())
  const [storeTab, setStoreTab] = useState<'featured' | 'favorites' | 'connected' | 'history' | 'tags'>('featured')
  const [search, setSearch] = useState('')
  const [activeTag, setActiveTag] = useState('all')
  const favoriteKeys = new Set(storeState.favorites)
  const styleModules = storeStyleLibraries.map((item) => ({
    ...item,
    installed: storeState.activeStyle === item.themeId,
  }))
  const catalogModules = [
    ...storeModulesFromBuiltIns(modules),
    ...storeModulesFromCustom(customModules),
    ...styleModules,
  ]
  const allTags = [...new Set(catalogModules.flatMap((module) => module.tags))].sort()
  const visibleModules = catalogModules.filter((module) => {
    const matchesSearch = `${module.name} ${module.key} ${module.description} ${module.tags.join(' ')}`.toLowerCase().includes(search.toLowerCase())
    const matchesTag = activeTag === 'all' || module.tags.includes(activeTag)
    const matchesTab = storeTab === 'featured'
      ? module.featured
      : storeTab === 'favorites'
        ? favoriteKeys.has(module.key)
      : storeTab === 'connected'
        ? module.installed
        : storeTab === 'tags'
          ? true
          : false
    return matchesSearch && matchesTag && matchesTab
  })
  const connectedCount = catalogModules.filter((module) => module.installed).length

  function persist(next: ModuleStoreState) {
    const normalized = saveModuleStoreState(next)
    setStoreState(normalized)
    applyStyleTheme(normalized.activeStyle)
  }

  function toggleFavorite(key: string) {
    const favorites = favoriteKeys.has(key) ? storeState.favorites.filter((item) => item !== key) : [...storeState.favorites, key]
    persist({ ...storeState, favorites })
  }

  const toggleMutation = useMutation({
    mutationFn: async (module: MarketplaceModule) => {
      if (module.kind === 'built-in') {
        if (!module.moduleId) {
          throw new Error('Built-in module id is missing.')
        }
        const command = module.installed ? 'disable' : 'enable'
        const result = await moduleCommand(module.moduleId, command)
        return { module, result, action: command === 'enable' ? 'Enabled built-in adapter' : 'Disabled built-in adapter' }
      }
      if (module.kind === 'custom') {
        const command = module.installed ? 'disable' : 'enable'
        const result = await customModuleCommand(module.key, command)
        return { module, result, action: command === 'enable' ? 'Enabled custom module' : 'Disabled custom module' }
      }

      const styleIsActive = Boolean(module.themeId && (storeState.activeStyle === module.themeId || storeState.installed.includes(module.key)))
      const nextActiveStyle = styleIsActive ? undefined : module.themeId
      const styleKeys = storeStyleLibraries.map((item) => item.key)
      const nextInstalled = styleIsActive
        ? storeState.installed.filter((item) => item !== module.key)
        : [...storeState.installed.filter((item) => !styleKeys.includes(item)), module.key]
      return {
        module,
        result: { success: true, message: nextActiveStyle ? `${module.name} applied.` : `${module.name} disabled.` },
        action: nextActiveStyle ? 'Applied style library' : 'Disabled style library',
        nextInstalled,
        nextActiveStyle,
      }
    },
    onSuccess: (payload) => {
      const styleStateChanged = Object.prototype.hasOwnProperty.call(payload, 'nextActiveStyle')
      const next = {
        ...storeState,
        installed: payload.nextInstalled ?? storeState.installed,
        activeStyle: styleStateChanged ? payload.nextActiveStyle : storeState.activeStyle,
        history: [{ key: payload.module.key, action: payload.action, at: new Date().toISOString() }, ...storeState.history].slice(0, 20),
      }
      persist(next)
      queryClient.invalidateQueries({ queryKey: ['modules'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      queryClient.invalidateQueries({ queryKey: ['custom-modules'] })
      pushToast({ tone: payload.result.success ? 'success' : 'warning', title: payload.result.message })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Store operation failed', message: mutationErrorMessage(error) }),
  })

  return (
    <PageFrame
      icon={Store}
      title="Module Store"
      actions={<Button onClick={() => setActivePage('modules')} type="button"><Blocks className="h-4 w-4" />Connected modules</Button>}
    >
      <section className="module-store-shell">
        <Panel className="module-store-hero">
          <div>
            <Badge tone="success">Extension-ready</Badge>
            <h2>Manage SysAssist modules and styles</h2>
            <p>Built-in adapters, imported runtime modules, and style libraries are managed from the same catalog state used by the Modules page.</p>
          </div>
          <div className="module-store-hero-stats">
            <article>
              <span className="store-stat-icon"><Store className="h-4 w-4" /></span>
              <div>
                <span>Catalog</span>
                <strong>{catalogModules.length}</strong>
              </div>
            </article>
            <article>
              <span className="store-stat-icon"><Star className="h-4 w-4" /></span>
              <div>
                <span>Favorites</span>
                <strong>{storeState.favorites.length}</strong>
              </div>
            </article>
            <article>
              <span className="store-stat-icon"><PlugZap className="h-4 w-4" /></span>
              <div>
                <span>Enabled</span>
                <strong>{connectedCount}</strong>
              </div>
            </article>
          </div>
        </Panel>
        <Panel className="module-store-toolbar">
          <div className="segmented">
            <button className={cn(storeTab === 'featured' && 'active')} onClick={() => setStoreTab('featured')} type="button"><Star className="h-3.5 w-3.5" />Featured</button>
            <button className={cn(storeTab === 'favorites' && 'active')} onClick={() => setStoreTab('favorites')} type="button"><Star className="h-3.5 w-3.5" />Favorites</button>
            <button className={cn(storeTab === 'connected' && 'active')} onClick={() => setStoreTab('connected')} type="button"><PlugZap className="h-3.5 w-3.5" />Connected</button>
            <button className={cn(storeTab === 'history' && 'active')} onClick={() => setStoreTab('history')} type="button"><Clock3 className="h-3.5 w-3.5" />History</button>
            <button className={cn(storeTab === 'tags' && 'active')} onClick={() => setStoreTab('tags')} type="button"><Tag className="h-3.5 w-3.5" />Tags</button>
          </div>
          <label className="relative block min-w-0">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
            <input className="field search-field h-10 pl-9" placeholder="Search store modules..." value={search} onChange={(event) => setSearch(event.target.value)} />
          </label>
        </Panel>
        {storeTab === 'history' ? (
          <Panel className="module-store-history">
            <div className="module-section-head">
              <div>
                <h2 className="panel-title">Installation history</h2>
                <div className="mt-1 text-xs text-slate-500">Local catalog activity for this workspace.</div>
              </div>
              <Badge>{storeState.history.length} records</Badge>
            </div>
            <div className="store-history-list">
              {storeState.history.map((entry) => {
                const module = catalogModules.find((item) => item.key === entry.key)
                return (
                  <article key={`${entry.key}-${entry.at}`}>
                    <span><Clock3 className="h-4 w-4" /></span>
                    <div>
                      <strong>{module?.name ?? entry.key}</strong>
                      <small>{entry.action} / {fullDateTimeLabel(entry.at)}</small>
                    </div>
                  </article>
                )
              })}
              {storeState.history.length === 0 ? <EmptyState icon={Clock3} title="No module history yet" /> : null}
            </div>
          </Panel>
        ) : storeTab === 'tags' ? (
          <Panel className="module-tags-panel">
            <div className="module-section-head">
              <div>
                <h2 className="panel-title">Tags</h2>
                <div className="mt-1 text-xs text-slate-500">Filter modules by capability, source, or runtime.</div>
              </div>
              <Badge>{allTags.length} tags</Badge>
            </div>
            <div className="module-store-tags">
              <button className={cn(activeTag === 'all' && 'active')} onClick={() => setActiveTag('all')} type="button">All tags</button>
              {allTags.map((tag) => <button key={tag} className={cn(activeTag === tag && 'active')} onClick={() => setActiveTag(tag)} type="button">{tag}</button>)}
            </div>
          </Panel>
        ) : null}
        {storeTab !== 'history' ? (
          <div className="module-store-grid">
            {visibleModules.map((module) => (
              <article key={module.key} className={cn('store-module-card', module.featured && 'featured')}>
                <div className="store-module-card-head">
                  <span className="store-module-icon">
                    {module.kind === 'style' ? <Palette className="h-5 w-5" /> : module.kind === 'built-in' ? (() => {
                      const Icon = moduleIconFor(module.category)
                      return <Icon className="h-5 w-5" />
                    })() : <Layers3 className="h-5 w-5" />}
                  </span>
                  <button className={cn('store-favorite', favoriteKeys.has(module.key) && 'active')} onClick={() => toggleFavorite(module.key)} type="button" aria-label="Toggle favorite"><Star className="h-4 w-4" /></button>
                </div>
                <div>
                  <div className="store-module-title">{module.name}</div>
                  <div className="store-module-meta">{module.author} / {module.category} / {module.version}</div>
                </div>
                <p>{module.description}</p>
                {module.packagePath ? <div className="store-package-path">{module.packagePath}</div> : null}
                <div className="module-tag-row">{module.tags.map((tag) => <span key={tag}>{tag}</span>)}</div>
                <div className="store-module-footer">
                  <Badge tone={module.installed ? 'success' : 'neutral'}>{module.installed ? (module.kind === 'style' ? 'Applied' : 'Enabled') : module.featured ? 'Featured' : 'Available'}</Badge>
                  {module.healthStatus ? <StatusBadge value={module.healthStatus} /> : null}
                  <Button className="h-9" disabled={toggleMutation.isPending} onClick={() => toggleMutation.mutate(module)} type="button">
                    {module.installed ? <X className="h-4 w-4" /> : <Download className="h-4 w-4" />}
                    {module.installed ? (module.kind === 'style' ? 'Disable' : 'Disable') : (module.kind === 'style' ? 'Apply' : 'Enable')}
                  </Button>
                </div>
              </article>
            ))}
            {visibleModules.length === 0 ? <EmptyState icon={Store} title={storeTab === 'favorites' ? 'No favorite modules yet' : 'No modules match this view'} /> : null}
          </div>
        ) : null}
      </section>
    </PageFrame>
  )
}

function ActionsPage() {
  const pushToast = useShellStore((state) => state.pushToast)
  const queryClient = useQueryClient()
  const { data: actions = [] } = useQuery({ queryKey: ['actions'], queryFn: listActions })
  const { data: modules = [] } = useQuery({ queryKey: ['modules'], queryFn: listModules })
  const [moduleFilter, setModuleFilter] = useState('all')
  const [riskFilter, setRiskFilter] = useState('all')
  const [approvalFilter, setApprovalFilter] = useState('all')
  const [actionSearch, setActionSearch] = useState('')
  const [selectedActionId, setSelectedActionId] = useState<string>()
  const [runningActionId, setRunningActionId] = useState<string>()
  const [retryUntilByAction, setRetryUntilByAction] = useState<Record<string, number>>({})
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 500)
    return () => window.clearInterval(timer)
  }, [])
  const mutation = useMutation({
    mutationFn: (actionId: string) => executeAction(actionId),
    onMutate: (actionId) => {
      setRunningActionId(actionId)
    },
    onSuccess: (result) => {
      pushToast({ tone: result.success ? 'success' : 'warning', title: result.message })
      queryClient.invalidateQueries({ queryKey: ['actions'] })
      queryClient.invalidateQueries({ queryKey: ['modules'] })
      queryClient.invalidateQueries({ queryKey: ['audit'] })
      queryClient.invalidateQueries({ queryKey: ['logs'] })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Action failed', message: mutationErrorMessage(error) }),
    onSettled: (_result, _error, actionId) => {
      if (actionId) {
        setRetryUntilByAction((items) => ({ ...items, [actionId]: Date.now() + 2_000 }))
      }
      setRunningActionId(undefined)
      window.setTimeout(() => mutation.reset(), 250)
    },
  })
  const moduleById = new Map(modules.map((module) => [module.id, module]))
  const connectedModules = modules.filter(moduleReadyForActions)
  const enabledActions = actions.filter((action) => action.isEnabled)
  const runnableActions = enabledActions.filter((action) => moduleReadyForActions(moduleById.get(action.moduleId)))
  const filtered = enabledActions.filter((action) => {
    const module = moduleById.get(action.moduleId)
    const haystack = `${action.name} ${action.actionKey} ${action.description ?? ''} ${module?.name ?? ''}`.toLowerCase()
    return haystack.includes(actionSearch.toLowerCase())
      && (moduleFilter === 'all' || action.moduleId === moduleFilter)
      && (riskFilter === 'all' || action.riskLevel === riskFilter)
      && (approvalFilter === 'all' || String(action.requiresApproval) === approvalFilter)
  })
  const selectedAction = filtered.find((action) => action.id === selectedActionId) ?? filtered[0]
  const selectedModule = selectedAction ? moduleById.get(selectedAction.moduleId) : undefined
  const selectedModuleReady = moduleReadyForActions(selectedModule)
  const selectedActionRetryUntil = selectedAction ? retryUntilByAction[selectedAction.id] ?? 0 : 0
  const selectedActionCooldownMs = Math.max(0, selectedActionRetryUntil - now)
  const selectedActionRunning = selectedAction ? runningActionId === selectedAction.id : false
  const anotherActionRunning = Boolean(runningActionId && runningActionId !== selectedAction?.id)
  const actionRunDisabled = !selectedModuleReady || selectedActionRunning || anotherActionRunning || selectedActionCooldownMs > 0
  const actionRunLabel = selectedActionRunning
    ? 'Running...'
    : anotherActionRunning
      ? 'Another action is running'
      : selectedActionCooldownMs > 0
        ? `Retry in ${Math.ceil(selectedActionCooldownMs / 1000)}s`
        : selectedAction?.requiresApproval ? 'Request / Run' : 'Run action'
  const approvalCount = enabledActions.filter((action) => action.requiresApproval).length
  const risks = ['Low', 'Medium', 'High', 'Critical']
  return (
    <PageFrame icon={Play} title="Actions">
      <section className="actions-shell">
        <Panel className="actions-list-panel">
          <div className="actions-summary">
            <div><strong>{enabledActions.length}</strong><span>Actions</span></div>
            <div><strong>{runnableActions.length}</strong><span>Ready</span></div>
            <div><strong>{connectedModules.length}</strong><span>Connected modules</span></div>
            <div><strong>{approvalCount}</strong><span>Approval</span></div>
          </div>
          <div className="actions-toolbar">
            <label className="relative block actions-search">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
              <input className="field search-field h-10 pl-9" placeholder="Search actions..." value={actionSearch} onChange={(event) => setActionSearch(event.target.value)} />
            </label>
            <select className="field h-10" value={moduleFilter} onChange={(event) => setModuleFilter(event.target.value)}>
              <option value="all">All modules</option>
              {modules.map((module) => <option key={module.id} value={module.id}>{module.name}</option>)}
            </select>
            <select className="field h-10" value={riskFilter} onChange={(event) => setRiskFilter(event.target.value)}>
              <option value="all">All risks</option>
              {risks.map((risk) => <option key={risk}>{risk}</option>)}
            </select>
            <select className="field h-10" value={approvalFilter} onChange={(event) => setApprovalFilter(event.target.value)}>
              <option value="all">Any approval</option>
              <option value="true">Needs approval</option>
              <option value="false">Direct</option>
            </select>
          </div>
          <div className="actions-list">
            {filtered.map((action) => {
              const module = moduleById.get(action.moduleId)
              const Icon = moduleIconFor(module?.type ?? '')
              return (
                <button key={action.id} className={cn('action-list-item', selectedAction?.id === action.id && 'active')} onClick={() => setSelectedActionId(action.id)} type="button">
                  <span className="action-list-icon"><Icon className="h-4 w-4" /></span>
                  <span className="min-w-0 flex-1">
                    <span className="action-list-title">{action.name}</span>
                    <span className="action-list-meta">{module?.name ?? shortId(action.moduleId)} / {action.actionKey}</span>
                  </span>
                  <Badge tone={riskTone(action.riskLevel)}>{action.riskLevel}</Badge>
                  <span className={cn('action-enabled-dot', action.isEnabled && 'on')} />
                </button>
              )
            })}
            {filtered.length === 0 ? <EmptyState icon={Play} title="No actions match this view" /> : null}
          </div>
        </Panel>
        <Panel className="action-inspector-panel">
          {selectedAction ? (
            <>
              <div className="action-inspector-head">
                <div>
                  <div className="module-subtitle">{selectedModule?.name ?? shortId(selectedAction.moduleId)}</div>
                  <h2 className="panel-title mt-1">{selectedAction.name}</h2>
                </div>
                <Badge tone={riskTone(selectedAction.riskLevel)}>{selectedAction.riskLevel}</Badge>
              </div>
              <p className="action-description">{selectedAction.description ?? 'Operational action for the connected module.'}</p>
              <div className="action-facts">
                <DetailRow label="Action key" value={selectedAction.actionKey} />
                <DetailRow label="Execution" value={selectedAction.requiresApproval ? 'Approval required' : 'Direct run'} />
                <DetailRow label="Status" value={selectedModuleReady ? 'Ready' : 'Unavailable'} />
                <DetailRow label="Module" value={selectedModule?.healthStatus ?? 'Not connected'} />
                <DetailRow label="SafeMode" value={selectedModule ? (selectedModule.safeMode ? 'Enabled' : 'Disabled') : 'Module not found'} />
              </div>
              <div className="action-policy-note">
                {selectedAction.requiresApproval
                  ? 'This action opens the approval path before execution.'
                  : selectedModule?.safeMode
                    ? 'SafeMode is enabled for this module. SysAssist will block unsafe remediation, then the action button unlocks automatically for retry.'
                    : 'This action can run directly. If an adapter hangs, the UI unlocks automatically after the action timeout.'}
              </div>
              <div className="action-runbar">
                <Button disabled={actionRunDisabled} onClick={() => mutation.mutate(selectedAction.id)} variant="primary">
                  {selectedActionRunning ? <Loader2 className="h-4 w-4 animate-spin" /> : <Play className="h-4 w-4" />}
                  {actionRunLabel}
                </Button>
              </div>
            </>
          ) : (
            <EmptyState icon={Play} title="Connect a module to enable actions" />
          )}
        </Panel>
      </section>
    </PageFrame>
  )
}

function AuditPage() {
  const { data = [] } = useQuery({ queryKey: ['audit'], queryFn: listAudit })
  const [actor, setActor] = useState('all')
  const [result, setResult] = useState('all')
  const [search, setSearch] = useState('')
  const [selectedId, setSelectedId] = useState<string>()
  const filtered = data.filter((item) => {
    const haystack = `${item.actor} ${item.action} ${item.resource} ${item.result} ${item.correlationId ?? ''}`.toLowerCase()
    return (actor === 'all' || item.actor === actor)
      && (result === 'all' || item.result === result)
      && haystack.includes(search.toLowerCase())
  })
  const selected = filtered.find((item) => item.id === selectedId) ?? filtered[0]
  const successCount = filtered.filter((item) => item.result === 'Success').length
  const failedCount = filtered.filter((item) => ['Failed', 'Error', 'Rejected'].includes(item.result)).length
  const actorCount = new Set(filtered.map((item) => item.actor)).size
  const correlationCount = filtered.filter((item) => item.correlationId).length
  const actionGroups = [...new Map(filtered.map((item) => [item.action, filtered.filter((entry) => entry.action === item.action).length])).entries()]
    .sort((a, b) => b[1] - a[1])
    .slice(0, 6)

  return (
    <PageFrame
      icon={History}
      title="Audit"
      actions={
        <>
          <select className="field h-10 w-44" value={actor} onChange={(event) => setActor(event.target.value)}>
            <option value="all">All actors</option>
            {[...new Set(data.map((item) => item.actor))].map((item) => <option key={item}>{item}</option>)}
          </select>
          <select className="field h-10 w-44" value={result} onChange={(event) => setResult(event.target.value)}>
            <option value="all">All results</option>
            {[...new Set(data.map((item) => item.result))].map((item) => <option key={item}>{item}</option>)}
          </select>
        </>
      }
    >
      <section className="audit-redesign">
        <Panel className="audit-command-panel">
          <div className="audit-command-copy">
            <Badge tone="success">Immutable trail</Badge>
            <h2>Evidence explorer</h2>
            <p>Audit records are grouped for review, correlation, and support bundle export without compressing operational context into a dense table.</p>
          </div>
          <label className="relative block audit-search">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
            <input className="field search-field h-10 pl-9" placeholder="Search actor, action, resource, correlation..." value={search} onChange={(event) => setSearch(event.target.value)} />
          </label>
          <div className="audit-metric-grid">
            <article><strong>{filtered.length}</strong><span>Records</span></article>
            <article><strong>{successCount}</strong><span>Success</span></article>
            <article><strong>{failedCount}</strong><span>Attention</span></article>
            <article><strong>{actorCount}</strong><span>Actors</span></article>
          </div>
        </Panel>
        <div className="audit-workspace">
          <Panel className="audit-stream-panel">
            <div className="module-section-head">
              <div>
                <h2 className="panel-title">Record stream</h2>
                <div className="mt-1 text-xs text-slate-500">Latest filtered audit entries.</div>
              </div>
              <Badge>{correlationCount}/{filtered.length} correlated</Badge>
            </div>
            <div className="audit-record-stream">
              {filtered.map((item) => <AuditRecordButton key={item.id} item={item} selected={selected?.id === item.id} onClick={() => setSelectedId(item.id)} />)}
              {filtered.length === 0 ? <EmptyState icon={History} title="No audit records match filters" /> : null}
            </div>
          </Panel>
          <Panel className="audit-inspector">
            {selected ? (
              <>
                <div className="audit-inspector-head">
                  <div>
                    <div className="module-subtitle">{fullDateTimeLabel(selected.createdAt)}</div>
                    <h2 className="panel-title mt-1">{selected.action}</h2>
                  </div>
                  <StatusBadge value={selected.result} />
                </div>
                <div className="audit-inspector-grid">
                  <DetailRow label="Actor" value={selected.actor} />
                  <DetailRow label="Resource" value={selected.resource} />
                  <DetailRow label="Correlation" value={selected.correlationId ?? 'No correlation'} />
                  <DetailRow label="Record id" value={selected.id} />
                </div>
                <div className="audit-evidence-box">
                  <span>Evidence JSON</span>
                  <pre>{JSON.stringify({
                    id: selected.id,
                    createdAt: selected.createdAt,
                    actor: selected.actor,
                    action: selected.action,
                    resource: selected.resource,
                    result: selected.result,
                    correlationId: selected.correlationId ?? null,
                  }, null, 2)}</pre>
                </div>
              </>
            ) : <EmptyState icon={History} title="Select audit record" />}
          </Panel>
        </div>
        <Panel className="audit-action-map">
          <div className="module-section-head">
            <div>
              <h2 className="panel-title">Action distribution</h2>
              <div className="mt-1 text-xs text-slate-500">Most frequent actions in the current filter.</div>
            </div>
            <Badge>{actionGroups.length} groups</Badge>
          </div>
          <div className="audit-action-bars">
            {actionGroups.map(([action, count]) => (
              <article key={action}>
                <span>{action}</span>
                <div><i style={{ width: `${Math.max(8, (count / Math.max(1, filtered.length)) * 100)}%` }} /></div>
                <strong>{count}</strong>
              </article>
            ))}
            {actionGroups.length === 0 ? <EmptyState icon={History} title="No action groups" /> : null}
          </div>
        </Panel>
      </section>
    </PageFrame>
  )
}

function AuditRecordButton({ item, selected, onClick }: { item: AuditEntryDto; selected: boolean; onClick: () => void }) {
  return (
    <button className={cn('audit-record-card', selected && 'active')} onClick={onClick} type="button">
      <span className={cn('audit-record-icon', toneForStatus(item.result))}><History className="h-4 w-4" /></span>
      <span className="audit-record-main">
        <strong>{item.action}</strong>
        <small>{item.actor} / {item.resource}</small>
      </span>
      <span className="audit-record-side">
        <StatusBadge value={item.result} />
        <small>{timeLabel(item.createdAt)}</small>
      </span>
    </button>
  )
}

function LogsPage() {
  const { data = [] } = useQuery({ queryKey: ['logs'], queryFn: listLogs })
  const [level, setLevel] = useState('all')
  const [component, setComponent] = useState('all')
  const [selected, setSelected] = useState<string>()
  const filtered = data.filter((item) => (level === 'all' || item.level === level) && (component === 'all' || item.component === component))
  const selectedLog = data.find((item) => item.id === selected) ?? filtered[0]
  return (
    <PageFrame
      icon={Logs}
      title="System Logs"
      actions={
        <>
          <select className="field h-10 w-40" value={level} onChange={(event) => setLevel(event.target.value)}>
            <option value="all">All levels</option>
            {[...new Set(data.map((item) => item.level))].map((item) => <option key={item}>{item}</option>)}
          </select>
          <select className="field h-10 w-48" value={component} onChange={(event) => setComponent(event.target.value)}>
            <option value="all">All components</option>
            {[...new Set(data.map((item) => item.component))].map((item) => <option key={item}>{item}</option>)}
          </select>
        </>
      }
    >
      <section className="logs-layout">
        <Panel className="logs-list-panel">
          <div className="logs-list">
            {filtered.map((item) => (
              <button key={item.id} className={cn('log-entry', selectedLog?.id === item.id && 'active', item.level.toLowerCase().includes('warn') && 'warning', item.level.toLowerCase().includes('error') && 'error')} onClick={() => setSelected(item.id)} type="button">
                <span className="log-entry-level"><StatusBadge value={item.level} /></span>
                <span className="log-entry-main">
                  <strong>{item.message}</strong>
                  <small>{item.component} / {shortId(item.correlationId)}</small>
                </span>
                <span className="log-entry-time">{fullDateTimeLabel(item.createdAt)}</span>
              </button>
            ))}
            {filtered.length === 0 ? <EmptyState icon={Logs} title="No logs match filters" /> : null}
          </div>
        </Panel>
        <Panel className="log-detail-panel">
          <div className="mb-4 flex items-center justify-between gap-3">
            <h2 className="panel-title">Log details</h2>
            {selectedLog ? <StatusBadge value={selectedLog.level} /> : null}
          </div>
          {selectedLog ? (
            <div className="log-detail-grid">
              <DetailRow label="Message" value={selectedLog.message} />
              <DetailRow label="Component" value={selectedLog.component} />
              <DetailRow label="Created" value={fullDateTimeLabel(selectedLog.createdAt)} />
              <DetailRow label="Correlation" value={selectedLog.correlationId ?? 'No correlation'} />
              <pre>{JSON.stringify({ id: selectedLog.id, component: selectedLog.component, level: selectedLog.level, correlationId: selectedLog.correlationId ?? null, details: parseLogDetails(selectedLog.detailsJson) }, null, 2)}</pre>
            </div>
          ) : <EmptyState icon={Logs} title="No log selected" />}
        </Panel>
      </section>
    </PageFrame>
  )
}

function parseLogDetails(detailsJson?: string) {
  if (!detailsJson) {
    return null
  }

  try {
    return JSON.parse(detailsJson)
  } catch {
    return detailsJson
  }
}

function NotificationsPage() {
  const pushToast = useShellStore((state) => state.pushToast)
  const queryClient = useQueryClient()
  const { data = [] } = useQuery({ queryKey: ['notifications'], queryFn: listNotifications })
  const mutation = useMutation({
    mutationFn: markNotificationRead,
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ['notifications'] })
      queryClient.setQueryData<NotificationDto[]>(['notifications'], (current = []) => current.map((item) => item.id === id ? { ...item, status: 'Read' } : item))
    },
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] })
      pushToast({ tone: result.success ? 'success' : 'warning', title: result.message })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Mark read failed', message: mutationErrorMessage(error) }),
  })
  const channelIcon = (channel: string) => channel.toLowerCase().includes('telegram') ? MessageCircle : channel.toLowerCase().includes('smtp') || channel.toLowerCase().includes('email') ? Mail : Bell
  const visibleNotifications = data.filter((notification) => notification.status !== 'Read')
  return (
    <PageFrame icon={Bell} title="Notifications">
      <section className="grid gap-3 xl:grid-cols-2">
        {visibleNotifications.map((notification: NotificationDto) => (
          <Panel key={notification.id} className="card-hover">
            <div className="flex items-start justify-between gap-3">
              <div className="flex min-w-0 gap-3">
                <div className="grid h-10 w-10 shrink-0 place-items-center rounded-lg border border-cyan-300/20 bg-cyan-300/10 text-cyan-200">
                  {(() => {
                    const Icon = channelIcon(notification.channel)
                    return <Icon className="h-4 w-4" />
                  })()}
                </div>
                <div className="min-w-0">
                  <div className="truncate text-sm font-medium text-slate-100">{notification.subject ?? notification.channel}</div>
                  <div className="mt-1 text-xs text-slate-500">{notification.recipient} / {timeLabel(notification.createdAt)}</div>
                  <p className="mt-3 text-sm text-slate-400">Message preview is available in support bundle evidence and linked to event <span className="mono text-cyan-200">{shortId(notification.relatedEventId)}</span>.</p>
                </div>
              </div>
              <StatusBadge value={notification.status} />
            </div>
            <div className="mt-4 flex items-center justify-between gap-3">
              <Badge>{notification.channel}</Badge>
              <Button className="h-9" disabled={mutation.isPending} onClick={() => mutation.mutate(notification.id)}><Check className="h-4 w-4" />Mark read</Button>
            </div>
          </Panel>
        ))}
        {visibleNotifications.length === 0 ? <div className="xl:col-span-2"><EmptyState icon={Bell} title="No notifications" /></div> : null}
      </section>
    </PageFrame>
  )
}

function UsersPage() {
  const queryClient = useQueryClient()
  const pushToast = useShellStore((state) => state.pushToast)
  const { data: users = [] } = useQuery({ queryKey: ['users'], queryFn: listUsers })
  const { data: roles = [] } = useQuery({ queryKey: ['roles'], queryFn: listRoles })
  const [modalOpen, setModalOpen] = useState(false)
  const createMutation = useMutation({
    mutationFn: async ({ user, roles: selectedRoles }: { user: CreateUserRequest; roles: string[] }) => {
      const created = await createUser(user)
      if (selectedRoles.length > 0) {
        await updateUserRoles(created.id, selectedRoles)
      }
      return { ...created, roles: selectedRoles }
    },
    onSuccess: (user) => {
      queryClient.invalidateQueries({ queryKey: ['users'] })
      pushToast({ tone: 'success', title: `User ${user.login} created` })
      setModalOpen(false)
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'User creation failed', message: mutationErrorMessage(error) }),
  })
  const permissionRows = [
    ['Dashboard', 'Admin', 'SeniorAdmin', 'Engineer', 'Operator', 'Auditor'],
    ['Approvals', 'Admin', 'SeniorAdmin'],
    ['Modules', 'Admin', 'SeniorAdmin', 'Engineer'],
    ['Actions', 'Admin', 'SeniorAdmin', 'Engineer'],
    ['Audit', 'Admin', 'SeniorAdmin', 'Auditor'],
    ['Support Bundle', 'Admin', 'SeniorAdmin', 'Auditor'],
  ]
  return (
    <PageFrame icon={UsersRound} title="Users / Roles" actions={<Button onClick={() => setModalOpen(true)} variant="primary"><UserCog className="h-4 w-4" />Create user</Button>}>
      <section className="grid gap-5 xl:grid-cols-2">
        <Panel>
          <h2 className="panel-title mb-4">Users</h2>
          <div className="overflow-x-auto">
            <table className="data-table">
              <thead><tr><th>Login</th><th>Name</th><th>Email</th><th>Roles</th><th>Status</th></tr></thead>
              <tbody>
                {users.map((user) => (
                  <tr key={user.id}>
                    <td className="mono">{user.login}</td>
                    <td>{user.displayName}</td>
                    <td>{user.email}</td>
                    <td><div className="flex flex-wrap gap-1">{user.roles.map((role) => <Badge key={role}>{role}</Badge>)}</div></td>
                    <td><StatusBadge value={user.isActive ? 'Active' : 'Disabled'} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
        <Panel>
          <h2 className="panel-title mb-4">Roles</h2>
          <div className="space-y-3">
            {roles.map((role) => (
              <article key={role.id} className="rounded-lg border border-slate-700/60 bg-slate-950/45 p-3">
                <div className="flex items-center justify-between gap-2">
                  <div className="font-medium text-slate-100">{role.name}</div>
                  <Badge>{users.filter((user) => user.roles.includes(role.name)).length}</Badge>
                </div>
                <div className="mt-1 text-sm text-slate-400">{role.description}</div>
              </article>
            ))}
          </div>
        </Panel>
      </section>
      <Panel>
        <h2 className="panel-title mb-4">Permission Matrix</h2>
        <div className="overflow-x-auto">
          <div className="permission-grid text-sm">
            <div className="text-slate-500">Page / Action</div>
            {['Admin', 'SeniorAdmin', 'Engineer', 'Operator', 'Auditor'].map((role) => <div key={role} className="text-slate-300">{role}</div>)}
            {permissionRows.map(([page, ...allowed]) => (
              <div key={page} className="contents">
                <div className="text-slate-200">{page}</div>
                {['Admin', 'SeniorAdmin', 'Engineer', 'Operator', 'Auditor'].map((role) => (
                  <div key={`${page}-${role}`}>{allowed.includes(role) ? <CheckCircle2 className="h-4 w-4 text-emerald-300" /> : <X className="h-4 w-4 text-slate-600" />}</div>
                ))}
              </div>
            ))}
          </div>
        </div>
      </Panel>
      {modalOpen ? (
        <UserModal
          isPending={createMutation.isPending}
          roles={roles.map((role) => role.name)}
          onClose={() => setModalOpen(false)}
          onSubmit={(payload) => createMutation.mutate(payload)}
        />
      ) : null}
    </PageFrame>
  )
}

function UserModal({
  isPending,
  roles,
  onClose,
  onSubmit,
}: {
  isPending: boolean
  roles: string[]
  onClose: () => void
  onSubmit: (payload: { user: CreateUserRequest; roles: string[] }) => void
}) {
  const [loginValue, setLoginValue] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [selectedRole, setSelectedRole] = useState(roles[0] ?? 'Operator')
  const [isActive, setIsActive] = useState(true)
  const roleOptions = roles.length > 0 ? roles : ['Operator']
  const effectiveRole = roleOptions.includes(selectedRole) ? selectedRole : roleOptions[0]

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit({
      user: {
        login: loginValue.trim(),
        displayName: displayName.trim(),
        email: email.trim(),
        password,
        isActive,
      },
      roles: effectiveRole ? [effectiveRole] : [],
    })
  }

  return (
    <div className="fixed inset-0 z-40 grid place-items-center bg-black/60 p-4 backdrop-blur-sm">
      <form className="glass-panel w-full max-w-lg rounded-lg p-5" onSubmit={handleSubmit}>
        <h2 className="font-display text-xl font-semibold text-slate-50">Create user</h2>
        <div className="mt-4 grid gap-3 md:grid-cols-2">
          <input className="field" placeholder="login" required value={loginValue} onChange={(event) => setLoginValue(event.target.value)} />
          <input className="field" placeholder="display name" required value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
          <input className="field md:col-span-2" placeholder="email" required type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
          <input className="field" minLength={12} placeholder="temporary password" required type="password" value={password} onChange={(event) => setPassword(event.target.value)} />
          <select className="field" value={effectiveRole} onChange={(event) => setSelectedRole(event.target.value)}>
            {roleOptions.map((role) => <option key={role}>{role}</option>)}
          </select>
          <button className="flex items-center justify-between rounded-lg border border-slate-700/60 bg-slate-950/45 px-3 py-2 text-sm text-slate-300" onClick={() => setIsActive((value) => !value)} type="button">
            <span>Active account</span>
            <span className={cn('switch-visual', isActive && 'on')} />
          </button>
        </div>
        <div className="mt-5 flex justify-end gap-2">
          <Button disabled={isPending} onClick={onClose} type="button">Cancel</Button>
          <Button disabled={isPending} type="submit" variant="primary">
            {isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
            Create user
          </Button>
        </div>
      </form>
    </div>
  )
}

function SupportPage() {
  const pushToast = useShellStore((state) => state.pushToast)
  const { data, refetch, isFetching } = useQuery({ queryKey: ['support-bundle-preview'], queryFn: downloadSupportBundle, enabled: false })
  const mutation = useMutation({
    mutationFn: downloadSupportBundle,
    onSuccess: (bundle) => {
      const blob = new Blob([JSON.stringify(bundle, null, 2)], { type: 'application/json' })
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = `sysassist-support-bundle-${new Date().toISOString().replaceAll(':', '-')}.json`
      anchor.click()
      URL.revokeObjectURL(url)
      pushToast({ tone: 'success', title: 'Support bundle downloaded', message: `${bundle.incidents.length} incidents included` })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Support bundle failed', message: mutationErrorMessage(error) }),
  })
  const bundle = data
  const sections = bundle ? Object.keys(bundle).filter((key) => !['generatedAt', 'product', 'version'].includes(key)) : ['modules', 'healthHistory', 'incidents', 'recommendations', 'approvals', 'actions', 'audit', 'logs', 'notifications', 'users', 'roles', 'diagnostics']
  return (
    <PageFrame
      icon={LifeBuoy}
      title="Support Bundle"
      actions={
        <>
          <Button onClick={() => refetch()}><FileJson className="h-4 w-4" />Preview</Button>
          <Button onClick={() => mutation.mutate()} variant="primary"><Download className="h-4 w-4" />Download</Button>
        </>
      }
    >
      <section className="hero-panel mb-5 rounded-lg p-5">
        <div className="relative flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <Badge tone="success">No passwords / no raw tokens</Badge>
            <h2 className="mt-3 font-display text-2xl font-semibold text-slate-50">Export operational evidence package</h2>
            <p className="mt-2 max-w-2xl text-sm text-slate-400">Support bundle gathers module state, health, incidents, approvals, audit, logs, notifications, users without hashes, and diagnostics for defense or audit review.</p>
          </div>
          <FileText className="h-16 w-16 text-cyan-200/70" />
        </div>
      </section>
      <section className="grid gap-5 xl:grid-cols-2">
        <Panel>
          <h2 className="panel-title mb-4">Included sections</h2>
          <div className="grid gap-2">
            {sections.map((section) => (
              <div key={section} className="flex items-center gap-2 rounded-lg border border-slate-700/60 bg-slate-950/45 px-3 py-2">
                <CheckCircle2 className="h-4 w-4 text-emerald-300" />
                <span className="text-sm text-slate-200">{section}</span>
              </div>
            ))}
          </div>
        </Panel>
        <Panel>
          <h2 className="panel-title mb-4">Recent export preview</h2>
          {isFetching ? <SkeletonBlock className="h-72" /> : bundle ? (
            <pre className="max-h-[460px] overflow-auto rounded-lg border border-slate-700/60 bg-[#17110d] p-4 text-xs text-slate-300">{JSON.stringify(bundle, null, 2)}</pre>
          ) : <EmptyState icon={Archive} title="No preview loaded" />}
        </Panel>
      </section>
      <Panel>
        <h2 className="panel-title mb-4">Recent exports</h2>
        <div className="overflow-x-auto">
          <table className="data-table">
            <thead><tr><th>Generated</th><th>File</th><th>Sections</th><th>Policy</th></tr></thead>
            <tbody>
              <tr>
                <td className="mono">{bundle ? timeLabel(bundle.generatedAt) : timeLabel(new Date().toISOString())}</td>
                <td className="mono">sysassist-support-bundle.json</td>
                <td>{sections.length}</td>
                <td><Badge tone="success">Sanitized</Badge></td>
              </tr>
            </tbody>
          </table>
        </div>
      </Panel>
    </PageFrame>
  )
}

function DiagnosticsPage() {
  const queryClient = useQueryClient()
  const pushToast = useShellStore((state) => state.pushToast)
  const { data, isLoading, isFetching } = useQuery({ queryKey: ['diagnostics'], queryFn: getDiagnostics })
  const { data: readiness, isLoading: readinessLoading } = useQuery({ queryKey: ['production-readiness'], queryFn: getProductionReadiness })
  const mutation = useMutation({
    mutationFn: runDiagnostics,
    onSuccess: (result: OperationResultDto) => {
      queryClient.invalidateQueries({ queryKey: ['diagnostics'] })
      queryClient.invalidateQueries({ queryKey: ['production-readiness'] })
      pushToast({ tone: result.success ? 'success' : 'warning', title: result.message })
    },
    onError: (error) => pushToast({ tone: 'danger', title: 'Diagnostics failed', message: mutationErrorMessage(error) }),
  })
  const components = data?.components ?? []
  const readinessGates = readiness?.gates ?? []
  const failedRequiredGates = readinessGates.filter((gate) => gate.required && !gate.passed)
  const optionalGateWarnings = readinessGates.filter((gate) => !gate.required && !gate.passed)
  const warningCount = components.filter(diagnosticFindingIsWarning).length
  const score = components.length === 0 ? 0 : Math.max(0, Math.round(((components.length - warningCount) / components.length) * 100))
  const healthyCount = Math.max(components.length - warningCount, 0)
  const scoreLoading = isLoading || mutation.isPending || (isFetching && !data)
  return (
    <PageFrame icon={TerminalSquare} title="Diagnostics" actions={<Button onClick={() => mutation.mutate()} variant="primary"><RefreshCcw className="h-4 w-4" />Run</Button>}>
      <section className="diagnostics-layout">
        <Panel className="diagnostics-score-panel">
          <div className="diagnostics-panel-head">
            <div>
              <h2 className="panel-title">Overall health score</h2>
              <div className="mt-1 text-xs text-slate-500">{components.length} checks / {warningCount} warnings</div>
            </div>
            <StatusBadge value={data?.status ?? 'Unknown'} />
          </div>
          <div className="diagnostic-score-stage">
            <div className={cn('diagnostic-score-ring', scoreLoading && 'loading')}>
              <svg aria-hidden="true" className="diagnostic-score-svg" viewBox="0 0 120 120">
                <circle className="diagnostic-score-track" cx="60" cy="60" r="50" />
                {scoreLoading ? (
                  <circle className="diagnostic-score-spinner" cx="60" cy="60" r="50" />
                ) : (
                  <motion.circle
                    animate={{ pathLength: score / 100 }}
                    className="diagnostic-score-fill"
                    cx="60"
                    cy="60"
                    initial={{ pathLength: 0 }}
                    r="50"
                    transition={{ duration: 0.9, ease: [0.22, 1, 0.36, 1] }}
                  />
                )}
              </svg>
              <div className="diagnostic-score-core">
                {scoreLoading ? (
                  <span className="diagnostic-loading-label">Checking</span>
                ) : (
                  <>
                    <AnimatedNumber className="font-display text-4xl font-semibold text-slate-50" value={score} />
                    <div className="text-xs text-slate-500">/ 100</div>
                  </>
                )}
              </div>
            </div>
          </div>
          <div className="diagnostic-score-footer">
            <span className="text-sm text-slate-400">{timeLabel(data?.checkedAt)}</span>
            <span className="text-sm text-slate-500">{healthyCount} healthy</span>
          </div>
        </Panel>
        <Panel className="diagnostics-findings-panel">
          <div className="diagnostics-panel-head">
            <div>
              <h2 className="panel-title">Findings</h2>
              <div className="mt-1 text-xs text-slate-500">Configuration and connectivity signals</div>
            </div>
            <Badge tone={warningCount ? 'warning' : 'success'}>{warningCount ? `${warningCount} warnings` : 'Clear'}</Badge>
          </div>
          <div className="diagnostic-findings-list">
            {components.map((component) => {
              const isWarning = diagnosticFindingIsWarning(component)
              const [name, ...detailParts] = component.split('=')
              const detail = detailParts.join('=')
              return (
                <motion.article
                  key={component}
                  animate={{ opacity: 1, y: 0 }}
                  className={cn('diagnostic-finding-row', isWarning && 'warning')}
                  initial={{ opacity: 0, y: 8 }}
                  transition={{ duration: 0.22 }}
                >
                  <span className="diagnostic-finding-icon">
                    {isWarning ? <AlertTriangle className="h-4 w-4" /> : <CheckCircle2 className="h-4 w-4" />}
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-sm font-semibold text-slate-100">{name}</div>
                    <div className="mt-1 break-words text-xs text-slate-500">{detail || component}</div>
                  </div>
                </motion.article>
              )
            })}
            {components.length === 0 ? <EmptyState icon={TerminalSquare} title="No diagnostics yet" /> : null}
          </div>
        </Panel>
        <Panel className="diagnostics-readiness-panel">
          <div className="diagnostics-panel-head">
            <div>
              <h2 className="panel-title">Production gates</h2>
              <div className="mt-1 text-xs text-slate-500">
                {readinessGates.length} gates / {failedRequiredGates.length} blockers / {optionalGateWarnings.length} warnings
              </div>
            </div>
            <StatusBadge value={readiness?.status ?? 'Syncing'} />
          </div>
          <div className="grid gap-2 lg:grid-cols-2">
            {readinessLoading ? (
              <>
                <SkeletonBlock className="h-16" />
                <SkeletonBlock className="h-16" />
                <SkeletonBlock className="h-16" />
                <SkeletonBlock className="h-16" />
              </>
            ) : readinessGates.map((gate) => (
              <motion.article
                key={gate.key}
                animate={{ opacity: 1, y: 0 }}
                className={cn('diagnostic-finding-row', !gate.passed && 'warning')}
                initial={{ opacity: 0, y: 8 }}
                transition={{ duration: 0.22 }}
              >
                <span className="diagnostic-finding-icon">
                  {gate.passed ? <CheckCircle2 className="h-4 w-4" /> : <AlertTriangle className="h-4 w-4" />}
                </span>
                <div className="min-w-0 flex-1">
                  <div className="flex min-w-0 flex-wrap items-center gap-2">
                    <span className="truncate text-sm font-semibold text-slate-100">{gate.key.replaceAll('-', ' ')}</span>
                    {!gate.required ? <Badge tone="neutral">Optional</Badge> : null}
                  </div>
                  <div className="mt-1 break-words text-xs text-slate-500">{gate.message}</div>
                </div>
              </motion.article>
            ))}
            {!readinessLoading && readinessGates.length === 0 ? <EmptyState icon={ShieldCheck} title="No production gates yet" /> : null}
          </div>
        </Panel>
      </section>
    </PageFrame>
  )
}

function RoleLockPage({ page }: { page: PageId }) {
  return (
    <PageFrame icon={Lock} title="Access Restricted">
      <Panel>
        <EmptyState icon={ShieldCheck} title={`${page} requires a higher role`} />
      </Panel>
    </PageFrame>
  )
}

function PageSwitch() {
  const activePage = useShellStore((state) => state.activePage)
  const user = useShellStore((state) => state.user)
  if (!hasAnyRole(user, pageRoles(activePage))) {
    return <RoleLockPage page={activePage} />
  }
  const page = (() => {
    switch (activePage) {
      case 'dashboard':
        return <DashboardPage />
      case 'events':
        return <EventsPage />
      case 'event-detail':
        return <EventDetailPage />
      case 'approvals':
        return <ApprovalsPage />
      case 'modules':
        return <ModulesPage />
      case 'modules-store':
        return <ModuleStorePage />
      case 'actions':
        return <ActionsPage />
      case 'audit':
        return <AuditPage />
      case 'logs':
        return <LogsPage />
      case 'notifications':
        return <NotificationsPage />
      case 'users':
        return <UsersPage />
      case 'support':
        return <SupportPage />
      case 'diagnostics':
        return <DiagnosticsPage />
      default:
        return <DashboardPage />
    }
  })()

  return (
    <AnimatePresence mode="wait">
      <motion.div
        key={activePage}
        animate={{ opacity: 1, y: 0, filter: 'blur(0px)' }}
        className="flex min-h-0 min-w-0 flex-1"
        exit={{ opacity: 0, y: -6, filter: 'blur(3px)' }}
        initial={{ opacity: 0, y: 8, filter: 'blur(3px)' }}
        transition={{ duration: 0.16, ease: 'easeOut' }}
      >
        {page}
      </motion.div>
    </AnimatePresence>
  )
}

export function AppShell() {
  const shellRef = useRef<HTMLDivElement>(null)
  const user = useShellStore((state) => state.user)

  useEffect(() => {
    applyStyleTheme(loadModuleStoreState().activeStyle)
  }, [])

  useEffect(() => {
    if (!user || !shellRef.current) {
      return
    }

    const ctx = gsap.context(() => {
      gsap.fromTo('.sidebar-shell', { x: -26, opacity: 0 }, { x: 0, opacity: 1, duration: 0.72, ease: 'power3.out' })
      gsap.fromTo('.topbar-panel', { y: -18, opacity: 0 }, { y: 0, opacity: 1, duration: 0.68, delay: 0.08, ease: 'power3.out' })
    }, shellRef)

    return () => ctx.revert()
  }, [user])

  if (!user) {
    return <LoginPage />
  }
  return (
    <div ref={shellRef} className="app-shell coffee-stage flex min-h-screen text-stone-200">
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar />
        <PageSwitch />
      </div>
      <Toasts />
    </div>
  )
}
