import { describe, expect, it } from 'vitest'
import { ROOT_ID } from '../api/client'
import type { NodeDetails } from '../api/types'
import { flattenTree, treeReducer, type ExpandedNodes } from './treeModel'

const positions = (rootCount: number, expanded: ExpandedNodes) =>
  flattenTree(rootCount, expanded).rows.map(({ parentId, index, depth }) => [
    parentId,
    index,
    depth,
  ])

describe('flattenTree', () => {
  it('lists only roots when nothing is expanded', () => {
    expect(positions(2, new Map())).toEqual([
      [ROOT_ID, 0, 0],
      [ROOT_ID, 1, 0],
    ])
  })

  it('places the children of an expanded node directly under it', () => {
    const expanded: ExpandedNodes = new Map([
      [10, { parentId: ROOT_ID, index: 0, childCount: 2 }],
      [21, { parentId: 10, index: 1, childCount: 1 }],
    ])

    expect(positions(2, expanded)).toEqual([
      [ROOT_ID, 0, 0],
      [10, 0, 1],
      [10, 1, 1],
      [21, 0, 2],
      [ROOT_ID, 1, 0],
    ])
  })

  it('hides expanded descendants of a collapsed node', () => {
    const expanded: ExpandedNodes = new Map([[21, { parentId: 10, index: 1, childCount: 3 }]])

    expect(positions(1, expanded)).toEqual([[ROOT_ID, 0, 0]])
  })

  it('reports every open parent so their pages can be loaded', () => {
    const expanded: ExpandedNodes = new Map([
      [10, { parentId: ROOT_ID, index: 0, childCount: 250 }],
    ])

    expect(flattenTree(1, expanded).openParents).toEqual([
      { id: ROOT_ID, childCount: 1 },
      { id: 10, childCount: 250 },
    ])
  })
})

describe('treeReducer', () => {
  const node: NodeDetails = {
    id: 40,
    label: 'rudbeckia',
    path: 'life > plant > coneflower > rudbeckia',
    size: 0,
    childCount: 0,
    depth: 3,
    index: 0,
    ancestors: [
      { id: 1, label: 'life', index: 0, childCount: 2 },
      { id: 2, label: 'plant', index: 0, childCount: 2 },
      { id: 4, label: 'coneflower', index: 1, childCount: 1 },
    ],
  }

  it('reveals a node by expanding its ancestors', () => {
    const state = treeReducer(new Map(), { type: 'reveal', node })

    expect([...state]).toEqual([
      [1, { parentId: ROOT_ID, index: 0, childCount: 2 }],
      [2, { parentId: 1, index: 0, childCount: 2 }],
      [4, { parentId: 2, index: 1, childCount: 1 }],
    ])
  })

  it('keeps the same state when the node is already revealed', () => {
    const state = treeReducer(new Map(), { type: 'reveal', node })

    expect(treeReducer(state, { type: 'reveal', node })).toBe(state)
  })

  it('toggles a node open and closed', () => {
    const expansion = { parentId: ROOT_ID, index: 0, childCount: 2 }
    const opened = treeReducer(new Map(), { type: 'toggle', id: 1, expansion })

    expect(opened.get(1)).toEqual(expansion)
    expect(treeReducer(opened, { type: 'toggle', id: 1, expansion }).has(1)).toBe(false)
  })
})
