import { AppShell, Avatar, Group, Image, NavLink, Text } from '@mantine/core'
import { IconHome, IconPlugConnected, IconTopologyStar3 } from '@tabler/icons-react'
import { Link, Outlet, useLocation } from 'react-router'
import { useProfile } from './useProfile.ts'

const navItems = [
  { label: 'Home', to: '/', icon: IconHome },
  { label: 'Connections', to: '/connections', icon: IconPlugConnected },
  { label: 'Resources', to: '/resources', icon: IconTopologyStar3 },
]

export function Shell() {
  const { pathname } = useLocation()
  const { name, picture } = useProfile()

  return (
    <AppShell navbar={{ width: 240, breakpoint: 0 }} padding="lg">
      <AppShell.Navbar p="md">
        <AppShell.Section mb="lg">
          <Group gap="xs">
            <Image src="/favicon.svg" w={28} h={28} />
            <Text fw={600}>Cloud Spend Guard</Text>
          </Group>
        </AppShell.Section>
        <AppShell.Section grow>
          {navItems.map(({ label, to, icon: Icon }) => (
            <NavLink
              key={to}
              component={Link}
              to={to}
              label={label}
              leftSection={<Icon size={18} />}
              active={to === '/' ? pathname === '/' : pathname.startsWith(to)}
            />
          ))}
        </AppShell.Section>
        <AppShell.Section>
          <NavLink
            component={Link}
            to="/account"
            label={name}
            noWrap
            leftSection={<Avatar src={picture} name={name} color="initials" size="sm" />}
            active={pathname === '/account'}
          />
        </AppShell.Section>
      </AppShell.Navbar>
      <AppShell.Main>
        <Outlet />
      </AppShell.Main>
    </AppShell>
  )
}
