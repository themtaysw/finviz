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

export type OpenParent = {
  id: number
  childCount: number
}

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
  const openParents: OpenParent[] = []

  const visit = (parentId: number, childCount: number, depth: number) => {
    openParents.push({ id: parentId, childCount })
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
  return { rows, openParents }
}

export const pageOf = (index: number) => Math.floor(index / PAGE_SIZE)

export const rowKey = (parentId: number, index: number) => `${parentId}:${index}`

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
