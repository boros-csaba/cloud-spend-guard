import { Badge } from '@mantine/core'
import type { Connection } from './api.ts'

export function ConnectionState({ connection }: { connection: Connection }) {
  switch (connection.state) {
    case 'working':
      return <Badge color="green">Working</Badge>
    case 'broken':
      return <Badge color="red">Broken since {new Date(connection.verification!.failingSince!).toLocaleDateString()}</Badge>
    default:
      return <Badge color="gray">Waiting for setup</Badge>
  }
}
