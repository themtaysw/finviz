import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { App } from '../App'
import { createFakeApi } from '../test/fakeApi'
import { renderWithQueries } from '../test/render'
import { server } from '../test/server'

const api = createFakeApi([
  {
    label: 'life',
    children: [
      { label: 'plant', children: [{ label: 'fern' }, { label: 'moss' }] },
      { label: 'animal', children: [{ label: 'dog' }] },
    ],
  },
])

async function focusTree() {
  renderWithQueries(<App />)
  await screen.findByRole('treeitem', { name: 'animal' })
  await userEvent.tab()
  await userEvent.tab()
  expect(screen.getByRole('tree')).toHaveFocus()
}

const active = () => {
  const id = screen.getByRole('tree').getAttribute('aria-activedescendant')
  return id ? document.getElementById(id)?.getAttribute('aria-label') : undefined
}

const item = (name: string) => screen.getByRole('treeitem', { name })

describe('tree keyboard navigation', () => {
  beforeEach(() => server.use(...api.handlers))

  it('walks into and out of a node with the arrow keys', async () => {
    await focusTree()

    await userEvent.keyboard('{ArrowDown}{ArrowDown}')
    expect(active()).toBe('plant')

    await userEvent.keyboard('{ArrowRight}')
    expect(await screen.findByRole('treeitem', { name: 'fern' })).toBeInTheDocument()
    expect(item('plant')).toHaveAttribute('aria-expanded', 'true')

    await userEvent.keyboard('{ArrowRight}{ArrowDown}')
    expect(active()).toBe('moss')

    await userEvent.keyboard('{ArrowLeft}')
    expect(active()).toBe('plant')

    await userEvent.keyboard('{ArrowLeft}')
    expect(item('plant')).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByRole('treeitem', { name: 'fern' })).not.toBeInTheDocument()
  })

  it('jumps to the first and last rows', async () => {
    await focusTree()

    await userEvent.keyboard('{End}')
    expect(active()).toBe('animal')

    await userEvent.keyboard('{Home}')
    expect(active()).toBe('life')
  })

  it('selects the active row with Enter', async () => {
    await focusTree()

    await userEvent.keyboard('{End}{Enter}')

    expect(window.location.search).toBe(`?node=${api.find('animal').id}`)
    expect(await screen.findByRole('heading', { name: 'animal' })).toBeInTheDocument()
    expect(item('animal')).toHaveAttribute('aria-selected', 'true')
  })

  it('continues from a row clicked with the mouse', async () => {
    renderWithQueries(<App />)

    await userEvent.click(await screen.findByRole('button', { name: 'plant' }))
    await userEvent.keyboard('{ArrowDown}')

    expect(screen.getByRole('tree')).toHaveFocus()
    expect(active()).toBe('animal')
  })

  it('starts from the selected node', async () => {
    window.history.replaceState(null, '', `/?node=${api.find('dog').id}`)
    await focusTree()

    await userEvent.keyboard('{ArrowUp}')

    expect(active()).toBe('animal')
  })
})
