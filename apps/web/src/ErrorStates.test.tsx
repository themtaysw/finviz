import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { App } from './App'
import { createFakeApi } from './test/fakeApi'
import { renderWithQueries } from './test/render'
import { server } from './test/server'

const api = createFakeApi([{ label: 'life', children: [{ label: 'plant' }] }])

const failOnce = (path: string) =>
  server.use(http.get(path, () => new HttpResponse(null, { status: 500 }), { once: true }))

describe('error states', () => {
  beforeEach(() => server.use(...api.handlers))

  it('says so when a linked category does not exist', async () => {
    window.history.replaceState(null, '', '/?node=999')
    renderWithQueries(<App />)

    expect(await screen.findByText('This category doesn’t exist.')).toBeInTheDocument()
  })

  it('recovers the tree after a failed load', async () => {
    failOnce('/api/nodes/roots')
    renderWithQueries(<App />)

    await userEvent.click(await screen.findByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('treeitem', { name: 'plant' })).toBeInTheDocument()
  })

  it('recovers the details after a failed load', async () => {
    const plant = api.find('plant').id
    failOnce(`/api/nodes/${plant}`)
    window.history.replaceState(null, '', `/?node=${plant}`)
    renderWithQueries(<App />)

    expect(await screen.findByText('Couldn’t load this category.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('heading', { name: 'plant' })).toBeInTheDocument()
    expect(document.title).toBe('plant · ImageNet taxonomy')
  })
})
