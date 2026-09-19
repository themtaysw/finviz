import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { App } from './App'
import { createFakeApi } from './test/fakeApi'
import { renderWithQueries } from './test/render'
import { server } from './test/server'

const api = createFakeApi([
  {
    label: 'life',
    children: [
      {
        label: 'plant',
        children: Array.from({ length: 150 }, (_, i) => ({ label: `fern ${i + 1}` })),
      },
      { label: 'animal', children: [{ label: 'dog', children: [{ label: 'puppy' }] }] },
    ],
  },
])

const treeItem = async (name: string) =>
  within(await screen.findByRole('tree')).findByRole('treeitem', { name })

describe('taxonomy tree', () => {
  beforeEach(() => server.use(...api.handlers))

  it('loads children when a node is expanded', async () => {
    renderWithQueries(<App />)

    await userEvent.click(within(await treeItem('life')).getByRole('button', { name: 'Expand' }))

    expect(await treeItem('plant')).toHaveAttribute('aria-level', '2')
    expect(await treeItem('animal')).toHaveAttribute('aria-posinset', '2')
  })

  it('loads every page of a large child list', async () => {
    renderWithQueries(<App />)

    await userEvent.click(within(await treeItem('life')).getByRole('button', { name: 'Expand' }))
    await userEvent.click(within(await treeItem('plant')).getByRole('button', { name: 'Expand' }))

    expect(await treeItem('fern 150')).toHaveAttribute('aria-setsize', '150')
  })

  it('reveals the node from the URL', async () => {
    window.history.replaceState(null, '', `/?node=${api.find('puppy').id}`)
    renderWithQueries(<App />)

    expect(await treeItem('puppy')).toHaveAttribute('aria-selected', 'true')
    expect(
      screen
        .getAllByRole('treeitem')
        .map((item) => [item.getAttribute('aria-label'), item.getAttribute('aria-level')]),
    ).toEqual([
      ['life', '1'],
      ['plant', '2'],
      ['animal', '2'],
      ['dog', '3'],
      ['puppy', '4'],
    ])
    expect(await screen.findByRole('heading', { name: 'puppy' })).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Breadcrumb' })).toHaveTextContent(
      /life.*animal.*dog/,
    )
  })

  it('selects a node and puts it in the URL', async () => {
    renderWithQueries(<App />)

    await userEvent.click(within(await treeItem('life')).getByRole('button', { name: 'life' }))

    expect(window.location.search).toBe(`?node=${api.find('life').id}`)
    expect(await treeItem('life')).toHaveAttribute('aria-selected', 'true')
  })
})
