import { useEffect, useState } from 'react'

function App() {
  const [text, setText] = useState('Loading…')

  useEffect(() => {
    fetch('/audits')
      .then((response) => {
        if (!response.ok) {
          throw new Error(`Request failed with status ${response.status}`)
        }

        return response.json()
      })
      .then((audits: { result: string }) => setText(audits.result))
      .catch((error: Error) => setText(`Error: ${error.message}`))
  }, [])

  return <p>{text}</p>
}

export default App
