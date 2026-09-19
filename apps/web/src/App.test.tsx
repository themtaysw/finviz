import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http } from 'msw'
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
        children: Array.from({ length: 1000 }, (_, i) => ({ label: `fern ${i + 1}` })),
      },
      { label: 'animal', children: [{ label: 'dog', children: [{ label: 'puppy' }] }] },
    ],
  },
])

const treeItem = async (name: string) =>
  within(await screen.findByRole('tree')).findByRole('treeitem', { name })

async function expandPlant() {
  await userEvent.click(within(await treeItem('plant')).getByRole('button', { name: 'Expand' }))
}

function scrollTreeTo(row: number) {
  screen.getByRole('tree').scrollTo({ top: row * 28 })
}

function recordChildRequests({ slow = false } = {}) {
  const plantId = api.find('plant').id
  const offsets = { requested: [] as number[], aborted: [] as number[] }

  server.use(
    http.get(`/api/nodes/${plantId}/children`, async ({ request }) => {
      const offset = Number(new URL(request.url).searchParams.get('offset'))
      offsets.requested.push(offset)
      request.signal.addEventListener('abort', () => offsets.aborted.push(offset))
      if (slow && offset === 0) await delay(200)
      return undefined
    }),
  )

  return offsets
}

describe('taxonomy tree', () => {
  beforeEach(() => server.use(...api.handlers))

  it('opens the root on load and loads children on expand', async () => {
    renderWithQueries(<App />)

    expect(await treeItem('life')).toHaveAttribute('aria-expanded', 'true')
    expect(await treeItem('plant')).toHaveAttribute('aria-level', '2')

    await userEvent.click(within(await treeItem('animal')).getByRole('button', { name: 'Expand' }))

    expect(await treeItem('dog')).toHaveAttribute('aria-level', '3')
  })

  it('keeps the root collapsed once the user collapses it', async () => {
    renderWithQueries(<App />)
    await treeItem('plant')

    await userEvent.click(within(await treeItem('life')).getByRole('button', { name: 'Collapse' }))

    await waitFor(() =>
      expect(screen.queryByRole('treeitem', { name: 'plant' })).not.toBeInTheDocument(),
    )
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(await treeItem('life')).toHaveAttribute('aria-expanded', 'false')
  })

  it('renders only the rows near the viewport', async () => {
    renderWithQueries(<App />)
    await expandPlant()

    expect(await treeItem('fern 1')).toHaveAttribute('aria-setsize', '1000')
    expect(screen.getAllByRole('treeitem').length).toBeLessThan(60)
    expect(screen.queryByRole('treeitem', { name: 'fern 1000' })).not.toBeInTheDocument()
  })

  it('fetches only the pages it shows', async () => {
    const offsets = recordChildRequests()
    renderWithQueries(<App />)
    await expandPlant()
    await treeItem('fern 1')

    expect(offsets.requested).toEqual([0])
  })

  it('aborts page requests the user scrolled past', async () => {
    const offsets = recordChildRequests({ slow: true })
    renderWithQueries(<App />)
    await expandPlant()

    await act(async () => scrollTreeTo(500))

    expect(await treeItem('fern 500')).toBeInTheDocument()
    await waitFor(() => expect(offsets.aborted).toEqual([0]))
    expect(offsets.requested).toEqual([0, 400, 500])
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

  it('scrolls a deep link into a large list and loads only the page it lands on', async () => {
    const offsets = recordChildRequests()
    window.history.replaceState(null, '', `/?node=${api.find('fern 900').id}`)
    renderWithQueries(<App />)

    expect(await treeItem('fern 900')).toHaveAttribute('aria-selected', 'true')
    expect(offsets.requested).toContain(800)
    expect(offsets.requested).not.toContain(400)
  })

  it('selects a node and puts it in the URL', async () => {
    renderWithQueries(<App />)

    await userEvent.click(within(await treeItem('life')).getByRole('button', { name: 'life' }))

    expect(window.location.search).toBe(`?node=${api.find('life').id}`)
    expect(await treeItem('life')).toHaveAttribute('aria-selected', 'true')
  })
})
