import { useEffect, useState } from 'react'

function App() {
  const [apiStatus, setApiStatus] = useState('Checking...')

  useEffect(() => {
    fetch('/api/health')
      .then((response) => {
        if (!response.ok) {
          throw new Error('API request failed')
        }
        return response.json()
      })
      .then((data) => setApiStatus(data.status))
      .catch(() => setApiStatus('Unavailable'))
  }, [])

  return (
    <main>
      <h1>ResolveNest</h1>
      <p>AI-Powered SaaS Technical Support Investigator</p>
      <p>Backend status: {apiStatus}</p>
    </main>
  )
}

export default App