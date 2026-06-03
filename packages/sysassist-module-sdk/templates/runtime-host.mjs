import http from 'node:http'

const port = Number(process.env.PORT ?? 7077)

function readJson(request) {
  return new Promise((resolve, reject) => {
    let raw = ''
    request.on('data', (chunk) => { raw += chunk })
    request.on('end', () => {
      try {
        resolve(raw ? JSON.parse(raw) : {})
      } catch (error) {
        reject(error)
      }
    })
  })
}

function send(response, statusCode, payload) {
  response.writeHead(statusCode, { 'content-type': 'application/json' })
  response.end(JSON.stringify(payload))
}

const server = http.createServer(async (request, response) => {
  try {
    const url = new URL(request.url ?? '/', `http://localhost:${port}`)
    const body = await readJson(request)

    if (request.method === 'POST' && url.pathname === '/health') {
      send(response, 200, {
        status: 'Healthy',
        message: `Runtime host accepted module ${body.moduleKey ?? 'unknown'}.`
      })
      return
    }

    if (request.method === 'POST' && url.pathname === '/events') {
      send(response, 200, {
        success: true,
        message: 'Runtime host returned no active events.',
        events: []
      })
      return
    }

    if (request.method === 'POST' && url.pathname.startsWith('/actions/')) {
      const actionKey = decodeURIComponent(url.pathname.replace('/actions/', ''))
      send(response, 200, {
        success: true,
        message: `Runtime host executed ${actionKey}.`,
        result: { target: body.target ?? null }
      })
      return
    }

    send(response, 404, { success: false, message: 'Route not found.' })
  } catch (error) {
    send(response, 500, { success: false, message: error instanceof Error ? error.message : 'Runtime error.' })
  }
})

server.listen(port, () => {
  console.log(`SysAssist custom module runtime listening on http://localhost:${port}`)
})
