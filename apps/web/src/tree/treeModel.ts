import type { NodeDetails } from '../api/generated/model'

export const ROOT_ID = 0
export const PAGE_SIZE = 100

export type Expansion = {
  parentId: number
  index: number
  childCount: number
}

export type ExpandedNodes = ReadonlyMap<number, Expansion>

export type TreeRow = {
  parentId: number
  index: number
  depth: number
  siblingCount: number
}

export const pageOf = (index: number) => Math.floor(index / PAGE_SIZE)

export const rowKey = (parentId: number, index: number) => `${parentId}:${index}`

export function flattenTree(rootCount: number, expanded: ExpandedNodes) {
  const openChildren = new Map<number, Map<number, number>>()
  for (const [id, { parentId, index }] of expanded) {
    let byIndex = openChildren.get(parentId)
    if (!byIndex) {
      byIndex = new Map()
      openChildren.set(parentId, byIndex)
    }
    byIndex.set(index, id)
  }

  const rows: TreeRow[] = []

  const visit = (parentId: number, childCount: number, depth: number) => {
    const open = openChildren.get(parentId)

    for (let index = 0; index < childCount; index++) {
      rows.push({ parentId, index, depth, siblingCount: childCount })

      const childId = open?.get(index)
      const child = childId === undefined ? undefined : expanded.get(childId)
      if (childId !== undefined && child) {
        visit(childId, child.childCount, depth + 1)
      }
    }
  }

  visit(ROOT_ID, rootCount, 0)
  return rows
}

export type PageRef = {
  parentId: number
  page: number
}

export function pagesFor(rows: readonly TreeRow[], start: number, end: number): PageRef[] {
  const pages = new Map<string, PageRef>()
  for (let i = Math.max(start, 0); i <= Math.min(end, rows.length - 1); i++) {
    const { parentId, index } = rows[i]!
    const page = pageOf(index)
    pages.set(`${parentId}:${page}`, { parentId, page })
  }
  return [...pages.values()]
}

export function rowIndexOf(rows: readonly TreeRow[], parentId: number, index: number) {
  return rows.findIndex((row) => row.parentId === parentId && row.index === index)
}

export type TreeAction =
  | { type: 'toggle'; id: number; expansion: Expansion }
  | { type: 'collapse'; id: number }
  | { type: 'reveal'; node: NodeDetails }

export function treeReducer(state: ExpandedNodes, action: TreeAction): ExpandedNodes {
  switch (action.type) {
    case 'toggle': {
      const next = new Map(state)
      if (!next.delete(action.id)) next.set(action.id, action.expansion)
      return next
    }
    case 'collapse': {
      if (!state.has(action.id)) return state
      const next = new Map(state)
      next.delete(action.id)
      return next
    }
    case 'reveal': {
      const missing = action.node.ancestors
        .map((ancestor, i) => ({
          id: ancestor.id,
          expansion: {
            parentId: action.node.ancestors[i - 1]?.id ?? ROOT_ID,
            index: ancestor.index,
            childCount: ancestor.childCount,
          },
        }))
        .filter(({ id }) => !state.has(id))

      if (missing.length === 0) return state
      const next = new Map(state)
      for (const { id, expansion } of missing) next.set(id, expansion)
      return next
    }
  }
}
