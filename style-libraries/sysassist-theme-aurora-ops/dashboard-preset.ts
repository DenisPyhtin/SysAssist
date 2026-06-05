export const dashboardPreset = {
  id: 'aurora-ops',
  name: 'Aurora Ops',
  palette: ['#7bd6c2', '#8fe0a5', '#79a9ff', '#eefcf6'],
  charts: [
    { id: 'service-temperature', type: 'area', title: 'Service temperature', metric: 'severity', smoothing: 'monotone' },
    { id: 'uptime-sparkline', type: 'line', title: 'Uptime signal', metric: 'events', compact: true },
    { id: 'capacity-ribbon', type: 'composed', title: 'Capacity ribbon', metrics: ['modules', 'approvals'] }
  ]
}
