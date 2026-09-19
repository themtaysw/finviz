import { queryOptions } from '@tanstack/react-query'
import { fetchChildren, fetchLargestChildren, fetchNode } from './client'

export const childrenPageQuery = (parentId: number, page: number) =>
  queryOptions({
    queryKey: ['children', parentId, page],
    queryFn: ({ signal }) => fetchChildren(parentId, page, signal),
  })

export const largestChildrenQuery = (id: number, limit: number) =>
  queryOptions({
    queryKey: ['largest-children', id, limit],
    queryFn: ({ signal }) => fetchLargestChildren(id, limit, signal),
  })

export const nodeQuery = (id: number) =>
  queryOptions({
    queryKey: ['node', id],
    queryFn: ({ signal }) => fetchNode(id, signal),
  })
