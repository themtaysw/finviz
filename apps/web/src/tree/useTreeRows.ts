import { useQueries, useQuery, type UseQueryResult } from '@tanstack/react-query'
import { useCallback, useMemo } from 'react'
import { PAGE_SIZE, ROOT_ID } from '../api/client'
import { childrenPageQuery } from '../api/queries'
import type { NodeSummary, Page } from '../api/types'
import { flattenTree, pageOf, type ExpandedNodes, type TreeRow } from './treeModel'

const pageItems = (results: UseQueryResult<Page<NodeSummary>>[]) =>
  results.map((result) => result.data?.items)

export function useTreeRows(expanded: ExpandedNodes) {
  const roots = useQuery(childrenPageQuery(ROOT_ID, 0))
  const rootCount = roots.data?.total ?? 0

  const { rows, openParents } = useMemo(
    () => flattenTree(rootCount, expanded),
    [rootCount, expanded],
  )

  const pages = useMemo(
    () =>
      openParents.flatMap(({ id, childCount }) =>
        Array.from({ length: Math.ceil(childCount / PAGE_SIZE) }, (_, page) => ({
          parentId: id,
          page,
        })),
      ),
    [openParents],
  )

  const items = useQueries({
    queries: pages.map(({ parentId, page }) => childrenPageQuery(parentId, page)),
    combine: pageItems,
  })

  const loaded = useMemo(() => {
    const byPage = new Map<string, NodeSummary[]>()
    pages.forEach(({ parentId, page }, i) => {
      const pageItems = items[i]
      if (pageItems) byPage.set(`${parentId}:${page}`, pageItems)
    })
    return byPage
  }, [pages, items])

  const nodeAt = useCallback(
    (row: TreeRow) => loaded.get(`${row.parentId}:${pageOf(row.index)}`)?.[row.index % PAGE_SIZE],
    [loaded],
  )

  return { rows, nodeAt, isPending: roots.isPending, error: roots.error }
}
