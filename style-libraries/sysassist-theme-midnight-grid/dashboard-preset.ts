export const dashboardPreset = {
  id: 'midnight-grid',
  name: 'Midnight Grid',
  palette: ['#82aaff', '#73d7c0', '#d093ff', '#eef3ff'],
  charts: [
    { id: 'module-dependency-graph', type: 'network', title: 'Module dependency graph', metric: 'modules' },
    { id: 'incident-waterfall', type: 'waterfall', title: 'Incident waterfall', metric: 'events' },
    { id: 'correlation-density', type: 'scatter', title: 'Correlation density', metrics: ['events', 'severity'] }
  ]
}
