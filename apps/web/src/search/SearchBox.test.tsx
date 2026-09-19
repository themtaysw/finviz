import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { App } from '../App'
import { createFakeApi } from '../test/fakeApi'
import { renderWithQueries } from '../test/render'
import { server } from '../test/server'

const api = createFakeApi([
  {
    label: 'life',
    children: [
      {
        label: 'plant',
        children: Array.from({ length: 40 }, (_, i) => ({ label: `fern ${i + 1}` })),
      },
      { label: 'animal', children: [{ label: 'dog', children: [{ label: 'puppy, pup' }] }] },
    ],
  },
])

const searchBox = () => screen.getByRole('combobox', { name: 'Search categories' })

function recordSearches({ slowQuery }: { slowQuery?: string } = {}) {
  const searches = { requested: [] as string[], aborted: [] as string[] }

  server.use(
    http.get('/api/search', async ({ request }) => {
      const query = new URL(request.url).searchParams.get('q') ?? ''
      searches.requested.push(query)
      request.signal.addEventListener('abort', () => searches.aborted.push(query))
      if (query === slowQuery) await delay(300)
      return undefined
    }),
  )

  return searches
}

describe('search', () => {
  beforeEach(() => server.use(...api.handlers))

  it('sends one request for a burst of typing', async () => {
    const searches = recordSearches()
    renderWithQueries(<App />)

    await userEvent.type(searchBox(), 'fern 12')

    expect(await screen.findByRole('option', { name: 'fern 12' })).toBeInTheDocument()
    expect(searches.requested).toEqual(['fern 12'])
  })

  it('aborts the previous search when the query changes', async () => {
    const searches = recordSearches({ slowQuery: 'fern' })
    renderWithQueries(<App />)

    await userEvent.type(searchBox(), 'fern')
    await waitFor(() => expect(searches.requested).toEqual(['fern']))
    await userEvent.type(searchBox(), ' 7')

    expect(await screen.findByRole('option', { name: 'fern 7' })).toBeInTheDocument()
    expect(searches.aborted).toEqual(['fern'])
  })

  it('highlights the match and shows where the result sits', async () => {
    renderWithQueries(<App />)

    await userEvent.type(searchBox(), 'pup')

    const option = await screen.findByRole('option', { name: 'puppy, pup' })
    expect(within(option).getAllByText('pup', { selector: 'mark' })).toHaveLength(2)
    expect(option).toHaveAccessibleDescription('animal › dog')
  })

  it('selects a result with the keyboard and reveals it in the tree', async () => {
    renderWithQueries(<App />)

    await userEvent.type(searchBox(), 'puppy')
    await screen.findByRole('option', { name: 'puppy, pup' })
    await userEvent.keyboard('{Enter}')

    expect(window.location.search).toBe(`?node=${api.find('puppy, pup').id}`)
    expect(await screen.findByRole('treeitem', { name: 'puppy, pup' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('moves through results with the arrow keys', async () => {
    renderWithQueries(<App />)

    await userEvent.type(searchBox(), 'fern 1')
    await screen.findByRole('option', { name: 'fern 1' })
    await userEvent.keyboard('{ArrowDown}{ArrowDown}{ArrowUp}')

    const active = screen.getByRole('option', { selected: true })
    expect(searchBox()).toHaveAttribute('aria-activedescendant', active.id)
    expect(screen.getAllByRole('option').indexOf(active)).toBe(1)
  })

  it('says when nothing matches', async () => {
    renderWithQueries(<App />)

    await userEvent.type(searchBox(), 'zebra')

    expect(await screen.findByText('No categories match “zebra”.')).toBeInTheDocument()
  })

  it('does not search for a single character', async () => {
    const searches = recordSearches()
    renderWithQueries(<App />)

    await userEvent.type(searchBox(), 'f')
    await new Promise((resolve) => setTimeout(resolve, 300))

    expect(searches.requested).toEqual([])
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })
})
