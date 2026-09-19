import { useQueries, useQuery, type UseQueryResult } from '@tanstack/react-query'
import { useCallback, useMemo } from 'react'
import type { NodeSummary, PageOfNodeSummary } from '../api/generated/model'
import { childrenPageQuery } from './childrenPageQuery'
import {
  flattenTree,
  pageOf,
  pagesFor,
  PAGE_SIZE,
  ROOT_ID,
  type ExpandedNodes,
  type TreeRow,
} from './treeModel'

export function useTreeRows(expanded: ExpandedNodes) {
  const roots = useQuery(childrenPageQuery(ROOT_ID, 0))
  const rootCount = roots.data?.total ?? 0
  const rows = useMemo(() => flattenTree(rootCount, expanded), [rootCount, expanded])

  return { rows, isPending: roots.isPending, error: roots.error, retry: roots.refetch }
}

const pageItems = (results: UseQueryResult<PageOfNodeSummary>[]) =>
  results.map((result) => result.data?.items)

export function useRowNodes(rows: readonly TreeRow[], start: number, end: number) {
  const pages = useMemo(() => pagesFor(rows, start, end), [rows, start, end])

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

  return useCallback(
    (row: TreeRow) => loaded.get(`${row.parentId}:${pageOf(row.index)}`)?.[row.index % PAGE_SIZE],
    [loaded],
  )
}
