import { http, HttpResponse } from 'msw'
import type { NodeDetails, NodeSummary, Page } from '../api/types'

type FakeNode = NodeSummary & { parentId: number | null; children: FakeNode[] }

export type FakeTree = { label: string; children?: FakeTree[] }

export function createFakeApi(roots: FakeTree[]) {
  const nodes = new Map<number, FakeNode>()
  let nextId = 1

  const add = (tree: FakeTree, parentId: number | null): FakeNode => {
    const node: FakeNode = {
      id: nextId++,
      label: tree.label,
      size: 0,
      childCount: 0,
      parentId,
      children: [],
    }
    nodes.set(node.id, node)
    node.children = (tree.children ?? []).map((child) => add(child, node.id))
    node.childCount = node.children.length
    node.size = node.children.reduce((sum, child) => sum + child.size + 1, 0)
    return node
  }

  const rootNodes = roots.map((root) => add(root, null))
  const summary = ({ id, label, size, childCount }: FakeNode): NodeSummary => ({
    id,
    label,
    size,
    childCount,
  })
  const siblingsOf = (node: FakeNode) =>
    node.parentId === null ? rootNodes : nodes.get(node.parentId)!.children

  const page = (items: FakeNode[], url: URL): Page<NodeSummary> => {
    const offset = Number(url.searchParams.get('offset') ?? 0)
    const limit = Number(url.searchParams.get('limit') ?? 100)
    const ordered =
      url.searchParams.get('order') === 'size' ? [...items].sort((a, b) => b.size - a.size) : items
    return {
      items: ordered.slice(offset, offset + limit).map(summary),
      total: items.length,
      offset,
      limit,
    }
  }

  const details = (node: FakeNode): NodeDetails => {
    const chain: FakeNode[] = []
    for (let parent = node.parentId; parent !== null; parent = nodes.get(parent)!.parentId) {
      chain.unshift(nodes.get(parent)!)
    }
    return {
      ...summary(node),
      path: [...chain, node].map((n) => n.label).join(' > '),
      depth: chain.length,
      index: siblingsOf(node).indexOf(node),
      ancestors: chain.map((a) => ({
        id: a.id,
        label: a.label,
        index: siblingsOf(a).indexOf(a),
        childCount: a.childCount,
      })),
    }
  }

  const find = (label: string) => [...nodes.values()].find((node) => node.label === label)!

  const handlers = [
    http.get('/api/nodes/roots', ({ request }) =>
      HttpResponse.json(page(rootNodes, new URL(request.url))),
    ),
    http.get('/api/nodes/:id/children', ({ params, request }) => {
      const node = nodes.get(Number(params.id))
      return node
        ? HttpResponse.json(page(node.children, new URL(request.url)))
        : new HttpResponse(null, { status: 404 })
    }),
    http.get('/api/nodes/:id', ({ params }) => {
      const node = nodes.get(Number(params.id))
      return node ? HttpResponse.json(details(node)) : new HttpResponse(null, { status: 404 })
    }),
  ]

  return { handlers, find }
}
