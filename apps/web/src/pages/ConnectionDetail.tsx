import { Title } from '@mantine/core'
import { useParams } from 'react-router'

export function ConnectionDetail() {
  const { id } = useParams()

  return <Title order={2}>Connection {id}</Title>
}
