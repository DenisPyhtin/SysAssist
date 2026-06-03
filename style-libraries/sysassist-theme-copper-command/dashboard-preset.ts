export const dashboardPreset = {
  id: 'copper-command',
  name: 'Copper Command',
  palette: ['#e5a85f', '#f0d2a4', '#d7b56f', '#e68573'],
  charts: [
    { id: 'approval-pressure', type: 'bar', title: 'Approval pressure', metric: 'approvals' },
    { id: 'remediation-funnel', type: 'funnel', title: 'Remediation funnel', stages: ['intake', 'approval', 'action', 'audit'] },
    { id: 'risk-heat-strip', type: 'heatstrip', title: 'Risk heat strip', metric: 'severity' }
  ]
}
